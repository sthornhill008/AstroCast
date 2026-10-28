using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="FirdariaAnalyzer"/>: the day and night orders of the sources (T-D8: Abu Ma'shar by
/// night), the periods between the anniversaries of the birth day (R-D6), the sevenths of a planet's period
/// in Chaldean order from its lord, the undivided periods of the nodes, cycle wrapping, the refusal of a
/// reference before the birth day (T6-6) and the summaries. Every expected value is Tier A, calendar
/// arithmetic (h_truth.out for J2000 NYC).
/// </summary>
[Trait("Category", "Unit")]
public sealed class FirdariaAnalyzerTests
{
    #region Day/Night Sequence

    [Fact]
    public void Analyze_DayChart_StartsWithSun()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.CurrentMajorRuler.Should().Be("Sun");
        result.IsDayChart.Should().BeTrue();
    }

    [Fact]
    public void Analyze_NightChart_StartsWithMoon()
    {
        ChartResult chart = CreateChart(isDayChart: false);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.CurrentMajorRuler.Should().Be("Moon");
        result.IsDayChart.Should().BeFalse();
    }

    /// <summary>
    /// The day order of the sources: the planets in descending Chaldean order from the Sun, then the nodes
    /// (T-D8). Until 1.3.4 the order was Sun, Moon, North Node, Jupiter, Mercury, Saturn, South Node, Mars,
    /// Venus, no source's.
    /// </summary>
    [Fact]
    public void Analyze_DayChart_RunsSunVenusMercuryMoonSaturnJupiterMarsThenTheNodes()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1985, 1, 1));

        result.Periods.Select(p => (p.Ruler, p.Years)).Should().Equal(
            ("Sun", 10), ("Venus", 8), ("Mercury", 13), ("Moon", 9), ("Saturn", 11),
            ("Jupiter", 12), ("Mars", 7), ("North Node", 3), ("South Node", 2));
    }

    /// <summary>
    /// The night order of Abu Ma'shar: the planets in descending Chaldean order from the Moon, then the nodes
    /// (T-D8 option a). Until 1.3.4 the night order was Moon, Sun, then as by day.
    /// </summary>
    [Fact]
    public void Analyze_NightChart_RunsMoonSaturnJupiterMarsSunVenusMercuryThenTheNodes()
    {
        ChartResult chart = CreateChart(isDayChart: false);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1985, 1, 1));

        result.Periods.Select(p => (p.Ruler, p.Years)).Should().Equal(
            ("Moon", 9), ("Saturn", 11), ("Jupiter", 12), ("Mars", 7), ("Sun", 10),
            ("Venus", 8), ("Mercury", 13), ("North Node", 3), ("South Node", 2));
    }

    #endregion

    #region Major Period Identification

    [Fact]
    public void Analyze_Age0_FirstPeriodFirstSubPeriod()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.Age.Should().Be(0);
        result.CurrentMajorRuler.Should().Be("Sun");
        result.CurrentMajorYears.Should().Be(10);
    }

    [Fact]
    public void Analyze_DayChart_Age10_VenusPeriod()
    {
        // Day chart: Sun (10y) ends at 10, Venus (8y) runs 10 to 18.
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1990, 6, 1));
        result.CurrentMajorRuler.Should().Be("Venus");
        result.CurrentMajorYears.Should().Be(8);
    }

    [Fact]
    public void Analyze_DayChart_Age19_MercuryPeriod()
    {
        // Day: Sun (10) + Venus (8) = 18, so Mercury (13y) runs 18 to 31.
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1999, 6, 1));
        result.CurrentMajorRuler.Should().Be("Mercury");
        result.CurrentMajorYears.Should().Be(13);
    }

    [Fact]
    public void Analyze_DayChart_Age65_MarsPeriod()
    {
        // Day: Sun 10 + Venus 8 + Mercury 13 + Moon 9 + Saturn 11 + Jupiter 12 = 63, so Mars (7y) runs 63 to 70.
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(2045, 6, 1));
        result.CurrentMajorRuler.Should().Be("Mars");
    }

    /// <summary>A node's period is not divided: no sub-periods, no sub-ruler, the sub dates the major's.</summary>
    [Fact]
    public void Analyze_DayChart_Age71_NorthNodePeriod_IsUndivided()
    {
        // Day: Mars ends at 70, so the North Node (3y) runs 70 to 73.
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(2051, 6, 1));

        result.Age.Should().Be(71);
        result.CurrentMajorRuler.Should().Be("North Node");
        result.CurrentMajorYears.Should().Be(3);
        result.CurrentMajorStart.Should().Be(new DateTime(2050, 1, 1));
        result.CurrentMajorEnd.Should().Be(new DateTime(2053, 1, 1));
        result.Periods.Single(p => p.IsCurrent).SubPeriods.Should().BeEmpty();
        result.CurrentSubRuler.Should().BeEmpty();
        result.CurrentSubStart.Should().Be(result.CurrentMajorStart);
        result.CurrentSubEnd.Should().Be(result.CurrentMajorEnd);
        result.Summary.Should().Be(
            "Age 71: North Node major period (2050–2053), undivided. North Node themes of destiny, growth, " +
            "new direction, karmic development are the year’s background.");
    }

    #endregion

    #region Anniversaries

    /// <summary>
    /// A period runs from one anniversary of the birth day to another (R-D6): the Sun period of a day chart
    /// born 1980-01-01 ends on 1990-01-01, not 1989-12-31. Until 1.3.4 each period ran years × 365.25 days
    /// from local midnight, so a boundary fell up to a day from the birthday (T6-5).
    /// </summary>
    [Fact]
    public void Analyze_PeriodsRunBetweenTheAnniversariesOfTheBirthDay()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));

        result.Periods.Select(p => (p.Ruler, p.StartDate, p.EndDate)).Take(3).Should().Equal(
            ("Sun", new DateTime(1980, 1, 1), new DateTime(1990, 1, 1)),
            ("Venus", new DateTime(1990, 1, 1), new DateTime(1998, 1, 1)),
            ("Mercury", new DateTime(1998, 1, 1), new DateTime(2011, 1, 1)));
        result.Periods[^1].EndDate.Should().Be(new DateTime(2055, 1, 1));
    }

    /// <summary>The anniversary of a 29 February birth falls on 28 February in a common year (.NET's rule).</summary>
    [Fact]
    public void Analyze_LeapDayBirth_AnniversaryFallsOn28FebruaryInACommonYear()
    {
        ChartResult chart = CreateChart(isDayChart: true, birthDate: new DateTime(2000, 2, 29));
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(2010, 2, 28));

        result.Age.Should().Be(10, "the tenth anniversary of 2000-02-29 is 2010-02-28");
        result.CurrentMajorRuler.Should().Be("Venus");
        result.Periods.Select(p => (p.Ruler, p.StartDate, p.EndDate)).Take(4).Should().Equal(
            ("Sun", new DateTime(2000, 2, 29), new DateTime(2010, 2, 28)),
            ("Venus", new DateTime(2010, 2, 28), new DateTime(2018, 2, 28)),
            ("Mercury", new DateTime(2018, 2, 28), new DateTime(2031, 2, 28)),
            ("Moon", new DateTime(2031, 2, 28), new DateTime(2040, 2, 29)));
    }

    /// <summary>A reference date before the birth day is refused (T6-6).</summary>
    [Fact]
    public void Analyze_ReferenceBeforeTheBirthDay_IsRefused()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        Action act = () => FirdariaAnalyzer.Analyze(chart, new DateTime(1979, 12, 31));

        ArgumentOutOfRangeException refusal = act.Should().Throw<ArgumentOutOfRangeException>().Which;
        refusal.ParamName.Should().Be("referenceDate");
        refusal.Message.Should().StartWith("The reference date 1979-12-31 is before the birth day 1980-01-01.");
    }

    #endregion

    #region Cycle Wrapping

    [Fact]
    public void Analyze_Age76_CycleRestarts()
    {
        // At age 76, the 75-year cycle has restarted: day chart → Sun again
        ChartResult chart = CreateChart(isDayChart: true);
        // Birth 1980-01-01. First cycle ends at age 75. At age 76 we're in cycle 2.
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(2056, 6, 1));
        result.CurrentMajorRuler.Should().Be("Sun");
        result.Periods.Should().HaveCount(18, "the timeline holds (age ÷ 75) + 1 cycles of the nine");
    }

    #endregion

    #region Sub-Periods

    [Fact]
    public void Analyze_SubPeriodCount_SevenForAPlanet_NoneForANode()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));

        result.Periods.First(p => p.Ruler == "Sun").SubPeriods.Should().HaveCount(7);
        result.Periods.First(p => p.Ruler == "North Node").SubPeriods.Should().BeEmpty();
        result.Periods.First(p => p.Ruler == "South Node").SubPeriods.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_SubPeriodSequence_StartsWithMajorRuler()
    {
        // Day chart, age 0 → Sun period. Sub-periods start with Sun in Chaldean order.
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));

        FirdariaPeriod sunPeriod = result.Periods.First(p => p.Ruler == "Sun");
        sunPeriod.SubPeriods[0].Ruler.Should().Be("Sun");
        // Chaldean from Sun: Sun, Venus, Mercury, Moon, Saturn, Jupiter, Mars
        sunPeriod.SubPeriods[1].Ruler.Should().Be("Venus");
        sunPeriod.SubPeriods[2].Ruler.Should().Be("Mercury");
        sunPeriod.SubPeriods[3].Ruler.Should().Be("Moon");
        sunPeriod.SubPeriods[4].Ruler.Should().Be("Saturn");
        sunPeriod.SubPeriods[5].Ruler.Should().Be("Jupiter");
        sunPeriod.SubPeriods[6].Ruler.Should().Be("Mars");
    }

    [Fact]
    public void Analyze_SaturnPeriod_SubPeriodsStartWithSaturn()
    {
        // Chaldean order: Saturn, Jupiter, Mars, Sun, Venus, Mercury, Moon.
        // Day: Sun 10 + Venus 8 + Mercury 13 + Moon 9 = 40, so Saturn (11y) runs 40 to 51; age 47 is inside it.
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(2027, 6, 1));
        result.CurrentMajorRuler.Should().Be("Saturn");

        FirdariaPeriod saturnPeriod = result.Periods.First(p => p.Ruler == "Saturn" && p.IsCurrent);
        saturnPeriod.SubPeriods[0].Ruler.Should().Be("Saturn");
        saturnPeriod.SubPeriods[1].Ruler.Should().Be("Jupiter");
        saturnPeriod.SubPeriods[2].Ruler.Should().Be("Mars");
    }

    /// <summary>
    /// A planet's sub-periods are sevenths of its span in days, measured from the period's start, the seventh
    /// ending on the period's end: the Sun period 1980-01-01 to 1990-01-01 is 3,653 days.
    /// </summary>
    [Fact]
    public void Analyze_PlanetSubPeriods_AreSeventhsOfTheSpanInDays()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));

        FirdariaPeriod sunPeriod = result.Periods.First(p => p.Ruler == "Sun");
        DateTime start = new(1980, 1, 1);
        (sunPeriod.EndDate - sunPeriod.StartDate).TotalDays.Should().Be(3653.0);
        for (int i = 0; i < 6; i++)
        {
            sunPeriod.SubPeriods[i].EndDate.Should().Be(start.AddDays(3653.0 * (i + 1) / 7));
        }

        sunPeriod.SubPeriods[6].EndDate.Should().Be(sunPeriod.EndDate);
        for (int i = 1; i < 7; i++)
        {
            sunPeriod.SubPeriods[i].StartDate.Should().Be(sunPeriod.SubPeriods[i - 1].EndDate);
        }
    }

    [Fact]
    public void Analyze_CurrentSubPeriodIdentified()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.CurrentSubRuler.Should().NotBeEmpty();
        result.CurrentSubStart.Should().BeBefore(result.CurrentSubEnd);
    }

    #endregion

    #region Timeline Completeness

    [Fact]
    public void Analyze_DayChart_FirstNinePeriodYears_Sum75()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1985, 1, 1));

        result.Periods.Should().HaveCountGreaterThanOrEqualTo(9);
        // Sun 10 + Venus 8 + Mercury 13 + Moon 9 + Saturn 11 + Jupiter 12 + Mars 7 + North Node 3 + South Node 2 = 75
        int totalYears = result.Periods.Take(9).Sum(p => p.Years);
        totalYears.Should().Be(75);
    }

    [Fact]
    public void Analyze_NightChart_FirstNinePeriodYears_Sum75()
    {
        ChartResult chart = CreateChart(isDayChart: false);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1985, 1, 1));

        result.Periods.Should().HaveCountGreaterThanOrEqualTo(9);
        int totalYears = result.Periods.Take(9).Sum(p => p.Years);
        totalYears.Should().Be(75);
    }

    #endregion

    #region Summary

    [Fact]
    public void Analyze_Summary_ContainsMajorAndSubRulerNames()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(1985, 6, 1));
        result.Summary.Should().Contain("Sun"); // Major ruler for day chart at age 5
        result.Summary.Should().Contain("sub-period");
    }

    [Fact]
    public void Analyze_Summary_ContainsAge()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(2005, 6, 1));
        result.Summary.Should().Contain("Age 25");
    }

    #endregion

    #region Dates and Fields

    [Fact]
    public void Analyze_ReturnsCorrectBirthDateAndReferenceDate()
    {
        DateTime birth = new DateTime(1980, 1, 1);
        DateTime reference = new DateTime(2026, 3, 30);
        ChartResult chart = CreateChart(isDayChart: true, birthDate: birth);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, reference);
        result.BirthDate.Should().Be(birth);
        result.ReferenceDate.Should().Be(reference);
    }

    [Fact]
    public void Analyze_MajorPeriodDatesAreContiguous()
    {
        ChartResult chart = CreateChart(isDayChart: true);
        FirdariaResult result = FirdariaAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));

        for (int i = 1; i < result.Periods.Count; i++)
        {
            result.Periods[i].StartDate.Should().Be(result.Periods[i - 1].EndDate);
        }
    }

    [Fact]
    public void Analyze_NullChart_ThrowsArgumentNullException()
    {
        Action act = () => FirdariaAnalyzer.Analyze(null!, DateTime.Today);
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Real charts

    /// <summary>
    /// The firdaria of J2000 NYC, a night chart born 2000-01-01, at 2026-10-04 (Tier A, h_truth.out).
    /// </summary>
    [Collection("Ephemeris")]
    [Trait("Category", "Integration")]
    public sealed class IntegrationTests
    {
        private static readonly DateTime Reference = new(2026, 10, 4);

        private readonly EphemerisFixture _fixture;
        private readonly ChartResult _j2000;

        /// <summary>Casts J2000 NYC.</summary>
        /// <param name="fixture">The shared ephemeris fixture.</param>
        public IntegrationTests(EphemerisFixture fixture)
        {
            _fixture = fixture;
            _j2000 = GoldenFixtures.J2000Nyc(fixture);
        }

        /// <summary>
        /// Abu Ma'shar's night order between the anniversaries of 1 January: nine periods, no more, and the
        /// current one Jupiter, 2020 to 2032 (T-D8, R-D6). Until 1.3.4 the chart read Moon, Sun, North Node,
        /// Jupiter, ... from 2000-01-01 in 365.25-day years (Jupiter 2021-12-31 to 2033-12-31).
        /// </summary>
        [Fact]
        public void Analyze_J2000Nyc_NinePeriodsBetweenTheAnniversaries()
        {
            FirdariaResult result = FirdariaAnalyzer.Analyze(_j2000, Reference);

            result.IsDayChart.Should().BeFalse();
            result.Age.Should().Be(26);
            result.Periods.Select(p => (p.Ruler, p.Years, p.StartDate, p.EndDate)).Should().Equal(
                ("Moon", 9, new DateTime(2000, 1, 1), new DateTime(2009, 1, 1)),
                ("Saturn", 11, new DateTime(2009, 1, 1), new DateTime(2020, 1, 1)),
                ("Jupiter", 12, new DateTime(2020, 1, 1), new DateTime(2032, 1, 1)),
                ("Mars", 7, new DateTime(2032, 1, 1), new DateTime(2039, 1, 1)),
                ("Sun", 10, new DateTime(2039, 1, 1), new DateTime(2049, 1, 1)),
                ("Venus", 8, new DateTime(2049, 1, 1), new DateTime(2057, 1, 1)),
                ("Mercury", 13, new DateTime(2057, 1, 1), new DateTime(2070, 1, 1)),
                ("North Node", 3, new DateTime(2070, 1, 1), new DateTime(2073, 1, 1)),
                ("South Node", 2, new DateTime(2073, 1, 1), new DateTime(2075, 1, 1)));
            result.Periods.Single(p => p.IsCurrent).Ruler.Should().Be("Jupiter");
            result.CurrentMajorRuler.Should().Be("Jupiter");
            result.CurrentMajorStart.Should().Be(new DateTime(2020, 1, 1));
            result.CurrentMajorEnd.Should().Be(new DateTime(2032, 1, 1));
            result.CurrentMajorYears.Should().Be(12);
        }

        /// <summary>
        /// The Jupiter period's seven sub-periods are sevenths of its 4,383 days in Chaldean order from Jupiter,
        /// and Venus holds 2026-10-04.
        /// </summary>
        [Fact]
        public void Analyze_J2000Nyc_JupiterSubPeriodsAreSevenths_VenusCurrent()
        {
            FirdariaResult result = FirdariaAnalyzer.Analyze(_j2000, Reference);

            FirdariaPeriod jupiter = result.Periods.Single(p => p.IsCurrent);
            jupiter.SubPeriods.Select(s => (s.Ruler, s.StartDate.Date, s.EndDate.Date)).Should().Equal(
                ("Jupiter", new DateTime(2020, 1, 1), new DateTime(2021, 9, 18)),
                ("Mars", new DateTime(2021, 9, 18), new DateTime(2023, 6, 6)),
                ("Sun", new DateTime(2023, 6, 6), new DateTime(2025, 2, 21)),
                ("Venus", new DateTime(2025, 2, 21), new DateTime(2026, 11, 9)),
                ("Mercury", new DateTime(2026, 11, 9), new DateTime(2028, 7, 27)),
                ("Moon", new DateTime(2028, 7, 27), new DateTime(2030, 4, 14)),
                ("Saturn", new DateTime(2030, 4, 14), new DateTime(2032, 1, 1)));
            jupiter.SubPeriods.Single(s => s.IsCurrent).Ruler.Should().Be("Venus");
            result.CurrentSubRuler.Should().Be("Venus");
            result.CurrentSubStart.Date.Should().Be(new DateTime(2025, 2, 21));
            result.CurrentSubEnd.Date.Should().Be(new DateTime(2026, 11, 9));
            result.Summary.Should().StartWith(
                "Age 26: Jupiter major period (2020–2032), Venus sub-period (2025-02-21–2026-11-09).");
        }

        /// <summary>
        /// A reference instant (<see cref="DateTimeKind.Utc"/>) is read on the natal chart's clock before its day
        /// is taken (branch H, commit 4; T6-5, T6-6): a chart cast at 2000-01-01 01:00 Asia/Tokyo, whose birth
        /// instant is 1999-12-31 16:00 UT, analysed at that instant is age 0 on its birth day, 2000-01-01; the
        /// same clock of any other kind is a date as given, 1999-12-31, before the birth day, and is refused.
        /// Until commit 4 the instant's UT date was taken, so the chart's own birth-year solar return was
        /// refused.
        /// </summary>
        [Fact]
        public void Analyze_Tokyo_BirthInstantAsUtc_IsAge0_AndTheSameClockAsADate_IsRefused()
        {
            ChartResult tokyo = GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
            {
                Name = "Tokyo",
                LocalDateTime = new DateTime(2000, 1, 1, 1, 0, 0),
                TimeZone = "Asia/Tokyo",
                Latitude = 35.6762,
                Longitude = 139.6503,
                LocationName = "Tokyo",
                EventType = "Birth",
                HouseSystem = HouseSystem.Placidus,
            });
            tokyo.UtcDateTime.Should().Be(new DateTime(1999, 12, 31, 16, 0, 0, DateTimeKind.Utc));

            FirdariaResult atBirth = FirdariaAnalyzer.Analyze(tokyo, new DateTime(1999, 12, 31, 16, 0, 0, DateTimeKind.Utc));
            atBirth.Age.Should().Be(0);
            atBirth.ReferenceDate.Should().Be(new DateTime(2000, 1, 1), "the as-of day is the day the age was taken on");
            atBirth.CurrentMajorRuler.Should().Be(atBirth.Periods[0].Ruler);

            Action asGiven = () => FirdariaAnalyzer.Analyze(tokyo, new DateTime(1999, 12, 31, 16, 0, 0, DateTimeKind.Unspecified));
            ArgumentOutOfRangeException refusal = asGiven.Should().Throw<ArgumentOutOfRangeException>().Which;
            refusal.ParamName.Should().Be("referenceDate");
            refusal.Message.Should().StartWith("The reference date 1999-12-31 is before the birth day 2000-01-01.");
        }

        /// <summary>
        /// J2000 NYC at 2020-01-01 03:00 UT, 2019-12-31 22:00 EST: the Saturn period, age 19, the as-of day
        /// 2019-12-31; the same clock as a date, 2020-01-01: Jupiter's, age 20.
        /// </summary>
        [Fact]
        public void Analyze_J2000Nyc_ReferenceInstantReadInNewYork_SaturnOn20191231_JupiterOn20200101()
        {
            FirdariaResult asInstant = FirdariaAnalyzer.Analyze(_j2000, new DateTime(2020, 1, 1, 3, 0, 0, DateTimeKind.Utc));
            asInstant.Age.Should().Be(19);
            asInstant.CurrentMajorRuler.Should().Be("Saturn");
            asInstant.ReferenceDate.Should().Be(new DateTime(2019, 12, 31));

            FirdariaResult asDate = FirdariaAnalyzer.Analyze(_j2000, new DateTime(2020, 1, 1, 3, 0, 0, DateTimeKind.Unspecified));
            asDate.Age.Should().Be(20);
            asDate.CurrentMajorRuler.Should().Be("Jupiter");
            asDate.ReferenceDate.Should().Be(new DateTime(2020, 1, 1));
        }
    }

    #endregion

    #region Helpers

    private static ChartResult CreateChart(
        bool isDayChart = true,
        DateTime? birthDate = null)
    {
        DateTime birth = birthDate ?? new DateTime(1980, 1, 1);

        return new ChartResult
        {
            Input = new ChartInput
            {
                Name = "Test Person",
                LocalDateTime = birth,
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York",
                EventType = "Birth",
            },
            JulianDay = 2444239.5,
            Obliquity = 23.4367,
            UtcDateTime = birth.ToUniversalTime(),
            Positions = CreateDefaultPositions(),
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12)
                    .Select(i => (15.0 + i * 30.0) % 360.0).ToList(),
                Ascendant = 15.0,
                Midheaven = 285.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = CreateChartContext(isDayChart),
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    private static IReadOnlyList<PlanetPosition> CreateDefaultPositions() =>
    [
        CreatePosition(CelestialBodyId.Sun, 280.0, 10),
        CreatePosition(CelestialBodyId.Moon, 100.0, 4),
        CreatePosition(CelestialBodyId.Mercury, 290.0, 10),
        CreatePosition(CelestialBodyId.Venus, 165.0, 7),
        CreatePosition(CelestialBodyId.Mars, 45.0, 2),
        CreatePosition(CelestialBodyId.Jupiter, 60.0, 3),
        CreatePosition(CelestialBodyId.Saturn, 330.0, 12),
    ];

    private static PlanetPosition CreatePosition(
        CelestialBodyId body, double longitude, int house, double speed = 1.0)
    {
        return new PlanetPosition
        {
            Body = body,
            Name = body.ToString(),
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = speed,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
            House = house,
        };
    }

    private static ChartContextInfo CreateChartContext(bool isDayChart)
    {
        return new ChartContextInfo
        {
            ChartRuler = "Mars",
            AscendantSign = ZodiacSign.Aries,
            ChartRulerDignity = new DignityInfo
            {
                PlanetName = "Mars",
                Sign = ZodiacSign.Aries,
                IsDomicile = true,
                IsExalted = false,
                IsInDetriment = false,
                IsInFall = false,
                Dispositor = "Mars",
            },
            ChartRulerHouse = 1,
            IsChartRulerRetrograde = false,
            IsDayChart = isDayChart,
            EventSignificator = null,
            EventSignificatorDignity = null,
            KeyHouses = [],
            AngularPlanets = [],
            MutualReceptions = [],
        };
    }

    #endregion
}
