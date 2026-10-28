using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Utilities;

/// <summary>
/// Pins <see cref="JulianDayUtility.LocalDayStartToJulianDay"/> (branch O1, 1.3.3): a local date begins at
/// the first instant its midnight reads — the earlier instant of a repeated midnight, the first instant after
/// the gap of a skipped one — so a range of local dates covers each of its days whole and no hour of the
/// next. D17 (a repeated reading is the later instant) stays the rule for a chart's reading, and
/// <see cref="JulianDayUtility.LocalMidnightToJulianDay"/> keeps it.
/// </summary>
/// <remarks>
/// Expected instants come from Python <c>zoneinfo</c> over the IANA 2026e data (<c>o1-bounds/truth/zone_figures.py</c>):
/// the first UTC minute whose reading falls on the date. Havana's clocks go back from 01:00 CDT to 00:00 CST on
/// 1 November 2026, so its midnight repeats; they go forward from 00:00 CST to 01:00 CDT on 8 March 2026, and
/// Cairo's from 00:00 EET to 01:00 EEST on 24 April 2026, so those midnights never occur.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class LocalDayStartTests
{
    private readonly EphemerisService _ephemeris;
    private readonly TzdbTimeZoneRules _rules = new();

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public LocalDayStartTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _ephemeris = fixture.EphemerisService;
    }

    /// <summary>The first instant of each local date, ordinary, repeated and skipped midnights alike.</summary>
    /// <param name="zone">The IANA zone.</param>
    /// <param name="date">The local date, yyyy-MM-dd.</param>
    /// <param name="expectedUtc">The first instant whose reading falls on the date, yyyy-MM-ddTHH:mm UT.</param>
    [Theory]
    [InlineData("America/Havana", "2026-10-30", "2026-10-30T04:00")]
    [InlineData("America/Havana", "2026-11-01", "2026-11-01T04:00")]
    [InlineData("America/Havana", "2028-11-05", "2028-11-05T04:00")]
    [InlineData("America/Havana", "2026-03-08", "2026-03-08T05:00")]
    [InlineData("America/Havana", "2026-03-09", "2026-03-09T04:00")]
    [InlineData("Africa/Cairo", "2026-04-24", "2026-04-23T22:00")]
    [InlineData("Africa/Cairo", "2026-10-30", "2026-10-29T22:00")]
    [InlineData("America/New_York", "2026-11-01", "2026-11-01T04:00")]
    public void LocalDayStartToJulianDay_IsTheFirstInstantTheDatesMidnightReads(string zone, string date, string expectedUtc)
    {
        double jd = JulianDayUtility.LocalDayStartToJulianDay(_ephemeris, DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), _rules, zone);

        JulianDayUtility.ToDateTimeUtc(_ephemeris, jd).Should().Be(
            DateTime.SpecifyKind(DateTime.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc));
    }

    /// <summary>
    /// Havana's 30 and 31 October 2026 are 48 hours long: from 30 Oct 00:00 CDT to the first 00:00 of
    /// 1 November, also CDT. Until 1.3.3 the range ended at the second 00:00 (CST) and scanned 49 hours.
    /// </summary>
    [Fact]
    public void LocalDayStartToJulianDay_HavanasLastTwoDaysOfOctober_Are48HoursLong()
    {
        double start = JulianDayUtility.LocalDayStartToJulianDay(_ephemeris, new DateOnly(2026, 10, 30), _rules, "America/Havana");
        double end = JulianDayUtility.LocalDayStartToJulianDay(_ephemeris, new DateOnly(2026, 11, 1), _rules, "America/Havana");

        ((end - start) * 24.0).Should().BeApproximately(48.0, 1e-6);
    }

    /// <summary>
    /// The reading's rule is unchanged: <see cref="JulianDayUtility.LocalMidnightToJulianDay"/> still reads a
    /// repeated midnight as its later instant (D17), 00:00 CST on 1 November 2026 at Havana.
    /// </summary>
    [Fact]
    public void LocalMidnightToJulianDay_StillReadsARepeatedMidnightAsTheLaterInstant()
    {
        double jd = JulianDayUtility.LocalMidnightToJulianDay(_ephemeris, new DateOnly(2026, 11, 1), _rules, "America/Havana");

        JulianDayUtility.ToDateTimeUtc(_ephemeris, jd).Should().Be(new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc));
    }
}
