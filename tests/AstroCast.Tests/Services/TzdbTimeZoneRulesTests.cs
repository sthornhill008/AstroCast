using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Services;
using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="TzdbTimeZoneRules"/>, the app's own IANA time-zone data
/// (NodaTime 3.3.5, tzdb 2026e) behind <see cref="ITimeZoneRules"/>.
/// </summary>
/// <remarks>
/// Every expected instant, offset and name comes from outside the code: the table
/// <see cref="TimeZoneVectorTable"/> and the five inputs below were worked out by
/// Python's <c>zoneinfo</c> over <c>tzdata</c> 2026.5 (IANA 2026e). The aliases are
/// IANA's <c>backward</c> links and the Windows ID is CLDR's <c>windowsZones.xml</c>.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class TzdbTimeZoneRulesTests
{
    private readonly TzdbTimeZoneRules _rules = new();

    #region The data

    /// <summary>The data is IANA 2026e, the release the truth table was generated from.</summary>
    [Fact]
    public void DataVersion_Should_Be2026e_TheTablesRelease()
    {
        _rules.DataVersion.Should().Be("2026e").And.Be(TimeZoneVectorTable.DataVersion);
    }

    /// <summary>
    /// The zones offered are zone.tab's 418, one per populated region, in ordinal order;
    /// America/Vancouver among them.
    /// </summary>
    [Fact]
    public void ZoneIds_Should_BeZoneTabs418_InOrdinalOrder()
    {
        _rules.ZoneIds.Should().HaveCount(418)
            .And.OnlyHaveUniqueItems()
            .And.BeInAscendingOrder(StringComparer.Ordinal)
            .And.Contain(["America/Vancouver", "America/New_York", "Europe/London", "Asia/Kolkata"])
            .And.NotContain(["EST5EDT", "US/Eastern", "Etc/UTC", "UTC"]);
    }

    #endregion

    #region The vectors

    /// <summary>
    /// Every reading of one zone in one set converts to the instant, offset and name
    /// IANA 2026e gives, with the D17 mapping; and the instant reads back on the zone's
    /// clock as the reading, or, for a skipped reading, as the end of the gap.
    /// </summary>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="set">The table's set: <c>grid</c>, <c>edges</c> or <c>lmt</c>.</param>
    [Theory]
    [MemberData(nameof(TimeZoneVectorTable.ZoneSets), MemberType = typeof(TimeZoneVectorTable))]
    public void Vectors_Should_MatchIana2026e(string zone, string set)
    {
        List<string> mismatches = [];

        foreach (TimeZoneVector v in TimeZoneVectorTable.For(zone, set))
        {
            ZoneInstant instant = _rules.ToUniversal(v.Local, zone);
            ZoneReading reading = _rules.ToLocal(v.ExpectedUtc, zone);
            DateTime gapEnd = DateTime.SpecifyKind(v.ExpectedUtc.AddSeconds(v.OffsetSeconds), DateTimeKind.Unspecified);
            LocalTimeMapping mapping = v.Kind switch
            {
                "repeated" => LocalTimeMapping.Repeated,
                "skipped" => LocalTimeMapping.Skipped,
                _ => LocalTimeMapping.Unique,
            };

            if (instant.UtcDateTime != v.ExpectedUtc || instant.UtcDateTime.Kind != DateTimeKind.Utc
                || instant.Offset != TimeSpan.FromSeconds(v.OffsetSeconds)
                || instant.Abbreviation != v.Abbreviation
                || instant.Mapping != mapping
                || instant.ResolvedLocalDateTime != (mapping == LocalTimeMapping.Skipped ? gapEnd : v.Local))
            {
                mismatches.Add($"ToUniversal {v.Local:s}: {instant} (expected {v.ExpectedUtc:s}Z, {v.OffsetSeconds} s, {v.Abbreviation}, {v.Kind})");
            }

            if (reading.LocalDateTime != gapEnd || reading.LocalDateTime.Kind != DateTimeKind.Unspecified
                || reading.Offset != TimeSpan.FromSeconds(v.OffsetSeconds)
                || reading.Abbreviation != v.Abbreviation)
            {
                mismatches.Add($"ToLocal {v.ExpectedUtc:s}Z: {reading} (expected {gapEnd:s}, {v.OffsetSeconds} s, {v.Abbreviation})");
            }
        }

        mismatches.Should().BeEmpty($"every {set} reading in {zone} must convert as IANA 2026e does");
    }

    #endregion

    #region The five inputs (design §3.2)

    /// <summary>
    /// The five inputs of the design, each through <see cref="ITimeZoneRules.ToUniversal"/>:
    /// two repeated hours and a repeated Almaty hour take the later instant, the skipped
    /// New York hour the first instant after the gap, and New York before 1883 its local
    /// mean time to the second.
    /// </summary>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="local">The wall-clock reading, as <c>yyyy-MM-ddTHH:mm:ss</c>.</param>
    /// <param name="expectedUtc">zoneinfo's instant under D17.</param>
    /// <param name="offsetSeconds">zoneinfo's offset at that instant.</param>
    /// <param name="abbreviation">zoneinfo's name for that offset.</param>
    /// <param name="mapping">How the reading maps.</param>
    /// <param name="resolvedLocal">The wall clock at that instant.</param>
    [Theory]
    [InlineData("Europe/London", "1941-08-10T02:30:00", "1941-08-10T01:30:00", 3600, "BST", LocalTimeMapping.Repeated, "1941-08-10T02:30:00")]
    [InlineData("Asia/Almaty", "2003-10-26T02:30:00", "2003-10-25T20:30:00", 21600, "+06", LocalTimeMapping.Repeated, "2003-10-26T02:30:00")]
    [InlineData("America/New_York", "2026-11-01T01:30:00", "2026-11-01T06:30:00", -18000, "EST", LocalTimeMapping.Repeated, "2026-11-01T01:30:00")]
    [InlineData("America/New_York", "2026-03-08T02:30:00", "2026-03-08T07:00:00", -14400, "EDT", LocalTimeMapping.Skipped, "2026-03-08T03:00:00")]
    [InlineData("America/New_York", "1880-06-15T12:00:00", "1880-06-15T16:56:02", -17762, "LMT", LocalTimeMapping.Unique, "1880-06-15T12:00:00")]
    public void ToUniversal_Should_GiveTheD17Instant_ForTheFiveInputs(
        string zone, string local, string expectedUtc, int offsetSeconds, string abbreviation,
        LocalTimeMapping mapping, string resolvedLocal)
    {
        ZoneInstant result = _rules.ToUniversal(DateTime.Parse(local, System.Globalization.CultureInfo.InvariantCulture), zone);

        result.UtcDateTime.Should().Be(DateTime.SpecifyKind(
            DateTime.Parse(expectedUtc, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc));
        result.UtcDateTime.Kind.Should().Be(DateTimeKind.Utc);
        result.Offset.Should().Be(TimeSpan.FromSeconds(offsetSeconds));
        result.Abbreviation.Should().Be(abbreviation);
        result.Mapping.Should().Be(mapping);
        result.ResolvedLocalDateTime.Should().Be(DateTime.Parse(resolvedLocal, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A reading of any <see cref="DateTimeKind"/> is a wall-clock reading in the zone:
    /// a UTC-kind or local-kind value converts exactly as an unspecified one does.
    /// </summary>
    [Fact]
    public void ToUniversal_Should_ReadTheValueAsAWallClock_WhateverItsKind()
    {
        DateTime reading = new(2026, 7, 15, 12, 0, 0);

        ZoneInstant unspecified = _rules.ToUniversal(reading, "America/New_York");

        _rules.ToUniversal(DateTime.SpecifyKind(reading, DateTimeKind.Utc), "America/New_York").Should().Be(unspecified);
        _rules.ToUniversal(DateTime.SpecifyKind(reading, DateTimeKind.Local), "America/New_York").Should().Be(unspecified);
        unspecified.UtcDateTime.Should().Be(new DateTime(2026, 7, 15, 16, 0, 0, DateTimeKind.Utc));
    }

    #endregion

    #region The other side of a change of clock (F5)

    /// <summary>
    /// A repeated reading carries its earlier instant too, with the offset and name in force
    /// then, taken from the zone's own record: the Time Note names both, and an explicit
    /// offset of the earlier offset casts the chart there.
    /// </summary>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="local">The wall-clock reading, as <c>yyyy-MM-ddTHH:mm:ss</c>.</param>
    /// <param name="earlierUtc">zoneinfo's earlier instant (fold 0).</param>
    /// <param name="earlierOffsetSeconds">zoneinfo's offset at the earlier instant.</param>
    /// <param name="earlierName">zoneinfo's name for that offset.</param>
    [Theory]
    [InlineData("America/New_York", "2026-11-01T01:30:00", "2026-11-01T05:30:00", -14400, "EDT")]
    [InlineData("Europe/London", "1941-08-10T02:30:00", "1941-08-10T00:30:00", 7200, "BDST")]
    [InlineData("Asia/Almaty", "2003-10-26T02:30:00", "2003-10-25T19:30:00", 25200, "+07")]
    public void ToUniversal_Should_GiveTheEarlierInstant_OfARepeatedReading(
        string zone, string local, string earlierUtc, int earlierOffsetSeconds, string earlierName)
    {
        ZoneInstant result = _rules.ToUniversal(DateTime.Parse(local, System.Globalization.CultureInfo.InvariantCulture), zone);

        result.EarlierUtcDateTime.Should().Be(DateTime.SpecifyKind(
            DateTime.Parse(earlierUtc, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc));
        result.EarlierUtcDateTime?.Kind.Should().Be(DateTimeKind.Utc);
        result.EarlierOffset.Should().Be(TimeSpan.FromSeconds(earlierOffsetSeconds));
        result.EarlierAbbreviation.Should().Be(earlierName);
    }

    /// <summary>
    /// A skipped reading has no earlier instant, but carries the offset and name in force
    /// before the gap, so the gap can be read off the zone's own record: 02:00 EST to
    /// 03:00 EDT in New York on 2026-03-08.
    /// </summary>
    [Fact]
    public void ToUniversal_Should_GiveTheOffsetBeforeTheGap_OfASkippedReading()
    {
        ZoneInstant result = _rules.ToUniversal(new DateTime(2026, 3, 8, 2, 30, 0), "America/New_York");

        result.EarlierUtcDateTime.Should().BeNull();
        result.EarlierOffset.Should().Be(TimeSpan.FromHours(-5));
        result.EarlierAbbreviation.Should().Be("EST");
    }

    /// <summary>A reading that occurs once has no other side, on standard time or local mean time.</summary>
    /// <param name="local">The wall-clock reading in New York.</param>
    [Theory]
    [InlineData("2026-06-15T12:00:00")]
    [InlineData("1880-06-15T12:00:00")]
    public void ToUniversal_Should_GiveNoOtherSide_ForAUniqueReading(string local)
    {
        ZoneInstant result = _rules.ToUniversal(DateTime.Parse(local, System.Globalization.CultureInfo.InvariantCulture), "America/New_York");

        result.EarlierUtcDateTime.Should().BeNull();
        result.EarlierOffset.Should().BeNull();
        result.EarlierAbbreviation.Should().BeNull();
    }

    #endregion

    #region Identifiers: aliases, case, Windows IDs, unknown IDs (card 5)

    /// <summary>
    /// An alias in IANA's <c>backward</c> file is known, canonicalizes to its target, and
    /// converts as its target does.
    /// </summary>
    /// <param name="alias">The alias.</param>
    /// <param name="canonical">Its canonical zone.</param>
    [Theory]
    [InlineData("US/Eastern", "America/New_York")]
    [InlineData("Asia/Calcutta", "Asia/Kolkata")]
    [InlineData("Europe/Kiev", "Europe/Kyiv")]
    public void Alias_Should_BeKnown_AndConvertAsItsCanonicalZone(string alias, string canonical)
    {
        DateTime reading = new(1990, 7, 15, 12, 0, 0);

        _rules.IsKnown(alias).Should().BeTrue();
        _rules.Canonicalize(alias).Should().Be(canonical);
        _rules.Canonicalize(canonical).Should().Be(canonical);
        _rules.ToUniversal(reading, alias).UtcDateTime.Should().Be(_rules.ToUniversal(reading, canonical).UtcDateTime);
    }

    /// <summary>
    /// A zone named in another case is the same zone: the platforms' own lookups have
    /// always matched without regard to case, and a stored chart may carry such a name.
    /// </summary>
    [Fact]
    public void ZoneId_Should_MatchWithoutRegardToCase()
    {
        DateTime reading = new(2026, 7, 15, 12, 0, 0);

        _rules.IsKnown("america/new_york").Should().BeTrue();
        _rules.Canonicalize("america/new_york").Should().Be("America/New_York");
        _rules.ToUniversal(reading, "america/new_york").Should().Be(_rules.ToUniversal(reading, "America/New_York"));
    }

    /// <summary>
    /// A Windows ID is not tzdb data, but converts through CLDR's mapping, and the
    /// translation is logged once.
    /// </summary>
    [Fact]
    public void WindowsId_Should_ConvertThroughTheCldrMapping_WithALogLine()
    {
        RecordingSink sink = new();
        TzdbTimeZoneRules rules = new(new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger());
        DateTime reading = new(2026, 1, 15, 12, 0, 0);

        rules.IsKnown("Eastern Standard Time").Should().BeFalse();
        rules.Canonicalize("Eastern Standard Time").Should().Be("America/New_York");
        rules.ToUniversal(reading, "Eastern Standard Time").Should().Be(rules.ToUniversal(reading, "America/New_York"));
        rules.ToLocal(new DateTime(2026, 1, 15, 17, 0, 0, DateTimeKind.Utc), "Eastern Standard Time")
            .Should().Be(rules.ToLocal(new DateTime(2026, 1, 15, 17, 0, 0, DateTimeKind.Utc), "America/New_York"));

        sink.Events.Should().ContainSingle(e => e.RenderMessage(null).Contains("Eastern Standard Time")
            && e.RenderMessage(null).Contains("America/New_York"));
    }

    /// <summary>
    /// An ID that is neither tzdb data nor a Windows ID is refused, naming the ID and the
    /// data version. There is no fallback to UTC or to the machine's zone.
    /// </summary>
    [Fact]
    public void UnknownId_Should_BeRefused_NamingTheIdAndTheDataVersion()
    {
        DateTime reading = new(2026, 1, 15, 12, 0, 0);

        _rules.IsKnown("Not/A/Zone").Should().BeFalse();
        _rules.Canonicalize("Not/A/Zone").Should().BeNull();

        Action toUniversal = () => _rules.ToUniversal(reading, "Not/A/Zone");
        Action toLocal = () => _rules.ToLocal(DateTime.SpecifyKind(reading, DateTimeKind.Utc), "Not/A/Zone");

        toUniversal.Should().Throw<TimeZoneNotFoundException>()
            .WithMessage("The time zone 'Not/A/Zone' is not in the app's time-zone data (tzdb 2026e).");
        toLocal.Should().Throw<TimeZoneNotFoundException>()
            .WithMessage("The time zone 'Not/A/Zone' is not in the app's time-zone data (tzdb 2026e).");
    }

    /// <summary>A blank zone is refused, not read as UTC.</summary>
    /// <param name="zone">A blank zone.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankId_Should_BeRefused(string zone)
    {
        Action act = () => _rules.ToUniversal(new DateTime(2026, 1, 15, 12, 0, 0), zone);

        act.Should().Throw<TimeZoneNotFoundException>().WithMessage("No time zone was given.");
        _rules.IsKnown(zone).Should().BeFalse();
        _rules.Canonicalize(zone).Should().BeNull();
    }

    #endregion

    /// <summary>Collects every log event written to it.</summary>
    private sealed class RecordingSink : ILogEventSink
    {
        /// <summary>The events, in the order written.</summary>
        public List<LogEvent> Events { get; } = [];

        /// <inheritdoc />
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
