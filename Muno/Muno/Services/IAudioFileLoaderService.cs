using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Represents the combined result of loading and analyzing an audio file.
/// </summary>
public sealed class LoadedAudio
{
    /// <summary>
    /// Gets the decoded audio file with metadata and PCM data.
    /// </summary>
    public required AudioFile AudioFile { get; init; }

    /// <summary>
    /// Gets the generated waveform peak data for visualization.
    /// </summary>
    public required WaveformData WaveformData { get; init; }
}

/// <summary>
/// Orchestrates loading an audio file: validating its extension, decoding it, and generating waveform data.
/// This is the single shared entry point used by both the file picker and drag-and-drop workflows.
/// </summary>
public interface IAudioFileLoaderService
{
    /// <summary>
    /// Loads an audio file, validating its extension is supported, decoding it, and generating waveform data.
    /// </summary>
    /// <param name="filePath">The path to the audio file.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A result containing the loaded audio file and its waveform data, or an error message.</returns>
    Task<Result<LoadedAudio>> LoadAudioFileAsync(string filePath, CancellationToken cancellationToken = default);
}
