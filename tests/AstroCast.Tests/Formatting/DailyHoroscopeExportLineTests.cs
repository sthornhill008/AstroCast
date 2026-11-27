using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Services;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Whole lines of the daily horoscope export (<see cref="DailyHoroscopeMarkdownFormatter.ToMarkdown"/>),
/// built as the heads build it: the days from <see cref="Core.Services.DailyHoroscopeService.BuildDays"/>
/// on the natal zone, the zone named in the header. Expected values come from outside the code (Tier B
/// Swiss Ephemeris, Python <c>zoneinfo</c> with tzdata 2026.5).
/// </summary>
/// <remarks>
/// Each test compares whole lines, never text with an embedded newline, so the tests read the same
/// whatever the platform's line ending.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class DailyHoroscopeExportLineTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public DailyHoroscopeExportLineTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// Pacific/Apia skipped 2011-12-30 (zoneinfo), so a report from 2011-12-28 to 2012-01-01 has
    /// four days, and the Report Period line counts the days that occurred.
    /// </summary>
    [Fact]
    public void ToMarkdown_Apia_ReportAcrossTheSkippedDate_Should_CountFourDays()
    {
        string[] lines = Export(DailyHoroscopeServiceTests.ApiaInput(), new DateOnly(2011, 12, 28), new DateOnly(2012, 1, 1));

        lines.Should().Contain("**Report Period:** 2011-12-28 to 2012-01-01 (4 days)");
    }

    /// <summary>
    /// The header names the zone as the clock of the days and of every time in them.
    /// </summary>
    [Fact]
    public void ToMarkdown_Hartwell_Should_NameTheClockOfEveryTime()
    {
        string[] lines = Export(DailyHoroscopeServiceTests.HartwellInput(), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3));

        lines.Should().ContainSingle(l => l.StartsWith("**Timezone:**", StringComparison.Ordinal))
            .Which.Should().Be("**Timezone:** America/New_York — the days and every time in them "
                + "(void of course, exact, entering and leaving orb, the Moon's sign change) are on this zone's clock");
    }

    /// <summary>
    /// Directly after the Timezone line, the header says each day's orbs and phases are read at noon,
    /// and which transits are listed.
    /// </summary>
    [Fact]
    public void ToMarkdown_Hartwell_Should_StateTheNoonReadingAfterTheTimezoneLine()
    {
        string[] lines = Export(DailyHoroscopeServiceTests.HartwellInput(), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3));

        int timezone = Array.FindIndex(lines, l => l.StartsWith("**Timezone:**", StringComparison.Ordinal));
        timezone.Should().BeGreaterThan(0);
        lines[timezone + 1].Should().Be("**Orbs and Phases:** at 12:00 each day on this clock; a transit is listed when it is in orb at 12:00");
    }

    /// <summary>
    /// Person A's pass of transiting Saturn square its natal Moon is exact at 2025-02-03 00:51:11 UT,
    /// 19:51 EST on 2 February (fixtures/truth/transits_hartwell.py; zoneinfo): 2 February's Exact
    /// table has the header with a plain Time column, its separator, and the row with the local time.
    /// </summary>
    [Fact]
    public void ToMarkdown_Hartwell_Feb2_Should_PrintTheExactTableInLocalTime()
    {
        string[] lines = Export(DailyHoroscopeServiceTests.HartwellInput(), new DateOnly(2025, 2, 1), new DateOnly(2025, 2, 4));
        string[] day = Section(lines, "### Sunday, February 2, 2025");

        int exact = Array.IndexOf(day, "**Exact Today:**");
        exact.Should().BeGreaterThan(0);
        day[exact + 2].Should().Be("| Transit | Aspect | Natal Target | House | Time |");
        day[exact + 3].Should().Be("|---------|--------|--------------|-------|------|");
        day.Should().Contain("| TR Saturn | sqr | NA Moon | 7 | 19:51 |");
    }

    /// <summary>
    /// Person A, 2026-07-05 to 07-25: transiting Jupiter enters a 1.5° square of the natal Mercury at
    /// 2026-07-17 01:05:42 UT, 21:05 EDT on 16 July (fixtures/truth/transits_hartwell.py; zoneinfo),
    /// and is still in orb after the report. The backdrop prints the entry's local date.
    /// </summary>
    [Fact]
    public void ToMarkdown_Hartwell_Backdrop_Should_PrintTheEntrysLocalDate()
    {
        string[] lines = Export(DailyHoroscopeServiceTests.HartwellInput(), new DateOnly(2026, 7, 5), new DateOnly(2026, 7, 25));

        lines.Should().Contain("| TR Jupiter | sqr | NA Mercury | 12 | 2026-07-16 – (beyond) |");
    }

    /// <summary>
    /// Person A, 2026-10-02: at 12:00 EDT the Moon is in Gemini, 258.2° from the Sun (Waning Gibbous,
    /// late); it enters Cancer at 19:54:04 UT, 15:54 EDT, after the void period that runs from the day's
    /// start (Tier B; zoneinfo; the Moon's sign, phase and void do not depend on the natal chart). The
    /// Moon line names the change after the phase.
    /// </summary>
    [Fact]
    public void ToMarkdown_Hartwell_Oct2_Should_NoteTheMoonsSignChangeAfterThePhase()
    {
        string[] lines = Export(DailyHoroscopeServiceTests.HartwellInput(), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 3));
        string[] day = Section(lines, "### Friday, October 2, 2026");

        day.Should().Contain("**Moon:** Gemini (Waning Gibbous (late)) (Moon enters Cancer at 15:54) | **VoC:** 00:00 – 15:54 (Gemini)");
    }

    /// <summary>
    /// With an unknown birth time the Exact table has no House column, and its Time column is plain.
    /// Person A's transiting Jupiter conjoins its natal Pluto exactly on 2026-09-05 at 01:44 UT,
    /// 21:44 EDT on 4 September (fixtures/truth/transits_hartwell.py; zoneinfo), so that day has an
    /// Exact table.
    /// </summary>
    [Fact]
    public void ToMarkdown_UnknownBirthTime_Should_PrintTheExactTableWithoutHouse()
    {
        string[] lines = Export(
            DailyHoroscopeServiceTests.HartwellInput(), new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 5), birthTimeSource: "Unknown");
        string[] day = Section(lines, "### Friday, September 4, 2026");

        int exact = Array.IndexOf(day, "**Exact Today:**");
        exact.Should().BeGreaterThan(0);
        day[exact + 2].Should().Be("| Transit | Aspect | Natal Target | Time |");
        day[exact + 3].Should().Be("|---------|--------|--------------|------|");
    }

    /// <summary>
    /// The lines of one day's section: from its heading to the next day's.
    /// </summary>
    /// <param name="lines">The export's lines.</param>
    /// <param name="heading">The day's heading, "### Sunday, May 31, 2026".</param>
    /// <returns>The section's lines, the heading first.</returns>
    private static string[] Section(string[] lines, string heading)
    {
        int start = Array.IndexOf(lines, heading);
        start.Should().BeGreaterThanOrEqualTo(0, "the export has the day {0}", heading);
        int end = Array.FindIndex(lines, start + 1, l => l.StartsWith("### ", StringComparison.Ordinal));
        return lines[start..(end < 0 ? lines.Length : end)];
    }

    /// <summary>
    /// The Report Period's day count takes the singular for one day: "(1 day)", and "(3 days)" for three
    /// (branch ExportText, 1.7.2; finding N12). Until 1.7.2 a one-day report read "(1 days)".
    /// </summary>
    /// <param name="lastDay">The report's last day of September 2026; its first is the 3rd.</param>
    /// <param name="expected">The count as the line prints it.</param>
    [Theory]
    [InlineData(3, "(1 day)")]
    [InlineData(5, "(3 days)")]
    public void ReportPeriod_DayCount_FollowsItsNumber(int lastDay, string expected)
    {
        string[] lines = Export(DailyHoroscopeServiceTests.HartwellInput(), new DateOnly(2026, 9, 3), new DateOnly(2026, 9, lastDay));

        lines.Should().Contain(line => line.TrimEnd() == $"**Report Period:** 2026-09-03 to 2026-09-{lastDay:00} {expected}");
    }

    /// <summary>
    /// The export of a chart's daily horoscope, as the heads write it, split into lines.
    /// </summary>
    /// <param name="natalInput">The natal chart's input.</param>
    /// <param name="start">The report's first date.</param>
    /// <param name="end">The report's last date.</param>
    /// <param name="minorAspects">Whether the transits include the minor aspects.</param>
    /// <param name="birthTimeSource">The birth-time source printed, "Unknown" for an unknown birth time.</param>
    /// <returns>The export's lines, without line endings.</returns>
    private string[] Export(
        ChartInput natalInput, DateOnly start, DateOnly end, bool minorAspects = false, string? birthTimeSource = null)
    {
        (ChartResult natal, TransitResult result, IReadOnlyList<DailyHoroscopeDay> days) =
            DailyHoroscopeServiceTests.Daily(_fixture, natalInput, start, end, minorAspects);
        string md = DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days, birthTimeSource, null, natal.Input.TimeZone);
        return md.Split('\n').Select(line => line.TrimEnd('\r', ' ')).ToArray();
    }
}
