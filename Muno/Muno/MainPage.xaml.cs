using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Muno.ViewModels;
using Muno.Models;
using Windows.Storage;

namespace Muno;

/// <summary>
/// The main page hosting the command bar, waveform view, and drag-and-drop target.
/// </summary>
public sealed partial class MainPage : Page
{
    /// <summary>
    /// Gets the ViewModel backing this page.
    /// </summary>
    public MainPageViewModel ViewModel { get; }

    public MainPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<MainPageViewModel>();
        InitializeComponent();
    }

    /// <summary>
    /// Sets the native window handle required by the file picker. Must be called by the hosting window.
    /// </summary>
    /// <param name="windowHandle">The native window handle.</param>
    public void SetWindowHandle(nint windowHandle)
    {
        ViewModel.WindowHandle = windowHandle;
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
        }
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        if (items.Count == 0)
        {
            return;
        }

        if (items[0] is StorageFile file)
        {
            await ViewModel.LoadFileAsync(file.Path);
        }
    }

    private void WaveformView_SeekRequested(object? sender, double ratio)
    {
        ViewModel.SeekToProgress(ratio);
    }

    private void VariantsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is TrackVariant variant)
        {
            ViewModel.SelectVariantCommand.Execute(variant);
        }
    }

    private async void ExportMenuFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem menuItem
            || menuItem.Tag is not TrackVariant variant
            || menuItem.CommandParameter is not string formatName
            || !Enum.TryParse<OutputFormat>(formatName, out var format))
        {
            return;
        }

        await ViewModel.ExportCommand.ExecuteAsync(new TrackExportRequest
        {
            Variant = variant,
            Format = format
        });
    }
}
