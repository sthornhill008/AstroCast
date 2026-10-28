using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AstroCast.Converters;

/// <summary>
/// Converts a value to <see cref="Visibility.Visible"/> when it equals the
/// converter parameter, or <see cref="Visibility.Collapsed"/> otherwise.
/// Useful for showing/hiding elements based on an enum selection.
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public sealed class EqualityToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
