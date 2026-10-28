using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Every export's engine version names the time-zone data beside the ephemeris, as
/// "AstroCast 1.3.3 (SwissEph 2.10.03, tzdb 2026e)" (card 2): the tz data decides a
/// chart's instant as much as the ephemeris decides its positions.
/// </summary>
/// <remarks>
/// One case per service that builds a version string, and synastry, which takes its
/// version from the first chart. The goldens mask the whole parenthesis, so they do not
/// see this; these tests do.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class EngineVersionTzdbTests
{
    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public EngineVersionTzdbTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The services whose results carry an engine version.</summary>
    public static TheoryData<string> Services => new()
    {
        "ChartEngine", "SynastryService", "CompositeService", "SolarReturnService", "LunarReturnService",
        "PlanetaryReturnService", "PlanetaryCycleTimelineService", "ReturnIngressTimelineService",
        "IngressService", "LunationService", "EclipseService", "SynodicCycleService",
        "GreatConjunctionService", "MundaneAspectService",
    };

    /// <summary>
    /// The service's engine version ends with the ephemeris version and the tzdb release,
    /// in one parenthesis.
    /// </summary>
    /// <param name="service">The service's name.</param>
    [Theory]
    [MemberData(nameof(Services))]
    public void EngineVersion_Should_NameTheTzdbRelease_BesideTheEphemeris(string service)
    {
        string version = VersionOf(service);

        version.Should().MatchRegex(@"^AstroCast \d+\.\d+\.\d+ \(SwissEph [^,()]+, tzdb 2026e\)$");
    }

    /// <summary>Runs the named service on a small job and returns its result's engine version.</summary>
    /// <param name="service">The service's name.</param>
    /// <returns>The engine version.</returns>
    private string VersionOf(string service)
    {
        EphemerisService eph = _fixture.EphemerisService;
        ChartEngine engine = GoldenFixtures.CreateChartEngine(_fixture);
        ChartResult natal = engine.Calculate(GoldenFixtures.J2000NycInput());
        const double Lat = 40.7128, Lon = -74.006;
        const string Place = "New York City", Zone = "America/New_York";

        return service switch
        {
            "ChartEngine" => natal.EngineVersion,
            "SynastryService" => new SynastryService(_logger)
                .Calculate(natal, engine.Calculate(GoldenFixtures.SecondaryNatalInput())).EngineVersion,
            "CompositeService" => new CompositeService(
                    eph, new AspectService(eph, _logger), new MidpointService(_logger), new DignityService(),
                    new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()),
                    new FixedStarService(eph, _logger), _logger)
                .Calculate(natal, engine.Calculate(GoldenFixtures.SecondaryNatalInput())).EngineVersion,
            "SolarReturnService" => new SolarReturnService(eph, engine, _logger)
                .Calculate(natal, 2026, Lat, Lon, Place, Zone).EngineVersion,
            "LunarReturnService" => new LunarReturnService(eph, engine, _logger)
                .Calculate(natal, new DateTime(2026, 4, 1), new DateTime(2026, 5, 1), Lat, Lon, Place, Zone)[0].EngineVersion,
            "PlanetaryReturnService" => new PlanetaryReturnService(eph, engine, _logger)
                .Calculate(natal, CelestialBodyId.Mars, 1, Lat, Lon, Place, Zone).EngineVersion,
            "PlanetaryCycleTimelineService" => new PlanetaryCycleTimelineService(
                    eph, new PlanetaryReturnService(eph, engine, _logger), _logger)
                .Calculate(natal, CelestialBodyId.Mars, 1).EngineVersion,
            "ReturnIngressTimelineService" => new ReturnIngressTimelineService(eph, _logger)
                .Calculate(natal, 2026, 2026).EngineVersion,
            "IngressService" => new IngressService(eph, engine, _logger)
                .FindIngresses(CelestialBodyId.Sun, 2026, 2026, true).EngineVersion,
            "LunationService" => new LunationService(eph, engine, _logger)
                .FindLunations(2026, 2026).EngineVersion,
            "EclipseService" => new EclipseService(eph, _logger)
                .FindEclipses(2026, 2026).EngineVersion,
            "SynodicCycleService" => new SynodicCycleService(eph, _logger)
                .FindConjunctions(CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2020, 2021).EngineVersion,
            "GreatConjunctionService" => new GreatConjunctionService(eph, engine, _logger)
                .FindConjunctions(2020, 2021).EngineVersion,
            "MundaneAspectService" => new MundaneAspectService(eph, _logger)
                .FindAspects(CelestialBodyId.Saturn, CelestialBodyId.Pluto, "90° Harmonic", 2019, 2021).EngineVersion,
            _ => throw new ArgumentOutOfRangeException(nameof(service), service, "no such service"),
        };
    }
}
