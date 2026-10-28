using System.Globalization;

using AstroCast.Core.Data;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Data;

/// <summary>
/// Saving a chart converts its wall clock to an instant on the app's own IANA data
/// (tzdb 2026e), so the stored Julian Day is the instant IANA gives, on every machine.
/// </summary>
/// <remarks>
/// The expected instants are the truth table's (<see cref="TimeZoneVectorTable"/>),
/// worked out by Python's <c>zoneinfo</c> over <c>tzdata</c> 2026.5. Before the app
/// carried its own data the save path used the platform's: on this Mac that was IANA
/// 2026c, which gave a repeated hour .NET's own default instant, threw on 872 of the 909
/// skipped readings and saved the other 37 at a wrong instant (TZ-2), and moved local
/// mean time to a whole minute.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartMapperTimeZoneTests
{
    private readonly EphemerisService _ephemeris;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ChartMapperTimeZoneTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
    }

    /// <summary>
    /// Every reading of one zone in one set saves at its IANA 2026e instant: the stored
    /// UTC time to the second, and the Julian Day of that instant.
    /// </summary>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="set">The table's set: <c>grid</c>, <c>edges</c> or <c>lmt</c>.</param>
    [Theory]
    [MemberData(nameof(TimeZoneVectorTable.ZoneSets), MemberType = typeof(TimeZoneVectorTable))]
    public void ToEntity_Should_SaveEveryReading_AtItsIana2026eInstant(string zone, string set)
    {
        List<string> mismatches = [];

        foreach (TimeZoneVector v in TimeZoneVectorTable.For(zone, set))
        {
            ChartInput input = Input(zone, v.Local);
            double expectedJd = _ephemeris.DateToJulianDay(
                v.ExpectedUtc.Year, v.ExpectedUtc.Month, v.ExpectedUtc.Day,
                v.ExpectedUtc.Hour + v.ExpectedUtc.Minute / 60.0 + v.ExpectedUtc.Second / 3600.0);

            try
            {
                ChartEntity entity = ChartMapper.ToEntity(input, _ephemeris);
                DateTime stored = DateTime.Parse(entity.UtcDateTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                if (stored != v.ExpectedUtc || stored.Kind != DateTimeKind.Utc || entity.JulianDay != expectedJd)
                    mismatches.Add($"{v.Local:s} ({v.Kind}): saved {entity.UtcDateTime}, JD {entity.JulianDay:R}; expected {v.ExpectedUtc:s}Z, JD {expectedJd:R}");
            }
            catch (Exception ex)
            {
                mismatches.Add($"{v.Local:s} ({v.Kind}): {ex.GetType().Name}: {ex.Message}");
            }
        }

        mismatches.Should().BeEmpty($"every {set} reading in {zone} must save at its IANA 2026e instant");
    }

    /// <summary>
    /// A chart in a skipped hour saves, at the first instant after the gap: New York
    /// 2026-03-08 02:30 is 07:00 UT (03:00 EDT). It used to throw (TZ-2), so a chart that
    /// calculated could not be kept.
    /// </summary>
    [Fact]
    public void ToEntity_Should_SaveASkippedHourChart_AtTheFirstInstantAfterTheGap()
    {
        ChartInput input = Input("America/New_York", new DateTime(2026, 3, 8, 2, 30, 0));

        ChartEntity entity = ChartMapper.ToEntity(input, _ephemeris);

        entity.UtcDateTime.Should().Be("2026-03-08T07:00:00.0000000Z");
        entity.JulianDay.Should().Be(_ephemeris.DateToJulianDay(2026, 3, 8, 7.0));
        entity.LocalDateTime.Should().Be("2026-03-08T02:30:00.0000000", "the reading the user entered is kept as entered");
    }

    /// <summary>A chart input for a reading in a zone.</summary>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="local">The wall-clock reading.</param>
    /// <returns>The input.</returns>
    private static ChartInput Input(string zone, DateTime local) => new()
    {
        Name = "TZ vector",
        LocalDateTime = local,
        TimeZone = zone,
        Latitude = 0.0,
        Longitude = 0.0,
        LocationName = "Vector",
        EventType = "Event",
    };
}
