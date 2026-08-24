using BasicPitch;
using Microsoft.Extensions.Logging;
using Muno.Models;
using NAudio.Midi;
using System.Diagnostics;

namespace Muno.Services;

/// <summary>
/// Extracts a single dominant melodic line from mixed audio, stores it as MIDI,
/// renders the MIDI locally, and encodes the result to MP3.
/// </summary>
public sealed class MelodySimplificationService : IMelodySimplificationService
{
    private const int MidiTempo = 120;
    private const int OutputSampleRate = 44100;
    private const int MinimumPitch = 40;
    private const int MaximumPitch = 88;
    private const float MinimumAmplitude = 0.12f;
    private const float MinimumDurationSeconds = 0.06f;
    private const float MaximumTransitionGapSeconds = 2.0f;

    private readonly ILogger<MelodySimplificationService> _logger;

    public MelodySimplificationService(ILogger<MelodySimplificationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<MelodySimplificationResult>> SimplifyAsync(
        string inputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
        {
            return Result<MelodySimplificationResult>.Failure("The source audio file was not found.");
        }

        var workDirectory = Path.Combine(
            Path.GetTempPath(),
            "Muno",
            "Melody",
            Guid.NewGuid().ToString("N"));
        var midiPath = Path.Combine(workDirectory, "Melody.mid");
        var wavPath = Path.Combine(workDirectory, "Melody.wav");
        var mp3Path = Path.Combine(workDirectory, "Melody.mp3");

        try
        {
            Directory.CreateDirectory(workDirectory);
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogInformation("Starting melody simplification for {InputPath}", inputPath);

            var detectedNotes = await TranscribeAsync(inputPath, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(0.68);
            var melodyNotes = SelectDominantMelody(detectedNotes);
            if (melodyNotes.Count == 0)
            {
                return Result<MelodySimplificationResult>.Failure(
                    "No clear dominant melody could be detected in this track.");
            }

            progress?.Report(0.75);
            WriteMidi(melodyNotes, midiPath);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0.82);
            await Task.Run(
                () => RenderMidiToWav(midiPath, wavPath, cancellationToken),
                cancellationToken);

            progress?.Report(0.94);
            var encodeResult = await EncodeMp3Async(wavPath, mp3Path, cancellationToken);
            if (!encodeResult.IsSuccess)
            {
                return Result<MelodySimplificationResult>.Failure(
                    encodeResult.ErrorMessage ?? "Failed to render the simplified melody.");
            }

            TryDeleteFile(wavPath);
            progress?.Report(1.0);

            _logger.LogInformation(
                "Melody simplification completed with {NoteCount} notes: {OutputPath}",
                melodyNotes.Count,
                mp3Path);

            return Result<MelodySimplificationResult>.Success(new MelodySimplificationResult
            {
                AudioFilePath = mp3Path,
                MidiFilePath = midiPath,
                NoteCount = melodyNotes.Count
            });
        }
        catch (OperationCanceledException)
        {
            TryDeleteDirectory(workDirectory);
            return Result<MelodySimplificationResult>.Failure("Melody simplification was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while simplifying melody for {InputPath}", inputPath);
            TryDeleteDirectory(workDirectory);
            return Result<MelodySimplificationResult>.Failure("Failed to simplify the track melody.");
        }
    }

    private static Task<List<Note>> TranscribeAsync(
        string inputPath,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reader = new AudioReader(inputPath);
            var audioBuffer = reader.ReadAll();
            cancellationToken.ThrowIfCancellationRequested();

            var model = new Model();
            var modelOutput = model.Predict(audioBuffer, modelProgress =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(Math.Clamp(modelProgress, 0d, 1d) * 0.65d);
            });

            cancellationToken.ThrowIfCancellationRequested();
            var converter = new NotesConverter(modelOutput);
            var options = new NotesConvertOptions
            {
                OnsetThreshold = 0.50f,
                FrameThreshold = 0.30f,
                MinNoteLength = 35,
                EnergyThreshold = 11,
                MinFreq = MidiPitchToFrequency(MinimumPitch),
                MaxFreq = MidiPitchToFrequency(MaximumPitch),
                InferOnsets = true,
                IncludePitchBends = false,
                MelodiaTrick = true
            };

            return converter.Convert(options);
        }, cancellationToken);
    }

    /// <summary>
    /// Selects one coherent path through polyphonic candidates. The score rewards audible,
    /// sustained notes and penalizes large jumps, long gaps, and overlapping candidates.
    /// </summary>
    private static List<Note> SelectDominantMelody(IReadOnlyCollection<Note> detectedNotes)
    {
        var candidates = detectedNotes
            .Where(IsMelodyCandidate)
            .OrderBy(note => note.StartTime)
            .ThenByDescending(note => note.Amplitude)
            .ToList();

        if (candidates.Count == 0)
        {
            return [];
        }

        var scores = new double[candidates.Count];
        var previous = new int[candidates.Count];
        Array.Fill(previous, -1);

        for (var i = 0; i < candidates.Count; i++)
        {
            var current = candidates[i];
            scores[i] = BaseNoteScore(current);

            for (var j = i - 1; j >= 0; j--)
            {
                var prior = candidates[j];
                var gap = current.StartTime - prior.EndTime;
                if (gap > MaximumTransitionGapSeconds)
                {
                    break;
                }

                var candidateScore = scores[j] + BaseNoteScore(current) + TransitionScore(prior, current);
                if (candidateScore > scores[i])
                {
                    scores[i] = candidateScore;
                    previous[i] = j;
                }
            }
        }

        var bestIndex = 0;
        for (var i = 1; i < scores.Length; i++)
        {
            if (scores[i] > scores[bestIndex])
            {
                bestIndex = i;
            }
        }

        var path = new List<Note>();
        for (var index = bestIndex; index >= 0; index = previous[index])
        {
            var note = candidates[index];
            path.Add(new Note(note.StartTime, note.EndTime, note.Pitch, note.Amplitude, null));
            if (previous[index] < 0)
            {
                break;
            }
        }

        path.Reverse();
        return MergeAdjacentNotes(RemoveOverlaps(path));
    }

    private static bool IsMelodyCandidate(Note note)
    {
        var duration = note.EndTime - note.StartTime;
        return note.Pitch is >= MinimumPitch and <= MaximumPitch
            && note.Amplitude >= MinimumAmplitude
            && duration >= MinimumDurationSeconds;
    }

    private static double BaseNoteScore(Note note)
    {
        var duration = Math.Clamp(note.EndTime - note.StartTime, 0.05f, 2.5f);
        return 1.25d + (note.Amplitude * 3.5d) + (Math.Sqrt(duration) * 1.4d);
    }

    private static double TransitionScore(Note previous, Note current)
    {
        var pitchDistance = Math.Abs(current.Pitch - previous.Pitch);
        var gap = current.StartTime - previous.EndTime;
        var overlap = Math.Max(0f, -gap);

        var continuity = gap switch
        {
            < -0.15f => -1.5d,
            <= 0.12f => 1.25d,
            <= 0.45f => 0.6d,
            <= 1.0f => -0.35d,
            _ => -1.2d
        };

        var pitchPenalty = pitchDistance switch
        {
            <= 2 => pitchDistance * 0.08d,
            <= 5 => 0.25d + ((pitchDistance - 2) * 0.16d),
            <= 12 => 0.75d + ((pitchDistance - 5) * 0.28d),
            _ => 2.8d + ((pitchDistance - 12) * 0.5d)
        };

        return continuity - pitchPenalty - (overlap * 3.0d);
    }

    private static List<Note> RemoveOverlaps(IReadOnlyList<Note> notes)
    {
        var result = new List<Note>(notes.Count);
        foreach (var note in notes)
        {
            if (result.Count == 0)
            {
                result.Add(note);
                continue;
            }

            var previous = result[^1];
            if (note.StartTime >= previous.EndTime)
            {
                result.Add(note);
                continue;
            }

            var shortenedEnd = Math.Max(previous.StartTime + MinimumDurationSeconds, note.StartTime);
            result[^1] = new Note(previous.StartTime, shortenedEnd, previous.Pitch, previous.Amplitude, null);
            result.Add(note);
        }

        return result;
    }

    private static List<Note> MergeAdjacentNotes(IReadOnlyList<Note> notes)
    {
        if (notes.Count == 0)
        {
            return [];
        }

        var result = new List<Note>(notes.Count) { notes[0] };
        for (var i = 1; i < notes.Count; i++)
        {
            var previous = result[^1];
            var current = notes[i];
            var gap = current.StartTime - previous.EndTime;

            if (previous.Pitch == current.Pitch && gap <= 0.10f)
            {
                result[^1] = new Note(
                    previous.StartTime,
                    current.EndTime,
                    previous.Pitch,
                    Math.Max(previous.Amplitude, current.Amplitude),
                    null);
            }
            else
            {
                result.Add(current);
            }
        }

        return result;
    }

    private static void WriteMidi(List<Note> melodyNotes, string midiPath)
    {
        var writer = new MidiWriter(melodyNotes);
        var midiEvents = writer.Write(new MidiWriteOptions
        {
            Tempo = MidiTempo,
            Patch = 0,
            MultiplePitchBends = false
        });
        MidiFile.Export(midiPath, midiEvents);
    }

    private static void RenderMidiToWav(
        string midiPath,
        string wavPath,
        CancellationToken cancellationToken)
    {
        var midi = new MidiFile(midiPath, strictChecking: false);
        var notes = ReadMidiNotes(midi);
        if (notes.Count == 0)
        {
            throw new InvalidOperationException("The generated MIDI contains no playable notes.");
        }

        var durationSeconds = notes.Max(note => note.EndSeconds) + 0.75d;
        var sampleCount = checked((int)Math.Ceiling(durationSeconds * OutputSampleRate));
        var samples = new float[sampleCount];

        foreach (var note in notes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenderNote(samples, note);
        }

        Normalize(samples);
        WritePcm16Wav(wavPath, samples, OutputSampleRate);
    }

    private static List<RenderedMidiNote> ReadMidiNotes(MidiFile midi)
    {
        var result = new List<RenderedMidiNote>();
        var activeNotes = new Dictionary<(int Channel, int Pitch), Stack<(long Tick, int Velocity)>>();
        var allEvents = midi.Events
            .SelectMany(track => track)
            .OrderBy(midiEvent => midiEvent.AbsoluteTime)
            .ToList();

        foreach (var midiEvent in allEvents)
        {
            if (midiEvent is NoteOnEvent noteOn)
            {
                var key = (noteOn.Channel, noteOn.NoteNumber);
                if (noteOn.Velocity > 0)
                {
                    if (!activeNotes.TryGetValue(key, out var stack))
                    {
                        stack = new Stack<(long Tick, int Velocity)>();
                        activeNotes[key] = stack;
                    }

                    stack.Push((noteOn.AbsoluteTime, noteOn.Velocity));
                    continue;
                }

                CompleteMidiNote(result, activeNotes, key, noteOn.AbsoluteTime, midi.DeltaTicksPerQuarterNote);
                continue;
            }

            if (midiEvent is NoteEvent noteOff && noteOff.CommandCode == MidiCommandCode.NoteOff)
            {
                var key = (noteOff.Channel, noteOff.NoteNumber);
                CompleteMidiNote(result, activeNotes, key, noteOff.AbsoluteTime, midi.DeltaTicksPerQuarterNote);
            }
        }

        return result;
    }

    private static void CompleteMidiNote(
        ICollection<RenderedMidiNote> result,
        IDictionary<(int Channel, int Pitch), Stack<(long Tick, int Velocity)>> activeNotes,
        (int Channel, int Pitch) key,
        long endTick,
        int ticksPerQuarterNote)
    {
        if (!activeNotes.TryGetValue(key, out var stack) || stack.Count == 0)
        {
            return;
        }

        var (startTick, velocity) = stack.Pop();
        if (endTick <= startTick)
        {
            return;
        }

        result.Add(new RenderedMidiNote(
            TicksToSeconds(startTick, ticksPerQuarterNote),
            TicksToSeconds(endTick, ticksPerQuarterNote),
            key.Pitch,
            velocity));
    }

    private static double TicksToSeconds(long ticks, int ticksPerQuarterNote) =>
        ticks * (60d / MidiTempo) / ticksPerQuarterNote;

    private static void RenderNote(float[] output, RenderedMidiNote note)
    {
        var startSample = Math.Clamp((int)Math.Round(note.StartSeconds * OutputSampleRate), 0, output.Length);
        var endSample = Math.Clamp((int)Math.Round((note.EndSeconds + 0.45d) * OutputSampleRate), 0, output.Length);
        var noteDuration = Math.Max(0.03d, note.EndSeconds - note.StartSeconds);
        var frequency = MidiPitchToFrequency(note.Pitch);
        var velocityGain = Math.Clamp(note.Velocity / 127f, 0.15f, 1f);

        for (var sampleIndex = startSample; sampleIndex < endSample; sampleIndex++)
        {
            var time = (sampleIndex - startSample) / (double)OutputSampleRate;
            var envelope = GetEnvelope(time, noteDuration);
            if (envelope <= 0d)
            {
                continue;
            }

            var phase = 2d * Math.PI * frequency * time;
            var tone = Math.Sin(phase)
                + (0.32d * Math.Sin(phase * 2d))
                + (0.14d * Math.Sin(phase * 3d))
                + (0.06d * Math.Sin(phase * 4d));

            output[sampleIndex] += (float)(tone * envelope * velocityGain * 0.42d);
        }
    }

    private static double GetEnvelope(double time, double noteDuration)
    {
        const double attack = 0.012d;
        const double release = 0.45d;

        if (time < attack)
        {
            return time / attack;
        }

        if (time <= noteDuration)
        {
            return 0.82d * Math.Exp(-(time - attack) * 0.85d) + 0.18d;
        }

        var releaseTime = time - noteDuration;
        if (releaseTime >= release)
        {
            return 0d;
        }

        var sustain = 0.82d * Math.Exp(-(noteDuration - attack) * 0.85d) + 0.18d;
        return sustain * (1d - (releaseTime / release));
    }

    private static void Normalize(float[] samples)
    {
        var peak = 0f;
        foreach (var sample in samples)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        if (peak <= 0f)
        {
            return;
        }

        var gain = Math.Min(1f, 0.92f / peak);
        if (gain >= 0.999f)
        {
            return;
        }

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= gain;
        }
    }

    private static void WritePcm16Wav(string path, float[] samples, int sampleRate)
    {
        const short channels = 1;
        const short bitsPerSample = 16;
        var bytesPerSample = bitsPerSample / 8;
        var dataSize = samples.Length * bytesPerSample;

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataSize);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bytesPerSample);
        writer.Write((short)(channels * bytesPerSample));
        writer.Write(bitsPerSample);
        writer.Write("data"u8.ToArray());
        writer.Write(dataSize);

        foreach (var sample in samples)
        {
            var clamped = Math.Clamp(sample, -1f, 1f);
            writer.Write((short)Math.Round(clamped * short.MaxValue));
        }
    }

    private async Task<Result> EncodeMp3Async(
        string wavPath,
        string mp3Path,
        CancellationToken cancellationToken)
    {
        var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "Libs", "ffmpeg.exe");
        if (!File.Exists(ffmpegPath))
        {
            return Result.Failure("The audio encoding engine is not available.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(wavPath);
        startInfo.ArgumentList.Add("-vn");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("libmp3lame");
        startInfo.ArgumentList.Add("-q:a");
        startInfo.ArgumentList.Add("2");
        startInfo.ArgumentList.Add(mp3Path);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            return Result.Failure("Failed to start melody audio encoding.");
        }

        using var registration = cancellationToken.Register(() =>
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        });

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode == 0 && File.Exists(mp3Path))
        {
            return Result.Success();
        }

        _logger.LogWarning("ffmpeg melody encoding failed with code {ExitCode}: {Error}", process.ExitCode, stderr);
        return Result.Failure("Failed to encode the simplified melody to MP3.");
    }

    private static float MidiPitchToFrequency(int midiPitch) =>
        440f * MathF.Pow(2f, (midiPitch - 69) / 12f);

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
        }
    }

    private sealed record RenderedMidiNote(double StartSeconds, double EndSeconds, int Pitch, int Velocity);
}
