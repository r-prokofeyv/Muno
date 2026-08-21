using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Windows.Media.Playback;
using Windows.Storage;

namespace Muno.Services;

/// <summary>
/// Plays audio files using the Windows Media Playback APIs.
/// </summary>
public sealed class AudioPlaybackService : IAudioPlaybackService
{
    private readonly ILogger<AudioPlaybackService> _logger;
    private readonly MediaPlayer _mediaPlayer;
    private readonly DispatcherQueue? _dispatcherQueue;
    private bool _disposed;
    private bool _isSourceLoaded;

    public event EventHandler<PlaybackStateChangedEventArgs>? PlaybackStateChanged;
    public event EventHandler<TimeSpan>? PositionChanged;

    public AudioPlaybackService(ILogger<AudioPlaybackService> logger)
    {
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _mediaPlayer = new MediaPlayer();
        _mediaPlayer.MediaEnded += OnMediaEnded;
        _mediaPlayer.MediaFailed += OnMediaFailed;
        _mediaPlayer.PlaybackSession.PlaybackStateChanged += OnPlaybackSessionStateChanged;
        _mediaPlayer.PlaybackSession.PositionChanged += OnPlaybackSessionPositionChanged;
    }

    public async Task<Result> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            ResetPlaybackState();

            var file = await StorageFile.GetFileFromPathAsync(filePath).AsTask(cancellationToken);
            _mediaPlayer.Source = Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
            _isSourceLoaded = true;
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _isSourceLoaded = false;
            _logger.LogError(ex, "Failed to load audio file for playback: {FilePath}", filePath);
            return Result.Failure("Failed to load the audio file for playback.");
        }
    }

    public Result Play()
    {
        if (!_isSourceLoaded)
        {
            _logger.LogWarning("Play was called with no audio file loaded");
            return Result.Failure("No audio file is loaded.");
        }

        try
        {
            _mediaPlayer.Play();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start playback");
            return Result.Failure("Failed to play the audio file.");
        }
    }

    public Result Pause()
    {
        if (!_isSourceLoaded)
        {
            _logger.LogWarning("Pause was called with no audio file loaded");
            return Result.Failure("No audio file is loaded.");
        }

        try
        {
            _mediaPlayer.Pause();
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause playback");
            return Result.Failure("Failed to pause the audio file.");
        }
    }

    public Result Seek(TimeSpan position)
    {
        if (!_isSourceLoaded)
        {
            _logger.LogWarning("Seek was called with no audio file loaded");
            return Result.Failure("No audio file is loaded.");
        }

        try
        {
            var session = _mediaPlayer.PlaybackSession;
            var clamped = position;
            if (clamped < TimeSpan.Zero)
            {
                clamped = TimeSpan.Zero;
            }
            else if (session.NaturalDuration > TimeSpan.Zero && clamped > session.NaturalDuration)
            {
                clamped = session.NaturalDuration;
            }

            session.Position = clamped;

            // The native PositionChanged event may not fire reliably while paused, so raise
            // it explicitly for immediate UI feedback.
            RaisePositionChanged(clamped);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seek playback");
            return Result.Failure("Failed to seek the audio file.");
        }
    }

    /// <summary>
    /// Pauses playback and resets the playback position, used when swapping tracks or after
    /// a track finishes/fails on its own.
    /// </summary>
    private void ResetPlaybackState()
    {
        try
        {
            _mediaPlayer.Pause();
            if (_mediaPlayer.PlaybackSession != null)
            {
                _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset playback state");
        }
        finally
        {
            RaisePositionChanged(TimeSpan.Zero);
        }
    }

    private void OnMediaEnded(MediaPlayer sender, object args)
    {
        // Reset position to beginning so the next Play starts from 0.
        // The session will naturally transition to Paused, which OnPlaybackSessionStateChanged will report.
        try
        {
            if (_mediaPlayer.PlaybackSession != null)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset position after media ended");
        }
    }

    private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        _logger.LogError("Media playback failed: {Error}", args.ErrorMessage);

        // Reset position since playback failed.
        try
        {
            if (_mediaPlayer.PlaybackSession != null)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.PlaybackSession.Position = TimeSpan.Zero;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reset position after media failure");
        }

        // Explicitly raise state change with error, since the PlaybackSession state transition
        // alone won't carry the error message context.
        RaisePlaybackStateChanged(new PlaybackStateChangedEventArgs(IsPlaying: false, ErrorMessage: args.ErrorMessage));
    }

    /// <summary>
    /// Handles MediaPlayer.PlaybackSession.PlaybackStateChanged, the canonical source of playback state
    /// for all control paths (in-app, external hardware/SMTC, natural completion).
    /// </summary>
    private void OnPlaybackSessionStateChanged(MediaPlaybackSession sender, object args)
    {
        var state = sender.PlaybackState;

        // Ignore transient Opening/Buffering states to avoid UI flicker; only report stable Playing/Paused/None.
        bool? isPlaying = state switch
        {
            MediaPlaybackState.Playing => true,
            MediaPlaybackState.Paused or MediaPlaybackState.None => false,
            _ => null // Opening, Buffering — ignore
        };

        if (isPlaying.HasValue)
        {
            RaisePlaybackStateChanged(new PlaybackStateChangedEventArgs(IsPlaying: isPlaying.Value));
        }
    }

    private void RaisePlaybackStateChanged(PlaybackStateChangedEventArgs eventArgs)
    {
        if (_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => PlaybackStateChanged?.Invoke(this, eventArgs));
        }
        else
        {
            PlaybackStateChanged?.Invoke(this, eventArgs);
        }
    }

    /// <summary>
    /// Handles MediaPlayer.PlaybackSession.PositionChanged, the native source of playback progress
    /// for all control paths (in-app, external hardware/SMTC).
    /// </summary>
    private void OnPlaybackSessionPositionChanged(MediaPlaybackSession sender, object args)
    {
        RaisePositionChanged(sender.Position);
    }

    private void RaisePositionChanged(TimeSpan position)
    {
        if (_dispatcherQueue != null && !_dispatcherQueue.HasThreadAccess)
        {
            _dispatcherQueue.TryEnqueue(() => PositionChanged?.Invoke(this, position));
        }
        else
        {
            PositionChanged?.Invoke(this, position);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _mediaPlayer.PlaybackSession.PlaybackStateChanged -= OnPlaybackSessionStateChanged;
        _mediaPlayer.PlaybackSession.PositionChanged -= OnPlaybackSessionPositionChanged;
        _mediaPlayer.MediaEnded -= OnMediaEnded;
        _mediaPlayer.MediaFailed -= OnMediaFailed;
        _mediaPlayer.Dispose();
    }
}
