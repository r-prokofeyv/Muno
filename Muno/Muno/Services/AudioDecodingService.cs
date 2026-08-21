using Microsoft.Extensions.Logging;
using Muno.Models;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Muno.Services;

/// <summary>
/// Service for decoding audio files to PCM data using the bundled ffmpeg.exe.
/// </summary>
public sealed partial class AudioDecodingService : IAudioDecodingService
{
    private readonly ILogger<AudioDecodingService> _logger;

    public AudioDecodingService(ILogger<AudioDecodingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<AudioFile>> DecodeAudioFileAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Result<AudioFile>.Failure($"File not found: {filePath}");
            }

            _logger.LogInformation("Decoding audio file: {FilePath}", filePath);

            // First, get audio metadata using ffprobe (part of ffmpeg)
            var metadataResult = await GetAudioMetadataAsync(filePath, cancellationToken);
            if (!metadataResult.IsSuccess || metadataResult.Value == null)
            {
                return Result<AudioFile>.Failure(metadataResult.ErrorMessage ?? "Failed to get audio metadata");
            }

            var metadata = metadataResult.Value;

            // Decode audio to raw PCM using ffmpeg
            var pcmResult = await DecodeToPcmAsync(filePath, metadata.SampleRate, metadata.Channels, cancellationToken);
            if (!pcmResult.IsSuccess || pcmResult.Value == null)
            {
                return Result<AudioFile>.Failure(pcmResult.ErrorMessage ?? "Failed to decode audio to PCM");
            }

            var audioFile = new AudioFile
            {
                FilePath = filePath,
                FileName = Path.GetFileName(filePath),
                Duration = metadata.Duration,
                SampleRate = metadata.SampleRate,
                Channels = metadata.Channels,
                PcmData = pcmResult.Value
            };

            _logger.LogInformation("Successfully decoded {FileName}: {Duration:F2}s, {SampleRate}Hz, {Channels} channel(s), {Samples} samples",
                audioFile.FileName, audioFile.Duration.TotalSeconds, audioFile.SampleRate, audioFile.Channels, audioFile.PcmData.Length);

            return Result<AudioFile>.Success(audioFile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decoding audio file: {FilePath}", filePath);
            return Result<AudioFile>.Failure($"Error decoding audio file: {ex.Message}");
        }
    }

    private async Task<Result<AudioMetadata>> GetAudioMetadataAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var ffprobePath = Path.Combine(AppContext.BaseDirectory, "Libs", "ffmpeg.exe");
            if (!File.Exists(ffprobePath))
            {
                return Result<AudioMetadata>.Failure("ffmpeg.exe not found in Libs folder");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ffprobePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Use ffmpeg to get duration, sample rate, and channels
            // ffmpeg -i input.mp3 -f null - (outputs info to stderr)
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("null");
            startInfo.ArgumentList.Add("-");

            using var process = new Process { StartInfo = startInfo };
            var stderrBuilder = new StringBuilder();

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    stderrBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            var stderr = stderrBuilder.ToString();

            // Parse metadata from ffmpeg output
            var metadata = ParseFfmpegMetadata(stderr);
            if (metadata == null)
            {
                _logger.LogWarning("Failed to parse ffmpeg metadata. Output: {Output}", stderr);
                return Result<AudioMetadata>.Failure("Failed to parse audio metadata from ffmpeg output");
            }

            return Result<AudioMetadata>.Success(metadata);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting audio metadata");
            return Result<AudioMetadata>.Failure($"Error getting audio metadata: {ex.Message}");
        }
    }

    private async Task<Result<float[]>> DecodeToPcmAsync(string filePath, int sampleRate, int channels, CancellationToken cancellationToken)
    {
        try
        {
            var ffmpegPath = Path.Combine(AppContext.BaseDirectory, "Libs", "ffmpeg.exe");
            if (!File.Exists(ffmpegPath))
            {
                return Result<float[]>.Failure("ffmpeg.exe not found in Libs folder");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // ffmpeg -i input.mp3 -f f32le -acodec pcm_f32le -ar 44100 -ac 2 pipe:1
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("f32le");  // 32-bit float PCM, little-endian
            startInfo.ArgumentList.Add("-acodec");
            startInfo.ArgumentList.Add("pcm_f32le");
            startInfo.ArgumentList.Add("-ar");
            startInfo.ArgumentList.Add(sampleRate.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("-ac");
            startInfo.ArgumentList.Add(channels.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("pipe:1");  // Write to stdout

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Both StandardOutput and StandardError are redirected, so both must be drained
            // concurrently. Otherwise, once ffmpeg fills the OS pipe buffer for the stream we are
            // not reading (typically stderr, which carries ffmpeg's verbose logging), it blocks on
            // writing to it and the process never exits, causing this method to hang indefinitely.
            using var stdout = process.StandardOutput.BaseStream;
            using var memoryStream = new MemoryStream();
            var stdoutTask = stdout.CopyToAsync(memoryStream, cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await Task.WhenAll(stdoutTask, stderrTask);

            await process.WaitForExitAsync(cancellationToken);

            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                _logger.LogWarning("ffmpeg exited with code {ExitCode}. Stderr: {Stderr}", process.ExitCode, stderr);
                return Result<float[]>.Failure($"ffmpeg failed with exit code {process.ExitCode}");
            }

            var pcmBytes = memoryStream.ToArray();
            var pcmSamples = new float[pcmBytes.Length / sizeof(float)];
            Buffer.BlockCopy(pcmBytes, 0, pcmSamples, 0, pcmBytes.Length);

            return Result<float[]>.Success(pcmSamples);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decoding to PCM");
            return Result<float[]>.Failure($"Error decoding to PCM: {ex.Message}");
        }
    }

    private AudioMetadata? ParseFfmpegMetadata(string ffmpegOutput)
    {
        // Example ffmpeg output:
        // Duration: 00:03:45.67, start: 0.000000, bitrate: 320 kb/s
        // Stream #0:0: Audio: mp3, 44100 Hz, stereo, fltp, 320 kb/s

        var durationMatch = DurationRegex().Match(ffmpegOutput);
        var streamMatch = StreamRegex().Match(ffmpegOutput);

        if (!durationMatch.Success || !streamMatch.Success)
        {
            return null;
        }

        if (!TimeSpan.TryParse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var duration))
        {
            return null;
        }

        if (!int.TryParse(streamMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var sampleRate))
        {
            return null;
        }

        var channelString = streamMatch.Groups[2].Value.ToLowerInvariant();
        var channels = channelString switch
        {
            "mono" => 1,
            "stereo" => 2,
            _ when channelString.Contains("5.1") => 6,
            _ when channelString.Contains("7.1") => 8,
            _ => 2  // Default to stereo
        };

        return new AudioMetadata
        {
            Duration = duration,
            SampleRate = sampleRate,
            Channels = channels
        };
    }

    [GeneratedRegex(@"Duration:\s*(\d{2}:\d{2}:\d{2}\.\d{2})")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"(\d+)\s*Hz,\s*(\w+)")]
    private static partial Regex StreamRegex();

    private sealed class AudioMetadata
    {
        public required TimeSpan Duration { get; init; }
        public required int SampleRate { get; init; }
        public required int Channels { get; init; }
    }
}
