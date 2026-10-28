using System.Text.RegularExpressions;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Pins the offsets the electional export prints in an hour the clocks repeat (branch O1, 1.3.3): where a printed
/// local reading occurs twice, the zone's abbreviation follows it — in the candidate heading, the Disqualified
/// Periods rows and the Moon Void-of-Course Calendar rows — and nowhere else changes.
/// </summary>
/// <remarks>
/// Until 1.3.3 two candidates in New York's repeated hour printed the same "1:00 AM", and "1:00 AM" (EST) was listed
/// above "1:30 AM" (EDT), half an hour before it. The expected readings come from Python <c>zoneinfo</c> (IANA 2026e,
/// <c>o1-bounds/out/zone-figures.json</c>); the void bounds are Tier B (see <c>ElectionalInstantTests</c>).
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalRepeatedHourPrintTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalRepeatedHourPrintTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// New York, 1 November 2026, every sample from 01:00 to 02:00: each heading in the repeated hour names its
    /// pass, EDT (05:00–05:45 UT) or EST (06:00–06:45 UT); 2:00 AM, which occurs once, prints as before.
    /// </summary>
    [Fact]
    public void ToMarkdown_NewYorksRepeatedHour_NamesEachHeadingsOffset()
    {
        ElectionalResult result = Search("America/New_York", 40.7128, -74.006, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1), new TimeOnly(1, 0), new TimeOnly(2, 0), 9);
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        Dictionary<DateTime, string> expected = new()
        {
            [Utc(5, 0)] = "1:00 AM EDT", [Utc(5, 15)] = "1:15 AM EDT", [Utc(5, 30)] = "1:30 AM EDT", [Utc(5, 45)] = "1:45 AM EDT",
            [Utc(6, 0)] = "1:00 AM EST", [Utc(6, 15)] = "1:15 AM EST", [Utc(6, 30)] = "1:30 AM EST", [Utc(6, 45)] = "1:45 AM EST",
            [Utc(7, 0)] = "2:00 AM",
        };

        result.Candidates.Select(c => c.Chart.UtcDateTime).Should().BeEquivalentTo(expected.Keys, "every sample from 01:00 to 02:00 is offered");
        foreach (CandidateWindow c in result.Candidates)
        {
            Lines(markdown).Should().Contain($"## Candidate instant {c.Rank} — Sun, 01 Nov 2026 {expected[c.Chart.UtcDateTime]}",
                "the heading names the pass of a repeated reading, and only of a repeated one");
        }
    }

    /// <summary>
    /// St. John's, 31 October and 1 November 2026: the Cancer void ends at 01:48 NDT (04:18:12 UT), a reading the
    /// clocks repeat at 01:48 NST, so its end carries "NDT" in both tables; its start, 7:29 PM on 31 October, occurs
    /// once and prints as before.
    /// </summary>
    [Fact]
    public void ToMarkdown_AVoidEndingInARepeatedHour_NamesItsOffsetInBothTables()
    {
        ElectionalResult result = Search("America/St_Johns", 47.5615, -52.7126, new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 1), new TimeOnly(0, 0), new TimeOnly(23, 45), 3);
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("| Sat, 31 Oct 2026 7:29 PM | Sun, 01 Nov 2026 1:48 AM NDT | Moon void-of-course in Cancer |");
        markdown.Should().Contain("| Sat, 31 Oct 2026 7:29 PM | Sun, 01 Nov 2026 1:48 AM NDT | Cancer |");
    }

    /// <summary>
    /// New York, 1 November 2026: the same void ends at 00:18 EDT, before the repeated hour, a reading that occurs
    /// once; its rows print as before, with no offset.
    /// </summary>
    [Fact]
    public void ToMarkdown_AVoidEndingOutsideTheRepeatedHour_PrintsAsBefore()
    {
        ElectionalResult result = Search("America/New_York", 40.7128, -74.006, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 1), new TimeOnly(1, 0), new TimeOnly(2, 0), 9);
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("| Sat, 31 Oct 2026 5:59 PM | Sun, 01 Nov 2026 12:18 AM | Moon void-of-course in Cancer |");
        markdown.Should().Contain("| Sat, 31 Oct 2026 5:59 PM | Sun, 01 Nov 2026 12:18 AM | Cancer |");
        Lines(markdown).Where(l => Regex.IsMatch(l, @"^## Candidate instant \d+ — .* [AP]M [A-Z]{3}$")).Should().HaveCount(8,
            "only the eight headings in the repeated hour carry an offset");
    }

    private ElectionalResult Search(string zone, double lat, double lon, DateOnly start, DateOnly end, TimeOnly earliest, TimeOnly latest, int count) =>
        new ElectionalService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture)).Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Repeated hour",
            RangeStart = start,
            RangeEnd = end,
            Latitude = lat,
            Longitude = lon,
            LocationName = zone,
            TimeZone = zone,
            CandidateCount = count,
            MinimumCandidateSeparationHours = 0.0,
            AvoidMercuryRetrograde = false,
            TimeConstraint = new TimeConstraint { Earliest = earliest, Latest = latest },
        });

    private static DateTime Utc(int hour, int minute) => new(2026, 11, 1, hour, minute, 0, DateTimeKind.Utc);

    /// <summary>The lines of an export, each without its terminator.</summary>
    /// <param name="markdown">The full markdown export.</param>
    /// <returns>Every line of <paramref name="markdown"/>, in order, trimmed of its line terminator.</returns>
    /// <remarks>
    /// Compare whole lines, not text with an embedded newline: the formatter writes <see cref="Environment.NewLine"/>,
    /// which is CRLF on Windows and LF on macOS, and both platforms run this suite. The same holds for a <c>$</c>
    /// anchor, which in .NET matches only before <c>\n</c>, so a heading ending <c>EST\r\n</c> never matched it.
    /// </remarks>
    private static string[] Lines(string markdown) => markdown.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
}
