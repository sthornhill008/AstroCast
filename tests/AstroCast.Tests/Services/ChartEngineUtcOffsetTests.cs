using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests verifying that <see cref="ChartEngine"/> correctly computes
/// <see cref="ChartResult.ResolvedUtcOffsetHours"/> and
/// <see cref="ChartResult.ResolvedOffsetLabel"/> for a range of timezone scenarios:
/// explicit UTC offsets, IANA standard/DST transitions, half-hour offsets (IST),
/// UTC itself, and timezones whose abbreviation is not in the lookup table.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartEngineUtcOffsetTests
{
    private readonly IChartEngine _engine;

    private const double NycLat = 40.7128;
    private const double NycLon = -74.0060;
    private const double KolkataLat = 22.5726;
    private const double KolkataLon = 88.3639;

    /// <summary>
    /// Unicode minus sign used in offset labels (U+2212, distinct from ASCII hyphen-minus).
    /// </summary>
    private const string Minus = "\u2212";

    /// <summary>
    /// Receives the shared ephemeris fixture and constructs a fully-wired
    /// <see cref="ChartEngine"/> using all production services.
    /// </summary>
    public ChartEngineUtcOffsetTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        DignityService dignity = new();
        _engine = new ChartEngine(
            fixture.EphemerisService, aspects, midpoints,
            dignity, new MoonConditionService(),
            new ChartContextService(dignity, new SectService()),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(), new PlanetConditionService(),
            new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()), new AntisciaService(),
            new StationService(), new ParanService(fixture.EphemerisService, logger),
            logger);
    }

    #region Explicit UTC Offset

    /// <summary>
    /// Explicit offset: ResolvedUtcOffsetHours must equal the value from
    /// <see cref="ChartInput.UtcOffsetHours"/> exactly.
    /// </summary>
    [Fact]
    public void Calculate_ExplicitOffset_Should_SetResolvedUtcOffsetHours()
    {
        ChartInput input = new()
        {
            Name = "Explicit Offset Test",
            LocalDateTime = new DateTime(2000, 1, 1, 9, 0, 0),
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
            TimeZone = "America/New_York",
            Latitude = NycLat,
            Longitude = NycLon,
            LocationName = "New York City",
            EventType = "Test",
        };

        ChartResult result = _engine.Calculate(input);

        result.ResolvedUtcOffsetHours.Should().Be(-5.0);
    }

    /// <summary>
    /// Explicit offset label must be "UTC{sign}{offset} (explicit)" with no
    /// timezone abbreviation prefix, regardless of the TimeZone field value.
    /// </summary>
    [Fact]
    public void Calculate_ExplicitOffset_Label_Should_ContainExplicitSuffix()
    {
        ChartInput input = new()
        {
            Name = "Explicit Label Test",
            LocalDateTime = new DateTime(2000, 1, 1, 9, 0, 0),
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
            TimeZone = "America/New_York",
            Latitude = NycLat,
            Longitude = NycLon,
            LocationName = "New York City",
            EventType = "Test",
        };

        ChartResult result = _engine.Calculate(input);

        result.ResolvedOffsetLabel.Should().Be($"UTC{Minus}5 (explicit)");
    }

    /// <summary>
    /// Explicit zero offset: label must be "UTC+0 (explicit)".
    /// </summary>
    [Fact]
    public void Calculate_ExplicitZeroOffset_Label_Should_BeUtcPlus0Explicit()
    {
        ChartInput input = new()
        {
            Name = "Explicit Zero Offset Test",
            LocalDateTime = new DateTime(2000, 6, 15, 12, 0, 0),
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            TimeZone = "Etc/UTC",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London",
            EventType = "Test",
        };

        ChartResult result = _engine.Calculate(input);

        result.ResolvedUtcOffsetHours.Should().Be(0.0);
        result.ResolvedOffsetLabel.Should().Be("UTC+0 (explicit)");
    }

    /// <summary>
    /// Explicit positive fractional offset: label must include the fractional
    /// hour value (e.g., "UTC+5.5 (explicit)").
    /// </summary>
    [Fact]
    public void Calculate_ExplicitHalfHourOffset_Label_Should_IncludeFraction()
    {
        ChartInput input = new()
        {
            Name = "Explicit +5.5h Test",
            LocalDateTime = new DateTime(2000, 6, 15, 12, 0, 0),
            UseExplicitOffset = true,
            UtcOffsetHours = 5.5,
            TimeZone = "Asia/Kolkata",
            Latitude = KolkataLat,
            Longitude = KolkataLon,
            LocationName = "Kolkata",
            EventType = "Test",
        };

        ChartResult result = _engine.Calculate(input);

        result.ResolvedUtcOffsetHours.Should().BeApproximately(5.5, precision: 0.01);
        result.ResolvedOffsetLabel.Should().Be("UTC+5.5 (explicit)");
    }

    #endregion

    #region IANA Timezone Resolution

    /// <summary>
    /// IANA standard time (Eastern Standard Time, UTC−5): offset must be −5.0
    /// and label must include the "EST" abbreviation.
    /// </summary>
    [Fact]
    public void Calculate_IanaEst_Should_SetOffsetAndLabel()
    {
        ChartInput input = new()
        {
            Name = "EST Test",
            LocalDateTime = new DateTime(2000, 1, 15, 12, 0, 0),  // January = standard time
            TimeZone = "America/New_York",
            Latitude = NycLat,
            Longitude = NycLon,
            LocationName = "New York City",
            EventType = "Test",
        };

        ChartResult result = _engine.Calculate(input);

        result.ResolvedUtcOffsetHours.Should().Be(-5.0);
        result.ResolvedOffsetLabel.Should().Be($"EST (UTC{Minus}5)");
    }

    /// <summary>
    /// IANA daylight saving time (Eastern Daylight Time, UTC−4): offset must be −4.0
    /// and label must include the "EDT" abbreviation.
    /// </summary>
    [Fact]
    public void Calculate_IanaEdt_Should_SetOffsetAndLabel()
    {
        ChartInput input = new()
        {
            Name = "EDT Test",
            LocalDateTime = new DateTime(2000, 7, 4, 12, 0, 0),  // July = DST
            TimeZone = "America/New_York",
            Latitude = NycLat,
            Longitude = NycLon,
            LocationName = "New York City",
            EventType = "Test",
        };

        ChartResult result = _engine.Calculate(input);

        result.ResolvedUtcOffsetHours.Should().Be(-4.0);
        result.ResolvedOffsetLabel.Should().Be($"EDT (UTC{Minus}4)");
    }

    /// <summary>
    /// Half-hour IANA offset (Asia/Kolkata = IST, UTC+5:30 = +5.5h): offset must
    /// be 5.5 and label must be "IST (UTC+5.5)".
    /// </summary>
    [Fact]
    public void Calculate_HalfHourOffset_Ist_Should_SetOffsetAndLabel()
    {
        ChartInput input = new()
        {
            Name = "IST Test",
            LocalDateTime = new DateTime(2000, 6, 15, 12, 0, 0),
            TimeZone = "Asia/Kolkata",
            Latitude = KolkataLat,
            Longitude = KolkataLon,
            LocationName = "Kolkata",
            EventType = "Test",
        };

        ChartResult result = _engine.Calculate(input);

        result.ResolvedUtcOffsetHours.Should().BeApproximately(5.5, precision: 0.01);
        result.ResolvedOffsetLabel.Should().Be("IST (UTC+5.5)");
    }

    /// <summary>
    /// UTC timezone (Etc/UTC): offset must be 0.0 and label must be "UTC (UTC+0)".
    /// </summary>
    [Fact]
    public void Calculate_UtcTimezone_Should_SetZeroOffsetAndLabel()
    {
        ChartInput input = new()
        {
            Name = "UTC Test",
            LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
            TimeZone = "Etc/UTC",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London (UTC)",
            EventType = "Test",
        };

        ChartResult result = _engine.Calculate(input);

        result.ResolvedUtcOffsetHours.Should().Be(0.0);
        result.ResolvedOffsetLabel.Should().Be("UTC (UTC+0)");
    }

    /// <summary>
    /// A zone outside the old 21-entry abbreviation table is named from its zone record
    /// all the same: America/Vancouver in January is "PST" in IANA's data. The label used
    /// to fall back to a bare "UTC−8", because only America/Los_Angeles was in the table.
    /// </summary>
    [Fact]
    public void Calculate_ZoneOutsideTheOldAbbreviationTable_Should_TakeTheZoneRecordsName()
    {
        // America/Vancouver has the same offsets as America/Los_Angeles (-8/-7), and
        // the same names in its zone record.
        ChartInput input = new()
        {
            Name = "Unknown TZ Abbreviation Test",
            LocalDateTime = new DateTime(2000, 1, 15, 12, 0, 0),  // Standard time
            TimeZone = "America/Vancouver",
            Latitude = 49.2827,
            Longitude = -123.1207,
            LocationName = "Vancouver",
            EventType = "Test",
        };

        ChartResult result = _engine.Calculate(input);

        result.ResolvedUtcOffsetHours.Should().Be(-8.0);
        result.ResolvedOffsetLabel.Should().Be($"PST (UTC{Minus}8)");
    }

    #endregion

    #region Culture independence

    // The offset label is built with the invariant culture, so its en-US
    // expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="Calculate_ExplicitHalfHourOffset_Label_Should_IncludeFraction"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Calculate_ExplicitHalfHourOffset_Label_Should_IncludeFraction_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        Calculate_ExplicitHalfHourOffset_Label_Should_IncludeFraction();
    }

    /// <summary>
    /// <see cref="Calculate_HalfHourOffset_Ist_Should_SetOffsetAndLabel"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Calculate_HalfHourOffset_Ist_Should_SetOffsetAndLabel_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        Calculate_HalfHourOffset_Ist_Should_SetOffsetAndLabel();
    }

    #endregion
}
