using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Converts a mixed audio track into a simplified monophonic melody and renders it to audio.
/// </summary>
public interface IMelodySimplificationService
{
    /// <summary>
    /// Extracts a dominant melody, writes it as MIDI, then renders the MIDI to MP3.
    /// </summary>
    Task<Result<MelodySimplificationResult>> SimplifyAsync(
        string inputPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
