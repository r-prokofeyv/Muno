namespace Muno.Models;

/// <summary>
/// Specifies the processing state of a track variant.
/// </summary>
public enum VariantState
{
    /// <summary>
    /// The variant is ready for use.
    /// </summary>
    Ready,

    /// <summary>
    /// The variant is currently being processed.
    /// </summary>
    Processing,

    /// <summary>
    /// Processing the variant failed.
    /// </summary>
    Failed
}
