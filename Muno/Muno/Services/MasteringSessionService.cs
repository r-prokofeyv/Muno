using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Muno.Models;

namespace Muno.Services;

/// <summary>
/// Coordinates mastering jobs and variants for the active audio session.
/// </summary>
public sealed class MasteringSessionService : IMasteringSessionService
{
    private readonly IPhaseLimiterEngine _phaseLimiterEngine;
    private readonly ILogger<MasteringSessionService> _logger;
    private readonly DispatcherQueue? _dispatcherQueue;
    private readonly ObservableCollection<TrackVariant> _variants = [];

    private int _nextMasteringNumber = 1;
    private CancellationTokenSource? _activeJobCts;
    private string? _sessionWorkDirectory;

    public MasteringSessionService(
        IPhaseLimiterEngine phaseLimiterEngine,
        ILogger<MasteringSessionService> logger)
    {
        _phaseLimiterEngine = phaseLimiterEngine;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    /// <inheritdoc />
    public ObservableCollection<TrackVariant> Variants => _variants;

    /// <inheritdoc />
    public TrackVariant? OriginalTrack { get; private set; }

    /// <inheritdoc />
    public MasteringSettings? LastSuccessfulSettings { get; private set; }

    /// <inheritdoc />
    public bool IsJobRunning { get; private set; }

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public event EventHandler<TrackVariant>? MasteringSucceeded;

    /// <inheritdoc />
    public void ResetSession(string originalFilePath)
    {
        CancelActiveJob();

        var previousWorkDirectory = _sessionWorkDirectory;
        _sessionWorkDirectory = Path.Combine(
            Path.GetTempPath(),
            "Muno",
            Guid.NewGuid().ToString("N"));

        if (!string.IsNullOrEmpty(previousWorkDirectory))
        {
            _ = Task.Run(() =>
            {
                try
                {
                    if (Directory.Exists(previousWorkDirectory))
                    {
                        Directory.Delete(previousWorkDirectory, recursive: true);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to clean up previous mastering session directory: {Directory}",
                        previousWorkDirectory);
                }
            });
        }

        RunOnUiThread(() =>
        {
            Variants.Clear();
            _nextMasteringNumber = 1;
            LastSuccessfulSettings = null;

            OriginalTrack = new TrackVariant
            {
                DisplayName = "Original track",
                AudioFilePath = originalFilePath,
                Kind = VariantKind.Original,
                MasteringNumber = 0,
                State = VariantState.Ready,
                CanSelect = true
            };

            Variants.Add(OriginalTrack);
            RaiseStateChanged();
        });
    }

    /// <inheritdoc />
    public async Task<Result<TrackVariant>> StartMasteringAsync(
        MasteringSettings settingsSnapshot,
        CancellationToken cancellationToken = default)
    {
        if (IsJobRunning)
        {
            return Result<TrackVariant>.Failure("A mastering job is already running.");
        }

        if (OriginalTrack == null)
        {
            return Result<TrackVariant>.Failure("No Original track is loaded.");
        }

        var originalTrack = OriginalTrack;
        var number = _nextMasteringNumber++;
        var extension = GetOutputExtension(settingsSnapshot.OutputFormat);
        var outputPath = Path.Combine(_sessionWorkDirectory!, $"Mastering_{number}.{extension}");
        var variant = new TrackVariant
        {
            DisplayName = $"Mastering {number}",
            AudioFilePath = outputPath,
            Kind = VariantKind.Mastering,
            MasteringNumber = number,
            SettingsSnapshot = settingsSnapshot,
            State = VariantState.Processing,
            Progress = 0,
            CanSelect = false
        };

        RunOnUiThread(() =>
        {
            Variants.Add(variant);
            IsJobRunning = true;
            RaiseStateChanged();
            RefreshSelectability();
        });

        _activeJobCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var request = new MasteringRequest
        {
            InputPath = originalTrack.AudioFilePath,
            OutputPath = outputPath,
            Settings = settingsSnapshot,
            PhaseLimiterExePath = Path.Combine(
                AppContext.BaseDirectory,
                "Libs",
                "PhaseLimiter",
                "bin",
                "phase_limiter.exe"),
            FfmpegPath = Path.Combine(AppContext.BaseDirectory, "Libs", "ffmpeg.exe"),
            SoundQualityCachePath = Path.Combine(
                AppContext.BaseDirectory,
                "Libs",
                "PhaseLimiter",
                "resource",
                "sound_quality2_cache"),
            TempDirectory = Path.Combine(_sessionWorkDirectory!, $"tmp_{number}")
        };

        _logger.LogDebug(
            "Starting mastering request for variant {VariantNumber} using original input {InputPath}.",
            number,
            request.InputPath);

        var progress = new Progress<double>(value =>
            RunOnUiThread(() => variant.Progress = Math.Clamp(value, 0d, 1d)*1000));

        try
        {
            var engineResult = await _phaseLimiterEngine.RunAsync(request, progress, _activeJobCts.Token);
            if (engineResult.IsSuccess)
            {
                RunOnUiThread(() =>
                {
                    variant.Progress = 1.0;
                    variant.State = VariantState.Ready;
                    LastSuccessfulSettings = settingsSnapshot;
                    CompleteJobState();
                });

                RaiseMasteringSucceeded(variant);
                return Result<TrackVariant>.Success(variant);
            }

            var errorMessage = engineResult.ErrorMessage ?? "Mastering failed.";
            RunOnUiThread(() =>
            {
                variant.State = VariantState.Failed;
                variant.ErrorMessage = errorMessage;
                CompleteJobState();
            });
            _logger.LogWarning(
                "Mastering variant {VariantNumber} failed: {Error}",
                number,
                errorMessage);
            return Result<TrackVariant>.Failure(errorMessage);
        }
        catch (Exception ex)
        {
            var errorMessage = "An unexpected error occurred while mastering the audio.";
            RunOnUiThread(() =>
            {
                variant.State = VariantState.Failed;
                variant.ErrorMessage = errorMessage;
            });
            _logger.LogError(ex, "Unexpected error while creating mastering variant {VariantNumber}.", number);
            return Result<TrackVariant>.Failure(errorMessage);
        }
        finally
        {
            RunOnUiThread(CompleteJobStateIfRunning);
            _activeJobCts?.Dispose();
            _activeJobCts = null;
        }
    }

    /// <inheritdoc />
    public void CancelActiveJob()
    {
        if (_activeJobCts is { IsCancellationRequested: false })
        {
            _activeJobCts.Cancel();
        }
    }

    private void CompleteJobState()
    {
        IsJobRunning = false;
        RefreshSelectability();
        RaiseStateChanged();
    }

    private void CompleteJobStateIfRunning()
    {
        if (IsJobRunning)
        {
            CompleteJobState();
        }
    }

    private void RefreshSelectability()
    {
        RunOnUiThread(() =>
        {
            foreach (var variant in Variants)
            {
                variant.CanSelect = variant.Kind == VariantKind.Original
                    || variant.State == VariantState.Ready && !IsJobRunning;
            }
        });
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseMasteringSucceeded(TrackVariant variant)
    {
        try
        {
            MasteringSucceeded?.Invoke(this, variant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A mastering success subscriber threw an exception.");
        }
    }

    private void RunOnUiThread(Action action)
    {
        if (_dispatcherQueue == null || _dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        if (!_dispatcherQueue.TryEnqueue(() => action()))
        {
            _logger.LogWarning("Unable to marshal mastering session update to the UI thread.");
        }
    }

    private static string GetOutputExtension(OutputFormat outputFormat) => outputFormat switch
    {
        OutputFormat.Wav => "wav",
        OutputFormat.Mp3 => "mp3",
        OutputFormat.Aac => "aac",
        _ => throw new ArgumentOutOfRangeException(nameof(outputFormat), outputFormat, "Unknown output format.")
    };
}
