using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Muno.Models;
using Muno.Services;

namespace Muno.ViewModels;

public sealed partial class MainPageViewModel
{
    private IMelodySimplificationService _melodySimplificationService = null!;
    private CancellationTokenSource? _melodyCancellationTokenSource;

    /// <summary>
    /// Gets whether the original track can currently be simplified to a melody.
    /// </summary>
    public bool CanSimplifyMelody =>
        HasOriginal
        && !IsMasteringJobRunning
        && !IsSimplifyingMelody;

    /// <summary>
    /// Gets whether primary file/mastering actions should remain enabled.
    /// </summary>
    public bool CanUsePrimaryActions => !IsSimplifyingMelody;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSimplifyMelody))]
    [NotifyPropertyChangedFor(nameof(CanUsePrimaryActions))]
    [NotifyCanExecuteChangedFor(nameof(SimplifyMelodyCommand))]
    public partial bool IsSimplifyingMelody { get; set; }

    /// <summary>
    /// Supplies the melody service through the application DI factory.
    /// </summary>
    internal void SetMelodySimplificationService(IMelodySimplificationService service)
    {
        _melodySimplificationService = service;
        _masteringSessionService.StateChanged += OnMelodyRelevantSessionStateChanged;
        RefreshMelodyCommandState();
    }

    /// <summary>
    /// Cancels a running melody simplification operation.
    /// </summary>
    public void CancelMelodySimplification()
    {
        if (_melodyCancellationTokenSource is { IsCancellationRequested: false })
        {
            _melodyCancellationTokenSource.Cancel();
        }
    }

    private void OnMelodyRelevantSessionStateChanged(object? sender, EventArgs e)
    {
        RefreshMelodyCommandState();
    }

    private void RefreshMelodyCommandState()
    {
        OnPropertyChanged(nameof(CanSimplifyMelody));
        SimplifyMelodyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSimplifyMelody))]
    private async Task SimplifyMelodyAsync()
    {
        if (_masteringSessionService.OriginalTrack is not { } originalTrack
            || IsMasteringJobRunning
            || IsSimplifyingMelody)
        {
            return;
        }

        _melodyCancellationTokenSource?.Cancel();
        _melodyCancellationTokenSource?.Dispose();
        _melodyCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _melodyCancellationTokenSource.Token;

        var variant = new TrackVariant
        {
            DisplayName = "Simplified melody",
            AudioFilePath = string.Empty,
            Kind = VariantKind.Melody,
            MasteringNumber = 0,
            State = VariantState.Processing,
            Progress = 0,
            CanSelect = false
        };

        ErrorMessage = null;
        IsSimplifyingMelody = true;
        Variants.Add(variant);
        MasterCommand.NotifyCanExecuteChanged();
        OpenFileCommand.NotifyCanExecuteChanged();

        var progress = new Progress<double>(value =>
            variant.Progress = Math.Clamp(value, 0d, 1d) * 1000d);

        try
        {
            var result = await _melodySimplificationService.SimplifyAsync(
                originalTrack.AudioFilePath,
                progress,
                cancellationToken);

            if (!result.IsSuccess || result.Value is null)
            {
                variant.State = VariantState.Failed;
                variant.ErrorMessage = result.ErrorMessage ?? "Melody simplification failed.";
                ErrorMessage = variant.ErrorMessage;
                _logger.LogWarning("Melody simplification failed: {Error}", variant.ErrorMessage);
                return;
            }

            variant.AudioFilePath = result.Value.AudioFilePath;
            variant.MidiFilePath = result.Value.MidiFilePath;
            variant.Progress = 1000d;
            variant.State = VariantState.Ready;
            variant.CanSelect = true;

            var capturedPosition = CurrentPosition;
            await SwitchPlaybackSourceAsync(
                variant.AudioFilePath,
                capturedPosition,
                autoPlay: true,
                CancellationToken.None);
            SelectedVariant = variant;
        }
        catch (OperationCanceledException)
        {
            variant.State = VariantState.Failed;
            variant.ErrorMessage = "Melody simplification was cancelled.";
        }
        catch (Exception ex)
        {
            variant.State = VariantState.Failed;
            variant.ErrorMessage = "Failed to load the simplified melody.";
            ErrorMessage = variant.ErrorMessage;
            _logger.LogError(ex, "Failed while finalizing the simplified melody variant.");
        }
        finally
        {
            IsSimplifyingMelody = false;
            MasterCommand.NotifyCanExecuteChanged();
            OpenFileCommand.NotifyCanExecuteChanged();
            _melodyCancellationTokenSource?.Dispose();
            _melodyCancellationTokenSource = null;
        }
    }
}
