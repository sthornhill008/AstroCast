using System.Globalization;
using System.Windows.Data;

using AstroCast.ViewModels;

namespace AstroCast.Converters;

/// <summary>
/// Shows a bound value as <see cref="DisplayText.Of"/> gives it: an enum's name in words, a saved chart's
/// row (1.4.0, E3-6). The Avalonia head's converter of the same name does the same.
/// </summary>
/// <remarks>
/// Used in the name-bearing ComboBoxes' item template, which WPF also uses for the closed box, so the list
/// and the selection show the name while <c>SelectedItem</c> stays the enum, and as the saved-chart row's
/// text. One way only.
/// </remarks>
[ValueConversion(typeof(object), typeof(string))]
public sealed class DisplayTextConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        DisplayText.Of(value);

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
