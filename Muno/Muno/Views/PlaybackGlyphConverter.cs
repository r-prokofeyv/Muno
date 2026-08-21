using Microsoft.UI.Xaml.Data;

namespace Muno.Views;

/// <summary>
/// Converts a playback boolean state into the corresponding Segoe Fluent Icons glyph
/// for the round Play/Pause button (Play when not playing, Pause when playing).
/// </summary>
public sealed class PlaybackGlyphConverter : IValueConverter
{
    private const string PlayGlyph = "\uE768";
    private const string PauseGlyph = "\uE769";

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? PauseGlyph : PlayGlyph;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
