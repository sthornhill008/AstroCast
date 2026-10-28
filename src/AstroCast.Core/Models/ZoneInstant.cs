using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// The instant a wall-clock reading in a time zone denotes, with the offset and the zone
/// record's name in force at that instant.
/// </summary>
/// <param name="UtcDateTime">
/// The instant, with <see cref="DateTimeKind.Utc"/>. For a repeated reading it is the later
/// of the two instants; for a skipped reading, the first instant after the gap.
/// </param>
/// <param name="Offset">
/// The zone's offset from UTC at that instant, to the second (local mean time carries
/// seconds: New York's is −4:56:02).
/// </param>
/// <param name="Abbreviation">
/// The zone record's name for that offset, exactly as the data gives it: a word such as
/// "EST", "BST" or "LMT", or a numeric name such as "+06" or "+0330".
/// </param>
/// <param name="Mapping">Whether the reading occurs once, twice, or not at all.</param>
/// <param name="ResolvedLocalDateTime">
/// The wall clock at <paramref name="UtcDateTime"/>, with <see cref="DateTimeKind.Unspecified"/>:
/// the reading itself unless it was skipped, when it is the reading at the end of the gap
/// (03:00 for a skipped 02:30 in New York).
/// </param>
/// <param name="EarlierUtcDateTime">
/// For a repeated reading, the earlier of its two instants, with <see cref="DateTimeKind.Utc"/>,
/// from the zone's own record (05:30 UT for 01:30 on 2026-11-01 in New York, whose later
/// instant is 06:30 UT). <see langword="null"/> for a unique or skipped reading.
/// </param>
/// <param name="EarlierOffset">
/// The offset in force before the clocks changed, to the second: at the earlier instant of a
/// repeated reading (EDT, −4), or just before the gap of a skipped one (EST, −5).
/// <see langword="null"/> for a unique reading.
/// </param>
/// <param name="EarlierAbbreviation">
/// The zone record's name for <paramref name="EarlierOffset"/>, exactly as the data gives it
/// ("EDT", "BDST", "+07"). <see langword="null"/> for a unique reading.
/// </param>
public sealed record ZoneInstant(
    DateTime UtcDateTime,
    TimeSpan Offset,
    string Abbreviation,
    LocalTimeMapping Mapping,
    DateTime ResolvedLocalDateTime,
    DateTime? EarlierUtcDateTime = null,
    TimeSpan? EarlierOffset = null,
    string? EarlierAbbreviation = null);
