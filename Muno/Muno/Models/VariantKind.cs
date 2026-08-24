namespace Muno.Models;

/// <summary>
/// Specifies the kind of a track variant.
/// </summary>
public enum VariantKind
{
    /// <summary>
    /// The original, unmastered track.
    /// </summary>
    Original,

    /// <summary>
    /// A mastered version of the track.
    /// </summary>
    Mastering,

    /// <summary>
    /// A simplified dominant-melody rendering derived from the original track.
    /// </summary>
    Melody
}
