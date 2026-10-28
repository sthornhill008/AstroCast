using AstroCast.Core.Interfaces;
using AstroCast.Core.Services;

using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// The chart-entry form converts on the app's own time-zone data, as the chart engine
/// does: its live offset label is the engine's label (card 6), and "Set to now" reads the
/// current instant on the chart's clock (card 8).
/// </summary>
/// <remarks>
/// The expected offsets and names are IANA 2026e's (Python <c>zoneinfo</c> over
/// <c>tzdata</c> 2026.5). The form used the platform's data and its own copy of a 21-entry
/// abbreviation table, so its label could disagree with the one the chart was saved with.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ChartEntryTimeZoneTests
{
    /// <summary>
    /// The live label is the engine's: the zone record's name when it is a word, then the
    /// offset in force at the entered reading under D17.
    /// </summary>
    /// <param name="zone">The zone entered.</param>
    /// <param name="date">The date entered, as <c>yyyy-MM-dd</c>.</param>
    /// <param name="time">The time entered.</param>
    /// <param name="expected">The label.</param>
    [Theory]
    [InlineData("America/Vancouver", "2026-01-15", "12:00:00", "PST (UTC−8)")]
    [InlineData("Asia/Jerusalem", "2026-06-07", "21:19:00", "IDT (UTC+3)")]
    [InlineData("America/New_York", "2026-03-08", "02:30:00", "EDT (UTC−4)")]
    [InlineData("Europe/London", "1941-08-10", "02:30:00", "BST (UTC+1)")]
    [InlineData("America/New_York", "1880-06-15", "12:00:00", "LMT (UTC−4:56:02)")]
    [InlineData("Asia/Tehran", "2026-02-28", "02:51:00", "UTC+3.5")]
    public void ResolvedOffsetDisplay_Should_BeTheEnginesLabel(string zone, string date, string time, string expected)
    {
        ChartEntryViewModel entry = MainViewModelFactory.Create().ChartEntry;

        entry.TimeZone = zone;
        entry.EventDate = DateTime.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        entry.EventTime = time;

        entry.ResolvedOffsetDisplay.Should().Be(expected);
    }

    /// <summary>A zone the data does not hold still shows a dash.</summary>
    [Fact]
    public void ResolvedOffsetDisplay_Should_ShowADash_ForAZoneTheDataDoesNotHold()
    {
        ChartEntryViewModel entry = MainViewModelFactory.Create().ChartEntry;

        entry.TimeZone = "Not/A/Zone";

        entry.ResolvedOffsetDisplay.Should().Be("—");
    }

    /// <summary>
    /// "Set to now" puts the current instant on the chart zone's clock, not the machine's:
    /// for Pacific/Kiritimati, UTC+14 with no daylight saving, that is UT plus fourteen
    /// hours wherever the machine is.
    /// </summary>
    [Fact]
    public void SetToNow_Should_ReadTheCurrentInstant_OnTheChartZonesClock()
    {
        MainViewModel main = MainViewModelFactory.Create();
        main.ChartEntry.TimeZone = "Pacific/Kiritimati";

        DateTime before = DateTime.UtcNow.AddHours(14);
        main.SetToNowCommand.Execute(null);
        DateTime after = DateTime.UtcNow.AddHours(14);

        DateTime set = main.ChartEntry.EventDate.Date.Add(TimeSpan.Parse(main.ChartEntry.EventTime, System.Globalization.CultureInfo.InvariantCulture));
        set.Should().BeOnOrAfter(DateTime.SpecifyKind(before, DateTimeKind.Unspecified).AddSeconds(-1))
            .And.BeOnOrBefore(DateTime.SpecifyKind(after, DateTimeKind.Unspecified));
    }

    /// <summary>
    /// A chart at an explicit offset reads "now" on that offset's clock: UTC−11 is UT less
    /// eleven hours.
    /// </summary>
    [Fact]
    public void SetToNow_Should_ReadTheCurrentInstant_OnAnExplicitOffsetsClock()
    {
        MainViewModel main = MainViewModelFactory.Create();
        main.ChartEntry.TimeZone = "Pacific/Kiritimati";
        main.ChartEntry.UseExplicitOffset = true;
        main.ChartEntry.UtcOffsetHoursText = "-11";

        DateTime before = DateTime.UtcNow.AddHours(-11);
        main.SetToNowCommand.Execute(null);
        DateTime after = DateTime.UtcNow.AddHours(-11);

        DateTime set = main.ChartEntry.EventDate.Date.Add(TimeSpan.Parse(main.ChartEntry.EventTime, System.Globalization.CultureInfo.InvariantCulture));
        set.Should().BeOnOrAfter(DateTime.SpecifyKind(before, DateTimeKind.Unspecified).AddSeconds(-1))
            .And.BeOnOrBefore(DateTime.SpecifyKind(after, DateTimeKind.Unspecified));
    }

    /// <summary>
    /// With no clock to read, no zone and no explicit offset, "Set to now" leaves the date
    /// and time as they are and says why, rather than putting the machine's clock in a chart
    /// whose zone is yet to be chosen.
    /// </summary>
    [Fact]
    public void SetToNow_Should_LeaveTheFields_When_TheChartHasNoClock()
    {
        MainViewModel main = MainViewModelFactory.Create();
        main.ChartEntry.TimeZone = string.Empty;
        main.ChartEntry.EventDate = new DateTime(1990, 5, 17);
        main.ChartEntry.EventTime = "08:15:00";

        main.SetToNowCommand.Execute(null);

        main.ChartEntry.EventDate.Should().Be(new DateTime(1990, 5, 17));
        main.ChartEntry.EventTime.Should().Be("08:15:00");
        main.StatusText.Should().Be("Choose the chart's time zone, or an explicit offset, before setting the time to now.");
    }

    /// <summary>
    /// The form's rules are the parent's, and by default the data the app carries.
    /// </summary>
    [Fact]
    public void TimeZoneRules_Should_DefaultToTheAppsOwnData()
    {
        ITimeZoneRules rules = MainViewModelFactory.Create().TimeZoneRules;

        rules.Should().BeOfType<TzdbTimeZoneRules>().Which.DataVersion.Should().Be("2026e");
    }
}
