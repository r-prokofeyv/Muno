using System.Diagnostics.CodeAnalysis;

namespace Muno.Models;

/// <summary>
/// Represents downsampled waveform peak data for visualization.
/// </summary>
public sealed class WaveformData
{
    /// <summary>
    /// Initializes a new instance with empty defaults.
    /// This parameterless constructor exists so the WinUI XAML compiler can
    /// generate type metadata for <see cref="WaveformData"/> when it is used
    /// as a dependency property type; callers should use the required members
    /// below to populate real waveform data.
    /// </summary>
    [SetsRequiredMembers]
    public WaveformData()
    {
        MinPeaks = [];
        MaxPeaks = [];
    }

    /// <summary>
    /// Gets the minimum peak values for each sample window.
    /// One value per pixel/column in the waveform display.
    /// </summary>
    public required float[] MinPeaks { get; set; }

    /// <summary>
    /// Gets the maximum peak values for each sample window.
    /// One value per pixel/column in the waveform display.
    /// </summary>
    public required float[] MaxPeaks { get; set; }

    /// <summary>
    /// Gets the total number of audio samples represented by this waveform data.
    /// </summary>
    public required int TotalSampleCount { get; set; }

    /// <summary>
    /// Gets the number of audio channels (1=mono, 2=stereo).
    /// </summary>
    public required int Channels { get; set; }
}
