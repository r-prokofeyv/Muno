namespace Muno.Models;

/// <summary>
/// Represents an audio file with its metadata and decoded PCM data.
/// </summary>
public sealed class AudioFile
{
    /// <summary>
    /// Gets the full path to the audio file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Gets the file name without the directory path.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// Gets the duration of the audio file.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets the sample rate in Hz (e.g., 44100, 48000).
    /// </summary>
    public required int SampleRate { get; init; }

    /// <summary>
    /// Gets the number of audio channels (1=mono, 2=stereo).
    /// </summary>
    public required int Channels { get; init; }

    /// <summary>
    /// Gets the decoded PCM audio data as floating-point samples.
    /// Interleaved format: for stereo [L, R, L, R, ...].
    /// Range: typically -1.0 to 1.0.
    /// </summary>
    public required float[] PcmData { get; init; }
}
