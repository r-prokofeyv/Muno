using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Muno.Models;

namespace Muno.Views;

/// <summary>
/// Converts an output format into the visibility of WAV-only bit-depth controls.
/// </summary>
public sealed class BitDepthVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is OutputFormat.Wav ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
