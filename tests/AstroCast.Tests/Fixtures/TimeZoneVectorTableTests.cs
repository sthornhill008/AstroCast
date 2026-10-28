using FluentAssertions;

namespace AstroCast.Tests.Fixtures;

/// <summary>
/// Tests for <see cref="TimeZoneVectorTable"/>: the committed table is the 2026e table
/// its generator writes, with every set complete. The conversion tests compare against
/// it, so a truncated or regenerated-at-the-wrong-release table must fail here first.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TimeZoneVectorTableTests
{
    /// <summary>The version line names IANA 2026e and the tzdata package it came from.</summary>
    [Fact]
    public void VersionLine_Should_NameIana2026e_And_Tzdata2026_5()
    {
        TimeZoneVectorTable.VersionLine.Should().StartWith("# IANA tzdb 2026e:")
            .And.Contain("tzdata 2026.5");
    }

    /// <summary>
    /// The three sets hold 1,612, 1,822 and 1,044 rows: 4,478 in all, over 13 zones each.
    /// </summary>
    [Fact]
    public void Rows_Should_HoldEverySet_Complete()
    {
        IReadOnlyList<TimeZoneVector> rows = TimeZoneVectorTable.Rows;

        rows.Should().HaveCount(4478);
        rows.Count(r => r.Set == "grid").Should().Be(1612);
        rows.Count(r => r.Set == "edges").Should().Be(1822);
        rows.Count(r => r.Set == "lmt").Should().Be(1044);

        foreach (string set in new[] { "grid", "edges", "lmt" })
            rows.Where(r => r.Set == set).Select(r => r.Zone).Distinct().Should().HaveCount(13, set);

        TimeZoneVectorTable.ZoneSets.Should().HaveCount(39);
    }

    /// <summary>
    /// Every edge is a repeated or a skipped reading (913 and 909), and every grid and
    /// local-mean-time reading occurs exactly once.
    /// </summary>
    [Fact]
    public void Rows_Should_CarryEveryKind_WhereExpected()
    {
        IReadOnlyList<TimeZoneVector> rows = TimeZoneVectorTable.Rows;

        rows.Count(r => r.Set == "edges" && r.Kind == "repeated").Should().Be(913);
        rows.Count(r => r.Set == "edges" && r.Kind == "skipped").Should().Be(909);
        rows.Where(r => r.Set != "edges").Should().OnlyContain(r => r.Kind == "unique");
    }

    /// <summary>
    /// Each row is self-consistent: away from a skipped reading, the reading is the
    /// instant plus the offset. A skipped reading lies inside the gap, so the instant
    /// read on its own offset, the one after the gap, is later than the reading.
    /// </summary>
    [Fact]
    public void Rows_Should_AgreeWithTheirOwnOffsets()
    {
        foreach (TimeZoneVector r in TimeZoneVectorTable.Rows)
        {
            DateTime reading = DateTime.SpecifyKind(r.ExpectedUtc.AddSeconds(r.OffsetSeconds), DateTimeKind.Unspecified);
            if (r.Kind == "skipped")
                reading.Should().BeAfter(r.Local, $"{r.Zone} {r.Local:s} is skipped, so the gap ends after it");
            else
                reading.Should().Be(r.Local, $"{r.Zone} {r.Local:s} ({r.Set})");
        }
    }
}
