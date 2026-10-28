using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// The civil-time rules of the world's time zones: the one place a wall-clock reading
/// becomes an instant, an instant becomes a wall-clock reading, and the zones offered for
/// a chart are listed.
/// </summary>
/// <remarks>
/// <para>
/// Every chart time the app converts goes through this seam, so every conversion uses the
/// same data on every machine. The implementation, <c>TzdbTimeZoneRules</c>, carries its own
/// copy of the IANA time-zone database; the operating system's copy is never consulted.
/// </para>
/// <para>
/// <b>Zone identifiers.</b> A tzdb identifier or alias is accepted in any letter case. A
/// Windows identifier is translated through CLDR's mapping. Anything else raises
/// <see cref="TimeZoneNotFoundException"/>: there is no fallback to UTC or to the machine's
/// zone, which would give a silently wrong instant.
/// </para>
/// </remarks>
public interface ITimeZoneRules
{
    /// <summary>The IANA release the rules come from, e.g. "2026e".</summary>
    string DataVersion { get; }

    /// <summary>
    /// The zones offered for a chart, in ordinal order: the IANA zone.tab identifiers, one
    /// per populated region (418 in 2026e).
    /// </summary>
    IReadOnlyList<string> ZoneIds { get; }

    /// <summary>
    /// Whether the data holds a zone identifier or alias, in any letter case. A Windows
    /// identifier is not tzdb data, so it is not known, though it converts.
    /// </summary>
    /// <param name="zoneId">The identifier to look up.</param>
    /// <returns><see langword="true"/> if the data holds the identifier.</returns>
    bool IsKnown(string zoneId);

    /// <summary>
    /// The canonical tzdb identifier of a zone: an alias's target ("US/Eastern" gives
    /// "America/New_York"), a Windows identifier's CLDR zone, or the identifier itself.
    /// </summary>
    /// <param name="zoneId">The identifier to canonicalize.</param>
    /// <returns>The canonical identifier, or <see langword="null"/> if the zone is not known.</returns>
    string? Canonicalize(string zoneId);

    /// <summary>
    /// The instant a wall-clock reading in a zone denotes, under rule D17: a repeated
    /// reading denotes the later of its two instants, and a skipped reading the first
    /// instant after the gap.
    /// </summary>
    /// <param name="localDateTime">
    /// The wall-clock reading. Its <see cref="DateTime.Kind"/> is ignored: the value is a
    /// reading on the zone's clock, not an instant.
    /// </param>
    /// <param name="zoneId">The zone the reading is on.</param>
    /// <returns>The instant, the offset and name in force then, and how the reading mapped.</returns>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when <paramref name="zoneId"/> is blank or names no zone the data holds.
    /// </exception>
    ZoneInstant ToUniversal(DateTime localDateTime, string zoneId);

    /// <summary>
    /// An instant read on a zone's clock.
    /// </summary>
    /// <param name="utcDateTime">The instant. It is read as UTC whatever its <see cref="DateTime.Kind"/>.</param>
    /// <param name="zoneId">The zone whose clock to read.</param>
    /// <returns>The wall clock, and the offset and name in force at the instant.</returns>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when <paramref name="zoneId"/> is blank or names no zone the data holds.
    /// </exception>
    ZoneReading ToLocal(DateTime utcDateTime, string zoneId);
}
