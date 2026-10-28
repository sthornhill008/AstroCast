using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// The transit report's stations include every body that can station (card O-D6 (b), accepted 2026-10-04): Mercury,
/// Venus and Mars always, as well as the slow bodies. The expected instants are Swiss Ephemeris stations computed
/// outside AstroCast (Tier B: the zero of the speed in longitude, pyswisseph over the ephemeris files).
/// </summary>
/// <remarks>
/// Until 1.3.3 only the active transit bodies were scanned, so the Stations table left out every station of Mercury
/// and Venus, and Mars's whenever Mars was not an active transit body, yet read as complete (E2-3).
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitStationsTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public TransitStationsTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Person A, transits 2026-08-30 to 2026-10-30: Venus stations retrograde on 2026-10-03 at 07:15:54 UT and Mercury
    /// on 2026-10-24 at 07:12:42 UT (Tier B), and both are listed, each within a minute.
    /// </summary>
    [Fact]
    public void TransitStations_IncludeMercuryAndVenus()
    {
        TransitResult result = Transits(new DateOnly(2026, 8, 30), new DateOnly(2026, 10, 30));

        AssertStation(result, TransitBody.Venus, "Retrograde", new DateTime(2026, 10, 3, 7, 15, 54, DateTimeKind.Utc));
        AssertStation(result, TransitBody.Mercury, "Retrograde", new DateTime(2026, 10, 24, 7, 12, 42, DateTimeKind.Utc));
    }

    /// <summary>
    /// Person A, transits 2026-12-01 to 2027-01-31: Mars moves 0.368°/day at the start (Tier B), above the 0.3°/day
    /// threshold, and stations retrograde on 2027-01-10 at 12:59:17 UT (Tier B). It is listed, within a minute.
    /// </summary>
    [Fact]
    public void TransitStations_IncludeMarsWhenFastAtStart()
    {
        TransitResult result = Transits(new DateOnly(2026, 12, 1), new DateOnly(2027, 1, 31));

        AssertStation(result, TransitBody.Mars, "Retrograde", new DateTime(2027, 1, 10, 12, 59, 17, DateTimeKind.Utc));
    }

    /// <summary>
    /// Asserts that the result lists exactly one station of the body of the given type on the expected instant's date,
    /// within a minute of it.
    /// </summary>
    /// <param name="result">The transit result.</param>
    /// <param name="body">The stationing body.</param>
    /// <param name="type">"Retrograde" or "Direct".</param>
    /// <param name="expectedUtc">The Tier B instant of the station.</param>
    private static void AssertStation(TransitResult result, TransitBody body, string type, DateTime expectedUtc)
    {
        List<TransitStation> stations = [.. result.Stations.Where(s => s.Planet == body && s.StationType == type)];
        stations.Should().ContainSingle($"{body} stations {type.ToLowerInvariant()} once in the window");
        TransitStation station = stations[0];
        station.Date.Should().Be(DateOnly.FromDateTime(expectedUtc));
        station.DateTimeUtc.Should().NotBeNull();
        station.DateTimeUtc.GetValueOrDefault().Should().BeCloseTo(expectedUtc, TimeSpan.FromMinutes(1));
    }

    /// <summary>The transits to person A over the given dates, major aspects, no progressions; the stations are sky events, whatever the natal chart.</summary>
    /// <param name="start">The report's first date.</param>
    /// <param name="end">The report's last date.</param>
    /// <returns>The transit result.</returns>
    private TransitResult Transits(DateOnly start, DateOnly end)
    {
        ChartResult natal = GoldenFixtures.CreateChartEngine(_fixture).Calculate(HartwellInput());
        return new TransitService(_fixture.EphemerisService, new LoggerConfiguration().CreateLogger()).Calculate(new TransitInput
        {
            NatalChart = natal,
            ReportStartDate = start,
            ReportEndDate = end,
            IncludeMinorAspects = false,
            IncludeSolarArcs = false,
            IncludeSecondaryProgressions = false,
        });
    }

    /// <summary>
    /// Person A's input (invented): 1947-10-05 09:30:00 at an explicit UTC−5 (JD UT 2432464.1041666665),
    /// Harrisburg, Pennsylvania (40.2737 N, 76.8844 W), Placidus.
    /// </summary>
    /// <returns>A new <see cref="ChartInput"/> for person A.</returns>
    private static ChartInput HartwellInput() => new()
    {
        Name = "Eleanor Hartwell",
        LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
        TimeZone = "America/New_York",
        UtcOffsetHours = -5.0,
        UseExplicitOffset = true,
        Latitude = 40.2737,
        Longitude = -76.8844,
        LocationName = "Harrisburg, Pennsylvania",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
    };
}
