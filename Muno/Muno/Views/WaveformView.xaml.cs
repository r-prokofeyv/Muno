using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Muno.Models;
using Windows.UI;

namespace Muno.Views;

/// <summary>
/// A reusable control that renders the waveform of a decoded audio file using Win2D.
/// </summary>
public sealed partial class WaveformView : UserControl
{
    /// <summary>
    /// Identifies the <see cref="WaveformData"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty WaveformDataProperty = DependencyProperty.Register(
        nameof(WaveformData),
        typeof(WaveformData),
        typeof(WaveformView),
        new PropertyMetadata(null, OnWaveformDataChanged));

    /// <summary>
    /// Gets or sets the waveform peak data to render.
    /// </summary>
    public WaveformData? WaveformData
    {
        get => (WaveformData?)GetValue(WaveformDataProperty);
        set => SetValue(WaveformDataProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="PlaybackProgress"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PlaybackProgressProperty = DependencyProperty.Register(
        nameof(PlaybackProgress),
        typeof(double),
        typeof(WaveformView),
        new PropertyMetadata(0d, OnPlaybackProgressChanged));

    /// <summary>
    /// Gets or sets the current playback progress as a ratio between 0 and 1.
    /// </summary>
    public double PlaybackProgress
    {
        get => (double)GetValue(PlaybackProgressProperty);
        set => SetValue(PlaybackProgressProperty, value);
    }

    /// <summary>
    /// Raised when the user clicks/taps within the waveform, requesting a seek to the
    /// corresponding ratio (0-1) of the track.
    /// </summary>
    public event EventHandler<double>? SeekRequested;

    public WaveformView()
    {
        InitializeComponent();
    }

    private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformView view)
        {
            view.WaveformCanvas.Invalidate();
        }
    }

    private static void OnPlaybackProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is WaveformView view)
        {
            view.WaveformCanvas.Invalidate();
        }
    }

    private void Grid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(WaveformCanvas);
        var width = WaveformCanvas.ActualWidth;
        if (width <= 0)
        {
            return;
        }

        var ratio = Math.Clamp(point.Position.X / width, 0d, 1d);
        SeekRequested?.Invoke(this, ratio);
    }

    private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var waveformData = WaveformData;
        if (waveformData == null || waveformData.MinPeaks.Length == 0)
        {
            return;
        }

        var width = (float)sender.ActualWidth;
        var height = (float)sender.ActualHeight;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var minPeaks = waveformData.MinPeaks;
        var maxPeaks = waveformData.MaxPeaks;
        var peakCount = minPeaks.Length;
        var midY = height / 2f;
        var halfHeight = height / 2f;

        var waveformColor = Color.FromArgb(255, 0, 175, 240);
        var playedColor = Color.FromArgb(255, 0, 88, 120);
        var indicatorColor = Colors.White;

        var progress = Math.Clamp(PlaybackProgress, 0d, 1d);
        var progressX = width * (float)progress;

        for (var i = 0; i < peakCount; i++)
        {
            var x = width * i / peakCount;
            var yMin = midY - (maxPeaks[i] * halfHeight);
            var yMax = midY - (minPeaks[i] * halfHeight);

            var color = x < progressX ? playedColor : waveformColor;
            args.DrawingSession.DrawLine(x, yMin, x, yMax, color);
        }

        args.DrawingSession.DrawLine(progressX, 0, progressX, height, indicatorColor, 2f);
    }
}
