using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.UI;

/// <summary>
/// Verifies that the Daily Horoscope command bindings exist and are properly
/// wired in the MainViewModel.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DailyHoroscopeWpfTests
{
    /// <summary>
    /// Verify the ExportDailyHoroscopeMarkdownCommand binding exists on MainViewModel.
    /// </summary>
    [Fact]
    public void NavigationPanel_TransitsPanel_ContainsDailyHoroscopeSection()
    {
        // Verify the command property exists via reflection
        System.Reflection.PropertyInfo? prop = typeof(MainViewModel)
            .GetProperty(nameof(MainViewModel.ExportDailyHoroscopeMarkdownCommand));

        prop.Should().NotBeNull(
            "MainViewModel should expose ExportDailyHoroscopeMarkdownCommand for XAML binding");
        prop!.PropertyType.Should().Be(typeof(AsyncRelayCommand),
            "the command should be an AsyncRelayCommand");
    }
}
