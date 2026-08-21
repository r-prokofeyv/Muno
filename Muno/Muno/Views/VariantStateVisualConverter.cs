using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Muno.Models;

namespace Muno.Views;

/// <summary>
/// Converts a variant state into visibility for a state-specific visual.
/// Pass <c>Processing</c> or <c>Failed</c> as the converter parameter.
/// </summary>
public sealed class VariantStateVisualConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not VariantState state || parameter is not string expectedState)
        {
            return Visibility.Collapsed;
        }

        return Enum.TryParse<VariantState>(expectedState, ignoreCase: true, out var expected)
            && state == expected
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
