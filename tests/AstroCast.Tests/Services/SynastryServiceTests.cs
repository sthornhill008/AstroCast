using AstroCast.Core.Configuration;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.Tests.Services.MinorPoints;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="SynastryService"/> verifying inter-chart aspect calculation,
/// house overlay placement, and the complete synastry analysis pipeline.
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class SynastryServiceTests
{
    private readonly ChartEngine _engine;
    private readonly SynastryService _synastryService;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine and SynastryService.
    /// </summary>
    public SynastryServiceTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
        _synastryService = new SynastryService(logger);
    }

    /// <summary>
    /// Standard J2000.0 test input for New York City (Person A).
    /// </summary>
    private static ChartInput CreatePersonAInput() => new()
    {
        Name = "Person A",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    };

    /// <summary>
    /// Second test input for London (Person B).
    /// </summary>
    private static ChartInput CreatePersonBInput() => new()
    {
        Name = "Person B",
        LocalDateTime = new DateTime(1990, 6, 15, 14, 0, 0),
        TimeZone = "Europe/London",
        Latitude = 51.5074,
        Longitude = -0.1278,
        LocationName = "London, UK",
        EventType = "Birth",
    };

    /// <summary>
    /// Calculate both charts and return the synastry result.
    /// </summary>
    private SynastryResult CalculateTestSynastry()
    {
        ChartResult personA = _engine.Calculate(CreatePersonAInput());
        ChartResult personB = _engine.Calculate(CreatePersonBInput());
        return _synastryService.Calculate(personA, personB);
    }

    [Fact]
    public void Calculate_Should_ReturnInterChartAspects_When_TwoValidCharts()
    {
        // Arrange & Act
        SynastryResult result = CalculateTestSynastry();

        // Assert
        result.InterChartAspects.Should().NotBeEmpty(
            "two valid charts should produce at least one inter-chart aspect");

        result.InterChartAspects.Should().AllSatisfy(a =>
            a.Orb.Should().BeGreaterThan(0,
                "all aspects should have a positive orb (0 would be astronomically improbable)"));
    }

    [Fact]
    public void Calculate_Should_SortAspectsByOrb_When_Calculated()
    {
        // Arrange & Act
        SynastryResult result = CalculateTestSynastry();

        // Assert
        for (int i = 1; i < result.InterChartAspects.Count; i++)
        {
            result.InterChartAspects[i].Orb.Should().BeGreaterThanOrEqualTo(
                result.InterChartAspects[i - 1].Orb,
                "inter-chart aspects should be sorted by orb ascending");
        }
    }

    /// <summary>
    /// A chart against itself ties every A X–B Y pair with its A Y–B X twin exactly, by construction: within each run
    /// of equal orbs the aspects stand by <see cref="InterChartAspect.Body1Name"/>, then
    /// <see cref="InterChartAspect.Body2Name"/>, both ordinal (1.6.1, item G, branch TiesAndGuards). Until 1.6.1 a
    /// tie's order was the sort's own, the same on every run of one build but undefined by the code.
    /// </summary>
    [Fact]
    public void CalculateInterChartAspects_OfAChartWithItself_OrdersEveryTieByNames()
    {
        ChartResult chart = _engine.Calculate(CreatePersonAInput());

        SynastryResult result = _synastryService.Calculate(chart, chart);

        IReadOnlyList<InterChartAspect> aspects = result.InterChartAspects;
        int ties = 0;
        for (int i = 1; i < aspects.Count; i++)
        {
            if (aspects[i].Orb != aspects[i - 1].Orb)
                continue;
            ties++;
            int byBody1 = string.CompareOrdinal(aspects[i - 1].Body1Name, aspects[i].Body1Name);
            byBody1.Should().BeLessThanOrEqualTo(0, $"row {i} ties row {i - 1} at {aspects[i].Orb} and must follow it by Body1Name");
            if (byBody1 == 0)
            {
                string.CompareOrdinal(aspects[i - 1].Body2Name, aspects[i].Body2Name)
                    .Should().BeLessThanOrEqualTo(0, $"row {i} ties row {i - 1} on Body1Name and must follow it by Body2Name");
            }
        }

        ties.Should().BeGreaterThan(0, "a chart against itself ties every pair with its twin");
    }

    /// <summary>
    /// The comparer of both inter-chart lists: by orb, then a tie by <see cref="InterChartAspect.Body1Name"/>,
    /// <see cref="InterChartAspect.Body2Name"/> and the aspect's angle (1.6.1, item G, branch TiesAndGuards).
    /// </summary>
    [Fact]
    public void CompareByOrbThenNames_OrdersATieByNamesThenAngle()
    {
        AspectDefinition trine = AspectDefinition.All.Single(d => d.Angle == 120);
        AspectDefinition sextile = AspectDefinition.All.Single(d => d.Angle == 60);
        InterChartAspect venusToMars = TiedAspect("A Venus", "B Mars", trine);
        InterChartAspect marsToVenus = TiedAspect("A Mars", "B Venus", trine);
        InterChartAspect marsToVenusSextile = TiedAspect("A Mars", "B Venus", sextile);

        SynastryService.CompareByOrbThenNames(venusToMars, marsToVenus).Should().BePositive("A Mars sorts before A Venus");
        SynastryService.CompareByOrbThenNames(marsToVenus, venusToMars).Should().BeNegative();
        SynastryService.CompareByOrbThenNames(marsToVenusSextile, marsToVenus).Should().BeNegative("the same names at a smaller angle sort first");
        SynastryService.CompareByOrbThenNames(marsToVenus, marsToVenus).Should().Be(0);

        List<InterChartAspect> sorted = [venusToMars, marsToVenus, marsToVenusSextile];
        sorted.Sort(SynastryService.CompareByOrbThenNames);
        sorted.Should().ContainInOrder(marsToVenusSextile, marsToVenus, venusToMars);
    }

    /// <summary>An inter-chart aspect at an orb of 1.5°, the tie the comparer test breaks.</summary>
    /// <param name="body1">Person A's body.</param>
    /// <param name="body2">Person B's body.</param>
    /// <param name="aspect">The aspect.</param>
    /// <returns>The aspect.</returns>
    private static InterChartAspect TiedAspect(string body1, string body2, AspectDefinition aspect) => new()
    {
        Body1Name = body1,
        Body1Longitude = 10.0,
        Body2Name = body2,
        Body2Longitude = 10.0 + aspect.Angle + 1.5,
        Aspect = aspect,
        Orb = 1.5,
    };

    [Fact]
    public void Calculate_Should_PrefixBodyNames_When_Calculated()
    {
        // Arrange & Act
        SynastryResult result = CalculateTestSynastry();

        // Assert
        result.InterChartAspects.Should().AllSatisfy(a =>
        {
            bool body1Valid = a.Body1Name.StartsWith("A ") || a.Body1Name.StartsWith("B ");
            bool body2Valid = a.Body2Name.StartsWith("A ") || a.Body2Name.StartsWith("B ");
            body1Valid.Should().BeTrue(
                $"Body1Name '{a.Body1Name}' should start with 'A ' or 'B '");
            body2Valid.Should().BeTrue(
                $"Body2Name '{a.Body2Name}' should start with 'A ' or 'B '");
        });
    }

    [Fact]
    public void Calculate_Should_IncludeAngleAspects_When_Calculated()
    {
        // Arrange & Act
        SynastryResult result = CalculateTestSynastry();

        // Assert
        bool hasAngleAspect = result.InterChartAspects.Any(a =>
            a.Body2Name.Contains("ASC") || a.Body2Name.Contains("MC") ||
            a.Body1Name.Contains("ASC") || a.Body1Name.Contains("MC"));

        hasAngleAspect.Should().BeTrue(
            "at least one inter-chart aspect should involve ASC or MC");
    }

    [Fact]
    public void Calculate_Should_ReturnHouseOverlays_When_Calculated()
    {
        // Arrange & Act
        SynastryResult result = CalculateTestSynastry();

        // Assert
        result.PersonAInPersonBHouses.Should().HaveCount(12,
            "Person A has 12 planets to place in Person B's houses");

        result.PersonBInPersonAHouses.Should().HaveCount(12,
            "Person B has 12 planets to place in Person A's houses");

        result.PersonAInPersonBHouses.Should().AllSatisfy(h =>
            h.House.Should().BeInRange(1, 12,
                "house overlay should be between 1 and 12"));

        result.PersonBInPersonAHouses.Should().AllSatisfy(h =>
            h.House.Should().BeInRange(1, 12,
                "house overlay should be between 1 and 12"));
    }

    [Fact]
    public void Calculate_Should_UseSynastryOrbs_When_Calculated()
    {
        // Arrange & Act
        SynastryResult result = CalculateTestSynastry();

        // Assert — verify no aspect exceeds its synastry orb limit
        result.InterChartAspects.Should().AllSatisfy(a =>
        {
            double maxOrb = SynastryOrbs.GetOrb(a.Aspect.Type, a.Body1Name, a.Body2Name);
            a.Orb.Should().BeLessThanOrEqualTo(maxOrb,
                $"{a.Body1Name} {a.Aspect.Abbreviation} {a.Body2Name} orb {a.Orb:F2}° " +
                $"should not exceed the synastry limit of {maxOrb}°");
        });
    }

    [Fact]
    public void Calculate_Should_SetEngineVersion_When_Calculated()
    {
        // Arrange & Act
        SynastryResult result = CalculateTestSynastry();

        // Assert
        result.EngineVersion.Should().NotBeNullOrEmpty(
            "EngineVersion should contain version information");
        result.EngineVersion.Should().Contain("AstroCast",
            "EngineVersion should include the application name");
    }

    [Fact]
    public void Calculate_Should_SetCalculatedAt_When_Calculated()
    {
        // Arrange
        DateTime before = DateTime.UtcNow;

        // Act
        SynastryResult result = CalculateTestSynastry();

        // Assert
        result.CalculatedAt.Should().BeCloseTo(before, TimeSpan.FromMinutes(1),
            "CalculatedAt should be within 1 minute of the current time");
    }

    [Fact]
    public void Calculate_Should_PreserveBothChartResults_When_Calculated()
    {
        // Arrange & Act
        SynastryResult result = CalculateTestSynastry();

        // Assert
        result.PersonA.Input.Name.Should().Be("Person A");
        result.PersonB.Input.Name.Should().Be("Person B");
        result.PersonA.Positions.Should().HaveCount(12);
        result.PersonB.Positions.Should().HaveCount(12);
    }

    #region Asteroids and Lilith (branch AsteroidsExport, 1.6.0)

    /// <summary>The five, the only bodies a minor-point row's first name may carry.</summary>
    private static readonly string[] Five = ["Ceres", "Pallas", "Juno", "Vesta", "Lilith"];

    /// <summary>The targets a minor-point row's second name may carry: the Sun to Pluto, the Ascendant and the Midheaven.</summary>
    private static readonly string[] Targets =
        ["Sun", "Moon", "Mercury", "Venus", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune", "Pluto", "ASC", "MC"];

    /// <summary>J2000 NYC with the Secondary fixture, each chart with the switch as given.</summary>
    /// <param name="personAOn">Whether Person A, J2000 NYC, carries the five.</param>
    /// <param name="personBOn">Whether Person B, the Secondary fixture, carries the five.</param>
    /// <returns>The synastry.</returns>
    private SynastryResult CalculateGoldenPair(bool personAOn, bool personBOn) =>
        _synastryService.Calculate(
            _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: personAOn)),
            _engine.Calculate(GoldenFixtures.SecondaryNatalInput(includeMinorPoints: personBOn)));

    /// <summary>
    /// With both charts carrying the five, the minor-point aspects are Tier B's synastry list row for row
    /// (ast_truth_export.py): A's five to B's Sun to Pluto, B ASC and B MC, then B's five to A's, the five
    /// Ptolemaic aspects at 2° inclusive, the note "exact" below ten minutes, sorted by orb; ten rows, A Ceres
    /// conjunct B Uranus at 0.2078° the tightest, every orb within 1e-6°.
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_MinorPointAspectsMatchTierB()
    {
        List<MinorPointTierB.SynastryRow> rows = MinorPointTierB.LoadSynastry();
        rows.Should().HaveCount(10, "the list holds the ten aspects the export summary reports");

        SynastryResult result = CalculateGoldenPair(personAOn: true, personBOn: true);

        result.MinorPointAspects.Should().HaveCount(rows.Count);
        for (int i = 0; i < rows.Count; i++)
        {
            MinorPointTierB.SynastryRow row = rows[i];
            InterChartAspect aspect = result.MinorPointAspects[i];
            string because = $"of row {i + 1}, {row.Body1} {row.Aspect} {row.Body2}";

            aspect.Body1Name.Should().Be(row.Body1, because);
            aspect.Aspect.Type.Should().Be(row.Aspect, because);
            aspect.Body2Name.Should().Be(row.Body2, because);
            aspect.Orb.Should().BeApproximately(row.Orb, MinorPointTierB.OrbTolerance, because);
            (aspect.Note ?? string.Empty).Should().Be(row.Note, because);
        }
    }

    /// <summary>
    /// No row pairs two of the five, and none reaches the True Node, Chiron or the Part of Fortune: every row is
    /// one chart's minor point to the other chart's Sun to Pluto, Ascendant or Midheaven (R5, R6).
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_NoRowPairsTwoOfTheFive()
    {
        SynastryResult result = CalculateGoldenPair(personAOn: true, personBOn: true);

        result.MinorPointAspects.Should().NotBeEmpty();
        result.MinorPointAspects.Should().AllSatisfy(a =>
        {
            string[] first = a.Body1Name.Split(' ', 2);
            string[] second = a.Body2Name.Split(' ', 2);
            first[0].Should().BeOneOf("A", "B");
            second[0].Should().BeOneOf("A", "B");
            second[0].Should().NotBe(first[0], "a row joins the two charts");
            Five.Should().Contain(first[1], "the first name is one of the five");
            Targets.Should().Contain(second[1], "the second name is a target, never one of the five, the node, Chiron or the Lot");
            a.Orb.Should().BeLessThanOrEqualTo(2.0, "the orb is 2°, inclusive");
        });
        result.InterChartAspects.Should().NotContain(a => Five.Any(f => a.Body1Name.EndsWith(f, StringComparison.Ordinal) || a.Body2Name.EndsWith(f, StringComparison.Ordinal)),
            "the chart's own inter-chart aspects never read the five");
    }

    /// <summary>
    /// The list is empty unless both charts carry the five, and the ordinary inter-chart aspects are the same
    /// whichever charts do (R9).
    /// </summary>
    [Fact]
    public void Calculate_SwitchOffOnEitherChart_MinorPointAspectsEmpty()
    {
        SynastryResult bothOff = CalculateGoldenPair(personAOn: false, personBOn: false);
        SynastryResult onlyAOn = CalculateGoldenPair(personAOn: true, personBOn: false);
        SynastryResult onlyBOn = CalculateGoldenPair(personAOn: false, personBOn: true);
        SynastryResult bothOn = CalculateGoldenPair(personAOn: true, personBOn: true);

        bothOff.MinorPointAspects.Should().BeEmpty();
        onlyAOn.MinorPointAspects.Should().BeEmpty("Person B carries no five to aspect");
        onlyBOn.MinorPointAspects.Should().BeEmpty("Person A carries no five to aspect");
        bothOn.MinorPointAspects.Should().NotBeEmpty();
        foreach (SynastryResult result in new[] { onlyAOn, onlyBOn, bothOn })
        {
            result.InterChartAspects.Select(a => (a.Body1Name, a.Aspect.Type, a.Body2Name, a.Orb))
                .Should().Equal(bothOff.InterChartAspects.Select(a => (a.Body1Name, a.Aspect.Type, a.Body2Name, a.Orb)));
        }
    }

    #endregion
}
