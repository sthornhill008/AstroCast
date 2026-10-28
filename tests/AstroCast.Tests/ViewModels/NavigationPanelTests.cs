using AstroCast.ViewModels;
using FluentAssertions;
using Xunit;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Tests for the <see cref="NavigationPanel"/> enum values.
/// </summary>
public sealed class NavigationPanelTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void NavigationPanel_HasExpectedValues()
    {
        var values = Enum.GetValues<NavigationPanel>();
        values.Should().HaveCount(10);
        values.Should().Contain(NavigationPanel.Chart);
        values.Should().Contain(NavigationPanel.Synastry);
        values.Should().Contain(NavigationPanel.Transits);
        values.Should().Contain(NavigationPanel.Returns);
        values.Should().Contain(NavigationPanel.Electional);
        values.Should().Contain(NavigationPanel.Medical);
        values.Should().Contain(NavigationPanel.Map);
        values.Should().Contain(NavigationPanel.Export);
        values.Should().Contain(NavigationPanel.Relocation);
        values.Should().Contain(NavigationPanel.Directions);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NavigationPanel_DefaultValue_IsChart()
    {
        // Chart should be the first (default) enum value
        default(NavigationPanel).Should().Be(NavigationPanel.Chart);
    }
}
