namespace Muno.Services;

/// <summary>
/// Event arguments describing a playback state transition.
/// </summary>
public sealed record PlaybackStateChangedEventArgs(bool IsPlaying, string? ErrorMessage = null);

/// <summary>
/// Service for playing back a loaded audio file. Keeps all media-player specific
/// details isolated from the ViewModel/View layers.
/// </summary>
public interface IAudioPlaybackService : IDisposable
{
    /// <summary>
    /// Raised when playback state changes for any reason: user-initiated Play/Pause,
    /// external control (headset/hardware buttons, SMTC), natural track completion, or failure.
    /// </summary>
    event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;

    /// <summary>
    /// Raised when the current playback position changes, whether from normal playback progress,
    /// a seek operation, or a reset of the current track.
    /// </summary>
    event EventHandler<TimeSpan>? PositionChanged;

    /// <summary>
    /// Loads the audio file at the specified path as the current playback source, stopping and
    /// resetting any previously loaded playback. Does not start playback.
    /// </summary>
    /// <param name="filePath">The full path to the audio file to load.</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    Task<Result> LoadAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts or resumes playback of the currently loaded audio file from its current position.
    /// </summary>
    Result Play();

    /// <summary>
    /// Pauses playback of the currently loaded audio file, preserving the current position.
    /// </summary>
    Result Pause();

    /// <summary>
    /// Moves the playback position of the currently loaded audio file to the specified position,
    /// clamped to the valid range of the track.
    /// </summary>
    /// <param name="position">The target position within the track.</param>
    Result Seek(TimeSpan position);
}
