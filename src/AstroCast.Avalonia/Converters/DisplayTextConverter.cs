using System.Globalization;

using Avalonia.Data;
using Avalonia.Data.Converters;

using AstroCast.ViewModels;

namespace AstroCast.Avalonia.Converters;

/// <summary>
/// Shows a bound value as <see cref="DisplayText.Of"/> gives it: an enum's name in words, a saved chart's
/// row (1.4.0, E3-6). The WPF head's converter of the same name does the same.
/// </summary>
/// <remarks>
/// Used as each name-bearing ComboBox's <c>DisplayMemberBinding</c>, so the items and the closed box show the
/// name while <c>SelectedItem</c> stays the enum, and as the saved-chart row's text. One way only.
/// </remarks>
public sealed class DisplayTextConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        DisplayText.Of(value);

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
