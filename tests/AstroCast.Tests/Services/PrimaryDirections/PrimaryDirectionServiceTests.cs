using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Services.PrimaryDirections;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.Tests.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.Services.PrimaryDirections;

/// <summary>
/// <see cref="PrimaryDirectionService"/> (branch Primary, 1.5.0): the period of rule R7, the sensitivity and range of
/// rule R8, the circumpolar stars of rule R1, the Part of Fortune under a sect flip, and the settings' limits.
/// </summary>
/// <param name="fixture">The shared ephemeris fixture.</param>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class PrimaryDirectionServiceTests(EphemerisFixture fixture)
{
    /// <summary>The direct Moon conjunct the Midheaven of J2000 NYC is exact at 2016-03-26 21:03:58 UT (Tier B).</summary>
    private static readonly DateTime MoonToMidheavenInstant = new(2016, 3, 26, 21, 3, 58, DateTimeKind.Utc);

    /// <summary>A hit's instant is its UT date: the Moon to the Midheaven is in [2016-03-26, 2016-03-26] and not in the day after.</summary>
    [Fact]
    public void FindHits_IsByUtDate()
    {
        PrimaryDirectionService service = Service();
        ChartResult natal = J2000Nyc();

        IReadOnlyList<PrimaryDirectionHit> onTheDay = service.FindHits(
            natal, PrimaryDirectionSettings.Default, new DateOnly(2016, 3, 26), new DateOnly(2016, 3, 26));
        IReadOnlyList<PrimaryDirectionHit> theDayAfter = service.FindHits(
            natal, PrimaryDirectionSettings.Default, new DateOnly(2016, 3, 27), new DateOnly(2016, 3, 27));

        PrimaryDirectionHit? hit = MoonToMidheaven(onTheDay);
        hit.Should().NotBeNull();
        hit?.InstantUtc.Should().Be(MoonToMidheavenInstant);
        MoonToMidheaven(theDayAfter).Should().BeNull();
    }

    /// <summary>A minute of birth time moves the Moon to the Midheaven by −2.9539 months: a later birth carries the RAMC past the Moon sooner.</summary>
    [Fact]
    public void MonthsPerMinute_OfTheMoonToTheMidheaven()
    {
        PrimaryDirectionResult result = Service().Calculate(J2000Nyc(), PrimaryDirectionSettings.Default);

        PrimaryDirectionHit? hit = MoonToMidheaven(result.Hits);

        hit.Should().NotBeNull();
        hit?.MonthsPerMinute.Should().NotBeNull();
        hit?.MonthsPerMinute.Should().BeApproximately(-2.9539, 1e-3);
    }

    /// <summary>With an uncertainty of 4 minutes the Moon to the Midheaven ranges from 2015-04-02 05:50:20 to 2017-03-21 12:18:15 UT, the earlier first.</summary>
    [Fact]
    public void Range_AtFourMinutes_IsEarlierToLater()
    {
        PrimaryDirectionSettings settings = new() { BirthTimeUncertaintyMinutes = 4 };

        PrimaryDirectionResult result = Service().Calculate(J2000Nyc(), settings);

        PrimaryDirectionHit? hit = MoonToMidheaven(result.Hits);
        hit.Should().NotBeNull();
        hit?.RangeStartUtc.Should().BeCloseTo(new DateTime(2015, 4, 2, 5, 50, 20, DateTimeKind.Utc), TimeSpan.FromSeconds(2));
        hit?.RangeEndUtc.Should().BeCloseTo(new DateTime(2017, 3, 21, 12, 18, 15, DateTimeKind.Utc), TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// The five stars that never rise or set at New York direct only to the meridian, direct: Polaris to the IC at
    /// 11.73895°, Eltanin to the Midheaven at 62.68971°, Canopus to the IC at 69.54508°, Toliman to the Midheaven at
    /// 13.44101°, Achernar to neither within 100 years; and none of them converse, nor to any other significator.
    /// </summary>
    [Fact]
    public void CircumpolarStars_DirectOnlyToTheMeridian()
    {
        PrimaryDirectionResult result = Service().Calculate(J2000Nyc(), PrimaryDirectionSettings.Default);

        (string SwissEphName, string? Significator, double Arc)[] stars =
        [
            ("Polaris", "IC", 11.73895),
            ("Eltanin", "Midheaven", 62.68971),
            ("Canopus", "IC", 69.54508),
            ("Toliman", "Midheaven", 13.44101),
            ("Achernar", null, 0.0),
        ];

        foreach ((string swissEphName, string? significator, double arc) in stars)
        {
            string promissor = FixedStarCatalog.Stars.Single(s => s.SwissEphName == swissEphName).DisplayName;
            List<PrimaryDirectionHit> hits = result.Hits.Where(h => h.Promissor == promissor).ToList();

            hits.Should().OnlyContain(h => h.Motion == PrimaryDirectionMotion.Direct, $"{promissor} has no semi-arcs for a converse direction");
            hits.Should().OnlyContain(h => h.Significator == "Midheaven" || h.Significator == "IC", $"{promissor} reaches only the meridian");

            if (significator is null)
            {
                hits.Should().BeEmpty($"{promissor} reaches neither meridian within 100 years");
                continue;
            }

            hits.Should().ContainSingle($"{promissor} reaches one meridian within 100 years");
            hits[0].Significator.Should().Be(significator);
            hits[0].Arc.Should().BeApproximately(arc, 1e-4);
            hits[0].PromissorKind.Should().Be(PrimaryDirectionPromissorKind.FixedStar);
        }
    }

    /// <summary>
    /// A birth at 07:24:40 in New York on 2000-01-01, the Sun's degree rising at 12:25:09 UT: the sect flips within the
    /// minute, so every Part of Fortune hit has no months per minute, while the Moon to the Midheaven keeps its figure.
    /// </summary>
    [Fact]
    public void SectFlipWithinTheMinute_LeavesTheFortuneWithoutAFigure()
    {
        ChartInput j2000 = GoldenFixtures.J2000NycInput();
        ChartInput input = new()
        {
            Name = "J2000 NYC at 07:24:40",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 24, 40),
            TimeZone = j2000.TimeZone,
            Latitude = j2000.Latitude,
            Longitude = j2000.Longitude,
            LocationName = j2000.LocationName,
            EventType = j2000.EventType,
            HouseSystem = j2000.HouseSystem,
        };
        ChartResult natal = GoldenFixtures.CreateChartEngine(fixture).Calculate(input);

        PrimaryDirectionResult result = Service().Calculate(natal, PrimaryDirectionSettings.Default);

        List<PrimaryDirectionHit> fortune = result.Hits.Where(h => h.Significator == "Part of Fortune").ToList();
        fortune.Should().NotBeEmpty();
        fortune.Should().OnlyContain(h => h.MonthsPerMinute == null, "the Lot's formula differs at one of the two instants");
        MoonToMidheaven(result.Hits)?.MonthsPerMinute.Should().NotBeNull();
    }

    /// <summary>
    /// The Descendant and IC take no point without latitude and no node (rule R5 as amended on 2026-10-18,
    /// branch PrimaryExport): such a point and its exact opposite give the same arc, so an antiscion to the
    /// Midheaven is already the direction of its contra-antiscion to the IC, and the node to the IC that of
    /// its opposition to the Midheaven. J2000 NYC: no Descendant or IC hit from an antiscion, a
    /// contra-antiscion or the true node; Mars's antiscion still directs to the Midheaven at 3.41039° (Tier B).
    /// </summary>
    [Fact]
    public void DescendantAndIc_TakeNoPointWithoutLatitude()
    {
        PrimaryDirectionResult result = Service().Calculate(J2000Nyc(), PrimaryDirectionSettings.Default);

        List<PrimaryDirectionHit> lowerAngles = result.Hits
            .Where(h => h.Significator == "Descendant" || h.Significator == "IC")
            .ToList();
        lowerAngles.Should().NotBeEmpty();
        lowerAngles.Should().NotContain(h => h.Aspect == PrimaryDirectionAspect.Antiscion);
        lowerAngles.Should().NotContain(h => h.Aspect == PrimaryDirectionAspect.ContraAntiscion);
        lowerAngles.Should().NotContain(h => h.Promissor == "True Node");
        lowerAngles.Should().OnlyContain(h => h.Aspect == PrimaryDirectionAspect.Conjunction);

        PrimaryDirectionHit? marsAntiscion = result.Hits.SingleOrDefault(h =>
            h.Motion == PrimaryDirectionMotion.Direct
            && h.Significator == "Midheaven"
            && h.Aspect == PrimaryDirectionAspect.Antiscion
            && h.Promissor == "Mars");
        marsAntiscion.Should().NotBeNull();
        marsAntiscion?.Arc.Should().BeApproximately(3.41039, 1e-4);
    }

    /// <summary>The settings refuse an age limit of 0 or 121 and an uncertainty of −1 or 121 minutes.</summary>
    /// <param name="ageLimitYears">The age limit.</param>
    /// <param name="uncertaintyMinutes">The uncertainty.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(121, 0)]
    [InlineData(100, -1)]
    [InlineData(100, 121)]
    public void Settings_RefuseValuesOutsideTheirLimits(int ageLimitYears, int uncertaintyMinutes)
    {
        Action build = () => _ = new PrimaryDirectionSettings
        {
            AgeLimitYears = ageLimitYears,
            BirthTimeUncertaintyMinutes = uncertaintyMinutes,
        };

        build.Should().Throw<ArgumentOutOfRangeException>();
    }

    #region The ephemeris-end guard (1.6.1, item F, branch TiesAndGuards)

    /// <summary>
    /// The solar-arc key refuses a birth whose bracket, the age limit plus 20 days, passes the end of the ephemeris
    /// files, before anything is read: the message names the span's end and the last birth whose bracket stays
    /// inside it (age limit 100: 2399-09-02), and the exception carries no parameter name, since the Directions
    /// catch prints the message as it is. The ephemeris double records every Julian Day asked, so "before any read"
    /// is its empty record. Until 1.6.1 the key read the Sun past the span, on the Moshier model with a log line only.
    /// </summary>
    [Fact]
    public void Calculate_SolarArcKey_BirthWithinTheBracketOfTheSpanEnd_IsRefusedBeforeAnyRead()
    {
        double birth = EphemerisSpan.LastJulianDay - 100.0;
        RecordingEphemerisService recorder = new(birth);
        PrimaryDirectionService service = new(recorder);
        PrimaryDirectionSettings settings = new() { Key = PrimaryDirectionKey.SolarArcRightAscension, AgeLimitYears = 100 };

        Action act = () => service.Calculate(ChartAt(birth), settings);

        ArgumentException refusal = act.Should().Throw<ArgumentException>().Which;
        refusal.ParamName.Should().BeNull("the Directions catch prints the message unstripped");
        refusal.Message.Should().Contain("2399-12-31 23:51:20 UT").And.Contain("2399-09-02").And.Contain("120 days");
        recorder.JulianDaysAsked.Should().BeEmpty("the guard runs before the speculum reads the ephemeris");
    }

    /// <summary>The guard is the solar-arc key's alone: a birth in 2399 runs under the Naibod key.</summary>
    [Fact]
    public void Calculate_NaibodKey_BirthIn2399_Runs()
    {
        ChartInput late = new()
        {
            Name = "J2000 NYC in 2399",
            LocalDateTime = new DateTime(2399, 10, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        };
        ChartResult natal = GoldenFixtures.CreateChartEngine(fixture).Calculate(late);
        PrimaryDirectionSettings settings = new() { Key = PrimaryDirectionKey.Naibod };

        Action act = () => Service().Calculate(natal, settings);

        act.Should().NotThrow();
    }

    /// <summary>
    /// The solar-arc inverter clamps its bracket to the span: a birth 50 days before the span's end with the
    /// default limit reads the Sun no later than the span's last instant. The guard makes this unreachable from
    /// <see cref="PrimaryDirectionService.Calculate"/>; the internal <c>YearsFor</c> reaches it. Until 1.6.1 the
    /// bracket's end was read at the birth plus 120 days.
    /// </summary>
    [Fact]
    public void SolarArcInverter_ClampsItsBracketToTheSpan()
    {
        double birth = EphemerisSpan.LastJulianDay - 50.0;
        RecordingEphemerisService recorder = new(birth);
        PrimaryDirectionService service = new(recorder);

        double? years = service.YearsFor(10.0, PrimaryDirectionKey.SolarArcRightAscension, birth, 100);

        years.Should().NotBeNull("an arc of 10° is reached within the clamped bracket of 50 days");
        recorder.JulianDaysAsked.Should().NotBeEmpty();
        recorder.MaxJulianDay.Should().BeLessThanOrEqualTo(EphemerisSpan.LastJulianDay);
    }

    /// <summary>A chart at a Julian Day, enough for the guard, which runs before the speculum reads anything.</summary>
    /// <param name="julianDay">The birth instant, as a Julian Day (UT).</param>
    /// <returns>The chart.</returns>
    private static ChartResult ChartAt(double julianDay) => new()
    {
        Input = new ChartInput
        {
            Name = "Late birth",
            LocalDateTime = new DateTime(2399, 9, 22, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        },
        JulianDay = julianDay,
        Obliquity = 23.4367,
        UtcDateTime = new DateTime(2399, 9, 22, 12, 0, 0, DateTimeKind.Utc),
        Positions = [],
        Houses = new HouseData
        {
            Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
            Ascendant = 0.0,
            Midheaven = 270.0,
            Armc = 0.0,
            Vertex = 0.0,
        },
        Aspects = [],
        Midpoints = [],
        MidpointActivations = [],
        Dignities = [],
        MoonCondition = null,
        ChartContext = null,
        FixedStarConjunctions = [],
        ResolvedUtcOffsetHours = 0.0,
        ResolvedOffsetLabel = "UTC+0",
        HouseSystemName = "Placidus",
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    #endregion

    #region The order of tied instants (1.6.1, item G, branch TiesAndGuards)

    /// <summary>
    /// Two hits within a second of each other order by significator, whatever the ulps between their instants: a
    /// direct conjunction of P to S and the converse of S to P are one Placidian distance and land ulps apart. Two
    /// hits a full second apart keep the instant order whatever their significators. Until 1.6.1 the exact instant
    /// decided, so a pair ulps apart was ordered by rounding.
    /// </summary>
    [Fact]
    public void CompareHits_OrdersHitsWithinASecondBySignificator()
    {
        Dictionary<string, int> order = new(StringComparer.Ordinal) { ["Sun"] = 0, ["Moon"] = 1 };
        const double instant = 2460000.0;
        PrimaryDirectionHit moonFirstByUlps = HitAt("Moon", "Sun", instant);
        PrimaryDirectionHit sunAnUlpLater = HitAt("Sun", "Moon", instant + 1e-9);
        PrimaryDirectionHit moonTwoSecondsEarlier = HitAt("Moon", "Sun", instant - 2.0 / 86400.0);

        PrimaryDirectionService.CompareHits(moonFirstByUlps, sunAnUlpLater, order)
            .Should().BePositive("within a second the Sun, the lower significator, sorts first");
        PrimaryDirectionService.CompareHits(sunAnUlpLater, moonFirstByUlps, order).Should().BeNegative();
        PrimaryDirectionService.CompareHits(moonTwoSecondsEarlier, sunAnUlpLater, order)
            .Should().BeNegative("two seconds apart the instant decides, whatever the significators");
        PrimaryDirectionService.CompareHits(moonFirstByUlps, moonFirstByUlps, order).Should().Be(0);
    }

    /// <summary>A direct conjunction hit at an instant, for the comparer.</summary>
    /// <param name="significator">The significator.</param>
    /// <param name="promissor">The promissor.</param>
    /// <param name="julianDayUt">The instant, as a Julian Day (UT).</param>
    /// <returns>The hit.</returns>
    private static PrimaryDirectionHit HitAt(string significator, string promissor, double julianDayUt) => new()
    {
        Significator = significator,
        Promissor = promissor,
        PromissorKind = PrimaryDirectionPromissorKind.Body,
        Aspect = PrimaryDirectionAspect.Conjunction,
        AspectAngle = 0.0,
        Motion = PrimaryDirectionMotion.Direct,
        Arc = 10.0,
        Years = 10.0,
        JulianDayUt = julianDayUt,
        InstantUtc = JulianDayUtility.JulianDayToInstant(julianDayUt),
    };

    #endregion

    #region The turning row (branch DirectionNotes, 1.7.2)

    /// <summary>
    /// Person A's converse Moon to Saturn's dexter square (found by script among the invented charts): the
    /// direction's date turns about five minutes before the recorded birth time. At an uncertainty of 4 or 6
    /// minutes its own date lies between the dates at the window's two ends and the hit is not turning; at 8, 10
    /// and 15 it lies outside them and the hit is. The date, the age and the months per minute are the birth's own
    /// whatever the window.
    /// </summary>
    /// <param name="window">The birth-time uncertainty, in minutes.</param>
    /// <param name="expected">Whether the hit is turning.</param>
    [Theory]
    [InlineData(4, false)]
    [InlineData(6, false)]
    [InlineData(8, true)]
    [InlineData(10, true)]
    [InlineData(15, true)]
    public void IsTurning_HartwellMoonToSaturnsSquare_FollowsTheWindow(int window, bool expected)
    {
        PrimaryDirectionResult result = Service().Calculate(
            Hartwell(), new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = window });

        PrimaryDirectionHit hit = result.Hits.Single(h =>
            h.Motion == PrimaryDirectionMotion.Converse
            && h.Significator == "Moon"
            && h.Promissor == "Saturn"
            && h.Aspect == PrimaryDirectionAspect.Square
            && h.AspectAngle < 0.0);

        hit.IsTurning.Should().Be(expected);
        DateOnly.FromDateTime(hit.InstantUtc).Should().Be(new DateOnly(1984, 12, 15));
        hit.Years.Should().BeApproximately(37.20, 0.005);
        hit.MonthsPerMinute.Should().BeApproximately(-0.2, 0.05);
        hit.RangeStartUtc.Should().NotBeNull();
        bool outsideItsRange = hit.InstantUtc < hit.RangeStartUtc || hit.InstantUtc > hit.RangeEndUtc;
        outsideItsRange.Should().Be(expected);
    }

    /// <summary>
    /// On the three invented charts at an uncertainty of 10 minutes no direction turns within the minute, so a hit
    /// is turning exactly when it has a range and its own date lies outside it; and with no uncertainty set no hit
    /// of the three is turning.
    /// </summary>
    [Fact]
    public void IsTurning_OnTheInventedCharts_IsTheDateOutsideItsRange()
    {
        foreach (ChartResult natal in new[] { J2000Nyc(), Hartwell(), Whitfield() })
        {
            PrimaryDirectionResult atTen = Service().Calculate(natal, new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 10 });
            foreach (PrimaryDirectionHit hit in atTen.Hits)
            {
                bool outsideItsRange = hit.RangeStartUtc is DateTime start && hit.RangeEndUtc is DateTime end
                    && (hit.InstantUtc < start || hit.InstantUtc > end);
                hit.IsTurning.Should().Be(outsideItsRange, $"{natal.Input.Name}: {hit.Motion} {hit.Significator} {hit.Aspect} {hit.Promissor}");
            }

            Service().Calculate(natal, PrimaryDirectionSettings.Default).Hits
                .Should().NotContain(h => h.IsTurning, $"{natal.Input.Name} has no direction that turns within a minute of the birth");
        }
    }

    /// <summary>
    /// The turn inside the minute: person A's chart cast at 09:24:50 (found by script: every reading from 09:24:40
    /// to 09:25:00) has the same direction's date turning within a minute of the birth, so with no uncertainty set
    /// the converse Moon to Saturn's dexter square is turning by the dates a minute either side alone, and its
    /// figure stays.
    /// </summary>
    [Fact]
    public void IsTurning_WithinTheMinute_IsFoundWithoutAWindow()
    {
        ChartInput hartwell = GoldenFixtures.HartwellInput("Birth Certificate");
        ChartInput input = new()
        {
            Name = "Eleanor Hartwell at 09:24:50",
            SourceNotes = hartwell.SourceNotes,
            LocalDateTime = TurnWithinTheMinuteReading,
            TimeZone = hartwell.TimeZone,
            UtcOffsetHours = hartwell.UtcOffsetHours,
            UseExplicitOffset = hartwell.UseExplicitOffset,
            Latitude = hartwell.Latitude,
            Longitude = hartwell.Longitude,
            LocationName = hartwell.LocationName,
            EventType = hartwell.EventType,
            HouseSystem = hartwell.HouseSystem,
        };
        ChartResult natal = GoldenFixtures.CreateChartEngine(fixture).Calculate(input);

        PrimaryDirectionResult result = Service().Calculate(natal, PrimaryDirectionSettings.Default);

        PrimaryDirectionHit hit = result.Hits.Single(h =>
            h.Motion == PrimaryDirectionMotion.Converse
            && h.Significator == "Moon"
            && h.Promissor == "Saturn"
            && h.Aspect == PrimaryDirectionAspect.Square
            && h.AspectAngle < 0.0);
        hit.IsTurning.Should().BeTrue();
        hit.MonthsPerMinute.Should().NotBeNull();
        hit.RangeStartUtc.Should().BeNull("no uncertainty is set");
    }

    /// <summary>The reading at which person A's converse Moon to Saturn's dexter square turns within the minute.</summary>
    private static readonly DateTime TurnWithinTheMinuteReading = new(1947, 10, 5, 9, 24, 50);

    #endregion

    #region Why a figure is missing (branch DirectionNotes, 1.7.2)

    /// <summary>
    /// J2000 NYC at an uncertainty of 4 minutes has seven hits without a range (found by script): four fall before
    /// birth at one end of the window, at ages 0.22 to 0.52, and three beyond the age limit, at ages 99.20 to
    /// 99.39. Every hit without a range has its reason, and every hit with one has none.
    /// </summary>
    [Fact]
    public void NoRangeReasons_J2000AtFourMinutes_AreBeforeBirthOrBeyondTheAgeLimit()
    {
        PrimaryDirectionResult result = Service().Calculate(J2000Nyc(), new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 4 });

        List<PrimaryDirectionHit> withoutRange = result.Hits.Where(h => h.RangeStartUtc is null).ToList();
        withoutRange.Should().HaveCount(7);
        result.Hits.Should().OnlyContain(h => (h.RangeStartUtc == null) == (h.NoRangeReasons != PrimaryDirectionGapReasons.None));
        withoutRange.Where(h => h.Years < 1.0).Should().HaveCount(4)
            .And.OnlyContain(h => h.NoRangeReasons == PrimaryDirectionGapReasons.BeforeBirth);
        withoutRange.Where(h => h.Years > 99.0).Should().HaveCount(3)
            .And.OnlyContain(h => h.NoRangeReasons == PrimaryDirectionGapReasons.BeyondAgeLimit);

        PrimaryDirectionHit marsToMidheaven = withoutRange.Single(h =>
            h.Motion == PrimaryDirectionMotion.Converse && h.Significator == "Midheaven" && h.Promissor == "Mars");
        marsToMidheaven.NoRangeReasons.Should().Be(PrimaryDirectionGapReasons.BeforeBirth);
        marsToMidheaven.MonthsPerMinute.Should().NotBeNull("the direction exists a minute either side of the birth");
        marsToMidheaven.NoMonthsPerMinuteReasons.Should().Be(PrimaryDirectionGapReasons.None);

        PrimaryDirectionHit alcyoneToAscendant = withoutRange.Single(h => h.Significator == "Ascendant" && h.Promissor == "Alcyone");
        alcyoneToAscendant.NoRangeReasons.Should().Be(PrimaryDirectionGapReasons.BeyondAgeLimit);
    }

    /// <summary>With no uncertainty set no range is sought, so no hit carries a reason for its absence.</summary>
    [Fact]
    public void NoRangeReasons_WithoutAWindow_AreNone()
    {
        PrimaryDirectionResult result = Service().Calculate(J2000Nyc(), PrimaryDirectionSettings.Default);

        result.Hits.Should().OnlyContain(h => h.NoRangeReasons == PrimaryDirectionGapReasons.None);
    }

    /// <summary>
    /// A hit without months per minute has its reason too (found by script): Daniel Whitfield's chart has six, three
    /// that fall before birth a minute from the birth, at ages 0.06 to 0.17, and three beyond the age limit, at
    /// ages 99.77 to 99.96; person A's has one, the converse Ascendant to Pollux at age 99.95.
    /// </summary>
    [Fact]
    public void NoMonthsPerMinuteReasons_AreBeforeBirthOrBeyondTheAgeLimit()
    {
        PrimaryDirectionResult whitfield = Service().Calculate(Whitfield(), PrimaryDirectionSettings.Default);

        List<PrimaryDirectionHit> withoutFigure = whitfield.Hits.Where(h => h.MonthsPerMinute is null).ToList();
        withoutFigure.Should().HaveCount(6);
        whitfield.Hits.Should().OnlyContain(h => (h.MonthsPerMinute == null) == (h.NoMonthsPerMinuteReasons != PrimaryDirectionGapReasons.None));
        withoutFigure.Where(h => h.Years < 1.0).Should().HaveCount(3)
            .And.OnlyContain(h => h.NoMonthsPerMinuteReasons == PrimaryDirectionGapReasons.BeforeBirth);
        withoutFigure.Where(h => h.Years > 99.0).Should().HaveCount(3)
            .And.OnlyContain(h => h.NoMonthsPerMinuteReasons == PrimaryDirectionGapReasons.BeyondAgeLimit);

        PrimaryDirectionResult hartwell = Service().Calculate(Hartwell(), PrimaryDirectionSettings.Default);
        hartwell.Hits.Where(h => h.MonthsPerMinute is null).Should().ContainSingle()
            .Which.Should().Match<PrimaryDirectionHit>(h =>
                h.Significator == "Ascendant" && h.Promissor == "Pollux" && h.NoMonthsPerMinuteReasons == PrimaryDirectionGapReasons.BeyondAgeLimit);
    }

    /// <summary>
    /// The birth at 07:24:40 in New York of <see cref="SectFlipWithinTheMinute_LeavesTheFortuneWithoutAFigure"/>:
    /// the reason every Part of Fortune hit has no months per minute is the change of sect, and at an uncertainty
    /// of 4 minutes it is the reason they have no range.
    /// </summary>
    [Fact]
    public void SectFlip_IsTheReasonTheFortuneHasNoFigure()
    {
        ChartInput j2000 = GoldenFixtures.J2000NycInput();
        ChartInput input = new()
        {
            Name = "J2000 NYC at 07:24:40",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 24, 40),
            TimeZone = j2000.TimeZone,
            Latitude = j2000.Latitude,
            Longitude = j2000.Longitude,
            LocationName = j2000.LocationName,
            EventType = j2000.EventType,
            HouseSystem = j2000.HouseSystem,
        };
        ChartResult natal = GoldenFixtures.CreateChartEngine(fixture).Calculate(input);

        PrimaryDirectionResult result = Service().Calculate(natal, new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 4 });

        List<PrimaryDirectionHit> fortune = result.Hits.Where(h => h.Significator == "Part of Fortune").ToList();
        fortune.Should().NotBeEmpty();
        fortune.Should().OnlyContain(h => h.NoMonthsPerMinuteReasons == PrimaryDirectionGapReasons.SectChange);
        fortune.Should().OnlyContain(h => h.RangeStartUtc == null && h.NoRangeReasons == PrimaryDirectionGapReasons.SectChange);
        result.Hits.Where(h => h.Significator != "Part of Fortune")
            .Should().NotContain(h => h.NoRangeReasons.HasFlag(PrimaryDirectionGapReasons.SectChange));
    }

    #endregion

    #region Helpers

    /// <summary>Person A, Eleanor Hartwell (invented), with a birth-time source.</summary>
    private ChartResult Hartwell() =>
        GoldenFixtures.CreateChartEngine(fixture).Calculate(GoldenFixtures.HartwellInput("Birth Certificate"));

    /// <summary>Daniel Whitfield (invented): 1964-12-23 11:00 America/New_York, Albany, New York.</summary>
    private ChartResult Whitfield() => GoldenFixtures.CreateChartEngine(fixture).Calculate(new ChartInput
    {
        Name = "Daniel Whitfield",
        LocalDateTime = new DateTime(1964, 12, 23, 11, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 42.6526,
        Longitude = -73.7562,
        LocationName = "Albany, New York, United States",
        EventType = "Birth",
    });

    /// <summary>The direct Moon conjunct the Midheaven among the hits, or null.</summary>
    private static PrimaryDirectionHit? MoonToMidheaven(IEnumerable<PrimaryDirectionHit> hits) =>
        hits.SingleOrDefault(h =>
            h.Motion == PrimaryDirectionMotion.Direct
            && h.Significator == "Midheaven"
            && h.Aspect == PrimaryDirectionAspect.Conjunction
            && h.Promissor == "Moon");

    /// <summary>A service over the shared ephemeris.</summary>
    private PrimaryDirectionService Service() => new(fixture.EphemerisService);

    /// <summary>J2000 NYC.</summary>
    private ChartResult J2000Nyc() => GoldenFixtures.J2000Nyc(fixture);

    #endregion
}
