using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Muno.Models;
using Muno.Services;
using System.Collections.ObjectModel;
using System.Threading;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Muno.ViewModels;

/// <summary>
/// ViewModel for the main page, coordinating audio file loading via the file picker or drag-and-drop.
/// </summary>
public sealed partial class MainPageViewModel : ObservableObject, IDisposable
{
    IProgress<double> _progressReporter = new Progress<double>(progress => { });
    private readonly IAudioFileLoaderService _audioFileLoaderService;
    private readonly IAudioPlaybackService _audioPlaybackService;
    private readonly IAudioExportService _audioExportService;
    private readonly IMasteringSessionService _masteringSessionService;
    private readonly ILogger<MainPageViewModel> _logger;

    private CancellationTokenSource? _loadCancellationTokenSource;

    /// <summary>
    /// Gets or sets the native window handle used to initialize WinRT pickers.
    /// Must be set by the hosting View before invoking picker-based commands.
    /// </summary>
    public nint WindowHandle { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAudioFile))]
    [NotifyPropertyChangedFor(nameof(PositionText))]
    [NotifyPropertyChangedFor(nameof(PlaybackProgress))]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    public partial AudioFile? CurrentAudioFile { get; set; }

    /// <summary>
    /// Gets whether an audio file is currently loaded, for use by view enabled-state bindings.
    /// </summary>
    public bool HasAudioFile => CurrentAudioFile != null;

    [ObservableProperty]
    public partial WaveformData? CurrentWaveformData { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsPlaying { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PositionText))]
    [NotifyPropertyChangedFor(nameof(PlaybackProgress))]
    public partial TimeSpan CurrentPosition { get; set; }

    /// <summary>
    /// Gets the formatted "current position / total duration" text for display, e.g. "1:22 / 3:31".
    /// </summary>
    public string PositionText
    {
        get
        {
            var duration = CurrentAudioFile?.Duration ?? TimeSpan.Zero;
            return $"{FormatTimeSpan(CurrentPosition)} / {FormatTimeSpan(duration)}";
        }
    }

    /// <summary>
    /// Gets the playback progress as a ratio between 0 and 1, for use by progress-indicator bindings.
    /// </summary>
    public double PlaybackProgress
    {
        get
        {
            var duration = CurrentAudioFile?.Duration ?? TimeSpan.Zero;
            if (duration <= TimeSpan.Zero)
            {
                return 0d;
            }

            var ratio = CurrentPosition.TotalSeconds / duration.TotalSeconds;
            return Math.Clamp(ratio, 0d, 1d);
        }
    }

    private static string FormatTimeSpan(TimeSpan value)
    {
        if (value < TimeSpan.Zero)
        {
            value = TimeSpan.Zero;
        }

        return value.Hours > 0
            ? $"{(int)value.TotalHours}:{value.Minutes:D2}:{value.Seconds:D2}"
            : $"{value.Minutes}:{value.Seconds:D2}";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorMessage))]
    public partial string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets whether an error message is currently set, for use by view visibility bindings.
    /// </summary>
    public Visibility HasErrorMessage => string.IsNullOrEmpty(ErrorMessage) ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MasterCommand))]
    public partial double TargetLoudness { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MasterCommand))]
    public partial double MasteringIntensity { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MasterCommand))]
    public partial double TruePeakCeiling { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MasterCommand))]
    public partial bool PreserveBass { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MasterCommand))]
    public partial OptimizationAlgorithm OptimizationAlgorithm { get; set; }

    /// <summary>
    /// Nullable adapter for the Segmented control, which can temporarily report no selected item
    /// while its template is being applied.
    /// </summary>
    public OptimizationAlgorithm? SelectedOptimizationAlgorithm
    {
        get => OptimizationAlgorithm;
        set
        {
            if (value.HasValue)
            {
                OptimizationAlgorithm = value.Value;
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MasterCommand))]
    [NotifyPropertyChangedFor(nameof(IsBitDepthVisible))]
    public partial OutputFormat OutputFormat { get; set; }

    /// <summary>
    /// Nullable adapter for the output-format Segmented control during template initialization.
    /// </summary>
    public OutputFormat? SelectedOutputFormat
    {
        get => OutputFormat;
        set
        {
            if (value.HasValue)
            {
                OutputFormat = value.Value;
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MasterCommand))]
    public partial BitDepth BitDepth { get; set; }

    /// <summary>
    /// Nullable adapter for the bit-depth Segmented control during template initialization.
    /// </summary>
    public BitDepth? SelectedBitDepth
    {
        get => BitDepth;
        set
        {
            if (value.HasValue)
            {
                BitDepth = value.Value;
            }
        }
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MasterCommand))]
    public partial int SampleRate { get; set; }

    /// <summary>
    /// Nullable adapter for the sample-rate Segmented control during template initialization.
    /// </summary>
    public int? SelectedSampleRate
    {
        get => SampleRate;
        set
        {
            if (value.HasValue)
            {
                SampleRate = value.Value;
            }
        }
    }

    /// <summary>
    /// Gets whether the bit-depth setting applies to the selected output format.
    /// </summary>
    public bool IsBitDepthVisible => OutputFormat == OutputFormat.Wav;

    /// <summary>
    /// Gets whether an original track is available for mastering.
    /// </summary>
    public bool HasOriginal => _masteringSessionService.OriginalTrack != null;

    /// <summary>
    /// Gets whether a mastering job is currently running.
    /// </summary>
    public bool IsMasteringJobRunning => _masteringSessionService.IsJobRunning;

    /// <summary>
    /// Gets the variants in the active mastering session.
    /// </summary>
    public ObservableCollection<TrackVariant> Variants => _masteringSessionService.Variants;

    public IReadOnlyList<OptimizationAlgorithm> OptimizationAlgorithms { get; } = Enum.GetValues<OptimizationAlgorithm>();

    public IReadOnlyList<OutputFormat> OutputFormats { get; } = Enum.GetValues<OutputFormat>();

    public IReadOnlyList<BitDepth> BitDepths { get; } = Enum.GetValues<BitDepth>();

    public IReadOnlyList<int> SampleRates { get; } = [44100, 48000, 88200, 96000];

    [ObservableProperty]
    public partial TrackVariant? SelectedVariant { get; set; }

    public MainPageViewModel(
        IAudioFileLoaderService audioFileLoaderService,
        IAudioPlaybackService audioPlaybackService,
        IAudioExportService audioExportService,
        IMasteringSessionService masteringSessionService,
        ILogger<MainPageViewModel> logger)
    {
        _audioFileLoaderService = audioFileLoaderService;
        _audioPlaybackService = audioPlaybackService;
        _audioExportService = audioExportService;
        _masteringSessionService = masteringSessionService;
        _logger = logger;

        var defaults = MasteringSettings.CreateDefault();
        TargetLoudness = defaults.TargetLoudness;
        MasteringIntensity = defaults.MasteringIntensity;
        TruePeakCeiling = defaults.TruePeakCeiling;
        PreserveBass = defaults.PreserveBass;
        OptimizationAlgorithm = defaults.OptimizationAlgorithm;
        OutputFormat = defaults.OutputFormat;
        BitDepth = defaults.BitDepth;
        SampleRate = defaults.SampleRate;

        _audioPlaybackService.PlaybackStateChanged += OnPlaybackStateChanged;
        _audioPlaybackService.PositionChanged += OnPositionChanged;
        _masteringSessionService.StateChanged += OnMasteringSessionStateChanged;
        _masteringSessionService.MasteringSucceeded += OnMasteringSucceeded;
    }

    private MasteringSettings BuildCurrentSettingsSnapshot() => new()
    {
        TargetLoudness = TargetLoudness,
        MasteringIntensity = MasteringIntensity/100,
        TruePeakCeiling = TruePeakCeiling,
        PreserveBass = PreserveBass,
        OptimizationAlgorithm = OptimizationAlgorithm,
        OutputFormat = OutputFormat,
        BitDepth = BitDepth,
        SampleRate = SampleRate
    };

    private bool CanMaster() =>
        HasOriginal
        && !IsMasteringJobRunning
        && (_masteringSessionService.LastSuccessfulSettings == null
            || !_masteringSessionService.LastSuccessfulSettings.Equals(BuildCurrentSettingsSnapshot()));

    [RelayCommand(CanExecute = nameof(CanMaster))]
    private async Task MasterAsync()
    {
        var snapshot = BuildCurrentSettingsSnapshot();
        var result = await _masteringSessionService.StartMasteringAsync(snapshot);
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Mastering failed to start or complete: {Error}", result.ErrorMessage);
            ErrorMessage = result.ErrorMessage ?? "Mastering failed.";
        }
    }

    private static bool CanExport(TrackExportRequest? request) => request?.Variant.CanExport == true;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync(TrackExportRequest request)
    {
        if (!request.Variant.CanExport || _masteringSessionService.OriginalTrack is not { } originalTrack)
        {
            return;
        }

        var extension = GetFormatExtension(request.Format);
        var sourceName = Path.GetFileNameWithoutExtension(originalTrack.AudioFilePath);
        var suggestedName = $"{(string.IsNullOrWhiteSpace(sourceName) ? "MasteredTrack" : sourceName)}(Master).{extension}";
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.MusicLibrary,
            SuggestedFileName = suggestedName
        };
        picker.FileTypeChoices.Add(GetFormatDisplayName(request.Format), [$".{extension}"]);

        if (WindowHandle == 0)
        {
            _logger.LogWarning("WindowHandle was not set before invoking the export picker");
            ErrorMessage = "Unable to open the export dialog.";
            return;
        }

        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);

        StorageFile? file;
        try
        {
            file = await picker.PickSaveFileAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening export save picker");
            ErrorMessage = "Failed to open the export dialog.";
            return;
        }

        if (file == null)
        {
            return;
        }

        var result = await _audioExportService.ExportAsync(
            request.Variant.AudioFilePath,
            file.Path,
            request.Format);
        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Failed to export mastered variant {Variant}: {Error}",
                request.Variant.DisplayName,
                result.ErrorMessage);
            ErrorMessage = result.ErrorMessage ?? "Failed to export the mastered audio.";
        }
    }

    private static string GetFormatExtension(OutputFormat format) => format switch
    {
        OutputFormat.Wav => "wav",
        OutputFormat.Mp3 => "mp3",
        OutputFormat.Aac => "aac",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.")
    };

    private static string GetFormatDisplayName(OutputFormat format) => format switch
    {
        OutputFormat.Wav => "WAV audio",
        OutputFormat.Mp3 => "MP3 audio",
        OutputFormat.Aac => "AAC audio",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.")
    };

    private void OnMasteringSessionStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasOriginal));
        OnPropertyChanged(nameof(IsMasteringJobRunning));
        MasterCommand.NotifyCanExecuteChanged();
        OpenFileCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Toggles playback of the currently loaded audio file between playing and stopped.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasAudioFile))]
    private void PlayPause()
    {
        if (CurrentAudioFile == null)
        {
            return;
        }

        if (IsPlaying)
        {
            var pauseResult = _audioPlaybackService.Pause();
            if (!pauseResult.IsSuccess)
            {
                _logger.LogWarning("Failed to pause playback: {Error}", pauseResult.ErrorMessage);
                ErrorMessage = pauseResult.ErrorMessage ?? "Failed to pause playback.";
            }
            // IsPlaying will be updated by OnPlaybackStateChanged event
        }
        else
        {
            var playResult = _audioPlaybackService.Play();
            if (!playResult.IsSuccess)
            {
                _logger.LogWarning("Failed to start playback: {Error}", playResult.ErrorMessage);
                ErrorMessage = playResult.ErrorMessage ?? "Failed to play the audio file.";
            }
            // IsPlaying will be updated by OnPlaybackStateChanged event
        }
    }

    /// <summary>
    /// Seeks to the position corresponding to the given ratio (0-1) of the currently loaded track's duration.
    /// </summary>
    /// <param name="ratio">A value between 0 and 1 representing the position within the track.</param>
    public void SeekToProgress(double ratio)
    {
        if (CurrentAudioFile == null)
        {
            return;
        }

        var clampedRatio = Math.Clamp(ratio, 0d, 1d);
        var targetPosition = TimeSpan.FromSeconds(CurrentAudioFile.Duration.TotalSeconds * clampedRatio);

        var seekResult = _audioPlaybackService.Seek(targetPosition);
        if (!seekResult.IsSuccess)
        {
            _logger.LogWarning("Failed to seek playback: {Error}", seekResult.ErrorMessage);
            ErrorMessage = seekResult.ErrorMessage ?? "Failed to seek the audio file.";
        }
    }

    /// <summary>
    /// Handles all playback state changes, including user-initiated Play/Pause, external controls
    /// (headphones/SMTC), natural track completion, and playback failures.
    /// </summary>
    private void OnPlaybackStateChanged(object? sender, PlaybackStateChangedEventArgs e)
    {
        IsPlaying = e.IsPlaying;

        if (!string.IsNullOrEmpty(e.ErrorMessage))
        {
            _logger.LogWarning("Playback state changed with error: {Error}", e.ErrorMessage);
            ErrorMessage = "Playback failed unexpectedly.";
        }
    }

    /// <summary>
    /// Handles playback position updates, reflecting normal progress, seeks, and track resets.
    /// </summary>
    private void OnPositionChanged(object? sender, TimeSpan position)
    {
        CurrentPosition = position;
    }

    private async Task SwitchPlaybackSourceAsync(
        string filePath,
        TimeSpan? seekPosition,
        bool autoPlay,
        CancellationToken cancellationToken)
    {
        var result = await _audioFileLoaderService.LoadAudioFileAsync(filePath, cancellationToken);
        if (!result.IsSuccess || result.Value == null)
        {
            throw new InvalidOperationException(result.ErrorMessage ?? "Failed to load the audio file.");
        }

        var playbackLoadResult = await _audioPlaybackService.LoadAsync(filePath, cancellationToken);
        if (!playbackLoadResult.IsSuccess)
        {
            throw new InvalidOperationException(
                playbackLoadResult.ErrorMessage ?? "Failed to prepare the audio file for playback.");
        }

        CurrentAudioFile = result.Value.AudioFile;
        CurrentWaveformData = result.Value.WaveformData;

        if (seekPosition.HasValue)
        {
            var clampedPosition = TimeSpan.FromSeconds(Math.Clamp(
                seekPosition.Value.TotalSeconds,
                0d,
                CurrentAudioFile.Duration.TotalSeconds));
            var seekResult = _audioPlaybackService.Seek(clampedPosition);
            if (!seekResult.IsSuccess)
            {
                _logger.LogWarning("Failed to seek switched playback source: {Error}", seekResult.ErrorMessage);
            }
        }

        if (autoPlay)
        {
            var playResult = _audioPlaybackService.Play();
            if (!playResult.IsSuccess)
            {
                throw new InvalidOperationException(playResult.ErrorMessage ?? "Failed to play the audio file.");
            }
        }
    }

    public void Dispose()
    {
        _audioPlaybackService.PlaybackStateChanged -= OnPlaybackStateChanged;
        _audioPlaybackService.PositionChanged -= OnPositionChanged;
        _masteringSessionService.StateChanged -= OnMasteringSessionStateChanged;
        _masteringSessionService.MasteringSucceeded -= OnMasteringSucceeded;
        _masteringSessionService.CancelActiveJob();
        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
    }

    public void PrepareForShutdown()
    {
        _masteringSessionService.CancelActiveJob();
    }

    private bool CanOpenFile() => !IsMasteringJobRunning;

    private async void OnMasteringSucceeded(object? sender, TrackVariant variant)
    {
        try
        {
            var capturedPosition = CurrentPosition;
            await SwitchPlaybackSourceAsync(
                variant.AudioFilePath,
                capturedPosition,
                autoPlay: true,
                CancellationToken.None);
            SelectedVariant = variant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch playback to mastered variant {Variant}", variant.DisplayName);
            ErrorMessage = "Failed to load the mastered audio variant.";
        }
    }

    [RelayCommand]
    private async Task SelectVariantAsync(TrackVariant? variant)
    {
        if (variant is null || !variant.CanSelect || ReferenceEquals(variant, SelectedVariant))
        {
            return;
        }

        var capturedPosition = CurrentPosition;
        SelectedVariant = variant;
        try
        {
            await SwitchPlaybackSourceAsync(
                variant.AudioFilePath,
                capturedPosition,
                autoPlay: true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch playback to variant {Variant}", variant.DisplayName);
            ErrorMessage = "Failed to load the selected audio variant.";
        }
    }

    /// <summary>
    /// Opens the standard Windows file picker filtered to supported audio formats and loads the selected file.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanOpenFile))]
    private async Task OpenFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.MusicLibrary
        };
        picker.FileTypeFilter.Add(".wav");
        picker.FileTypeFilter.Add(".mp3");
        picker.FileTypeFilter.Add(".aac");

        if (WindowHandle == 0)
        {
            _logger.LogWarning("WindowHandle was not set before invoking the file picker");
            ErrorMessage = "Unable to open the file picker.";
            return;
        }

        WinRT.Interop.InitializeWithWindow.Initialize(picker, WindowHandle);

        StorageFile? file;
        try
        {
            file = await picker.PickSingleFileAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening file picker");
            ErrorMessage = "Failed to open the file picker.";
            return;
        }

        if (file == null)
        {
            // User cancelled the picker.
            return;
        }

        await LoadFileAsync(file.Path);
    }

    /// <summary>
    /// Loads the audio file at the specified path, decoding it and generating waveform data.
    /// Used by both the file picker and drag-and-drop workflows.
    /// </summary>
    /// <param name="filePath">The full path to the audio file.</param>
    public async Task LoadFileAsync(string filePath)
    {
        if (_masteringSessionService.IsJobRunning)
        {
            ErrorMessage = "Cannot open a new file while mastering is in progress.";
            return;
        }

        _loadCancellationTokenSource?.Cancel();
        _loadCancellationTokenSource?.Dispose();
        var cts = new CancellationTokenSource();
        _loadCancellationTokenSource = cts;

        IsLoading = true;
        ErrorMessage = null;

        try
        {
            await SwitchPlaybackSourceAsync(filePath, seekPosition: null, autoPlay: false, cts.Token);
            if (cts.Token.IsCancellationRequested)
            {
                return;
            }

            _masteringSessionService.ResetSession(filePath);
            SelectedVariant = _masteringSessionService.OriginalTrack;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer load request; ignore.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch playback source to {FilePath}", filePath);
            ErrorMessage = ex.Message;
            CurrentAudioFile = null;
            CurrentWaveformData = null;
            CurrentPosition = TimeSpan.Zero;
        }
        finally
        {
            if (ReferenceEquals(_loadCancellationTokenSource, cts))
            {
                IsLoading = false;
            }
        }
    }
}
