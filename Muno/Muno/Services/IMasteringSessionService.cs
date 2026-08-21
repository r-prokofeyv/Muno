using System.Collections.ObjectModel;
using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Coordinates the original track and its mastering variants for one session.
/// </summary>
public interface IMasteringSessionService
{
    /// <summary>
    /// Gets the variants currently available in the session.
    /// </summary>
    ObservableCollection<TrackVariant> Variants { get; }

    /// <summary>
    /// Gets the original track for the current session.
    /// </summary>
    TrackVariant? OriginalTrack { get; }

    /// <summary>
    /// Gets the settings used by the most recent successful mastering operation.
    /// </summary>
    MasteringSettings? LastSuccessfulSettings { get; }

    /// <summary>
    /// Gets a value indicating whether a mastering job is currently running.
    /// </summary>
    bool IsJobRunning { get; }

    /// <summary>
    /// Occurs when session state changes.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Occurs when a mastering operation completes successfully.
    /// </summary>
    event EventHandler<TrackVariant>? MasteringSucceeded;

    /// <summary>
    /// Resets the session for a new original audio file.
    /// </summary>
    /// <param name="originalFilePath">The path to the original audio file.</param>
    void ResetSession(string originalFilePath);

    /// <summary>
    /// Starts a mastering operation using an immutable settings snapshot.
    /// </summary>
    /// <param name="settingsSnapshot">The settings to use for the operation.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The created mastering variant or an operational failure.</returns>
    Task<Result<TrackVariant>> StartMasteringAsync(
        MasteringSettings settingsSnapshot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests cancellation of the active mastering operation.
    /// </summary>
    void CancelActiveJob();
}
