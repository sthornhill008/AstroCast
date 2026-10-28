using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Tests.Fixtures;
using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// <see cref="DisplayText"/>, the text both heads' converter shows for a value (1.4.0, E3-6): the names of
/// the Presentation enums and the saved-chart row.
/// </summary>
/// <remarks>
/// The row prints the stored reading as typed, <c>yyyy-MM-dd HH:mm:ss</c> in the invariant culture, through
/// <c>ChartMapper.ParseStoredLocalDateTime</c>, and never through <c>CalendarDateFormatter</c>: a stored
/// reading is already on the calendar in force, so a pre-1583 one would be moved a second time.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class DisplayTextTests
{
    /// <summary>A saved chart as the database holds it.</summary>
    /// <param name="eventType">The stored event type.</param>
    /// <param name="localDateTime">The stored local reading.</param>
    /// <returns>The chart.</returns>
    private static ChartEntity Saved(string eventType = "Birth", string localDateTime = "1947-10-05T09:30:00.0000000") => new()
    {
        Name = "Saved",
        EventType = eventType,
        LocalDateTime = localDateTime,
        TimeZone = "America/New_York",
        UtcDateTime = "1947-10-05T14:30:00Z",
        JulianDay = 2432464.1041666665,
        Latitude = 40.2737,
        Longitude = -76.8844,
        LocationName = "Harrisburg, Pennsylvania, United States",
        CreatedAt = "2026-10-16T00:00:00Z",
        UpdatedAt = "2026-10-16T00:00:00Z",
    };

    /// <summary>The planetary-return find modes read as the box's tooltip names them.</summary>
    [Fact]
    public void ReturnFindMode_IsNamedAsTheTooltipNamesIt()
    {
        DisplayText.Of(ReturnFindMode.ByYear).Should().Be("By Year");
        DisplayText.Of(ReturnFindMode.ByReturnNumber).Should().Be("By Return #");
    }

    /// <summary>
    /// Every primary-direction key reads by its Core display name, as the Directions panel's Key box shows it
    /// (branch PrimaryPanel, 1.5.0): "Naibod (0°59′08″ a year)".
    /// </summary>
    [Fact]
    public void PrimaryDirectionKey_IsNamedByItsDisplayName()
    {
        Enum.GetValues<PrimaryDirectionKey>().Select(k => DisplayText.Of(k))
            .Should().Equal(Enum.GetValues<PrimaryDirectionKey>().Select(k => k.DisplayName));
        DisplayText.Of(PrimaryDirectionKey.Naibod).Should().Be("Naibod (0\u00b059\u203208\u2033 a year)");
    }

    /// <summary>Every sort field reads in words.</summary>
    [Fact]
    public void ChartSortField_IsNamedInWords()
    {
        Enum.GetValues<ChartSortField>().Select(f => DisplayText.Of(f))
            .Should().Equal("Name", "Date", "Event Type", "Created");
    }

    /// <summary>
    /// The saved-chart row reads the type, the place and the reading as typed, the same under every culture.
    /// </summary>
    /// <param name="culture">The culture the screen runs under.</param>
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    [InlineData("th-TH")]
    public void SavedChartDetail_PrintsTheReadingAsTyped(string culture)
    {
        using CultureScope scope = new(culture);

        DisplayText.SavedChartDetail(Saved())
            .Should().Be("Birth — Harrisburg, Pennsylvania, United States (1947-10-05 09:30:00)");
    }

    /// <summary>A legacy row's offset suffix, the saving machine's, is ignored.</summary>
    [Fact]
    public void SavedChartDetail_IgnoresALegacyOffsetSuffix()
    {
        DisplayText.SavedChartDetail(Saved(localDateTime: "1947-10-05T09:30:00.0000000-04:00"))
            .Should().EndWith("(1947-10-05 09:30:00)");
    }

    /// <summary>An extended stored event type reads by its display name.</summary>
    [Fact]
    public void SavedChartDetail_NamesAnExtendedEventType()
    {
        DisplayText.SavedChartDetail(Saved(eventType: "JobStart"))
            .Should().StartWith("Job / Career Start — ");
    }

    /// <summary>A stored reading that does not parse prints as stored.</summary>
    [Fact]
    public void SavedChartDetail_FallsBackToTheStoredTextForAMalformedDate()
    {
        DisplayText.SavedChartDetail(Saved(localDateTime: "not a date"))
            .Should().Be("Birth — Harrisburg, Pennsylvania, United States (not a date)");
    }
}
