using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Service for decoding audio files to PCM data using ffmpeg.
/// </summary>
public interface IAudioDecodingService
{
    /// <summary>
    /// Decodes an audio file to PCM data and extracts metadata.
    /// </summary>
    /// <param name="filePath">The path to the audio file.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A result containing the decoded AudioFile or an error message.</returns>
    Task<Result<AudioFile>> DecodeAudioFileAsync(string filePath, CancellationToken cancellationToken = default);
}
