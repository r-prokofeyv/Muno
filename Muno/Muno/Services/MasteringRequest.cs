using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Describes one PhaseLimiter mastering operation.
/// </summary>
public sealed class MasteringRequest
{
    /// <summary>
    /// Gets the input audio file path.
    /// </summary>
    public required string InputPath { get; init; }

    /// <summary>
    /// Gets the output audio file path.
    /// </summary>
    public required string OutputPath { get; init; }

    /// <summary>
    /// Gets the mastering settings for this operation.
    /// </summary>
    public required MasteringSettings Settings { get; init; }

    /// <summary>
    /// Gets the PhaseLimiter executable path.
    /// </summary>
    public required string PhaseLimiterExePath { get; init; }

    /// <summary>
    /// Gets the FFmpeg executable path.
    /// </summary>
    public required string FfmpegPath { get; init; }

    /// <summary>
    /// Gets the sound quality cache path.
    /// </summary>
    public required string SoundQualityCachePath { get; init; }

    /// <summary>
    /// Gets the writable temporary directory path.
    /// </summary>
    public required string TempDirectory { get; init; }
}
