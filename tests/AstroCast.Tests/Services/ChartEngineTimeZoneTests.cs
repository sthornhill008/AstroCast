using System.Globalization;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// A chart is cast on the app's own IANA data (tzdb 2026e): the instant of its wall clock
/// under D17, the offset to the second, and a label built from the zone record's name.
/// </summary>
/// <remarks>
/// <para>
/// The five inputs are the design's (§3.2). Their instants, offsets and names are
/// Python <c>zoneinfo</c>'s over <c>tzdata</c> 2026.5; the labels follow card 6 (the zone
/// record's name when it is a word; whole and half hours as hours, anything else as h:mm
/// or h:mm:ss).
/// </para>
/// <para>
/// On this Mac's own data (2026c) four of the five were wrong: the London and Almaty
/// repeated hours took today's base offset, the skipped New York hour was labelled
/// "UTC−4.5", and New York in 1880 was moved to a whole minute and labelled "EDT".
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartEngineTimeZoneTests
{
    private readonly EphemerisFixture _fixture;
    private readonly ChartEngine _engine;

    /// <summary>Receives the shared ephemeris fixture and builds a fully wired engine.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ChartEngineTimeZoneTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _engine = GoldenFixtures.CreateChartEngine(fixture);
    }

    /// <summary>
    /// Each of the five inputs casts at its D17 instant, with the offset in force then, the
    /// card-6 label, the mapping and the resolved wall clock; and saving the chart stores
    /// the same Julian Day the engine cast it at.
    /// </summary>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="local">The wall-clock reading.</param>
    /// <param name="expectedUtc">zoneinfo's instant under D17.</param>
    /// <param name="offsetSeconds">zoneinfo's offset at that instant.</param>
    /// <param name="label">The expected offset label.</param>
    /// <param name="mapping">How the reading maps.</param>
    /// <param name="resolvedLocal">The wall clock at the instant.</param>
    [Theory]
    [InlineData("Europe/London", "1941-08-10T02:30:00", "1941-08-10T01:30:00", 3600, "BST (UTC+1)", LocalTimeMapping.Repeated, "1941-08-10T02:30:00")]
    [InlineData("Asia/Almaty", "2003-10-26T02:30:00", "2003-10-25T20:30:00", 21600, "UTC+6", LocalTimeMapping.Repeated, "2003-10-26T02:30:00")]
    [InlineData("America/New_York", "2026-11-01T01:30:00", "2026-11-01T06:30:00", -18000, "EST (UTC−5)", LocalTimeMapping.Repeated, "2026-11-01T01:30:00")]
    [InlineData("America/New_York", "2026-03-08T02:30:00", "2026-03-08T07:00:00", -14400, "EDT (UTC−4)", LocalTimeMapping.Skipped, "2026-03-08T03:00:00")]
    [InlineData("America/New_York", "1880-06-15T12:00:00", "1880-06-15T16:56:02", -17762, "LMT (UTC−4:56:02)", LocalTimeMapping.Unique, "1880-06-15T12:00:00")]
    public void Calculate_Should_CastAtTheD17Instant_WithTheZoneRecordsLabel(
        string zone, string local, string expectedUtc, int offsetSeconds, string label,
        LocalTimeMapping mapping, string resolvedLocal)
    {
        ChartInput input = new()
        {
            Name = "Five inputs",
            LocalDateTime = DateTime.Parse(local, CultureInfo.InvariantCulture),
            TimeZone = zone,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "Test",
            EventType = "Event",
        };
        DateTime utc = DateTime.SpecifyKind(DateTime.Parse(expectedUtc, CultureInfo.InvariantCulture), DateTimeKind.Utc);
        double expectedJd = _fixture.EphemerisService.DateToJulianDay(
            utc.Year, utc.Month, utc.Day, utc.Hour + utc.Minute / 60.0 + utc.Second / 3600.0);

        ChartResult result = _engine.Calculate(input);

        result.UtcDateTime.Should().Be(utc);
        result.JulianDay.Should().Be(expectedJd);
        result.ResolvedUtcOffsetHours.Should().Be(offsetSeconds / 3600.0);
        result.ResolvedOffsetLabel.Should().Be(label);
        result.LocalTimeMapping.Should().Be(mapping);
        result.ResolvedLocalDateTime.Should().Be(DateTime.Parse(resolvedLocal, CultureInfo.InvariantCulture));
        ChartMapper.ToEntity(input, _fixture.EphemerisService).JulianDay.Should().Be(result.JulianDay,
            "a saved chart must store the instant the engine cast it at");
    }

    /// <summary>
    /// A chart records the rules it was cast with, so its exports convert its other
    /// instants on the same data.
    /// </summary>
    [Fact]
    public void Calculate_Should_RecordTheRulesTheChartWasCastWith()
    {
        TzdbTimeZoneRules rules = new();
        ChartEngine engine = new(
            _fixture.EphemerisService,
            new AspectService(_fixture.EphemerisService, Serilog.Core.Logger.None),
            new MidpointService(Serilog.Core.Logger.None),
            new DignityService(),
            new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(_fixture.EphemerisService, Serilog.Core.Logger.None),
            new DeclinationAspectService(),
            new PlanetConditionService(),
            new MoonMansionService(),
            new PlanetaryHourCalculator(_fixture.EphemerisService),
            new ArabicPartService(new DignityService()),
            new AntisciaService(),
            new StationService(),
            new ParanService(_fixture.EphemerisService, Serilog.Core.Logger.None),
            Serilog.Core.Logger.None,
            rules);

        ChartResult result = engine.Calculate(new ChartInput
        {
            Name = "Rules",
            LocalDateTime = new DateTime(2026, 7, 15, 12, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York",
            EventType = "Event",
        });

        result.TimeZoneRules.Should().BeSameAs(rules);

        ChartResult byDefault = _engine.Calculate(new ChartInput
        {
            Name = "Default rules",
            LocalDateTime = new DateTime(2026, 7, 15, 12, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York",
            EventType = "Event",
        });

        byDefault.TimeZoneRules.Should().BeOfType<TzdbTimeZoneRules>()
            .Which.DataVersion.Should().Be("2026e", "an engine given no rules uses the embedded data");
    }

    /// <summary>
    /// An explicit-offset chart is cast on its own offset, untouched by any zone's rules:
    /// its reading maps once and resolves to itself, and its label stays "(explicit)".
    /// </summary>
    [Fact]
    public void Calculate_ExplicitOffset_Should_MapOnce_AndResolveToItsOwnReading()
    {
        DateTime reading = new(2026, 3, 8, 2, 30, 0);

        ChartResult result = _engine.Calculate(new ChartInput
        {
            Name = "Explicit in New York's gap",
            LocalDateTime = reading,
            TimeZone = "America/New_York",
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York",
            EventType = "Event",
        });

        result.UtcDateTime.Should().Be(new DateTime(2026, 3, 8, 7, 30, 0, DateTimeKind.Utc));
        result.LocalTimeMapping.Should().Be(LocalTimeMapping.Unique);
        result.ResolvedLocalDateTime.Should().Be(reading);
        result.ResolvedOffsetLabel.Should().Be("UTC−5 (explicit)");
    }
}
