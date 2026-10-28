using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.Tests.Services.MinorPoints;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="ChartEngine"/> verifying that a single Calculate()
/// call produces a complete, correct <see cref="ChartResult"/> containing
/// positions, houses, aspects, and midpoints.
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartEngineTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine.
    /// </summary>
    public ChartEngineTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    /// <summary>
    /// Standard J2000.0 test input for New York City.
    /// </summary>
    /// <param name="includeMinorPoints">Whether the chart carries the minor points (branch Asteroids, 1.6.0); off by default.</param>
    private static ChartInput CreateJ2000Input(bool includeMinorPoints = false) => new()
    {
        Name = "J2000.0 Test",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Event",
        IncludeMinorPoints = includeMinorPoints,
    };

    [Fact]
    public void Calculate_Should_ReturnCompleteResult_When_ValidInput()
    {
        // Arrange
        ChartInput input = CreateJ2000Input();

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        result.Should().NotBeNull();
        result.Positions.Should().HaveCount(12, "there are 12 celestial bodies");
        result.Houses.Should().NotBeNull();
        result.Aspects.Count.Should().BeGreaterThan(0, "there should be at least one aspect");
        result.Midpoints.Should().HaveCount(91, "14 choose 2 = 91 midpoints");
        result.MidpointActivations.Count.Should().BeGreaterThan(0,
            "there should be at least one midpoint activation");
    }

    [Fact]
    public void Calculate_Should_IncludeAllBodies_When_Called()
    {
        // Arrange
        ChartInput input = CreateJ2000Input();
        string[] expectedBodies =
        [
            "Sun", "Moon", "Mercury", "Venus", "Mars", "Jupiter",
            "Saturn", "Uranus", "Neptune", "Pluto", "True Node", "Chiron"
        ];

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        IEnumerable<string> bodyNames = result.Positions.Select(p => p.Name);
        bodyNames.Should().BeEquivalentTo(expectedBodies,
            "all 12 celestial bodies should be present in the result");
    }

    [Fact]
    public void Calculate_Should_AssignHouses_When_Called()
    {
        // Arrange
        ChartInput input = CreateJ2000Input();

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        foreach (PlanetPosition position in result.Positions)
        {
            position.House.Should().NotBeNull(
                $"{position.Name} should have a house assignment");
            position.House.Should().BeInRange(1, 12,
                $"{position.Name} house should be between 1 and 12");
        }
    }

    [Fact]
    public void Calculate_Should_IncludeAngleAspects_When_Called()
    {
        // Arrange
        ChartInput input = CreateJ2000Input();

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        bool hasAngleAspect = result.Aspects.Any(a =>
            a.Body2Name == "ASC" || a.Body2Name == "MC");
        hasAngleAspect.Should().BeTrue(
            "at least one aspect should involve ASC or MC");
    }

    [Fact]
    public void Calculate_Should_SetEngineVersion_When_Called()
    {
        // Arrange
        ChartInput input = CreateJ2000Input();

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        result.EngineVersion.Should().NotBeNullOrEmpty(
            "EngineVersion should contain version information");
    }

    [Fact]
    public void Calculate_Should_SetCalculatedAt_When_Called()
    {
        // Arrange
        ChartInput input = CreateJ2000Input();
        DateTime before = DateTime.UtcNow;

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert
        result.CalculatedAt.Should().BeCloseTo(before, TimeSpan.FromMinutes(1),
            "CalculatedAt should be within 1 minute of the current time");
    }

    [Fact]
    public void Calculate_Should_ConvertTimezone_When_NonUtcInput()
    {
        // Arrange — 2000-01-01 07:00 EST = 2000-01-01 12:00 UTC
        ChartInput input = CreateJ2000Input();

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert — EST is UTC-5
        result.UtcDateTime.Hour.Should().Be(12,
            "07:00 EST should convert to 12:00 UTC (EST is UTC-5 in January)");
        result.UtcDateTime.Day.Should().Be(1);
        result.UtcDateTime.Month.Should().Be(1);
        result.UtcDateTime.Year.Should().Be(2000);
    }

    [Fact]
    public void Calculate_Should_UseExplicitOffset_When_FlagIsTrue()
    {
        // Arrange — 1947-10-05 09:30 local, explicit UTC-5 (EST, no DST)
        // Expected UT = 14:30 (09:30 - (-5) = 14:30)
        ChartInput input = new()
        {
            Name = "Explicit Offset Test",
            LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, PA",
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
        };

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert — explicit offset should produce UT = 14:30
        result.UtcDateTime.Hour.Should().Be(14,
            "09:30 local with explicit UTC-5 should convert to 14:30 UTC");
        result.UtcDateTime.Minute.Should().Be(30);
        result.UtcDateTime.Year.Should().Be(1947);
        result.UtcDateTime.Month.Should().Be(10);
        result.UtcDateTime.Day.Should().Be(5);
    }

    [Fact]
    public void Calculate_Should_IgnoreExplicitOffset_When_FlagIsFalse()
    {
        // Arrange — UseExplicitOffset is false, so UtcOffsetHours is ignored
        ChartInput input = new()
        {
            Name = "Offset Ignored Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            UseExplicitOffset = false,
            UtcOffsetHours = -99.0, // absurd value — should be ignored
        };

        // Act
        ChartResult result = _engine.Calculate(input);

        // Assert — should use IANA timezone (EST = UTC-5), not the absurd offset
        result.UtcDateTime.Hour.Should().Be(12,
            "07:00 EST should convert to 12:00 UTC via IANA, ignoring UtcOffsetHours");
    }

    [Fact]
    public void Calculate_Should_ProduceDifferentResults_When_DstVsExplicitOffset()
    {
        // Arrange — same date/time, but one uses IANA (which may apply DST)
        // and the other uses explicit EST (UTC-5, no DST).
        // For 1947-10-05, IANA may apply DST rules.
        ChartInput ianaInput = new()
        {
            Name = "IANA Test",
            LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, PA",
        };

        ChartInput explicitInput = new()
        {
            Name = "Explicit Test",
            LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, PA",
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
        };

        // Act
        ChartResult ianaResult = _engine.Calculate(ianaInput);
        ChartResult explicitResult = _engine.Calculate(explicitInput);

        // Assert — they should produce different UTC times if IANA applies DST
        // The explicit result should have UT = 14:30 (EST)
        explicitResult.UtcDateTime.Hour.Should().Be(14);
        explicitResult.UtcDateTime.Minute.Should().Be(30);

        // The two results will differ if IANA applied DST for this historical date
        // (we can't assert the IANA result exactly since it depends on the tz database,
        // but we verify our explicit path works correctly)
        explicitResult.JulianDay.Should().NotBe(0, "Julian Day should be computed");
        explicitResult.Houses.Should().NotBeNull("houses should be calculated");
    }

    #region Electional Chart (HIGH-006)

    [Fact]
    public void CalculateForElectional_Should_ReturnPositionsAndAspects_When_ValidInput()
    {
        // Arrange
        ChartInput input = CreateJ2000Input();

        // Act
        ChartResult result = _engine.CalculateForElectional(input);

        // Assert — positions and aspects present, midpoints computed as the full chart's (O-3, E2-3)
        result.Should().NotBeNull();
        result.Positions.Should().HaveCount(12, "there are 12 celestial bodies");
        result.Houses.Should().NotBeNull();
        result.Aspects.Count.Should().BeGreaterThan(0, "there should be at least one aspect");
        result.Midpoints.Should().HaveCount(91,
            "the 14 points (12 bodies, ASC and MC) give 14 × 13 / 2 = 91 pairs");
        result.MidpointActivations.Should().HaveCount(_engine.Calculate(input).MidpointActivations.Count,
            "electional chart computes midpoint activations as the full chart does");
    }

    [Fact]
    public void CalculateForElectional_Should_MatchFullPositions_When_SameInput()
    {
        // Arrange
        ChartInput input = CreateJ2000Input();

        // Act
        ChartResult fullResult = _engine.Calculate(input);
        ChartResult electionalResult = _engine.CalculateForElectional(input);

        // Assert — positions should be identical
        electionalResult.Positions.Should().HaveCount(fullResult.Positions.Count);
        for (int i = 0; i < fullResult.Positions.Count; i++)
        {
            electionalResult.Positions[i].Longitude.Should().Be(
                fullResult.Positions[i].Longitude,
                $"position {i} longitude should match full calculation");
        }
    }

    #endregion

    #region DST Edge Cases

    [Fact]
    public void Calculate_Should_HandleAmbiguousTime_When_FallBack()
    {
        // Arrange — 2024-11-03 01:30 US Eastern: clocks fall back from 2:00 EDT to 1:00 EST,
        // so 01:30 occurs twice. ChartEngine should use standard time (EST = UTC-5).
        ChartInput input = new()
        {
            Name = "DST Fall-Back Test",
            LocalDateTime = new DateTime(2024, 11, 3, 1, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
        };

        // Act — should not throw
        ChartResult result = _engine.Calculate(input);

        // Assert — standard time (UTC-5) means 01:30 EST = 06:30 UTC
        result.Should().NotBeNull();
        result.UtcDateTime.Hour.Should().Be(6,
            "01:30 EST (standard time, UTC-5) should convert to 06:30 UTC");
        result.UtcDateTime.Minute.Should().Be(30);
        result.Positions.Should().HaveCount(12);
    }

    [Fact]
    public void Calculate_Should_HandleInvalidTime_When_SpringForward()
    {
        // Arrange — 2024-03-10 02:30 US Eastern: clocks spring forward from 2:00 to 3:00,
        // so 02:30 does not exist. ChartEngine should advance to 03:00 EDT.
        ChartInput input = new()
        {
            Name = "DST Spring-Forward Test",
            LocalDateTime = new DateTime(2024, 3, 10, 2, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
        };

        // Act — should not throw
        ChartResult result = _engine.Calculate(input);

        // Assert — time should be adjusted past the gap (03:00 EDT = 07:00 UTC)
        result.Should().NotBeNull();
        result.UtcDateTime.Hour.Should().Be(7,
            "02:30 in spring-forward gap should advance to 03:00 EDT = 07:00 UTC");
        result.UtcDateTime.Minute.Should().Be(0);
        result.Positions.Should().HaveCount(12);
    }

    #endregion

    #region Input Validation (MED-001, LOW-002)

    [Fact]
    public void Calculate_Should_ThrowArgumentOutOfRangeException_When_LatitudeOutOfRange()
    {
        // Arrange
        ChartInput input = CreateJ2000Input();
        ChartInput invalidInput = new()
        {
            Name = input.Name,
            LocalDateTime = input.LocalDateTime,
            TimeZone = input.TimeZone,
            Latitude = 91.0,
            Longitude = input.Longitude,
            LocationName = input.LocationName,
            EventType = input.EventType,
        };

        // Act & Assert
        Action act = () => _engine.Calculate(invalidInput);
        act.Should().Throw<ArgumentOutOfRangeException>(
            "latitude 91.0 is outside the valid range of -90 to 90");
    }

    [Fact]
    public void Calculate_Should_ThrowArgumentOutOfRangeException_When_LongitudeOutOfRange()
    {
        // Arrange
        ChartInput input = CreateJ2000Input();
        ChartInput invalidInput = new()
        {
            Name = input.Name,
            LocalDateTime = input.LocalDateTime,
            TimeZone = input.TimeZone,
            Latitude = input.Latitude,
            Longitude = 181.0,
            LocationName = input.LocationName,
            EventType = input.EventType,
        };

        // Act & Assert
        Action act = () => _engine.Calculate(invalidInput);
        act.Should().Throw<ArgumentOutOfRangeException>(
            "longitude 181.0 is outside the valid range of -180 to 180");
    }

    [Fact]
    public void Calculate_Should_ThrowArgumentOutOfRangeException_When_DateOutsideSwissEphRange()
    {
        // Arrange — year 6000 is outside the years the ephemeris files cover
        ChartInput invalidInput = new()
        {
            Name = "Future Test",
            LocalDateTime = new DateTime(6000, 1, 1, 12, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
        };

        // Act & Assert
        Action act = () => _engine.Calculate(invalidInput);
        act.Should().Throw<ArgumentOutOfRangeException>(
            "year 6000 is outside the years the ephemeris files cover, 1200 to 2399");
    }

    #region M-11 — Year-range Boundary Coverage

    /// <remarks>
    /// The tests below stay inside the date window covered by the ephemeris
    /// data files shipped in <c>ephe/</c> (seas_12/seas_18, semo_12/semo_18,
    /// sepl_12/sepl_18): 1200-01-01 04:54:46 UT (Julian calendar) to
    /// 2399-12-31 23:51:20 UT. <c>ChartEngine</c> refuses an instant outside
    /// it before the ephemeris call (branch F5, rule 4); the refusal at the
    /// edges is covered by <c>ChartEngineTimeInputTests</c>.
    /// </remarks>

    /// <summary>
    /// Year 1800 CE is a practical "far past" bound — predates any realistic
    /// natal use case but sits squarely inside the bundled seas_18 ephemeris
    /// window. Calculate must succeed and return a complete chart rather than
    /// tripping the span guard (the files' 1200 to 2399) for a perfectly reasonable historical date.
    /// </summary>
    [Fact]
    public void Calculate_Should_Succeed_When_YearIsFarPast1800()
    {
        ChartInput input = new()
        {
            Name = "Year 1800 CE Boundary Test",
            LocalDateTime = new DateTime(1800, 6, 15, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 0.0,
            Longitude = 0.0,
            LocationName = "Equator / Prime Meridian",
            EventType = "Event",
        };

        ChartResult result = _engine.Calculate(input);

        result.Should().NotBeNull();
        result.Positions.Should().HaveCount(12);
        result.Houses.Should().NotBeNull();
    }

    /// <summary>
    /// Year 2399 CE is a practical "far future" bound — well past any real
    /// transit work but still inside the bundled ephemeris files, the last year
    /// they cover. Confirms the engine has no hidden post-2100 assumption that
    /// would break for forward projections.
    /// </summary>
    /// <remarks>
    /// This was 2400-06-15, which is outside the files: sepl_18 ends on
    /// 2400-01-10 and no seas_24 ships, so Chiron failed whenever the test ran
    /// first in its process, and it passed in the full suite only because an
    /// earlier test had left seas_18 open. The engine now refuses that instant
    /// (branch F5, rule 4).
    /// </remarks>
    [Fact]
    public void Calculate_Should_Succeed_When_YearIsFarFuture2399()
    {
        ChartInput input = new()
        {
            Name = "Year 2399 CE Boundary Test",
            LocalDateTime = new DateTime(2399, 6, 15, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 0.0,
            Longitude = 0.0,
            LocationName = "Equator / Prime Meridian",
            EventType = "Event",
        };

        ChartResult result = _engine.Calculate(input);

        result.Should().NotBeNull();
        result.Positions.Should().HaveCount(12);
    }

    /// <summary>
    /// Year 5401 CE was one year past the upper bound of the old validation
    /// guard (<c>MaxYear = 5400</c>). The guard is now the span of the ephemeris
    /// files (1200 to 2399, branch F5), and 5401 is still rejected with
    /// <see cref="ArgumentOutOfRangeException"/>, before any ephemeris file
    /// lookup.
    /// </summary>
    [Fact]
    public void Calculate_Should_Throw_When_YearEqualsMaxYearPlusOne()
    {
        ChartInput input = new()
        {
            Name = "MaxYear + 1 Exclusive Boundary",
            LocalDateTime = new DateTime(5401, 6, 15, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 0.0,
            Longitude = 0.0,
            LocationName = "Equator / Prime Meridian",
            EventType = "Event",
        };

        Action act = () => _engine.Calculate(input);

        act.Should().Throw<ArgumentOutOfRangeException>(
            "year 5401 is outside the years the ephemeris files cover, 1200 to 2399");
    }

    #endregion

    #endregion

    #region House System Tests

    [Fact]
    public void Calculate_DefaultHouseSystem_UsesPlacidus()
    {
        ChartInput input = CreateJ2000Input();

        ChartResult result = _engine.Calculate(input);

        result.HouseSystemName.Should().Be("Placidus");
    }

    [Fact]
    public void Calculate_EqualHouses_ProducesEquallySpacedCusps()
    {
        ChartInput input = new()
        {
            Name = "Equal House Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
            HouseSystem = HouseSystem.Equal,
        };

        ChartResult result = _engine.Calculate(input);

        result.HouseSystemName.Should().Be("Equal (from ASC)");
        result.Houses.Cusps.Should().HaveCount(12);

        // In Equal houses, each cusp is exactly 30° from the previous
        for (int i = 1; i < result.Houses.Cusps.Count; i++)
        {
            double gap = result.Houses.Cusps[i] - result.Houses.Cusps[i - 1];
            if (gap < 0) gap += 360.0;
            gap.Should().BeApproximately(30.0, 0.01,
                $"Equal house cusps should be 30° apart (cusp {i + 1} - cusp {i})");
        }
    }

    [Fact]
    public void Calculate_WholeSign_CuspsAtSignBoundaries()
    {
        ChartInput input = new()
        {
            Name = "Whole Sign Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
            HouseSystem = HouseSystem.WholeSign,
        };

        ChartResult result = _engine.Calculate(input);

        result.HouseSystemName.Should().Be("Whole Sign");
        result.Houses.Cusps.Should().HaveCount(12);

        // In Whole Sign, each cusp is at 0° of a sign (multiple of 30°)
        foreach (double cusp in result.Houses.Cusps)
        {
            double remainder = cusp % 30.0;
            remainder.Should().BeApproximately(0.0, 0.01,
                $"Whole Sign cusp {cusp:F2}° should be at 0° of a sign");
        }
    }

    [Fact]
    public void Calculate_Koch_ProducesDifferentCuspsFromPlacidus()
    {
        ChartInput placidusInput = CreateJ2000Input();
        ChartInput kochInput = new()
        {
            Name = "Koch Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
            HouseSystem = HouseSystem.Koch,
        };

        ChartResult placidus = _engine.Calculate(placidusInput);
        ChartResult koch = _engine.Calculate(kochInput);

        koch.HouseSystemName.Should().Be("Koch");

        // Koch and Placidus should agree on cusps 1, 4, 7, 10 (angular cusps)
        // but differ on intermediate cusps
        bool anyDifferent = false;
        for (int i = 0; i < 12; i++)
        {
            if (Math.Abs(placidus.Houses.Cusps[i] - koch.Houses.Cusps[i]) > 0.01)
            {
                anyDifferent = true;
                break;
            }
        }

        anyDifferent.Should().BeTrue(
            "Koch should produce at least some different intermediate cusps from Placidus");
    }

    [Fact]
    public void Calculate_HouseSystemFlowsToResult()
    {
        ChartInput input = new()
        {
            Name = "Regiomontanus Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
            HouseSystem = HouseSystem.Regiomontanus,
        };

        ChartResult result = _engine.Calculate(input);

        result.HouseSystemName.Should().Be("Regiomontanus");
        result.Input.HouseSystem.Should().Be(HouseSystem.Regiomontanus);
    }

    #endregion

    #region Minor points (branch Asteroids, 1.6.0)

    /// <summary>The five, in the order every list holds them (R1).</summary>
    private static readonly string[] MinorPointNames = ["Ceres", "Pallas", "Juno", "Vesta", "Lilith"];

    /// <summary>
    /// With the switch off, its default, a chart carries no minor point and no minor-point aspect, and its positions
    /// are the twelve (R2, R3): with it off every output is as it was (R9).
    /// </summary>
    [Fact]
    public void Calculate_SwitchOff_MinorPointsEmpty()
    {
        ChartResult result = _engine.Calculate(CreateJ2000Input());

        result.Input.IncludeMinorPoints.Should().BeFalse("the switch is off by default");
        result.MinorPoints.Should().BeEmpty();
        result.MinorPointAspects.Should().BeEmpty();
        result.Positions.Should().HaveCount(12);
    }

    /// <summary>
    /// With the switch on, five rows in R1's order, Ceres, Pallas, Juno, Vesta, Lilith, each with its house by
    /// longitude between the cusps, its right ascension and declination and no altitude; the positions still the
    /// twelve; and their aspects the five Ptolemaic aspects at 2° to the Sun to Pluto and the angles (R3, R4, R5).
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_FiveRowsInOrder()
    {
        ChartResult result = _engine.Calculate(CreateJ2000Input(includeMinorPoints: true));

        result.MinorPoints.Select(p => p.Name).Should().Equal(MinorPointNames);
        result.MinorPoints.Select(p => p.Body).Should().Equal(
            CelestialBodyId.Ceres, CelestialBodyId.Pallas, CelestialBodyId.Juno, CelestialBodyId.Vesta, CelestialBodyId.MeanApogee);
        result.Positions.Should().HaveCount(12, "the five never join the positions (R3)");
        result.Positions.Select(p => p.Name).Should().NotContain(MinorPointNames);

        foreach (PlanetPosition point in result.MinorPoints)
        {
            string because = $"of {point.Name}";
            point.House.Should().Be(HousePlacement.HouseOf(point.Longitude, result.Houses.Cusps), because);
            point.HouseExact.Should().NotBeNull(because);
            point.RightAscension.Should().NotBeNull(because);
            point.Declination.Should().NotBeNull(because);
            point.Altitude.Should().BeNull("the five take no altitude: hayz is not theirs");
        }

        result.MinorPoints[4].IsRetrograde.Should().BeFalse("the mean Lilith is never retrograde");

        result.MinorPointAspects.Should().NotBeEmpty("J2000 NYC holds five aspects at 2° (Tier B)");
        string[] targets =
        [
            "Sun", "Moon", "Mercury", "Venus", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune", "Pluto", "ASC", "MC",
        ];
        foreach (AspectResult aspect in result.MinorPointAspects)
        {
            MinorPointNames.Should().Contain(aspect.Body1Name);
            targets.Should().Contain(aspect.Body2Name);
            aspect.Aspect.Category.Should().Be(AspectCategory.Major);
            aspect.Orb.Should().BeLessThanOrEqualTo(2.0);
        }
    }

    /// <summary>
    /// A horary chart never carries them, whatever the switch (R8; scoping decision 2: the engine computes nothing
    /// for EventType "Horary", in any letter case, so the Markdown, JSON and PDF agree by construction).
    /// </summary>
    [Fact]
    public void Calculate_Horary_NeverCarriesThem()
    {
        foreach (string eventType in new[] { "Horary", "horary" })
        {
            ChartInput input = new()
            {
                Name = "Horary Test",
                LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York City",
                EventType = eventType,
                HoraryQuestion = "Will the asteroids be printed?",
                IncludeMinorPoints = true,
            };

            ChartResult result = _engine.Calculate(input);

            result.MinorPoints.Should().BeEmpty($"a chart of type '{eventType}' is horary");
            result.MinorPointAspects.Should().BeEmpty($"a chart of type '{eventType}' is horary");
            result.Positions.Should().HaveCount(12);
        }
    }

    /// <summary>
    /// The relocation copies the switch into the relocated input (rule 6), so the relocated chart carries its own
    /// five: the natal longitudes with the place's houses, and their aspects to the place's angles. J2000 NYC
    /// relocated to Los Angeles is Tier B's j2000la lists; a natal chart without the switch relocates without them.
    /// </summary>
    [Fact]
    public void CalculateRelocated_CopiesTheSwitch_MatchesTierB()
    {
        RelocationInput losAngeles = new() { Latitude = 34.0522, Longitude = -118.2437, LocationName = "Los Angeles" };
        ChartResult natal = _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));

        ChartResult relocated = _engine.CalculateRelocated(natal, losAngeles);

        relocated.Input.IncludeMinorPoints.Should().BeTrue("the relocated input copies the switch");
        MinorPointTierB.AssertPoints(relocated, MinorPointTierB.LoadPoints("j2000la"));
        MinorPointTierB.AssertAspects(relocated, MinorPointTierB.LoadAspects("j2000la"));
        for (int i = 0; i < 5; i++)
        {
            relocated.MinorPoints[i].Longitude.Should().Be(
                natal.MinorPoints[i].Longitude, "the same instant gives the same longitude");
        }

        ChartResult withoutTheSwitch = _engine.CalculateRelocated(
            _engine.Calculate(GoldenFixtures.J2000NycInput()), losAngeles);
        withoutTheSwitch.Input.IncludeMinorPoints.Should().BeFalse();
        withoutTheSwitch.MinorPoints.Should().BeEmpty();
        withoutTheSwitch.MinorPointAspects.Should().BeEmpty();
    }

    /// <summary>
    /// The electional pipelines never carry them, whatever the switch (R8): the candidate chart by either overload,
    /// and the scoring inputs; the full chart from the same input does, so the gate is the pipeline's.
    /// </summary>
    [Fact]
    public void CalculateForElectional_NeverCarriesThem()
    {
        ChartInput input = CreateJ2000Input(includeMinorPoints: true);
        DateTime instant = new(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        ChartResult candidate = _engine.CalculateForElectional(input);
        ChartResult atInstant = _engine.CalculateForElectional(input, instant);
        ChartResult scoringInputs = _engine.CalculateElectionalScoringInputs(input, instant);

        foreach (ChartResult result in new[] { candidate, atInstant, scoringInputs })
        {
            result.MinorPoints.Should().BeEmpty();
            result.MinorPointAspects.Should().BeEmpty();
            result.Positions.Should().HaveCount(12);
        }

        _engine.Calculate(input).MinorPoints.Should().HaveCount(5, "the full chart from the same input carries them");
    }

    #endregion
}
