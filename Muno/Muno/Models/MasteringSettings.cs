namespace Muno.Models;

/// <summary>
/// Represents an immutable snapshot of the settings used for a mastering operation.
/// </summary>
public sealed record MasteringSettings
{
    /// <summary>
    /// Gets the target integrated loudness in LUFS.
    /// </summary>
    public required double TargetLoudness { get; init; }

    /// <summary>
    /// Gets the mastering intensity.
    /// </summary>
    public required double MasteringIntensity { get; init; }

    /// <summary>
    /// Gets the true-peak ceiling in dBTP.
    /// </summary>
    public required double TruePeakCeiling { get; init; }

    /// <summary>
    /// Gets a value indicating whether bass should be preserved.
    /// </summary>
    public required bool PreserveBass { get; init; }

    /// <summary>
    /// Gets the optimization algorithm.
    /// </summary>
    public required OptimizationAlgorithm OptimizationAlgorithm { get; init; }

    /// <summary>
    /// Gets the output audio format.
    /// </summary>
    public required OutputFormat OutputFormat { get; init; }

    /// <summary>
    /// Gets the output bit depth.
    /// </summary>
    public required BitDepth BitDepth { get; init; }

    /// <summary>
    /// Gets the output sample rate in Hz.
    /// </summary>
    public required int SampleRate { get; init; }

    /// <summary>
    /// Creates the default mastering settings.
    /// </summary>
    public static MasteringSettings CreateDefault() => new()
    {
        TargetLoudness = -9,
        MasteringIntensity = 100,
        TruePeakCeiling = 0,
        PreserveBass = false,
        OptimizationAlgorithm = OptimizationAlgorithm.DePrmm,
        OutputFormat = OutputFormat.Wav,
        BitDepth = BitDepth.Bit16,
        SampleRate = 44100
    };
}
