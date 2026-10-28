using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// <see cref="DavisonMarkdownFormatter"/> (branch Davison, 1.7.0): the header naming both charts and the midpoints,
/// the Davison chart printed complete (everything the composite export sets aside, and none of the Birth-only
/// time lords), the source summaries, the Calendar line for a Julian-era pair, the context and the save.
/// </summary>
/// <param name="fixture">The shared ephemeris fixture.</param>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class DavisonMarkdownFormatterTests(EphemerisFixture fixture)
{
    /// <summary>The header names both charts, the method, the midpoint moment in UT with its Julian Day, and the midpoint place.</summary>
    [Fact]
    public void ToMarkdown_Header_NamesBothChartsAndTheMidpoints()
    {
        string md = DavisonMarkdownFormatter.ToMarkdown(GoldenPair());

        // The title by its first line, not by a bare "\n": AppendLine ends in the platform newline,
        // CRLF on Windows, where Maple runs the suite.
        md.Split('\n')[0].TrimEnd('\r').Should().Be("# Davison Chart: J2000 NYC Fixture & Secondary Fixture");
        Line(md, "**Method:**").Should().Be(
            "**Method:** Tropical zodiac; the chart cast for the midpoint of the two birth instants (UT) at the midpoint " +
            "of the two birthplaces (the mean latitude, the mean longitude by the shorter arc); Placidus houses, Chart A's  ");
        Line(md, "**Midpoint Moment:**").Should().Be("**Midpoint Moment:** 1985-03-24 15:30:00 UTC (JD 2446149.145833)  ");
        Line(md, "**Midpoint Place:**").Should().Be("**Midpoint Place:** Midpoint of New York City & Los Angeles (37.3825, -96.1249)  ");
        Line(md, "**Chart A:**").Should().Be("**Chart A:** J2000 NYC Fixture, 2000-01-01 07:00:00 (America/New_York), New York City  ");
        Line(md, "**Chart B:**").Should().Be("**Chart B:** Secondary Fixture, 1970-06-15 12:00:00 (America/Los_Angeles), Los Angeles  ");
        Lines(md, "**Calendar:**").Should().BeEmpty("a 1985 midpoint of two twentieth-century charts is Gregorian");
    }

    /// <summary>
    /// The chart section is the ordinary full report: the Type and the "UTC+0 (explicit)" clock, the sections the composite sets
    /// aside, the Temperament; and none of the Birth-only time lords, no horary analysis, no Source line.
    /// </summary>
    [Fact]
    public void ToMarkdown_ChartSection_IsComplete()
    {
        string md = DavisonMarkdownFormatter.ToMarkdown(GoldenPair());
        string chart = Section(md, "## Davison Chart", "## Source Chart A:");

        Line(chart, "**Type:**").Should().Be("**Type:** Davison  ");
        Line(chart, "**Date/Time:**").Should().Be("**Date/Time:** 1985-03-24 15:30:00 UTC+0 (explicit)  ", "an explicit-offset chart's clock is labelled with its offset, as the returns are");
        chart.Should().Contain("### Equatorial Coordinates");
        chart.Should().Contain("### Out-of-Bounds Planets");
        chart.Should().Contain("| Ap/Sp |");
        chart.Should().ContainAny("**Void of Course:**", "**First Applying Aspect:**", "**Next Aspect:**");
        chart.Should().Contain("### Temperament");
        chart.Should().NotContain("## Annual Profection");
        chart.Should().NotContain("## Firdaria");
        chart.Should().NotContain("## Zodiacal Releasing");
        chart.Should().NotContain("Horary Perfection");
        Lines(chart, "**Source:**").Should().BeEmpty("the Davison input has no source notes");
    }

    /// <summary>The source summaries follow the chart, A then B, each with its Date/Time and Location lines.</summary>
    [Fact]
    public void ToMarkdown_SourceSummaries_FollowTheChart()
    {
        string md = DavisonMarkdownFormatter.ToMarkdown(GoldenPair());

        int chartAt = md.IndexOf("## Davison Chart", StringComparison.Ordinal);
        int aAt = md.IndexOf("## Source Chart A: J2000 NYC Fixture", StringComparison.Ordinal);
        int bAt = md.IndexOf("## Source Chart B: Secondary Fixture", StringComparison.Ordinal);
        chartAt.Should().BePositive();
        aAt.Should().BeGreaterThan(chartAt);
        bAt.Should().BeGreaterThan(aAt);

        string sourceA = Section(md, "## Source Chart A:", "## Source Chart B:");
        Line(sourceA, "**Date/Time:**").Should().Be("**Date/Time:** 2000-01-01 07:00:00 (America/New_York)  ");
        Line(sourceA, "**Location:**").Should().Be("**Location:** New York City (40.7128, -74.0060)");
        string sourceB = md[bAt..];
        Line(sourceB, "**Date/Time:**").Should().Be("**Date/Time:** 1970-06-15 12:00:00 (America/Los_Angeles)  ");
        Line(sourceB, "**Location:**").Should().Be("**Location:** Los Angeles (34.0522, -118.2437)");
    }

    /// <summary>A Julian-era pair prints the Calendar line in the header, after the Midpoint Moment line.</summary>
    [Fact]
    public void ToMarkdown_JulianEraPair_PrintsTheCalendarLine()
    {
        ChartResult n1 = Engine().Calculate(GoldenFixtures.N1Input());
        ChartResult aCenturyOn = Engine().Calculate(new ChartInput
        {
            Name = "N1 a century on",
            LocalDateTime = new DateTime(1400, 3, 15, 12, 0, 0),
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 51.4779,
            Longitude = -0.0015,
            LocationName = "Greenwich",
            EventType = "Birth",
        });
        DavisonResult result = Service().Calculate(n1, aCenturyOn, includeMinorPoints: false);

        string md = DavisonMarkdownFormatter.ToMarkdown(result);

        string header = Section(md, "# Davison Chart:", "## Davison Chart");
        Line(header, "**Midpoint Moment:**").Should().Be("**Midpoint Moment:** 1350-03-16 00:00:00 UTC (JD 2214219.500000)  ");
        Line(header, "**Calendar:**").Should().Be("**Calendar:** dates before 1582-10-15 are on the Julian calendar  ");
        header.IndexOf("**Calendar:**", StringComparison.Ordinal)
            .Should().BeGreaterThan(header.IndexOf("**Midpoint Moment:**", StringComparison.Ordinal));
    }

    /// <summary>The interpretation context prints, trimmed, when given.</summary>
    [Fact]
    public void ToMarkdown_Context_PrintsWhenGiven()
    {
        string md = DavisonMarkdownFormatter.ToMarkdown(GoldenPair(), "  A long friendship.  ");

        Line(md, "**Interpretation Context:**").Should().Be("**Interpretation Context:** A long friendship.  ");
    }

    /// <summary>A blank context prints no line.</summary>
    [Fact]
    public void ToMarkdown_Context_OmitsWhenBlank()
    {
        string md = DavisonMarkdownFormatter.ToMarkdown(GoldenPair(), "   ");

        Lines(md, "**Interpretation Context:**").Should().BeEmpty();
    }

    /// <summary>With the switch on the chart section carries the Asteroids and Lilith section; off, it does not.</summary>
    [Fact]
    public void ToMarkdown_MinorPoints_SectionWhenOn()
    {
        string on = DavisonMarkdownFormatter.ToMarkdown(GoldenPair(includeMinorPoints: true));
        string off = DavisonMarkdownFormatter.ToMarkdown(GoldenPair());

        Section(on, "## Davison Chart", "## Source Chart A:").Should().Contain("## Asteroids and Lilith");
        off.Should().NotContain("## Asteroids and Lilith");
    }

    /// <summary>SaveAsync writes exactly what ToMarkdown returns.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SaveAsync_WritesTheMarkdown()
    {
        DavisonResult result = GoldenPair();
        string path = Path.Combine(Path.GetTempPath(), $"davison-{Guid.NewGuid():N}.md");
        try
        {
            await DavisonMarkdownFormatter.SaveAsync(result, path, "Saved.");

            (await File.ReadAllTextAsync(path)).Should().Be(DavisonMarkdownFormatter.ToMarkdown(result, "Saved."));
        }
        finally
        {
            File.Delete(path);
        }
    }

    #region Helpers

    /// <summary>The goldens' pair, J2000 NYC and the Secondary fixture, each cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">The switch, for the two sources and the Davison chart.</param>
    /// <returns>The result.</returns>
    private DavisonResult GoldenPair(bool includeMinorPoints = false)
    {
        ChartEngine engine = Engine();
        ChartResult chartA = engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints));
        ChartResult chartB = engine.Calculate(GoldenFixtures.SecondaryNatalInput(includeMinorPoints));
        return Service().Calculate(chartA, chartB, includeMinorPoints);
    }

    /// <summary>A chart engine over the shared ephemeris.</summary>
    private ChartEngine Engine() => GoldenFixtures.CreateChartEngine(fixture);

    /// <summary>A Davison service over a fresh engine.</summary>
    private DavisonService Service() => new(Engine(), new Serilog.LoggerConfiguration().CreateLogger());

    /// <summary>The lines of <paramref name="md"/> that start with <paramref name="prefix"/>.</summary>
    /// <param name="md">The export.</param>
    /// <param name="prefix">The lines' start.</param>
    /// <returns>The lines, trailing spaces kept.</returns>
    private static List<string> Lines(string md, string prefix) =>
        md.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith(prefix, StringComparison.Ordinal)).ToList();

    /// <summary>The one line of <paramref name="md"/> that starts with <paramref name="prefix"/>.</summary>
    /// <param name="md">The export.</param>
    /// <param name="prefix">The line's start.</param>
    /// <returns>The line, trailing spaces kept.</returns>
    private static string Line(string md, string prefix) => Lines(md, prefix).Should().ContainSingle().Subject;

    /// <summary>The text of <paramref name="md"/> from <paramref name="from"/> up to <paramref name="to"/>.</summary>
    /// <param name="md">The export.</param>
    /// <param name="from">The start marker, included.</param>
    /// <param name="to">The end marker, excluded.</param>
    /// <returns>The section.</returns>
    private static string Section(string md, string from, string to)
    {
        int start = md.IndexOf(from, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"the export has '{from}'");
        int end = md.IndexOf(to, start, StringComparison.Ordinal);
        end.Should().BeGreaterThan(start, $"the export has '{to}' after '{from}'");
        return md[start..end];
    }

    #endregion
}
