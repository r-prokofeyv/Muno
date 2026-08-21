using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Service for generating downsampled waveform peak data from decoded PCM audio.
/// </summary>
public interface IWaveformGeneratorService
{
    /// <summary>
    /// Generates downsampled min/max peak waveform data from an audio file's PCM data.
    /// </summary>
    /// <param name="audioFile">The decoded audio file containing PCM data.</param>
    /// <param name="targetPeakCount">The desired number of peak pairs (columns) to generate.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A result containing the generated WaveformData or an error message.</returns>
    Task<Result<WaveformData>> GenerateWaveformAsync(AudioFile audioFile, int targetPeakCount, CancellationToken cancellationToken = default);
}
