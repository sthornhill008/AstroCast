using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="ChartTimeFormatter.FormatLocalTime"/>, which puts a chart's other
/// instants (its stations, its horary perfections) on the chart's own clock.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartTimeFormatterTests
{
    private readonly ChartResult _newYork1880;
    private readonly ChartResult _n1;

    /// <summary>Casts the New York 1880 chart and N1 (Julian 1300-03-15 12:00 UT at Greenwich, explicit offset 0) once, on the engine's default rules.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ChartTimeFormatterTests(EphemerisFixture fixture)
    {
        ChartEngine engine = GoldenFixtures.CreateChartEngine(fixture);
        _newYork1880 = engine.Calculate(new ChartInput
        {
            Name = "New York 1880",
            LocalDateTime = new DateTime(1880, 6, 15, 12, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
        });
        _n1 = engine.Calculate(new ChartInput
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
    }

    /// <summary>
    /// An instant before 1582-10-15 reads on the chart's clock on the calendar in force (branch R2, 1.3.4):
    /// N1's 1301 solar return, 1301-03-23T17:42:37Z as ticks, on a chart at explicit UTC+0 is
    /// "1301-03-15 17:42 UTC+0 (explicit)". Until 1.3.4 it read 1301-03-23, the proleptic label.
    /// </summary>
    [Fact]
    public void FormatLocalTime_BeforeTheSwitch_Should_ReadOnTheCalendarInForce()
    {
        ChartTimeFormatter.FormatLocalTime(new DateTime(1301, 3, 23, 17, 42, 37, DateTimeKind.Utc), _n1)
            .Should().Be("1301-03-15 17:42 UTC+0 (explicit)");
    }

    /// <summary>
    /// An instant on a chart in local mean time reads on that clock to the second: IANA
    /// 2026e gives New York −4:56:02 before 1883, so 16:56:02 UT is 12:00 and 04:56:01 UT
    /// the next day is 23:59 on 1880-06-15. On this Mac's own data the clock was a whole
    /// minute toward today's standard offset, which put the second one on the wrong day.
    /// </summary>
    [Fact]
    public void FormatLocalTime_Should_ReadTheInstant_OnTheChartsLocalMeanTime()
    {
        ChartTimeFormatter.FormatLocalTime(new DateTime(1880, 6, 15, 16, 56, 2, DateTimeKind.Utc), _newYork1880)
            .Should().Be("1880-06-15 12:00 (America/New_York)");
        ChartTimeFormatter.FormatLocalTime(new DateTime(1880, 6, 16, 4, 56, 1, DateTimeKind.Utc), _newYork1880)
            .Should().Be("1880-06-15 23:59 (America/New_York)");
    }
}
