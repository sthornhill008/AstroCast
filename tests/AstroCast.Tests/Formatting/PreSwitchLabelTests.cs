using System.Text;
using System.Text.RegularExpressions;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Every printed instant before 1582-10-15 prints on the Julian calendar through
/// <see cref="CalendarDateFormatter"/>, and a report that holds one carries the Calendar line after its
/// Period, Return Date/Time or Date/Time line (branch R2, 1.3.4; R-D1, R-D2): one test per formatter, each
/// with a model at a pre-1583 instant and the same model at a 2026 instant as a guard. The instants are
/// Tier B (r2_truth.out): N1's 1301 solar return, 1301-03-23T17:42:37Z as ticks, is Julian 1301-03-15
/// 17:42:37, a Wednesday; the Sun's Aries ingress of 1300, 1300-03-20T16:04:00Z, is Julian 1300-03-12;
/// the eclipse of 1300-02-28T08:23:32Z Julian 1300-02-21; the great conjunction of 1306-01-02T00:00Z
/// Julian 1305-12-25; Julian 1301-03-07 was a Tuesday. Until 1.3.4 every such label was the proleptic
/// Gregorian one, the daily heading's weekday the proleptic components', and no export named a calendar.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class PreSwitchLabelTests
{
    private const string CalendarLine = "**Calendar:** dates before 1582-10-15 are on the Julian calendar";
    private const string Engine = "AstroCast 1.3.4 (SwissEph 2.10.03, tzdb 2026e)";
    private const double GreenwichLatitude = 51.4779;
    private const double GreenwichLongitude = -0.0015;

    private static readonly DateTime CalcTime = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Return1301 = new(1301, 3, 23, 17, 42, 37, DateTimeKind.Utc);
    private static readonly DateTime Aries1300 = new(1300, 3, 20, 16, 4, 0, DateTimeKind.Utc);
    private static readonly DateTime Eclipse1300 = new(1300, 2, 28, 8, 23, 32, DateTimeKind.Utc);
    private static readonly DateTime GreatConjunction1306 = new(1306, 1, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Modern = new(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc);

    private readonly EphemerisFixture _fixture;
    private readonly ChartEngine _engine;
    private readonly ChartResult _n1;
    private readonly ChartResult _j2000;

    /// <summary>Casts N1 (Julian 1300-03-15 12:00 UT at Greenwich, explicit offset 0) and J2000 NYC.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public PreSwitchLabelTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _engine = GoldenFixtures.CreateChartEngine(fixture);
        _n1 = _engine.Calculate(new ChartInput
        {
            Name = "N1",
            LocalDateTime = new DateTime(1300, 3, 15, 12, 0, 0),
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = GreenwichLatitude,
            Longitude = GreenwichLongitude,
            LocationName = "Greenwich",
            EventType = "Birth",
        });
        _j2000 = GoldenFixtures.J2000Nyc(fixture);
    }

    #region Series

    /// <summary>The ingress series prints its instants on the calendar in force and names the calendar.</summary>
    [Fact]
    public void IngressFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = IngressFormatter.SeriesToMarkdown(IngressSeries(Aries1300));
        julian.Should().Contain("| 1 | 1300-03-12 16:04:00 | Sun | Aries |");
        LineAfter(julian, "**Software:**").Should().Be(CalendarLine + "  ");

        string gregorian = IngressFormatter.SeriesToMarkdown(IngressSeries(Modern));
        gregorian.Should().Contain("| 1 | 2026-04-15 12:00:00 | Sun | Aries |");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The eclipse series prints its maxima, in the table and the detail heading, on the calendar in force.</summary>
    [Fact]
    public void EclipseFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = EclipseFormatter.SeriesToMarkdown(EclipseSeries(Eclipse1300));
        julian.Should().Contain("| 1 | 1300-02-21 08:23:32 | Solar | Total |");
        julian.Should().Contain("### #1 — Total Solar Eclipse, February 21, 1300");
        LineAfter(julian, "**Period:**").Should().Be(CalendarLine + "  ");

        string gregorian = EclipseFormatter.SeriesToMarkdown(EclipseSeries(Modern));
        gregorian.Should().Contain("| 1 | 2026-04-15 12:00:00 | Solar | Total |");
        gregorian.Should().Contain("### #1 — Total Solar Eclipse, April 15, 2026");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The great-conjunction series prints its instants on the calendar in force.</summary>
    [Fact]
    public void GreatConjunctionFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = GreatConjunctionFormatter.ToMarkdown(GreatConjunctionSeries(GreatConjunction1306));
        julian.Should().Contain("| 1 | 1305-12-25 | 08°26' Sagittarius |");
        LineAfter(julian, "**Software:**").Should().Be(CalendarLine + "  ");

        string gregorian = GreatConjunctionFormatter.ToMarkdown(GreatConjunctionSeries(Modern));
        gregorian.Should().Contain("| 1 | 2026-04-15 | 08°26' Sagittarius |");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The lunation series prints its instants on the calendar in force.</summary>
    [Fact]
    public void LunationFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = LunationFormatter.SeriesToMarkdown(LunationSeries(Eclipse1300));
        julian.Should().Contain("| 1 | 1300-02-21 08:23 | New Moon |");
        LineAfter(julian, "**Period:**").Should().Be(CalendarLine + "  ");

        string gregorian = LunationFormatter.SeriesToMarkdown(LunationSeries(Modern));
        gregorian.Should().Contain("| 1 | 2026-04-15 12:00 | New Moon |");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The mundane aspect series prints its instants on the calendar in force.</summary>
    [Fact]
    public void MundaneAspectFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = MundaneAspectFormatter.ToMarkdown(MundaneAspectSeries(Eclipse1300));
        julian.Should().Contain("| 1 | 1300-02-21 08:23:32 | Waxing |");
        LineAfter(julian, "**Period:**").Should().Be(CalendarLine + "  ");

        string gregorian = MundaneAspectFormatter.ToMarkdown(MundaneAspectSeries(Modern));
        gregorian.Should().Contain("| 1 | 2026-04-15 12:00:00 | Waxing |");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The synodic cycle series prints its instants on the calendar in force.</summary>
    [Fact]
    public void SynodicCycleFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = SynodicCycleFormatter.ToMarkdown(SynodicCycleSeries(GreatConjunction1306));
        julian.Should().Contain("| 1 | 1305-12-25 | 08°26′ Sagittarius |");
        LineAfter(julian, "**Period:**").Should().Be(CalendarLine + "  ");

        string gregorian = SynodicCycleFormatter.ToMarkdown(SynodicCycleSeries(Modern));
        gregorian.Should().Contain("| 1 | 2026-04-15 | 08°26′ Sagittarius |");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The return and ingress timeline prints its entries on the calendar in force.</summary>
    [Fact]
    public void ReturnIngressTimelineFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = ReturnIngressTimelineFormatter.ToMarkdown(ReturnIngressTimeline(Return1301));
        julian.Should().Contain("| 1 | 1301-03-15 17:42:37 | Return | Sun |");
        julian.Should().Contain("| Sun | 00°00′00″ Aries | 1 | 1301-03-15 |");
        LineAfter(julian, "**Period:**").Should().Be(CalendarLine + "  ");

        string gregorian = ReturnIngressTimelineFormatter.ToMarkdown(ReturnIngressTimeline(Modern));
        gregorian.Should().Contain("| 1 | 2026-04-15 12:00:00 | Return | Sun |");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The cycle timeline prints its phase points and cycle period on the calendar in force.</summary>
    [Fact]
    public void PlanetaryCycleTimelineFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = PlanetaryCycleTimelineFormatter.ToMarkdown(CycleTimeline(Return1301));
        julian.Should().Contain("**Cycle Period:** 1301-03-15 → 1302-03-15");
        julian.Should().Contain("| Return | 0° | 00°00′ Aries | 1301-03-15 | 0 |");
        LineAfter(julian, "**Cycle Period:**").Should().Be(CalendarLine);

        string gregorian = PlanetaryCycleTimelineFormatter.ToMarkdown(CycleTimeline(Modern));
        gregorian.Should().Contain("**Cycle Period:** 2026-04-15 → 2027-04-15");
        gregorian.Should().NotContain(CalendarLine);
    }

    #endregion

    #region Returns, relocation and the chart

    /// <summary>The solar return export prints the return instant on the calendar in force.</summary>
    [Fact]
    public void SolarReturnMarkdownFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = SolarReturnMarkdownFormatter.ToMarkdown(SolarReturn(_n1, ChartAt(Return1301)));
        julian.Should().Contain("**Return Date/Time:** 1301-03-15 17:42:37 UTC");
        LineAfter(julian, "**Return Date/Time:**").Should().Be(CalendarLine + "  ");

        string gregorian = SolarReturnMarkdownFormatter.ToMarkdown(SolarReturn(_j2000, ChartAt(Modern)));
        gregorian.Should().Contain("**Return Date/Time:** 2026-04-15 12:00:00 UTC");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The lunar return export prints the return instant, in the title and the details, on the calendar in force.</summary>
    [Fact]
    public void LunarReturnMarkdownFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = LunarReturnMarkdownFormatter.ToMarkdown(LunarReturn(_n1, ChartAt(Return1301)));
        julian.Should().Contain("# Lunar Return: N1, 1301-03-15");
        julian.Should().Contain("**Return Date/Time:** 1301-03-15 17:42:37 UTC");
        LineAfter(julian, "**Return Date/Time:**").Should().Be(CalendarLine + "  ");

        string gregorian = LunarReturnMarkdownFormatter.ToMarkdown(LunarReturn(_j2000, ChartAt(Modern)));
        gregorian.Should().Contain("**Return Date/Time:** 2026-04-15 12:00:00 UTC");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The planetary return export prints the return and its other passes on the calendar in force.</summary>
    [Fact]
    public void PlanetaryReturnMarkdownFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = PlanetaryReturnMarkdownFormatter.ToMarkdown(PlanetaryReturn(_n1, ChartAt(Return1301), Return1301));
        julian.Should().Contain("**Return Date/Time:** 1301-03-15 17:42:37 UTC");
        julian.Should().Contain("**Other passes:** 1301-05-14 17:42:37 UTC (retrograde), 1301-07-13 17:42:37 UTC (direct)");
        LineAfter(julian, "**Return Date/Time:**").Should().Be(CalendarLine + "  ");

        string gregorian = PlanetaryReturnMarkdownFormatter.ToMarkdown(PlanetaryReturn(_j2000, ChartAt(Modern), Modern));
        gregorian.Should().Contain("**Return Date/Time:** 2026-04-15 12:00:00 UTC");
        gregorian.Should().Contain("**Other passes:** 2026-06-14 12:00:00 UTC (retrograde), 2026-08-13 12:00:00 UTC (direct)");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The relocation export prints the natal instant on the calendar in force.</summary>
    [Fact]
    public void RelocationMarkdownFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        RelocationService service = new(_engine, new Serilog.LoggerConfiguration().CreateLogger());
        RelocationInput florence = new() { Latitude = 43.7696, Longitude = 11.2558, LocationName = "Florence" };

        string julian = RelocationMarkdownFormatter.ToMarkdown(service.Calculate(_n1, florence));
        julian.Should().Contain("**UTC Moment:** 1300-03-15 12:00:00 UTC (UTC+0 (explicit))");
        LineAfter(julian, "**UTC Moment:**").Should().Be(CalendarLine + "  ");

        string gregorian = RelocationMarkdownFormatter.ToMarkdown(service.Calculate(_j2000, florence));
        gregorian.Should().Contain("**UTC Moment:** 2000-01-01 12:00:00 UTC");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>
    /// A chart export whose instant is before the switch carries the Calendar line directly after its
    /// Date/Time line; the J2000 export does not.
    /// </summary>
    [Fact]
    public void ChartMarkdownFormatter_NamesTheCalendar_AfterTheDateTimeLine()
    {
        string julian = ChartMarkdownFormatter.ToMarkdown(_n1);
        julian.Should().Contain("**Date/Time:** 1300-03-15 12:00:00 UTC+0 (explicit)");
        LineAfter(julian, "**Date/Time:**").Should().Be(CalendarLine + "  ");

        ChartMarkdownFormatter.ToMarkdown(_j2000).Should().NotContain(CalendarLine);
    }

    #endregion

    #region Transits, the timeline, the daily and the electional

    /// <summary>The transit report prints an exact pass on the calendar in force and names the calendar in its own header.</summary>
    [Fact]
    public void TransitMarkdownFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = TransitMarkdownFormatter.ToMarkdown(Transit(_n1, 1301, Return1301));
        julian.Should().Contain("- **Exact pass 1:** 1301-03-15 17:42:37 UTC");
        LineAfter(julian, "**Report Period:**").Should().Be(CalendarLine);

        string gregorian = TransitMarkdownFormatter.ToMarkdown(Transit(_j2000, 2026, Modern));
        gregorian.Should().Contain("- **Exact pass 1:** 2026-04-15 12:00:00 UTC");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>The timeline prints an exact pass, in the windows and the events table, on the calendar in force.</summary>
    [Fact]
    public void TransitTimelineMarkdownFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = TransitTimelineMarkdownFormatter.ToMarkdown(Transit(_n1, 1301, Return1301));
        julian.Should().Contain("- **Exact pass 1:** 1301-03-15 17:42 UTC");
        julian.Should().Contain("| 1301-03-15 17:42 UTC | TR Saturn cnj NA Sun | Exact pass 1 |");
        LineAfter(julian, "**Report Period:**").Should().Be(CalendarLine);

        string gregorian = TransitTimelineMarkdownFormatter.ToMarkdown(Transit(_j2000, 2026, Modern));
        gregorian.Should().Contain("| 2026-04-15 12:00 UTC | TR Saturn cnj NA Sun | Exact pass 1 |");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>
    /// A day's Date is a proleptic day (branch R3, 1.3.4), and the heading prints it on the calendar in
    /// force with its weekday: proleptic 1301-03-15, Julian 1301-03-07, reads "Tuesday, March 7, 1301"
    /// (until 1.3.4 the day was a typed date and its heading's weekday the proleptic components', Monday).
    /// </summary>
    [Fact]
    public void DailyHoroscopeMarkdownFormatter_HeadsTheDayWithItsInstantsWeekday_AndNamesTheCalendar()
    {
        string julian = DailyHoroscopeMarkdownFormatter.ToMarkdown(Transit(_n1, 1301, Return1301), [Day(new DateOnly(1301, 3, 15))]);
        julian.Should().Contain("### Tuesday, March 7, 1301");
        LineAfter(julian, "**Report Period:**").Should().Be(CalendarLine);

        string gregorian = DailyHoroscopeMarkdownFormatter.ToMarkdown(Transit(_j2000, 2026, Modern), [Day(new DateOnly(2026, 4, 15))]);
        gregorian.Should().Contain("### Wednesday, April 15, 2026");
        gregorian.Should().NotContain(CalendarLine);
    }

    /// <summary>A candidate instant before the switch prints on the calendar in force, with its instant's weekday.</summary>
    [Fact]
    public void ElectionalMarkdownFormatter_PrintsTheJulianLabel_AndNamesTheCalendar()
    {
        string julian = ElectionalMarkdownFormatter.ToMarkdown(Electional(_n1, Return1301));
        julian.Should().Contain("## Candidate instant 1 — Wed, 15 Mar 1301 5:42 PM");
        LineAfter(julian, "**Date Range:**").Should().Be(CalendarLine);

        string gregorian = ElectionalMarkdownFormatter.ToMarkdown(Electional(_j2000, Modern));
        gregorian.Should().Contain("## Candidate instant 1 — Wed, 15 Apr 2026 12:00 PM");
        gregorian.Should().NotContain(CalendarLine);
    }

    #endregion

    #region Time lords

    /// <summary>
    /// The firdaria of N1 at 1600-03-15 (Tier A, h_truth.out): age 299, four cycles of nine, 36 rows, every
    /// boundary an anniversary of the birth day printed on the calendar in force — Julian 03-15 in the 1300s,
    /// 03-14 from 1401, 03-13 from 1501 and Gregorian 03-23 from 1583 — and the current South Node period
    /// undivided (T-D8, R-D6). Until 1.3.4 each period ran 365.25 days from the typed reading as proleptic
    /// ticks and printed its boundaries as such, and a node's period was divided from Saturn.
    /// </summary>
    [Fact]
    public void FirdariaSection_PrintsTheJulianAnniversaries_ForN1At1600()
    {
        StringBuilder sb = new();
        ChartMarkdownFormatter.AppendFirdaria(sb, _n1, new DateTime(1600, 3, 15));
        string md = sb.ToString();

        md.Split('\n').Count(l => Regex.IsMatch(l, @"^\| (Sun|Venus|Mercury|Moon|Saturn|Jupiter|Mars|North Node|South Node) \| \d+ \| "))
            .Should().Be(36, "age 299 holds (299 ÷ 75) + 1 = 4 cycles of nine periods");
        md.Should().Contain("**Chart Sect:** Day");
        md.Should().Contain("| Sun | 10 | 1300-03-15 | 1310-03-15 |");
        md.Should().Contain("| Venus | 8 | 1535-03-13 | 1543-03-13 |");
        md.Should().Contain("| Jupiter | 12 | 1576-03-13 | 1588-03-23 |");
        md.Should().Contain("| South Node | 2 | 1598-03-23 | 1600-03-23 | **\u2190 current** |");
        md.Should().Contain("**Current Major Period:** South Node (1598\u20131600, 2y)");
        md.Should().Contain("**Current Sub-Period:** none (a node's period is not divided)");
        md.Should().NotContain("### Sub-Periods");
        md.Should().Contain("Age 299: South Node major period (1598\u20131600), undivided.");
    }

    #endregion

    #region Builders

    /// <summary>The line after the first line that starts with <paramref name="prefix"/>, without its line ending.</summary>
    private static string LineAfter(string markdown, string prefix)
    {
        string[] lines = markdown.Split('\n');
        int at = Array.FindIndex(lines, l => l.StartsWith(prefix, StringComparison.Ordinal));
        at.Should().BeGreaterThanOrEqualTo(0, "the export holds a line starting with {0}", prefix);
        return lines[at + 1].TrimEnd('\r');
    }

    /// <summary>A chart cast at an instant, at Greenwich at an explicit offset of zero, its input the instant's reading.</summary>
    private ChartResult ChartAt(DateTime instant) => _engine.CalculateAtInstant(
        new ChartInput
        {
            Name = "At " + CalendarDateFormatter.DateTime(instant),
            LocalDateTime = CalendarDateFormatter.Reading(instant),
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = GreenwichLatitude,
            Longitude = GreenwichLongitude,
            LocationName = "Greenwich",
            EventType = "Event",
        },
        JulianDayUtility.InstantToJulianDay(instant));

    private static IngressSeries IngressSeries(DateTime exact) => new()
    {
        Planet = CelestialBodyId.Sun,
        PlanetName = "Sun",
        Ingresses =
        [
            new IngressResult
            {
                Planet = CelestialBodyId.Sun, PlanetName = "Sun", Sign = "Aries", Element = "Fire", Modality = "Cardinal",
                TargetLongitude = 0.0, ActualLongitude = 0.00001, PrecisionArcSeconds = 0.03, ExactUtc = exact,
                IsCardinalIngress = true, SeasonName = "Vernal Equinox", EngineVersion = Engine, CalculatedAt = CalcTime,
            },
        ],
        ScanStart = new DateTime(exact.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ScanEnd = new DateTime(exact.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        Count = 1,
        EngineVersion = Engine,
        CalculatedAt = CalcTime,
    };

    private static EclipseSeries EclipseSeries(DateTime maximum) => new()
    {
        Eclipses =
        [
            new EclipseResult
            {
                Category = "Solar", IsSolar = true, Type = "Total", FullLabel = "Total Solar Eclipse",
                MaximumUtc = maximum, MaximumJd = JulianDayUtility.InstantToJulianDay(maximum),
                SunLongitude = 341.5, MoonLongitude = 341.5, Sign = "Pisces", Degree = 11.5, PositionFormatted = "11°30' Pisces",
                Element = "Water", Modality = "Mutable", NodeLongitude = 340.0, NodeDistance = 1.5, RawFlags = 0x0004,
                EngineVersion = Engine, CalculatedAt = CalcTime,
            },
        ],
        ScanStart = new DateTime(maximum.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ScanEnd = new DateTime(maximum.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        Count = 1,
        EngineVersion = Engine,
        CalculatedAt = CalcTime,
    };

    private static GreatConjunctionSeries GreatConjunctionSeries(DateTime exact) => new()
    {
        Conjunctions =
        [
            new GreatConjunctionResult
            {
                ExactUtc = exact, Longitude = 248.43, PositionFormatted = "08°26' Sagittarius", Sign = "Sagittarius",
                Element = "Fire", Modality = "Mutable", IsMutation = false, YearsSincePrevious = 19.9,
                JupiterLongitude = 248.43, SaturnLongitude = 248.43, PrecisionArcSeconds = 0.5,
            },
        ],
        ScanStart = new DateTime(exact.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ScanEnd = new DateTime(exact.Year + 1, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        Count = 1,
        EngineVersion = Engine,
        CalculatedAt = CalcTime,
    };

    private static LunationSeries LunationSeries(DateTime exact) => new()
    {
        Lunations =
        [
            new LunationResult
            {
                Type = "New Moon", IsNewMoon = true, ExactUtc = exact, SunLongitude = 341.5, MoonLongitude = 341.5,
                Sign = "Pisces", Degree = 11.5, PositionFormatted = "11°30' Pisces", Element = "Water", Modality = "Mutable",
                PrecisionArcSeconds = 0.4, IsNearNode = false, NodeDistance = 40.0, EngineVersion = Engine, CalculatedAt = CalcTime,
            },
        ],
        ScanStart = new DateTime(exact.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ScanEnd = new DateTime(exact.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        Count = 1,
        EngineVersion = Engine,
        CalculatedAt = CalcTime,
    };

    private static MundaneAspectSeries MundaneAspectSeries(DateTime exact) => new()
    {
        Body1 = CelestialBodyId.Saturn, Body1Name = "Saturn", Body2 = CelestialBodyId.Pluto, Body2Name = "Pluto",
        HarmonicMode = "90° Harmonic",
        Aspects =
        [
            new MundaneAspectResult
            {
                Body1 = CelestialBodyId.Saturn, Body1Name = "Saturn", Body2 = CelestialBodyId.Pluto, Body2Name = "Pluto",
                AspectName = "Waxing Square", TargetAngle = 90.0, ExactUtc = exact, Longitude1 = 124.15, Longitude2 = 34.15,
                Position1Formatted = "04°09' Leo", Position2Formatted = "04°09' Taurus", Sign1 = "Leo", Sign2 = "Taurus",
                ActualSeparation = 90.0, PrecisionArcSeconds = 0.25, CyclePhase = 90.0, PhaseLabel = "Waxing",
                EngineVersion = Engine, CalculatedAt = CalcTime,
            },
        ],
        ScanStart = new DateTime(exact.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ScanEnd = new DateTime(exact.Year, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        Count = 1,
        EngineVersion = Engine,
        CalculatedAt = CalcTime,
    };

    private static SynodicCycleSeries SynodicCycleSeries(DateTime exact) => new()
    {
        BodyAName = "Jupiter", BodyBName = "Saturn",
        Conjunctions =
        [
            new SynodicConjunction
            {
                Number = 1, DateTimeUtc = exact, JulianDay = JulianDayUtility.InstantToJulianDay(exact), Longitude = 248.43,
                Position = "08°26′ Sagittarius", Sign = "Sagittarius", Element = "Fire", Modality = "Mutable",
                BodyALongitude = 248.43, BodyBLongitude = 248.43, PrecisionArcSeconds = 0.5,
                SynodicPeriodDays = null, SynodicPeriodYears = null,
            },
        ],
        StartDate = new DateTime(exact.Year - 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(exact.Year + 1, 12, 31, 0, 0, 0, DateTimeKind.Utc),
        Count = 1,
        AverageSynodicPeriodDays = null, AverageSynodicPeriodYears = null, MinSynodicPeriodDays = null, MaxSynodicPeriodDays = null,
        EngineVersion = Engine,
        CalculatedAt = CalcTime,
    };

    private static ReturnIngressTimeline ReturnIngressTimeline(DateTime exact) => new()
    {
        NativeName = "N1",
        Entries =
        [
            new ReturnIngressTimelineEntry
            {
                EventType = "Return", Planet = CelestialBodyId.Sun, PlanetName = "Sun", ExactUtc = exact, Longitude = 0.0,
                PositionFormatted = "00°00′00″ Aries", Sign = "Aries", Label = "Return #1", PrecisionArcSeconds = 0.1,
            },
        ],
        ScanStart = new DateTime(exact.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ScanEnd = new DateTime(exact.Year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Count = 1,
        ReturnPlanets = ["Sun"],
        IngressPlanets = [],
        EngineVersion = Engine,
        CalculatedAt = CalcTime,
    };

    private static PlanetaryCycleTimeline CycleTimeline(DateTime start) => new()
    {
        Planet = CelestialBodyId.Mars, PlanetName = "Mars", NatalLongitude = 0.0, NatalPositionFormatted = "00°00′ Aries",
        NativeName = "N1", ReturnNumber = 1,
        PhasePoints =
        [
            Phase("Return", 0, start, 0.0, 0.0),
            Phase("Next Return", 360, start.AddDays(365), 365.0, 100.0),
        ],
        CycleDurationDays = 365.0,
        EngineVersion = Engine,
        CalculatedAt = CalcTime,
    };

    private static CyclePhasePoint Phase(string name, int angle, DateTime exact, double days, double percent) => new()
    {
        PhaseName = name, PhaseAngle = angle, TargetLongitude = 0.0, TargetPositionFormatted = "00°00′ Aries",
        ExactUtc = exact, Sign = "Aries", DaysFromStart = days, CyclePercentage = percent, PassCount = 1, AllPasses = [exact],
    };

    private static SolarReturnResult SolarReturn(ChartResult natal, ChartResult returnChart) => new()
    {
        NatalChart = natal, ReturnChart = returnChart, Year = returnChart.UtcDateTime.Year,
        NatalSunLongitude = 0.0, ReturnSunLongitude = 0.0, ReturnLocationName = "Greenwich",
        ReturnLatitude = GreenwichLatitude, ReturnLongitude = GreenwichLongitude, PrecisionArcSeconds = 0.1,
        EngineVersion = Engine, CalculatedAt = CalcTime,
    };

    private static LunarReturnResult LunarReturn(ChartResult natal, ChartResult returnChart) => new()
    {
        NatalChart = natal, ReturnChart = returnChart, SequenceNumber = 1,
        NatalMoonLongitude = 0.0, ReturnMoonLongitude = 0.0, ReturnLocationName = "Greenwich",
        ReturnLatitude = GreenwichLatitude, ReturnLongitude = GreenwichLongitude, PrecisionArcSeconds = 0.1,
        EngineVersion = Engine, CalculatedAt = CalcTime,
    };

    private static PlanetaryReturnResult PlanetaryReturn(ChartResult natal, ChartResult returnChart, DateTime firstPass) => new()
    {
        NatalChart = natal, ReturnChart = returnChart, Planet = CelestialBodyId.Mars, PlanetName = "Mars",
        NatalLongitude = 0.0, ReturnLongitude = 0.0, ReturnNumber = 1, AgeAtReturn = 1,
        Passes = [firstPass, firstPass.AddDays(60), firstPass.AddDays(120)],
        ReturnLocationName = "Greenwich", ReturnLatitude = GreenwichLatitude, ReturnLongitude_Geo = GreenwichLongitude,
        PrecisionArcSeconds = 0.1, EngineVersion = Engine, CalculatedAt = CalcTime,
    };

    /// <summary>A transit result over March (or April) of the year, Saturn conjunct the natal Sun exact at the instant.</summary>
    private static TransitResult Transit(ChartResult natal, int year, DateTime exact) => new()
    {
        NatalChart = natal,
        ReportStartDate = new DateOnly(year, exact.Month, 1),
        ReportEndDate = new DateOnly(year, exact.Month, 28),
        MidpointDate = new DateOnly(year, exact.Month, 14),
        IncludeMinorAspects = false,
        TransitPositions = new Dictionary<TransitBody, TransitPlanetPosition>(),
        Stations = [],
        Ingresses = [],
        ActiveTransits =
        [
            new ActiveTransit
            {
                TransitPlanet = TransitBody.Saturn, NatalTarget = "Sun", NatalTargetSymbol = "SU", AspectName = "Conjunction",
                AspectAbbreviation = "cnj", TransitLongitudeAtStart = 0.5, TransitSignAtStart = "Aries", NatalLongitude = 0.0,
                NatalSign = "Aries", NatalHouseNumber = 1, OrbAtStartDegrees = 0.5, OrbDirection = "applying", TransitHouseNumber = 1,
                Lifecycle =
                [
                    new TransitLifecycleEvent { EventType = TransitLifecycleEventType.Exact, Date = exact, PassNumber = 1, ExactOrbDegrees = 0.0 },
                ],
            },
        ],
        HouseOverlays = [],
        MidpointActivations = [],
        MarsIncluded = false,
    };

    private static DailyHoroscopeDay Day(DateOnly date) => new()
    {
        Date = date, MoonSign = ZodiacSign.Aries, MoonLongitude = 15.0, MoonPhaseLabel = "Waxing Crescent (mid)",
        VoidOfCoursePeriods = [], ActiveTransits = [], ExactToday = [], EnteringToday = [], ExitingToday = [],
    };

    /// <summary>An electional result with one candidate at the instant, at Greenwich on UTC, from the services.</summary>
    private ElectionalResult Electional(ChartResult natal, DateTime instant)
    {
        ChartResult chart = ChartAt(instant);
        MoonCondition moon = new MoonConditionEvaluator(_fixture.EphemerisService).Evaluate(chart.JulianDay, [], "Etc/UTC");
        PlanetaryHour hour = new PlanetaryHourCalculator(_fixture.EphemerisService)
            .Calculate(chart.JulianDay, GreenwichLatitude, GreenwichLongitude, "Etc/UTC");
        ScoreBreakdown breakdown = new ElectionalScorer().Score(chart, moon, hour, ElectionType.Other, natal);
        DateOnly day = DateOnly.FromDateTime(CalendarDateFormatter.Reading(instant));

        return new ElectionalResult
        {
            Input = new ElectionalInput
            {
                NatalChart = natal, ElectionType = ElectionType.Other, ElectionGoal = "A label", RangeStart = day, RangeEnd = day,
                Latitude = GreenwichLatitude, Longitude = GreenwichLongitude, LocationName = "Greenwich", TimeZone = "Etc/UTC",
            },
            NatalChart = natal,
            Candidates =
            [
                new CandidateWindow
                {
                    Rank = 1, LocalDateTime = DateTime.SpecifyKind(instant, DateTimeKind.Unspecified), Chart = chart, Moon = moon,
                    PlanetaryHour = hour, Score = breakdown.OverallScore, ScoreBreakdown = breakdown,
                    NatalHarmonyAspects = [], Strengths = ["A label"], Compromises = [],
                },
            ],
            RequestedCandidateCount = 1,
            SamplesWithoutPlanetaryHour = 0,
            DisqualifiedPeriods = [],
            VoidOfCoursePeriods = [],
            CalculatedAt = CalcTime,
        };
    }

    #endregion
}
