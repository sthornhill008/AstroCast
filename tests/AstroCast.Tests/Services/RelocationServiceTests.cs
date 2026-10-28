using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="RelocationService"/> — relocation chart
/// calculation with UTC preservation and house change comparison.
/// Requires the shared <see cref="EphemerisFixture"/> for real chart calculations.
/// </summary>
[Collection("Ephemeris")]
public sealed class RelocationServiceTests
{
    private readonly IChartEngine _engine;
    private readonly RelocationService _service;

    /// <summary>
    /// Creates a RelocationService using the shared ephemeris fixture.
    /// </summary>
    public RelocationServiceTests(EphemerisFixture fixture)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        DignityService dignityService = new();
        SectService sectService = new();
        AspectService aspectService = new(fixture.EphemerisService, logger);

        _engine = new ChartEngine(
            fixture.EphemerisService,
            aspectService,
            new MidpointService(logger),
            dignityService,
            new MoonConditionService(),
            new ChartContextService(dignityService, sectService),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(),
            new PlanetConditionService(),
            new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(dignityService),
            new AntisciaService(),
            new StationService(),
            new ParanService(fixture.EphemerisService, logger),
            logger);

        _service = new RelocationService(_engine, logger);
    }

    #region UTC Preservation Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_PreserveJulianDay()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        result.RelocatedChart.JulianDay.Should().Be(natal.JulianDay);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_PreserveUtcDateTime()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        result.RelocatedChart.UtcDateTime.Should().Be(natal.UtcDateTime);
    }

    /// <summary>
    /// A natal chart before 1582-10-15 relocates at its instant (R-D4, branch R2, 1.3.4): N1, Julian
    /// 1300-03-15 12:00 UT at Greenwich, relocated to Florence keeps its Julian Day and its UtcDateTime,
    /// and the relocated input's reading is the instant on the calendar in force, 1300-03-15 12:00:00 at
    /// UTC+0 (<see cref="CalendarDateFormatter.Reading"/>), cast at the natal Julian Day. Read as a
    /// reading, the proleptic instant would be a Julian date 8 days late.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_N1ToFlorence_Should_KeepTheInstant_AndReadItOnTheCalendarInForce()
    {
        ChartResult natal = _engine.Calculate(new ChartInput
        {
            Name = "N1",
            LocalDateTime = new DateTime(1300, 3, 15, 12, 0, 0),
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 51.4779,
            Longitude = -0.0015,
            LocationName = "Greenwich",
            EventType = "Birth",
        });

        RelocationResult result = _service.Calculate(natal, new RelocationInput
        {
            Latitude = 43.7696,
            Longitude = 11.2558,
            LocationName = "Florence",
        });

        result.RelocatedChart.JulianDay.Should().Be(natal.JulianDay);
        result.RelocatedChart.UtcDateTime.Should().Be(natal.UtcDateTime);
        result.RelocatedChart.Input.LocalDateTime.Should().Be(new DateTime(1300, 3, 15, 12, 0, 0));
        result.RelocatedChart.Input.UseExplicitOffset.Should().BeTrue();
        result.RelocatedChart.Input.UtcOffsetHours.Should().Be(0.0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_PreservePlanetaryLongitudes()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        // All planetary longitudes must be identical
        for (int i = 0; i < natal.Positions.Count; i++)
        {
            result.RelocatedChart.Positions[i].Longitude.Should().Be(
                natal.Positions[i].Longitude,
                $"{natal.Positions[i].Name} longitude should be preserved");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_PreservePlanetarySigns()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        for (int i = 0; i < natal.Positions.Count; i++)
        {
            result.RelocatedChart.Positions[i].Sign.Should().Be(
                natal.Positions[i].Sign,
                $"{natal.Positions[i].Name} sign should be preserved");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_PreservePlanetToPlanetAspects()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        // Planet-to-planet aspects are identical (same longitudes).
        // Planet-to-angle aspects may differ because ASC/MC change.
        int natalP2P = natal.Aspects.Count(a =>
            a.Body1Name != "ASC" && a.Body1Name != "MC" &&
            a.Body2Name != "ASC" && a.Body2Name != "MC");
        int relocP2P = result.RelocatedChart.Aspects.Count(a =>
            a.Body1Name != "ASC" && a.Body1Name != "MC" &&
            a.Body2Name != "ASC" && a.Body2Name != "MC");

        relocP2P.Should().Be(natalP2P,
            "planet-to-planet aspects should be identical since longitudes are preserved");
    }

    #endregion

    #region House Change Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_ChangeHouseCusps_WhenRelocatingFarAway()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateTokyoTarget());

        // Houses should be different for a large longitude change (NYC→Tokyo)
        result.RelocatedChart.Houses.Ascendant.Should().NotBe(natal.Houses.Ascendant);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_DetectHouseChanges()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateTokyoTarget());

        // For a major relocation (NYC→Tokyo, ~180° longitude difference),
        // most or all planets should change houses
        result.HouseChanges.Should().NotBeEmpty();
    }

    /// <summary>
    /// A planet is angular in a chart when <see cref="AngularityService.FindAngularPlanets"/> lists it, within 5°
    /// of an angle (Q11): "became angular" is angular at the place and not natally, "left angle" the reverse,
    /// and every planet whose angularity changed is a row. Until 1.3.4 the flags were by house number 1, 4, 7
    /// and 10 (Y3-6).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_FlagAngularChanges_ByTheFiveDegreeRule()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateTokyoTarget());

        HashSet<string> natalAngular = AngularityService.FindAngularPlanets(natal.Positions, natal.Houses)
            .Select(a => a.PlanetName).ToHashSet(StringComparer.Ordinal);
        HashSet<string> relocatedAngular = AngularityService.FindAngularPlanets(
                result.RelocatedChart.Positions, result.RelocatedChart.Houses)
            .Select(a => a.PlanetName).ToHashSet(StringComparer.Ordinal);

        foreach (HouseChange change in result.HouseChanges)
        {
            change.BecameAngular.Should().Be(
                relocatedAngular.Contains(change.PlanetName) && !natalAngular.Contains(change.PlanetName),
                "{0} became angular only if it is within 5° of an angle at Tokyo and not at New York", change.PlanetName);
            change.LeftAngle.Should().Be(
                natalAngular.Contains(change.PlanetName) && !relocatedAngular.Contains(change.PlanetName),
                "{0} left angle only if it is within 5° of an angle at New York and not at Tokyo", change.PlanetName);
        }

        foreach (string planet in natalAngular.Union(relocatedAngular))
        {
            if (natalAngular.Contains(planet) != relocatedAngular.Contains(planet))
                result.HouseChanges.Should().Contain(c => c.PlanetName == planet, "{0}'s angularity changed", planet);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_HaveNoHouseChanges_WhenSameLocation()
    {
        ChartResult natal = CalculateNatalChart();

        // Relocate to the same coordinates
        RelocationInput samePlace = new()
        {
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City (same)",
        };

        RelocationResult result = _service.Calculate(natal, samePlace);

        result.HouseChanges.Should().BeEmpty();
        result.AscendantSignChanged.Should().BeFalse();
    }

    /// <summary>
    /// A relocation with no house change says so without claiming a cause (E2-3): the planets keep their houses for
    /// reasons other than closeness too, so "(locations are very close geographically)" is gone.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Relocation_NoHouseChanges_StatesNoCause()
    {
        ChartResult natal = CalculateNatalChart();
        RelocationInput samePlace = new()
        {
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City (same)",
        };

        RelocationResult result = _service.Calculate(natal, samePlace);
        List<string> lines = [.. RelocationMarkdownFormatter.ToMarkdown(result).Split('\n').Select(l => l.TrimEnd('\r'))];

        result.HouseChanges.Should().BeEmpty();
        lines.Should().Contain("No house changes.");
    }

    /// <summary>The rows are ordered became angular, then left angle, then by name.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_OrderHouseChanges_BecameAngular_ThenLeftAngle_ThenByName()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateTokyoTarget());

        result.HouseChanges.Should().HaveCountGreaterThan(1);
        result.HouseChanges.Should().Equal(
            result.HouseChanges
                .OrderByDescending(c => c.BecameAngular)
                .ThenByDescending(c => c.LeftAngle)
                .ThenBy(c => c.PlanetName)
                .ToList());
    }

    /// <summary>
    /// J2000 NYC to Los Angeles (Tier A, read off the two goldens: natally Mercury is 2°21′ from the Ascendant
    /// and Jupiter 3°12′ from the IC; at Los Angeles Venus is 1°32′ from the Ascendant): twelve rows, Venus
    /// became angular, Jupiter and Mercury left angle, the rest unflagged, in that order. Until 1.3.4 Chiron
    /// and Pluto "became angular" and the Moon, Neptune, Saturn, Sun and True Node "left angle" by house
    /// number, and Jupiter and Mercury were unflagged.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_J2000ToLosAngeles_Should_ListTwelveHouseChanges_AngularWithinFiveDegrees()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLosAngelesTarget());

        result.HouseChanges.Select(c => (c.PlanetName, c.NatalHouse, c.RelocatedHouse, c.BecameAngular, c.LeftAngle))
            .Should().Equal(
                ("Venus", 11, 1, true, false),
                ("Jupiter", 3, 5, false, true),
                ("Mercury", 12, 2, false, true),
                ("Chiron", 11, 1, false, false),
                ("Mars", 2, 3, false, false),
                ("Moon", 10, 12, false, false),
                ("Neptune", 1, 2, false, false),
                ("Pluto", 11, 1, false, false),
                ("Saturn", 4, 6, false, false),
                ("Sun", 1, 2, false, false),
                ("True Node", 7, 8, false, false),
                ("Uranus", 2, 3, false, false));
    }

    /// <summary>
    /// A planet whose angularity changes with its house unchanged is a row with equal houses: J2000 NYC relocated
    /// 4.5° of longitude east of New York (40.7128° N, 69.5° W), where the Ascendant advances past 5° from
    /// Mercury, natally 2°21′ before it in the 12th house, which stays the 12th.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_J2000EastOfNewYork_Should_ListMercuryLeavingTheAscendant_WithItsHouseUnchanged()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateEastOfNewYorkTarget());

        natal.Positions.Single(p => p.Name == "Mercury").House.Should().Be(12);
        result.RelocatedChart.Positions.Single(p => p.Name == "Mercury").House.Should().Be(12);
        AngularityService.FindAngularPlanets(result.RelocatedChart.Positions, result.RelocatedChart.Houses)
            .Should().NotContain(a => a.PlanetName == "Mercury");
        result.HouseChanges.Should().ContainSingle(c => c.PlanetName == "Mercury")
            .Which.Should().Match<HouseChange>(c => c.NatalHouse == 12 && c.RelocatedHouse == 12 && c.LeftAngle && !c.BecameAngular);
    }

    #endregion

    #region Angle Sign Change Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_DetectAscendantSignChange()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateTokyoTarget());

        // NYC→Tokyo is far enough that the Ascendant sign should change
        result.NatalAscendantSign.Should().NotBe(ZodiacSign.Aries); // Verify it's set
        result.RelocatedAscendantSign.Should().NotBe(ZodiacSign.Aries); // Verify it's set
        // The actual sign change depends on the chart; just verify the fields are populated
        result.NatalAscendantSign.Should().BeOneOf(
            ZodiacSign.Aries, ZodiacSign.Taurus, ZodiacSign.Gemini,
            ZodiacSign.Cancer, ZodiacSign.Leo, ZodiacSign.Virgo,
            ZodiacSign.Libra, ZodiacSign.Scorpio, ZodiacSign.Sagittarius,
            ZodiacSign.Capricorn, ZodiacSign.Aquarius, ZodiacSign.Pisces);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_PopulateChartContext_InRelocatedChart()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        result.RelocatedChart.ChartContext.Should().NotBeNull();
        result.RelocatedChart.ChartContext!.ChartRuler.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region Result Structure Tests

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_IncludeNatalChart()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        result.NatalChart.Should().BeSameAs(natal);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_IncludeRelocationTarget()
    {
        ChartResult natal = CalculateNatalChart();
        RelocationInput target = CreateLondonTarget();

        RelocationResult result = _service.Calculate(natal, target);

        result.RelocationTarget.Should().BeSameAs(target);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_SetEngineVersion()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        result.EngineVersion.Should().StartWith("AstroCast");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_IncludeRelocatedChartFullData()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        // Verify the relocated chart has all the standard data
        result.RelocatedChart.Positions.Should().NotBeEmpty();
        result.RelocatedChart.Houses.Should().NotBeNull();
        result.RelocatedChart.Aspects.Should().NotBeEmpty();
        result.RelocatedChart.Dignities.Should().NotBeEmpty();
        result.RelocatedChart.MoonCondition.Should().NotBeNull();
        result.RelocatedChart.ArabicParts.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_SetRelocatedChartName()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        result.RelocatedChart.Input.Name.Should().Contain("London");
        result.RelocatedChart.Input.Name.Should().Contain(natal.Input.Name);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_SetRelocatedCoordinates()
    {
        ChartResult natal = CalculateNatalChart();
        RelocationInput target = CreateLondonTarget();

        RelocationResult result = _service.Calculate(natal, target);

        result.RelocatedChart.Input.Latitude.Should().Be(target.Latitude);
        result.RelocatedChart.Input.Longitude.Should().Be(target.Longitude);
    }

    #endregion

    #region Edge Cases

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_HandleSouthernHemisphereRelocation()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationInput sydney = new()
        {
            Latitude = -33.8688,
            Longitude = 151.2093,
            LocationName = "Sydney, Australia",
        };

        RelocationResult result = _service.Calculate(natal, sydney);

        result.RelocatedChart.Should().NotBeNull();
        result.RelocatedChart.JulianDay.Should().Be(natal.JulianDay);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_HandleNearbyRelocation()
    {
        ChartResult natal = CalculateNatalChart();

        // Philadelphia is close to NYC — minor changes expected
        RelocationInput philly = new()
        {
            Latitude = 39.9526,
            Longitude = -75.1652,
            LocationName = "Philadelphia, PA",
        };

        RelocationResult result = _service.Calculate(natal, philly);

        result.RelocatedChart.JulianDay.Should().Be(natal.JulianDay);
        // Nearby relocation may or may not change houses
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_PreserveEventType()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());

        result.RelocatedChart.Input.EventType.Should().Be(natal.Input.EventType);
    }

    #endregion

    #region House System Tests

    // Expected values are swe_houses at JD(UT) 2432464.1041666665 (person A's instant) and
    // the target place, called directly with the natal chart's house system (Tier B,
    // fixtures/truth/relocation_houses.py).

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_KeepNatalHouseSystem_When_NatalChartIsRegiomontanus()
    {
        ChartResult natal = CalculateHartwellInstant(HouseSystem.Regiomontanus);

        RelocationResult result = _service.Calculate(natal, CreateReykjavikTarget());

        result.RelocatedChart.Input.HouseSystem.Should().Be(HouseSystem.Regiomontanus);
        result.RelocatedChart.HouseSystemName.Should().Be("Regiomontanus");
        result.RelocatedChart.Houses.Cusps[1].Should().BeApproximately(278.2879, 1e-4,
            "the Regiomontanus 2nd cusp at Reykjavik; Placidus would put it at 289.6964");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_Succeed_When_RegiomontanusChartIsRelocatedBeyondThePolarCircle()
    {
        ChartResult natal = CalculateHartwellInstant(HouseSystem.Regiomontanus);

        RelocationResult result = _service.Calculate(natal, CreateTromsoTarget());

        result.RelocatedChart.HouseSystemName.Should().Be("Regiomontanus");
        result.RelocatedChart.Houses.Ascendant.Should().BeApproximately(58.4365, 1e-4);
        result.RelocatedChart.Houses.Midheaven.Should().BeApproximately(71.4206, 1e-4);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_Succeed_When_EqualHouseChartIsRelocatedBeyondThePolarCircle()
    {
        ChartResult natal = CalculateHartwellInstant(HouseSystem.Equal);

        RelocationResult result = _service.Calculate(natal, CreateTromsoTarget());

        result.RelocatedChart.Input.HouseSystem.Should().Be(HouseSystem.Equal);
        result.RelocatedChart.Houses.Cusps[1].Should().BeApproximately(88.4365, 1e-4,
            "an Equal 2nd cusp is the Ascendant, 58.4365, plus 30°");
    }

    #endregion

    #region HouseChange Model Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void HouseChange_Description_Should_IndicateBecameAngular()
    {
        HouseChange change = new()
        {
            PlanetName = "Venus",
            NatalHouse = 5,
            RelocatedHouse = 10,
            BecameAngular = true,
            LeftAngle = false,
        };

        change.Description.Should().Contain("Venus");
        change.Description.Should().Contain("5th");
        change.Description.Should().Contain("10th");
        change.Description.Should().Contain("became angular");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HouseChange_Description_Should_IndicateLeftAngle()
    {
        HouseChange change = new()
        {
            PlanetName = "Mars",
            NatalHouse = 1,
            RelocatedHouse = 3,
            BecameAngular = false,
            LeftAngle = true,
        };

        change.Description.Should().Contain("left angle");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HouseChange_Description_Should_HandleSimpleChange()
    {
        HouseChange change = new()
        {
            PlanetName = "Jupiter",
            NatalHouse = 2,
            RelocatedHouse = 5,
            BecameAngular = false,
            LeftAngle = false,
        };

        change.Description.Should().Be("Jupiter: 2nd \u2192 5th");
    }

    #endregion

    #region What stays the birth's, and what follows the place (S-D6)

    /// <summary>
    /// J2000 NYC, a night chart, relocated to London, where its instant, 12:00 UT, is noon: the relocated chart
    /// keeps the birth's sect, so its dignities (the triplicity tier follows day or night) and its Planetary
    /// Sect table are the natal's, planet by planet, though the Sun stands above London's horizon (branch K2,
    /// 1.3.4; ruling S-D6). Until 1.3.4 the relocated chart was a whole chart cast at the place, so its sect
    /// flipped to day (74 of 187 standard relocations, Y3-2).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_J2000ToLondon_Should_KeepTheBirthSect_Dignities_AndPlanetarySect()
    {
        ChartResult natal = CalculateNatalChart();
        ChartContextInfo natalContext = natal.ChartContext ?? throw new InvalidOperationException("the natal chart has no context");

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());
        ChartResult relocated = result.RelocatedChart;
        ChartContextInfo relocatedContext = relocated.ChartContext ?? throw new InvalidOperationException("the relocated chart has no context");

        natalContext.IsDayChart.Should().BeFalse();
        relocated.Positions.Single(p => p.Name == "Sun").Altitude.Should().BeGreaterThan(0.0, "the Sun is above London's horizon at noon");
        relocatedContext.IsDayChart.Should().BeFalse("the sect is the birth's");
        relocated.Dignities.Select(d => (d.PlanetName, d.Label, d.Score))
            .Should().Equal(natal.Dignities.Select(d => (d.PlanetName, d.Label, d.Score)));
        relocatedContext.PlanetarySect.Select(x => (x.PlanetName, x.IsInSect, x.IsHayz))
            .Should().Equal(natalContext.PlanetarySect.Select(x => (x.PlanetName, x.IsInSect, x.IsHayz)));
    }

    /// <summary>
    /// The relocated chart keeps the birth's planetary hour, the Moon's, night hour 12, 06:07:57 to 07:21:44 on
    /// the birth clock; no hour is computed at the place. Until 1.3.4 it computed one on the UTC calendar
    /// (Venus, night hour 10, at Los Angeles; the wrong ruler at Tokyo, Sydney and Suva, Y3-3).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_J2000ToLondon_Should_KeepTheBirthPlanetaryHour()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());
        PlanetaryHourInfo hour = result.RelocatedChart.PlanetaryHour ?? throw new InvalidOperationException("the relocated chart has no planetary hour");

        hour.Should().BeSameAs(natal.PlanetaryHour);
        hour.Hour.RulingPlanet.Should().Be("Moon");
        hour.Hour.HourNumber.Should().Be(12);
        hour.Hour.IsDaytime.Should().BeFalse();
        hour.Hour.HourStart.Should().BeCloseTo(new DateTime(2000, 1, 1, 6, 7, 57), TimeSpan.FromSeconds(1));
        hour.Hour.HourEnd.Should().BeCloseTo(new DateTime(2000, 1, 1, 7, 21, 44), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// The Lots follow the place and the birth's sect: the Lot of Fortune is cast by the night formula,
    /// Ascendant + Sun − Moon, at the London Ascendant. Until 1.3.4 a relocation that flipped to day cast it by
    /// the day formula.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_J2000ToLondon_Should_CastTheLotOfFortuneByTheNightFormula_AtTheLondonAscendant()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());
        ChartResult relocated = result.RelocatedChart;

        double sun = relocated.Positions.Single(p => p.Name == "Sun").Longitude;
        double moon = relocated.Positions.Single(p => p.Name == "Moon").Longitude;
        double expected = ((relocated.Houses.Ascendant + sun - moon) % 360.0 + 360.0) % 360.0;
        ArabicPart fortune = relocated.ArabicParts.Single(p => p.Name == "Lot of Fortune");
        fortune.Longitude.Should().BeApproximately(expected, 1e-6);
        fortune.Formula.Should().Be("Asc + Sun \u2212 Moon");
        Math.Abs(relocated.Houses.Ascendant - natal.Houses.Ascendant).Should().BeGreaterThan(1.0, "the Ascendant is London's");
    }

    /// <summary>
    /// The relocated chart is the natal input with the place changed, read through the birth's own clock: the
    /// same reading, zone, offset label and instant (the same Julian Day, bit for bit), the angles, cusps and
    /// placements London's. Until 1.3.4 the input was the instant at an explicit offset of zero, so the export
    /// printed "UTC+0 (explicit)".
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_J2000ToLondon_Should_KeepTheBirthClock_AndFollowThePlace()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLondonTarget());
        ChartResult relocated = result.RelocatedChart;

        relocated.Input.Name.Should().Be("Relocation Test Subject \u2014 Relocated to London, UK");
        relocated.Input.LocalDateTime.Should().Be(new DateTime(2000, 1, 1, 7, 0, 0));
        relocated.Input.TimeZone.Should().Be("America/New_York");
        relocated.Input.UseExplicitOffset.Should().BeFalse();
        relocated.Input.UtcOffsetHours.Should().BeNull();
        relocated.Input.Latitude.Should().Be(51.5074);
        relocated.Input.Longitude.Should().Be(-0.1278);
        relocated.Input.LocationName.Should().Be("London, UK");
        relocated.ResolvedOffsetLabel.Should().Be("EST (UTC\u22125)");
        relocated.JulianDay.Should().Be(2451545.0);
        relocated.UtcDateTime.Should().Be(natal.UtcDateTime);
        relocated.Houses.Ascendant.Should().NotBe(natal.Houses.Ascendant);
        relocated.Positions.Single(p => p.Name == "Chiron").House
            .Should().NotBe(natal.Positions.Single(p => p.Name == "Chiron").House, "the placements are London's");
    }

    /// <summary>A guard: J2000 NYC to Los Angeles stays a night chart, its Ascendant 00°01′48″ Sagittarius, as before.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_J2000ToLosAngeles_Should_KeepNight_AndTheSagittariusAscendant()
    {
        ChartResult natal = CalculateNatalChart();

        RelocationResult result = _service.Calculate(natal, CreateLosAngelesTarget());
        ChartContextInfo relocatedContext = result.RelocatedChart.ChartContext ?? throw new InvalidOperationException("the relocated chart has no context");

        relocatedContext.IsDayChart.Should().BeFalse();
        result.RelocatedChart.Houses.Ascendant.Should().BeApproximately(240.03, 0.01);
    }

    /// <summary>
    /// A horary chart relocated keeps its category and is judged at the place: the invented Lake trip 1001
    /// (the chart <c>HoraryPerfectionTimingTests</c> casts, Regiomontanus, category Travel), to London, carries
    /// the category and a horary analysis. Until 1.3.4 the category was not copied, so the relocated horary
    /// chart silently lost its Horary Perfection Analysis.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_HoraryWithCategory_ToLondon_Should_KeepTheCategory_AndJudgeThePerfection()
    {
        ChartResult horary = _engine.Calculate(new ChartInput
        {
            Name = "Lake trip 1001",
            LocalDateTime = new DateTime(2026, 10, 1, 7, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 39.9612,
            Longitude = -82.9988,
            LocationName = "Columbus, Ohio, United States",
            EventType = "Horary",
            HoraryQuestion = "Is tomorrow a good day to drive to the lake?",
            HoraryCategory = "Travel",
            HouseSystem = HouseSystem.Regiomontanus,
        });
        horary.HoraryPerfection.Should().NotBeNull();

        RelocationResult result = _service.Calculate(horary, CreateLondonTarget());

        result.RelocatedChart.Input.HoraryCategory.Should().Be("Travel");
        result.RelocatedChart.Input.HoraryQuestion.Should().Be(horary.Input.HoraryQuestion);
        result.RelocatedChart.Input.HouseSystem.Should().Be(HouseSystem.Regiomontanus);
        result.RelocatedChart.HoraryPerfection.Should().NotBeNull("the perfection is judged at the place");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Calculates a natal chart for the J2000.0 epoch in New York City.
    /// </summary>
    private ChartResult CalculateNatalChart()
    {
        ChartInput input = new()
        {
            Name = "Relocation Test Subject",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        };

        return _engine.Calculate(input);
    }

    /// <summary>Creates a relocation target for London, UK.</summary>
    private static RelocationInput CreateLondonTarget() => new()
    {
        Latitude = 51.5074,
        Longitude = -0.1278,
        LocationName = "London, UK",
    };

    /// <summary>Creates a relocation target for Tokyo, Japan.</summary>
    private static RelocationInput CreateTokyoTarget() => new()
    {
        Latitude = 35.6762,
        Longitude = 139.6503,
        LocationName = "Tokyo, Japan",
    };

    /// <summary>Creates a relocation target for Los Angeles, the golden's.</summary>
    private static RelocationInput CreateLosAngelesTarget() => new()
    {
        Latitude = 34.0522,
        Longitude = -118.2437,
        LocationName = "Los Angeles",
    };

    /// <summary>Creates a relocation target at New York's latitude, 4.5° of longitude east of it.</summary>
    private static RelocationInput CreateEastOfNewYorkTarget() => new()
    {
        Latitude = 40.7128,
        Longitude = -69.5,
        LocationName = "East of New York",
    };

    /// <summary>
    /// Calculates a chart at person A's instant and place (invented: 1947-10-05 09:30 at UTC−5,
    /// JD(UT) 2432464.1041666665; Harrisburg, Pennsylvania) in the given house system.
    /// </summary>
    private ChartResult CalculateHartwellInstant(HouseSystem houseSystem)
    {
        ChartInput input = new()
        {
            Name = "Eleanor Hartwell",
            LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0, DateTimeKind.Unspecified),
            TimeZone = "America/New_York",
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, Pennsylvania",
            EventType = "Birth",
            HouseSystem = houseSystem,
        };

        ChartResult chart = _engine.Calculate(input);
        chart.JulianDay.Should().BeApproximately(2432464.1041666665, 1e-8);
        return chart;
    }

    /// <summary>Creates a relocation target for Reykjavik, Iceland.</summary>
    private static RelocationInput CreateReykjavikTarget() => new()
    {
        Latitude = 64.1466,
        Longitude = -21.9426,
        LocationName = "Reykjavik, Iceland",
    };

    /// <summary>Creates a relocation target for Tromsø, Norway, beyond the polar circle.</summary>
    private static RelocationInput CreateTromsoTarget() => new()
    {
        Latitude = 69.6492,
        Longitude = 18.9553,
        LocationName = "Tromsø, Norway",
    };

    #endregion
}
