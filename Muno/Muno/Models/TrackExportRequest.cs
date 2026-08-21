namespace Muno.Models;

/// <summary>
/// Describes an export operation for a mastered track variant.
/// </summary>
public sealed record TrackExportRequest
{
    /// <summary>
    /// Gets the mastered track variant to export.
    /// </summary>
    public required TrackVariant Variant { get; init; }

    /// <summary>
    /// Gets the format to use for the exported file.
    /// </summary>
    public required OutputFormat Format { get; init; }
}
