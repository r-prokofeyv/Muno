using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Exports mastered audio files to supported audio formats.
/// </summary>
public interface IAudioExportService
{
    /// <summary>
    /// Exports a mastered track to the specified destination path and format.
    /// </summary>
    /// <param name="sourcePath">The path to the mastered source audio file.</param>
    /// <param name="destinationPath">The path for the exported audio file.</param>
    /// <param name="format">The output audio format.</param>
    /// <param name="cancellationToken">A token that cancels the export.</param>
    /// <returns>The result of the export operation.</returns>
    Task<Result> ExportAsync(
        string sourcePath,
        string destinationPath,
        OutputFormat format,
        CancellationToken cancellationToken = default);
}
