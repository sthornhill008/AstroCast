using System.Reflection;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the electional score's ceiling (branch Q, 1.3.3): <see cref="ElectionalScorer.ScoreCeiling"/> is the sum,
/// over the seven dimensions, of the type's weight times that dimension's highest score, and each highest score is
/// reached by a case the scorer's own method scores.
/// </summary>
/// <remarks>
/// <para>
/// The highest scores come from the scorer's terms (Tier A): Moon 100, Ascendant 85 (50 for a type with no preferred
/// rising signs, Other), Significator 90 (50 + 25 + 15), Natal Harmony 70 (50 + 10 + 10), Malefic Avoidance 100,
/// Mercury 100 (80 + 25, held to 100), Planetary Hour 100. Weighted by each type's profile they give the nine
/// ceilings below, the synthesis's four figures among them; the Q investigation saw every one of these highest scores
/// in its sets, and no score above its type's ceiling.
/// </para>
/// <para>
/// No moment need reach the ceiling: for Contract, Surgery, RealEstateClosing, Other and CreativeLaunch none can, since
/// the dimensions share bodies. Each constructed case below reaches one dimension's highest score alone.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ElectionalScoreCeilingTests
{
    /// <summary>
    /// The ceiling for each type, Tier A: each dimension's highest score times the type's weight, summed.
    /// </summary>
    /// <param name="electionType">The election type.</param>
    /// <param name="expected">Its ceiling.</param>
    [Theory]
    [InlineData(ElectionType.BusinessLaunch, 93.5)]
    [InlineData(ElectionType.Contract, 94.0)]
    [InlineData(ElectionType.Wedding, 93.0)]
    [InlineData(ElectionType.Surgery, 95.5)]
    [InlineData(ElectionType.RealEstateListing, 93.5)]
    [InlineData(ElectionType.RealEstateClosing, 93.5)]
    [InlineData(ElectionType.CreativeLaunch, 92.75)]
    [InlineData(ElectionType.Travel, 94.0)]
    [InlineData(ElectionType.Other, 90.5)]
    public void ScoreCeiling_IsEachDimensionsHighestScore_WeightedForTheType(ElectionType electionType, double expected)
    {
        ElectionalScorer.ScoreCeiling(electionType).Should().Be(expected);
    }

    /// <summary>
    /// Each dimension's highest score is reached by a constructed case, scored through the scorer's own method, and
    /// equals the scorer's constant for it.
    /// </summary>
    /// <param name="dimension">The case.</param>
    /// <param name="constant">The name of the scorer's private constant for that dimension's highest score.</param>
    /// <param name="expected">The highest score, Tier A.</param>
    [Theory]
    [InlineData("Moon", "HighestMoonScore", 100.0)]
    [InlineData("Ascendant", "HighestAscendantScore", 85.0)]
    [InlineData("Ascendant, type Other", "HighestAscendantScoreWithoutPreferredSigns", 50.0)]
    [InlineData("Significator", "HighestSignificatorScore", 90.0)]
    [InlineData("Natal Harmony", "HighestNatalHarmonyScore", 70.0)]
    [InlineData("Malefic Avoidance", "HighestMaleficAvoidanceScore", 100.0)]
    [InlineData("Mercury", "HighestMercuryScore", 100.0)]
    [InlineData("Planetary Hour", "HighestPlanetaryHourScore", 100.0)]
    public void HighestScore_IsReachedByAConstructedCase_AndEqualsItsConstant(string dimension, string constant, double expected)
    {
        double scored = dimension switch
        {
            // Waxing for a waxing-preferring type (+15), in Cancer (+25), applying by trine to Jupiter (+10): 50 + 50.
            "Moon" => ElectionalScorer.ScoreMoon(
                new MoonCondition
                {
                    Longitude = 100.0,
                    Sign = "Cancer",
                    Phase = MoonPhaseType.WaxingCrescent,
                    PhasePercent = 20.0,
                    PhasePosition = "mid",
                    IsVoidOfCourse = false,
                    IsInViaCombusta = false,
                    NextApplyingAspectDescription = "Moon trine Jupiter (applying, 3°12′)",
                    DailyMotion = 13.0,
                },
                ElectionType.BusinessLaunch),

            // Leo rising, one of BusinessLaunch's preferred signs.
            "Ascendant" => ElectionalScorer.ScoreAscendant(Chart(135.0, StandardPositions()), ElectionType.BusinessLaunch),

            // Other has no preferred rising sign: every sign scores the same.
            "Ascendant, type Other" => ElectionalScorer.ScoreAscendant(Chart(135.0, StandardPositions()), ElectionType.Other),

            // Jupiter, BusinessLaunch's significator, in Sagittarius (domicile, +25), direct, 105° from the Sun, in the
            // 10th house (angular, +15).
            "Significator" => ElectionalScorer.ScoreSignificator(
                Chart(0.0, StandardPositions(("Jupiter", CelestialBodyId.Jupiter, 255.0, 0.1, 10))),
                ElectionType.BusinessLaunch),

            // Jupiter at 255°, trine natal Jupiter at 138° (3°) and sextile natal Venus at 193° (2°); natal Mars and
            // Saturn make no aspect to it within 5°.
            "Natal Harmony" => ElectionalScorer.ScoreNatalHarmony(
                Chart(0.0, StandardPositions(("Jupiter", CelestialBodyId.Jupiter, 255.0, 0.1, 10))),
                Chart(0.0, StandardPositions(
                    ("Venus", CelestialBodyId.Venus, 193.0, 1.2, 7),
                    ("Mars", CelestialBodyId.Mars, 20.0, 0.5, 1),
                    ("Jupiter", CelestialBodyId.Jupiter, 138.0, 0.1, 5),
                    ("Saturn", CelestialBodyId.Saturn, 45.0, 0.05, 2))),
                ElectionType.BusinessLaunch),

            // Ascendant 0°, Midheaven 270°: Mars at 120° and Saturn at 210° lie 30° from the nearest angle.
            "Malefic Avoidance" => ElectionalScorer.ScoreMaleficAvoidance(Chart(0.0, StandardPositions())),

            // Mercury in Gemini (+25), direct, 75° from the Sun: 80 + 25, held to 100.
            "Mercury" => ElectionalScorer.ScoreMercury(
                Chart(0.0, StandardPositions(("Mercury", CelestialBodyId.Mercury, 75.0, 1.5, 3)))),

            // The hour of BusinessLaunch's significator, Jupiter.
            "Planetary Hour" => ElectionalScorer.ScorePlanetaryHour(
                new PlanetaryHour
                {
                    RulingPlanet = "Jupiter",
                    HourNumber = 2,
                    IsDaytime = true,
                    HourStart = new DateTime(2026, 4, 20, 7, 0, 0, DateTimeKind.Unspecified),
                    HourEnd = new DateTime(2026, 4, 20, 8, 0, 0, DateTimeKind.Unspecified),
                },
                ElectionType.BusinessLaunch),

            _ => double.NaN,
        };

        scored.Should().Be(expected, "the constructed case reaches the dimension's highest score");
        Constant(constant).Should().Be(expected, "the scorer's constant is that highest score");
    }

    /// <summary>The value of one of the scorer's private constants, by reflection.</summary>
    /// <param name="name">The constant's name.</param>
    /// <returns>Its value, or <see cref="double.NaN"/> when the scorer has no such private constant.</returns>
    private static double Constant(string name)
    {
        FieldInfo? field = typeof(ElectionalScorer).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
        field.Should().NotBeNull("the scorer declares {0}", name);

        return field is { IsLiteral: true, IsPrivate: true } && field.GetRawConstantValue() is double value
            ? value
            : double.NaN;
    }

    /// <summary>
    /// Twelve positions none of the constructed cases is affected by unless it replaces them: the Sun at 0° Aries,
    /// Mars at 120° and Saturn at 210°, every body direct and in a succedent house.
    /// </summary>
    /// <param name="replacements">Positions that replace the body of the same name.</param>
    /// <returns>The positions.</returns>
    private static List<PlanetPosition> StandardPositions(
        params (string Name, CelestialBodyId Body, double Longitude, double Speed, int House)[] replacements)
    {
        (string Name, CelestialBodyId Body, double Longitude, double Speed, int House)[] standard =
        [
            ("Sun", CelestialBodyId.Sun, 0.0, 1.0, 5),
            ("Moon", CelestialBodyId.Moon, 40.0, 13.0, 2),
            ("Mercury", CelestialBodyId.Mercury, 20.0, 1.5, 5),
            ("Venus", CelestialBodyId.Venus, 50.0, 1.2, 2),
            ("Mars", CelestialBodyId.Mars, 120.0, 0.5, 5),
            ("Jupiter", CelestialBodyId.Jupiter, 150.0, 0.1, 5),
            ("Saturn", CelestialBodyId.Saturn, 210.0, 0.05, 8),
            ("Uranus", CelestialBodyId.Uranus, 240.0, 0.01, 8),
            ("Neptune", CelestialBodyId.Neptune, 300.0, 0.005, 11),
            ("Pluto", CelestialBodyId.Pluto, 330.0, 0.003, 11),
            ("True Node", CelestialBodyId.TrueNode, 160.0, -0.05, 5),
            ("Chiron", CelestialBodyId.Chiron, 15.0, 0.02, 2),
        ];

        List<PlanetPosition> positions = [];
        foreach ((string Name, CelestialBodyId Body, double Longitude, double Speed, int House) s in standard)
        {
            (string Name, CelestialBodyId Body, double Longitude, double Speed, int House) p = s;
            foreach ((string Name, CelestialBodyId Body, double Longitude, double Speed, int House) r in replacements)
            {
                if (r.Name == s.Name)
                {
                    p = r;
                }
            }

            PlanetPosition position = new()
            {
                Body = p.Body,
                Name = p.Name,
                Longitude = p.Longitude,
                Latitude = 0.0,
                Distance = 1.0,
                SpeedInLongitude = p.Speed,
                SpeedInLatitude = 0.0,
                SpeedInDistance = 0.0,
            };
            position.House = p.House;
            positions.Add(position);
        }

        return positions;
    }

    /// <summary>
    /// A chart with the given Ascendant, the Midheaven 90° before it, equal cusps from the Ascendant, and the given
    /// positions; no context or dignities, so the scorer reads its own tables.
    /// </summary>
    /// <param name="ascendant">The Ascendant's longitude.</param>
    /// <param name="positions">The positions.</param>
    /// <returns>The chart.</returns>
    private static ChartResult Chart(double ascendant, List<PlanetPosition> positions) => new()
    {
        Input = new ChartInput
        {
            Name = "Ceiling case",
            LocalDateTime = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Unspecified),
            TimeZone = "UTC",
            Latitude = 51.5,
            Longitude = 0.0,
            LocationName = "London",
        },
        JulianDay = 2461151.0,
        Obliquity = 23.4367,
        UtcDateTime = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc),
        Positions = positions,
        Houses = new HouseData
        {
            Cusps = [.. Enumerable.Range(0, 12).Select(i => (ascendant + i * 30.0) % 360.0)],
            Ascendant = ascendant,
            Midheaven = (ascendant + 270.0) % 360.0,
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
        ResolvedOffsetLabel = "UTC",
        EngineVersion = "Test 1.0.0",
        CalculatedAt = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
    };
}
