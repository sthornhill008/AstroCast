using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for the medical astrology data embedded in transit Markdown output.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransitMarkdownMedicalTests
{
    [Fact]
    public void ToMarkdown_SaturnTransit_ContainsMedicalTransitPlanet()
    {
        TransitResult result = CreateTestResult(TransitBody.Saturn, "Sun", "SU", "Capricorn");
        string md = TransitMarkdownFormatter.ToMarkdown(result);
        md.Should().Contain("Medical (transit planet)");
        md.Should().Contain("Bones");
    }

    [Fact]
    public void ToMarkdown_NatalSunTarget_ContainsMedicalNatalTarget()
    {
        TransitResult result = CreateTestResult(TransitBody.Saturn, "Sun", "SU", "Capricorn");
        string md = TransitMarkdownFormatter.ToMarkdown(result);
        md.Should().Contain("Medical (natal target)");
        md.Should().Contain("Heart");
    }

    [Fact]
    public void ToMarkdown_ActiveTransits_ContainsDisclaimer()
    {
        TransitResult result = CreateTestResult(TransitBody.Saturn, "Sun", "SU", "Capricorn");
        string md = TransitMarkdownFormatter.ToMarkdown(result);
        md.Should().Contain("Not medical advice");
    }

    [Fact]
    public void ToMarkdown_ChironTransit_ShowsNoCorrespondences()
    {
        TransitResult result = CreateTestResult(TransitBody.Chiron, "Sun", "SU", "Cancer");
        string md = TransitMarkdownFormatter.ToMarkdown(result);
        md.Should().Contain("no traditional medical correspondences");
    }

    [Fact]
    public void ToMarkdown_AscTarget_UsesSignRegions()
    {
        // ASC in Aries — should show Aries sign regions (Head, Eyes, etc.)
        TransitResult result = CreateTestResult(TransitBody.Saturn, "ASC", "ASC", "Aries");
        string md = TransitMarkdownFormatter.ToMarkdown(result);
        md.Should().Contain("sign regions:");
        md.Should().Contain("Head");
    }

    #region Helpers

    private static TransitResult CreateTestResult(
        TransitBody transitPlanet, string natalTarget, string natalSymbol, string natalSign)
    {
        ChartResult natal = CreateMinimalNatalChart();

        Dictionary<TransitBody, TransitPlanetPosition> positions = new()
        {
            [transitPlanet] = new TransitPlanetPosition
            {
                Longitude = 280.0, Sign = "Capricorn", Degrees = 10, Minutes = 0,
                IsRetrograde = false, DailyMotion = 0.06,
            }
        };

        List<ActiveTransit> transits =
        [
            new ActiveTransit
            {
                TransitPlanet = transitPlanet,
                NatalTarget = natalTarget,
                NatalTargetSymbol = natalSymbol,
                AspectName = "Conjunction",
                AspectAbbreviation = "cnj",
                TransitLongitudeAtStart = 280.0,
                TransitSignAtStart = "Capricorn",
                NatalLongitude = 280.0,
                NatalSign = natalSign,
                NatalHouseNumber = 10,
                OrbAtStartDegrees = 0.5,
                OrbDirection = "applying",
                TransitHouseNumber = 10,
                Lifecycle =
                [
                    new TransitLifecycleEvent
                    {
                        EventType = TransitLifecycleEventType.Exact,
                        Date = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc),
                        PassNumber = 1,
                    },
                ],
            },
        ];

        return new TransitResult
        {
            NatalChart = natal,
            ReportStartDate = new DateOnly(2026, 3, 1),
            ReportEndDate = new DateOnly(2026, 3, 31),
            MidpointDate = new DateOnly(2026, 3, 16),
            IncludeMinorAspects = false,
            TransitPositions = positions.AsReadOnly(),
            Stations = new List<TransitStation>().AsReadOnly(),
            Ingresses = new List<TransitIngress>().AsReadOnly(),
            ActiveTransits = transits.AsReadOnly(),
            HouseOverlays = new List<TransitHouseOverlay>().AsReadOnly(),
            MidpointActivations = new List<TransitMidpointActivation>().AsReadOnly(),
            MarsIncluded = false,
        };
    }

    private static ChartResult CreateMinimalNatalChart()
    {
        List<PlanetPosition> positions =
        [
            CreatePosition(CelestialBodyId.Sun, "Sun", 280.0, 10),
            CreatePosition(CelestialBodyId.Moon, "Moon", 112.0, 4),
            CreatePosition(CelestialBodyId.Mercury, "Mercury", 265.0, 9),
            CreatePosition(CelestialBodyId.Venus, "Venus", 300.0, 10),
            CreatePosition(CelestialBodyId.Mars, "Mars", 330.0, 11),
            CreatePosition(CelestialBodyId.Jupiter, "Jupiter", 25.0, 1),
            CreatePosition(CelestialBodyId.Saturn, "Saturn", 40.0, 1),
            CreatePosition(CelestialBodyId.Uranus, "Uranus", 315.0, 11),
            CreatePosition(CelestialBodyId.Neptune, "Neptune", 303.0, 10),
            CreatePosition(CelestialBodyId.Pluto, "Pluto", 251.0, 9),
            CreatePosition(CelestialBodyId.TrueNode, "True Node", 130.0, 5),
            CreatePosition(CelestialBodyId.Chiron, "Chiron", 175.0, 7),
        ];

        return new ChartResult
        {
            Input = new ChartInput
            {
                Name = "Test", LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
                TimeZone = "America/New_York", Latitude = 40.7, Longitude = -74.0,
                LocationName = "New York", EventType = "Birth",
            },
            JulianDay = 2451545.0,
            Obliquity = 23.4367,
            UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Positions = positions,
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
                Ascendant = 0.0, Midheaven = 270.0, Armc = 270.0, Vertex = 180.0,
            },
            Aspects = [], Midpoints = [], MidpointActivations = [],
            Dignities = [], MoonCondition = null, ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    private static PlanetPosition CreatePosition(
        CelestialBodyId body, string name, double longitude, int house)
    {
        return new PlanetPosition
        {
            Body = body, Name = name, Longitude = longitude,
            Latitude = 0.0, Distance = 1.0, SpeedInLongitude = 1.0,
            SpeedInLatitude = 0.0, SpeedInDistance = 0.0, House = house,
        };
    }

    #endregion
}
