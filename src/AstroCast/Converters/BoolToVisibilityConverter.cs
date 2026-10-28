using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AstroCast.Converters;

/// <summary>
/// Converts a <see cref="bool"/> value to <see cref="Visibility"/>.
/// <c>true</c> maps to <see cref="Visibility.Visible"/>;
/// <c>false</c> maps to <see cref="Visibility.Collapsed"/>.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}
