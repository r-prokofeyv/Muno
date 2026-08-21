using CommunityToolkit.Mvvm.ComponentModel;

namespace Muno.Models;

/// <summary>
/// Represents an original or mastered variant of an audio track.
/// </summary>
public sealed partial class TrackVariant : ObservableObject
{
    /// <summary>
    /// Gets the display name shown to the user.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets or sets the path to the variant's audio file.
    /// </summary>
    public required string AudioFilePath { get; set; }

    /// <summary>
    /// Gets the kind of this track variant.
    /// </summary>
    public required VariantKind Kind { get; init; }

    /// <summary>
    /// Gets the mastering number for this variant.
    /// </summary>
    public required int MasteringNumber { get; init; }

    /// <summary>
    /// Gets the settings snapshot used to create this variant, if it is a mastered variant.
    /// </summary>
    public MasteringSettings? SettingsSnapshot { get; init; }

    /// <summary>
    /// Gets or sets the processing state of this variant.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExport))]
    public partial VariantState State { get; set; }

    /// <summary>
    /// Gets whether this variant has a completed mastering output that can be exported.
    /// </summary>
    public bool CanExport =>
        Kind == VariantKind.Mastering
        && State == VariantState.Ready
        && !string.IsNullOrWhiteSpace(AudioFilePath)
        && File.Exists(AudioFilePath);

    /// <summary>
    /// Gets or sets the processing progress from 0 to 1.
    /// </summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this variant can be selected.
    /// </summary>
    [ObservableProperty]
    public partial bool CanSelect { get; set; } = true;

    /// <summary>
    /// Gets or sets the user-facing processing error, if any.
    /// </summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }
}
