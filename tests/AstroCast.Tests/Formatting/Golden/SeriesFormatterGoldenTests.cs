using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Tests.Fixtures;

namespace AstroCast.Tests.Formatting.Golden;

/// <summary>
/// Golden-file tests for formatters that consume synthetic series / DTO
/// input and do not require the Swiss Ephemeris. Each test builds a canonical
/// input by hand so the golden exercises the formatter's output structure
/// (headings, tables, conditional sections, footers) independently of any
/// engine-side calculation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SeriesFormatterGoldenTests : GoldenFormatterTestBase
{
    private const string Engine = "AstroCast 1.0.207 (SwissEph 2.10.03)";

    private static readonly DateTime CalcTime =
        new(2026, 4, 17, 14, 23, 15, DateTimeKind.Utc);

    #region LunationFormatter

    /// <summary>
    /// Verifies <see cref="LunationFormatter.SeriesToMarkdown"/> renders a
    /// mixed New/Full moon series with a possible eclipse indicator.
    /// </summary>
    [Fact]
    public void LunationFormatter_SeriesToMarkdown_MatchesGolden()
    {
        LunationResult newMoon = new()
        {
            Type = "New Moon",
            IsNewMoon = true,
            ExactUtc = new DateTime(2024, 1, 11, 11, 57, 0, DateTimeKind.Utc),
            SunLongitude = 290.916,
            MoonLongitude = 290.916,
            Sign = "Capricorn",
            Degree = 20.916,
            PositionFormatted = "20\u00b055' Capricorn",
            Element = "Earth",
            Modality = "Cardinal",
            PrecisionArcSeconds = 0.45,
            IsNearNode = false,
            NodeDistance = 75.4,
            EclipseIndicator = null,
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        LunationResult fullMoon = new()
        {
            Type = "Full Moon",
            IsNewMoon = false,
            ExactUtc = new DateTime(2024, 3, 25, 7, 0, 0, DateTimeKind.Utc),
            SunLongitude = 5.125,
            MoonLongitude = 185.125,
            Sign = "Libra",
            Degree = 5.125,
            PositionFormatted = "5\u00b007' Libra",
            Element = "Air",
            Modality = "Cardinal",
            PrecisionArcSeconds = 0.12,
            IsNearNode = true,
            NodeDistance = 10.585,
            EclipseIndicator = "Near a node (within 12°)",
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        LunationSeries series = new()
        {
            Lunations = new List<LunationResult> { newMoon, fullMoon }.AsReadOnly(),
            ScanStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ScanEnd = new DateTime(2024, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            Count = 2,
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        string md = LunationFormatter.SeriesToMarkdown(series);

        AssertGolden(md, "LunationFormatter_SeriesToMarkdown_Sample.golden.md");
    }

    #endregion

    #region IngressFormatter

    /// <summary>
    /// Verifies <see cref="IngressFormatter.SeriesToMarkdown"/> renders a
    /// cardinal Sun ingress series with season names.
    /// </summary>
    [Fact]
    public void IngressFormatter_SeriesToMarkdown_MatchesGolden()
    {
        IngressResult vernal = new()
        {
            Planet = CelestialBodyId.Sun,
            PlanetName = "Sun",
            Sign = "Aries",
            Element = "Fire",
            Modality = "Cardinal",
            TargetLongitude = 0.0,
            ActualLongitude = 0.00001,
            PrecisionArcSeconds = 0.03,
            ExactUtc = new DateTime(2024, 3, 20, 3, 6, 0, DateTimeKind.Utc),
            IsCardinalIngress = true,
            SeasonName = "Vernal Equinox",
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        IngressResult summer = new()
        {
            Planet = CelestialBodyId.Sun,
            PlanetName = "Sun",
            Sign = "Cancer",
            Element = "Water",
            Modality = "Cardinal",
            TargetLongitude = 90.0,
            ActualLongitude = 89.99998,
            PrecisionArcSeconds = 0.05,
            ExactUtc = new DateTime(2024, 6, 20, 20, 51, 0, DateTimeKind.Utc),
            IsCardinalIngress = true,
            SeasonName = "Summer Solstice",
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        IngressSeries series = new()
        {
            Planet = CelestialBodyId.Sun,
            PlanetName = "Sun",
            Ingresses = new List<IngressResult> { vernal, summer }.AsReadOnly(),
            ScanStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ScanEnd = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            Count = 2,
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        string md = IngressFormatter.SeriesToMarkdown(series);

        AssertGolden(md, "IngressFormatter_SeriesToMarkdown_Sample.golden.md");
    }

    #endregion

    #region SynodicCycleFormatter

    /// <summary>
    /// Verifies <see cref="SynodicCycleFormatter.ToMarkdown"/> renders a
    /// three-conjunction series with summary statistics.
    /// </summary>
    [Fact]
    public void SynodicCycleFormatter_ToMarkdown_MatchesGolden()
    {
        SynodicConjunction first = new()
        {
            Number = 1,
            DateTimeUtc = new DateTime(2000, 5, 28, 16, 5, 0, DateTimeKind.Utc),
            JulianDay = 2451693.17,
            Longitude = 52.75,
            Position = "22\u00b046\u2032 Taurus",
            Sign = "Taurus",
            Element = "Earth",
            Modality = "Fixed",
            BodyALongitude = 52.75,
            BodyBLongitude = 52.75,
            PrecisionArcSeconds = 0.50,
            SynodicPeriodDays = null,
            SynodicPeriodYears = null,
        };

        SynodicConjunction second = new()
        {
            Number = 2,
            DateTimeUtc = new DateTime(2020, 12, 21, 18, 20, 0, DateTimeKind.Utc),
            JulianDay = 2459205.26,
            Longitude = 300.48,
            Position = "00\u00b029\u2032 Aquarius",
            Sign = "Aquarius",
            Element = "Air",
            Modality = "Fixed",
            BodyALongitude = 300.48,
            BodyBLongitude = 300.48,
            PrecisionArcSeconds = 0.20,
            SynodicPeriodDays = 7512.09,
            SynodicPeriodYears = 20.57,
        };

        SynodicConjunction third = new()
        {
            Number = 3,
            DateTimeUtc = new DateTime(2040, 10, 31, 13, 47, 0, DateTimeKind.Utc),
            JulianDay = 2466475.07,
            Longitude = 197.33,
            Position = "17\u00b020\u2032 Libra",
            Sign = "Libra",
            Element = "Air",
            Modality = "Cardinal",
            BodyALongitude = 197.33,
            BodyBLongitude = 197.33,
            PrecisionArcSeconds = 0.35,
            SynodicPeriodDays = 7269.81,
            SynodicPeriodYears = 19.90,
        };

        SynodicCycleSeries series = new()
        {
            BodyAName = "Jupiter",
            BodyBName = "Saturn",
            Conjunctions = new List<SynodicConjunction> { first, second, third }.AsReadOnly(),
            StartDate = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2050, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Count = 3,
            AverageSynodicPeriodDays = 7390.95,
            AverageSynodicPeriodYears = 20.23,
            MinSynodicPeriodDays = 7269.81,
            MaxSynodicPeriodDays = 7512.09,
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        string md = SynodicCycleFormatter.ToMarkdown(series);

        AssertGolden(md, "SynodicCycleFormatter_ToMarkdown_Sample.golden.md");
    }

    #endregion

    #region GreatConjunctionFormatter

    /// <summary>
    /// Verifies <see cref="GreatConjunctionFormatter.ToMarkdown"/> renders a
    /// two-entry series that includes a mutation.
    /// </summary>
    [Fact]
    public void GreatConjunctionFormatter_ToMarkdown_MatchesGolden()
    {
        GreatConjunctionResult c2000 = new()
        {
            ExactUtc = new DateTime(2000, 5, 28, 16, 5, 0, DateTimeKind.Utc),
            Longitude = 52.75,
            PositionFormatted = "22\u00b046' Taurus",
            Sign = "Taurus",
            Element = "Earth",
            Modality = "Fixed",
            IsMutation = false,
            YearsSincePrevious = 19.9,
            JupiterLongitude = 52.75,
            SaturnLongitude = 52.75,
            PrecisionArcSeconds = 0.50,
        };

        GreatConjunctionResult c2020 = new()
        {
            ExactUtc = new DateTime(2020, 12, 21, 18, 20, 0, DateTimeKind.Utc),
            Longitude = 300.48,
            PositionFormatted = "00\u00b029' Aquarius",
            Sign = "Aquarius",
            Element = "Air",
            Modality = "Fixed",
            IsMutation = true,
            YearsSincePrevious = 20.6,
            JupiterLongitude = 300.48,
            SaturnLongitude = 300.48,
            PrecisionArcSeconds = 0.20,
        };

        GreatConjunctionSeries series = new()
        {
            Conjunctions = new List<GreatConjunctionResult> { c2000, c2020 }.AsReadOnly(),
            ScanStart = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ScanEnd = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Count = 2,
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        string md = GreatConjunctionFormatter.ToMarkdown(series);

        AssertGolden(md, "GreatConjunctionFormatter_ToMarkdown_Sample.golden.md");
    }

    #endregion

    #region MundaneAspectFormatter

    /// <summary>
    /// Verifies <see cref="MundaneAspectFormatter.ToMarkdown"/> renders a
    /// mixed waxing/waning series.
    /// </summary>
    [Fact]
    public void MundaneAspectFormatter_ToMarkdown_MatchesGolden()
    {
        MundaneAspectResult waxingSquare = new()
        {
            Body1 = CelestialBodyId.Saturn,
            Body1Name = "Saturn",
            Body2 = CelestialBodyId.Pluto,
            Body2Name = "Pluto",
            AspectName = "Waxing Square",
            TargetAngle = 90.0,
            ExactUtc = new DateTime(2010, 1, 31, 13, 58, 0, DateTimeKind.Utc),
            Longitude1 = 124.15,
            Longitude2 = 34.15,
            Position1Formatted = "04\u00b009' Leo",
            Position2Formatted = "04\u00b009' Taurus",
            Sign1 = "Leo",
            Sign2 = "Taurus",
            ActualSeparation = 90.0,
            PrecisionArcSeconds = 0.25,
            CyclePhase = 90.0,
            PhaseLabel = "Waxing",
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        MundaneAspectResult opposition = new()
        {
            Body1 = CelestialBodyId.Saturn,
            Body1Name = "Saturn",
            Body2 = CelestialBodyId.Pluto,
            Body2Name = "Pluto",
            AspectName = "Opposition",
            TargetAngle = 180.0,
            ExactUtc = new DateTime(2016, 6, 18, 9, 30, 0, DateTimeKind.Utc),
            Longitude1 = 253.80,
            Longitude2 = 73.80,
            Position1Formatted = "13\u00b048' Sagittarius",
            Position2Formatted = "13\u00b048' Gemini",
            Sign1 = "Sagittarius",
            Sign2 = "Gemini",
            ActualSeparation = 180.0,
            PrecisionArcSeconds = 0.40,
            CyclePhase = 180.0,
            PhaseLabel = "Opposition",
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        MundaneAspectSeries series = new()
        {
            Body1 = CelestialBodyId.Saturn,
            Body1Name = "Saturn",
            Body2 = CelestialBodyId.Pluto,
            Body2Name = "Pluto",
            HarmonicMode = "90\u00b0 Harmonic",
            Aspects = new List<MundaneAspectResult> { waxingSquare, opposition }.AsReadOnly(),
            ScanStart = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ScanEnd = new DateTime(2020, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Count = 2,
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        string md = MundaneAspectFormatter.ToMarkdown(series);

        AssertGolden(md, "MundaneAspectFormatter_ToMarkdown_Sample.golden.md");
    }

    #endregion

    #region Culture independence

    // Each golden again under de-DE, sv-SE and th-TH, compared with the same golden
    // file: the export reads the same whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="LunationFormatter_SeriesToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void LunationFormatter_SeriesToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        LunationFormatter_SeriesToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="IngressFormatter_SeriesToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void IngressFormatter_SeriesToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        IngressFormatter_SeriesToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="SynodicCycleFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SynodicCycleFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SynodicCycleFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="GreatConjunctionFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void GreatConjunctionFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        GreatConjunctionFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="MundaneAspectFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void MundaneAspectFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        MundaneAspectFormatter_ToMarkdown_MatchesGolden();
    }

    #endregion
}
