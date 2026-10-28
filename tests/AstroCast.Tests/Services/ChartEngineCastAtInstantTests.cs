using System.Globalization;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;
using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// A chart cast at an instant (branch R1): <see cref="ChartEngine.CalculateAtInstant"/> casts at the
/// Julian Day it is given, to the whole second, and the electional instant overload of
/// <see cref="ChartEngine.CalculateForElectional(ChartInput, DateTime, CancellationToken)"/> converts
/// its instant by arithmetic. Before either, an instant before 1582-10-15 was labelled on the proleptic
/// Gregorian calendar and the label read back as a Julian date, so the chart landed 7 to 10 days late,
/// or was refused when the label fell on 1582-10-05 to 1582-10-14.
/// </summary>
/// <remarks>
/// Expected values come from outside the code: Tier B (direct <c>swe_calc_ut</c>, <c>swe_julday</c> and
/// <c>swe_revjul</c> on the shipped files, <c>r1-instants/truth/expected.py</c>) and Meeus, chapter 7,
/// formula 7.1, worked in exact fractions (<c>r1-instants/truth/meeus_cases.py</c>).
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartEngineCastAtInstantTests
{
    /// <summary>One second, in days.</summary>
    private const double OneSecond = 1.0 / 86400.0;

    /// <summary>A Julian Day the app computes is within this of Meeus's exact value.</summary>
    private const double JdTolerance = 1e-8;

    /// <summary>
    /// The solar return of 1300 of a native born at Julian 1296-02-29 12:00 UT: Tier B, Julian
    /// 1300-02-29 11:12:20 UT, a leap day of the Julian calendar the Gregorian does not have.
    /// </summary>
    private const double LeapDayReturnJd = 2195941.966898709;

    /// <summary>The natal Sun of that native (Julian 1296-02-29 12:00 UT), Tier B.</summary>
    private const double LeapDayNativeSun = 347.92173868696085;

    private readonly EphemerisFixture _fixture;
    private readonly ChartEngine _engine;

    /// <summary>Receives the shared ephemeris fixture and builds a fully wired engine.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ChartEngineCastAtInstantTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _engine = GoldenFixtures.CreateChartEngine(fixture);
    }

    #region The arithmetic conversions

    /// <summary>
    /// A Julian Day becomes the instant it denotes, to the whole second, on the ticks
    /// <see cref="DateTime"/> counts (the proleptic Gregorian calendar), whatever calendar was in
    /// force: Tier B's <c>swe_revjul</c> with the Gregorian flag.
    /// </summary>
    /// <param name="julianDay">The Julian Day (UT).</param>
    /// <param name="expected">The instant, as <c>yyyy-MM-ddTHH:mm:ss</c>.</param>
    [Theory]
    [InlineData(2299160.5, "1582-10-15T00:00:00")]
    [InlineData(2299160.499988426, "1582-10-14T23:59:59")]
    [InlineData(LeapDayReturnJd, "1300-03-08T11:12:20")]
    [InlineData(2159357.704699074, "1200-01-08T04:54:46")]
    [InlineData(2451545.0, "2000-01-01T12:00:00")]
    [InlineData(2597641.4939814815, "2399-12-31T23:51:20")]
    public void JulianDayToInstant_Should_GiveTheInstantToTheWholeSecond(double julianDay, string expected)
    {
        DateTime instant = JulianDayUtility.JulianDayToInstant(julianDay);

        instant.Should().Be(DateTime.Parse(expected, CultureInfo.InvariantCulture));
        instant.Kind.Should().Be(DateTimeKind.Utc);
    }

    /// <summary>
    /// An instant's Julian Day is Meeus's for its date on the proleptic Gregorian calendar, the
    /// calendar its ticks count on, before the switch as after it.
    /// </summary>
    /// <param name="instant">The instant, as <c>yyyy-MM-ddTHH:mm:ss</c>.</param>
    /// <param name="meeusJd">Meeus's Julian Day (formula 7.1, Gregorian).</param>
    [Theory]
    [InlineData("1582-10-15T00:00:00", 2299160.5)]
    [InlineData("1582-10-14T23:59:59", 2299160.499988426)]
    [InlineData("1582-10-15T00:00:01", 2299160.500011574)]
    [InlineData("1300-03-08T11:12:20", 2195941.966898148)]
    [InlineData("1200-01-08T04:54:46", 2159357.704699074)]
    [InlineData("2000-01-01T12:00:00", 2451545.0)]
    [InlineData("0001-01-01T00:00:00", 1721425.5)]
    public void InstantToJulianDay_Should_GiveMeeussJulianDay(string instant, double meeusJd)
    {
        DateTime value = DateTime.SpecifyKind(DateTime.Parse(instant, CultureInfo.InvariantCulture), DateTimeKind.Utc);

        JulianDayUtility.InstantToJulianDay(value).Should().BeApproximately(meeusJd, JdTolerance);
    }

    #endregion

    #region CalculateAtInstant

    /// <summary>
    /// The leap-day native's return of 1300 casts at its instant, Julian 1300-02-29 11:12:20 UT:
    /// the chart's Julian Day is Tier B's to the second, its instant the proleptic Gregorian
    /// 1300-03-08 11:12:20 UT (D4), and its Sun is back on the natal Sun. Read back from its label
    /// the chart was cast 8 days late, with the Sun 28,541″ past it.
    /// </summary>
    [Fact]
    public void CalculateAtInstant_OnAJulianLeapDay_Should_CastAtTheInstant()
    {
        ChartInput input = EventInput(new DateTime(1300, 3, 8, 11, 12, 20, DateTimeKind.Utc));

        ChartResult chart = _engine.CalculateAtInstant(input, LeapDayReturnJd);

        chart.JulianDay.Should().BeApproximately(LeapDayReturnJd, OneSecond);
        chart.UtcDateTime.Should().Be(new DateTime(1300, 3, 8, 11, 12, 20, DateTimeKind.Utc));
        chart.UtcDateTime.Kind.Should().Be(DateTimeKind.Utc);
        chart.ResolvedOffsetLabel.Should().Be("UTC+0 (explicit)");
        ArcSeconds(Longitude(chart, CelestialBodyId.Sun), LeapDayNativeSun).Should().BeLessThan(1.0,
            "a solar return's Sun is on the natal Sun");
    }

    /// <summary>
    /// An instant a second either side of the switch, JD 2299160.5 = 1582-10-15 00:00 UT, casts at
    /// that instant. The second before is labelled 1582-10-14 on the proleptic Gregorian calendar,
    /// a date that never occurred, which read back as a reading was refused.
    /// </summary>
    /// <param name="seconds">Seconds from the switch.</param>
    /// <param name="instant">The instant, as <c>yyyy-MM-ddTHH:mm:ss</c> on the proleptic Gregorian calendar.</param>
    /// <param name="meeusJd">Meeus's Julian Day of the instant.</param>
    [Theory]
    [InlineData(-1, "1582-10-14T23:59:59", 2299160.499988426)]
    [InlineData(1, "1582-10-15T00:00:01", 2299160.500011574)]
    public void CalculateAtInstant_EitherSideOfTheSwitch_Should_CastAtThatInstant(int seconds, string instant, double meeusJd)
    {
        DateTime label = DateTime.SpecifyKind(DateTime.Parse(instant, CultureInfo.InvariantCulture), DateTimeKind.Utc);
        double julianDay = 2299160.5 + seconds * OneSecond;

        ChartResult chart = _engine.CalculateAtInstant(EventInput(label), julianDay);

        chart.JulianDay.Should().BeApproximately(meeusJd, JdTolerance);
        chart.UtcDateTime.Should().Be(label);
    }

    /// <summary>
    /// Guard: on or after 1582-10-15 the chart cast at an instant is exactly the chart
    /// <see cref="ChartEngine.Calculate"/> gives for the instant's label at offset zero, the way every
    /// event chart was cast before: the same Julian Day to the bit, instant, positions and houses.
    /// </summary>
    /// <param name="julianDay">A Julian Day after the switch.</param>
    [Theory]
    [InlineData(2299160.5)]
    [InlineData(2299524.564930)]
    [InlineData(2451545.0)]
    [InlineData(2461117.923456789)]
    [InlineData(2597641.4939814815)]
    public void CalculateAtInstant_AfterTheSwitch_Should_GiveTheChartCalculateGivesForItsLabel(double julianDay)
    {
        DateTime label = JulianDayUtility.ToDateTimeUtc(_fixture.EphemerisService, julianDay);
        ChartInput input = EventInput(label);

        ChartResult atInstant = _engine.CalculateAtInstant(input, julianDay);
        ChartResult fromLabel = _engine.Calculate(input);

        BitConverter.DoubleToInt64Bits(atInstant.JulianDay).Should().Be(BitConverter.DoubleToInt64Bits(fromLabel.JulianDay));
        atInstant.UtcDateTime.Should().Be(fromLabel.UtcDateTime);
        atInstant.Positions.Select(p => (p.Body, p.Longitude, p.Latitude, p.SpeedInLongitude, p.Declination, p.House))
            .Should().Equal(fromLabel.Positions.Select(p => (p.Body, p.Longitude, p.Latitude, p.SpeedInLongitude, p.Declination, p.House)));
        atInstant.Houses.Cusps.Should().Equal(fromLabel.Houses.Cusps);
        atInstant.Midpoints.Count.Should().Be(fromLabel.Midpoints.Count);
        atInstant.ResolvedOffsetLabel.Should().Be(fromLabel.ResolvedOffsetLabel);
        atInstant.ResolvedLocalDateTime.Should().Be(fromLabel.ResolvedLocalDateTime);
    }

    /// <summary>
    /// The span is the engine's, to the whole second, with the engine's refusal: the first and last
    /// seconds the files cover cast, and the seconds outside them are refused as
    /// <see cref="ChartEngine.Calculate"/> refuses them.
    /// </summary>
    /// <param name="julianDay">The Julian Day (UT).</param>
    /// <param name="refused">Whether it is refused.</param>
    [Theory]
    [InlineData(2159357.704699074, false)]
    [InlineData(2159357.704699074 - OneSecond, true)]
    [InlineData(2597641.4939814815, false)]
    [InlineData(2597641.4939814815 + OneSecond, true)]
    public void CalculateAtInstant_AtTheSpanEdges_Should_CastInsideAndRefuseOutside(double julianDay, bool refused)
    {
        ChartInput input = EventInput(JulianDayUtility.JulianDayToInstant(julianDay));

        Func<ChartResult> cast = () => _engine.CalculateAtInstant(input, julianDay);

        if (refused)
        {
            cast.Should().Throw<ArgumentOutOfRangeException>().WithMessage(
                "* UT is outside the years the ephemeris files cover, 1200 to 2399: a chart can be cast from " +
                "1200-01-01 04:54:46 UT (Julian calendar) to 2399-12-31 23:51:20 UT.*");
        }
        else
        {
            cast().JulianDay.Should().BeApproximately(julianDay, JdTolerance);
        }
    }

    /// <summary>
    /// A Julian Day no instant can hold, or not a number, is refused as outside the files, naming the
    /// Julian Day.
    /// </summary>
    /// <param name="julianDay">The Julian Day.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(0.0)]
    [InlineData(1e9)]
    public void CalculateAtInstant_AtAJulianDayNoInstantHolds_Should_BeRefused(double julianDay)
    {
        Func<ChartResult> cast = () => _engine.CalculateAtInstant(EventInput(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)), julianDay);

        cast.Should().Throw<ArgumentOutOfRangeException>().WithMessage(
            "Julian Day * is outside the years the ephemeris files cover, 1200 to 2399*");
    }

    /// <summary>
    /// A chart cast at a Julian Day is labelled at an explicit offset; one read on a zone's clock is
    /// refused (the electional instant overload casts those).
    /// </summary>
    [Fact]
    public void CalculateAtInstant_ThroughAZone_Should_BeRefused()
    {
        ChartInput input = new()
        {
            Name = "Zone", LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0), TimeZone = "Etc/UTC",
            Latitude = 51.4769, Longitude = 0.0, LocationName = "Greenwich", EventType = "Event",
        };

        Func<ChartResult> cast = () => _engine.CalculateAtInstant(input, 2451545.0);

        cast.Should().Throw<ArgumentException>().WithMessage("A chart cast at a Julian Day is labelled at an explicit offset*");
    }

    #endregion

    #region The electional instant

    /// <summary>
    /// Every electional candidate before the switch is cast at the instant its scan sampled: the
    /// chart's Julian Day is Meeus's for the candidate's wall clock (zone Etc/UTC, so its instant)
    /// on the proleptic Gregorian calendar the sample is labelled on. Read back through the date's
    /// calendar switch it was the Julian reading of that label, 8 days late in 1300.
    /// </summary>
    [Fact]
    public void Electional_CandidatesBeforeTheSwitch_Should_BeCastAtTheirSamples()
    {
        ChartResult natal = _engine.Calculate(EventInput(new DateTime(1300, 3, 15, 12, 0, 0), "Birth"));
        ElectionalResult result = new ElectionalService(_fixture.EphemerisService, _engine).Calculate(new ElectionalInput
        {
            NatalChart = natal,
            ElectionType = ElectionType.Contract,
            ElectionGoal = "Instant probe",
            RangeStart = new DateOnly(1300, 3, 10),
            RangeEnd = new DateOnly(1300, 3, 12),
            Latitude = 51.4769,
            Longitude = 0.0,
            LocationName = "Greenwich",
            TimeZone = "Etc/UTC",
            CandidateCount = 3,
        });

        result.Candidates.Should().NotBeEmpty();
        foreach (CandidateWindow candidate in result.Candidates)
        {
            DateTime t = candidate.LocalDateTime;
            double sampleJd = MeeusGregorian(t.Year, t.Month, t.Day) + (t.Hour * 3600 + t.Minute * 60 + t.Second) / 86400.0;
            candidate.Chart.JulianDay.Should().BeApproximately(sampleJd, OneSecond,
                "the candidate {0} is cast at the instant sampled", t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }
    }

    #endregion

    #region Helpers

    /// <summary>An event chart's input as the services build it: the instant's label at offset zero.</summary>
    /// <param name="label">The label.</param>
    /// <param name="eventType">The event type.</param>
    /// <returns>The input.</returns>
    private static ChartInput EventInput(DateTime label, string eventType = "Event") => new()
    {
        Name = "Instant probe",
        LocalDateTime = label,
        TimeZone = "Etc/UTC",
        UseExplicitOffset = true,
        UtcOffsetHours = 0.0,
        Latitude = 51.4769,
        Longitude = 0.0,
        LocationName = "Greenwich",
        EventType = eventType,
        HouseSystem = HouseSystem.Placidus,
    };

    /// <summary>A body's longitude in a chart.</summary>
    private static double Longitude(ChartResult chart, CelestialBodyId body) =>
        chart.Positions.First(p => p.Body == body).Longitude;

    /// <summary>The shortest arc between two longitudes, in arcseconds.</summary>
    private static double ArcSeconds(double a, double b)
    {
        double d = Math.Abs(((a - b) % 360.0 + 540.0) % 360.0 - 180.0);
        return d * 3600.0;
    }

    /// <summary>
    /// Meeus's Julian Day of 0h UT on a date of the proleptic Gregorian calendar (formula 7.1, with
    /// B = 2 − A + ⌊A/4⌋), independent of the code under test.
    /// </summary>
    private static double MeeusGregorian(int year, int month, int day)
    {
        if (month <= 2)
        {
            year -= 1;
            month += 12;
        }

        int a = year / 100;
        int b = 2 - a + a / 4;
        return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1)) + day + b - 1524.5;
    }

    #endregion
}
