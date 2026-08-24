namespace Muno.Models;

/// <summary>
/// Describes the files produced by melody simplification.
/// </summary>
public sealed record MelodySimplificationResult
{
    public required string AudioFilePath { get; init; }
    public required string MidiFilePath { get; init; }
    public required int NoteCount { get; init; }
}
