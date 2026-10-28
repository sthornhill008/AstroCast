using AstroCast.Core.Interfaces;

namespace AstroCast.Core.Services;

/// <summary>
/// Provides a sorted list of IANA timezone identifiers for UI selection, and
/// maps zone identifiers from elsewhere (a place lookup, the machine) into it.
/// </summary>
/// <remarks>
/// <para>
/// The list is computed once and cached. Every entry is in the IANA
/// "Area/City" format (e.g., "America/New_York", "Europe/London")
/// that AstroCast uses for chart calculations.
/// </para>
/// <para>
/// The zones are those of the app's own IANA time-zone data
/// (<see cref="TzdbTimeZoneRules"/>): zone.tab's identifiers, one per populated
/// region, 418 in tzdb 2026e, the same on every platform. They used to come from
/// the operating system, which gave 418 on macOS and 131 on Windows, America/Vancouver
/// not among them.
/// </para>
/// </remarks>
public static class TimezoneProvider
{
    /// <summary>
    /// Cached sorted list of IANA timezone identifiers.
    /// </summary>
    private static readonly Lazy<IReadOnlyList<string>> s_timezoneIds = new(BuildTimezoneList);

    /// <summary>
    /// The part of an <c>/etc/localtime</c> link target that precedes the zone identifier.
    /// </summary>
    private const string ZoneinfoMarker = "zoneinfo/";

    /// <summary>
    /// Gets all available IANA timezone identifiers, sorted alphabetically.
    /// Each entry is a valid timezone string that can be used for chart
    /// calculations (e.g., "America/New_York", "Europe/London").
    /// </summary>
    /// <returns>Sorted, deduplicated list of IANA timezone identifiers.</returns>
    public static IReadOnlyList<string> GetTimezoneIds() => s_timezoneIds.Value;

    /// <summary>
    /// Maps a zone identifier from elsewhere to one of the offered zones: the identifier
    /// itself if it is offered, in the list's own spelling; else its canonical zone if that
    /// is offered (an IANA alias such as "Asia/Calcutta", or a Windows identifier through
    /// CLDR's mapping); else none.
    /// </summary>
    /// <param name="zoneId">The identifier, or <see langword="null"/>.</param>
    /// <param name="rules">The time-zone rules whose zones are offered.</param>
    /// <returns>The offered zone, or <see langword="null"/> if none matches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
    public static string? FindOfferedZone(string? zoneId, ITimeZoneRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (string.IsNullOrWhiteSpace(zoneId))
            return null;

        string trimmed = zoneId.Trim();
        string? offered = rules.ZoneIds.FirstOrDefault(id => id.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
        if (offered is not null)
            return offered;

        string? canonical = rules.Canonicalize(trimmed);
        return canonical is null
            ? null
            : rules.ZoneIds.FirstOrDefault(id => id.Equals(canonical, StringComparison.Ordinal));
    }

    /// <summary>
    /// The machine's zone among the offered ones, for a new chart's default: the zone
    /// <c>/etc/localtime</c> links to (macOS, Linux), else the platform's local zone (on
    /// Windows a Windows identifier, mapped through CLDR); blank if neither is offered.
    /// </summary>
    /// <param name="localtimeLinkTarget">
    /// Where <c>/etc/localtime</c> links, e.g. <c>/var/db/timezone/zoneinfo/America/New_York</c>,
    /// or <see langword="null"/> where it is not a link or does not exist.
    /// </param>
    /// <param name="localZoneId">The platform's local zone identifier, <see cref="TimeZoneInfo.Id"/> of <see cref="TimeZoneInfo.Local"/>.</param>
    /// <param name="rules">The time-zone rules whose zones are offered.</param>
    /// <returns>The offered zone, or <see cref="string.Empty"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
    /// <remarks>
    /// The link is tried first because .NET does not always name the zone it links to: on
    /// macOS the link points outside the directory .NET searches, so .NET identifies the zone
    /// by comparing file contents and can return another zone with the same contents. This
    /// Mac's local zone reads "EST5EDT", which is not offered and, since tzdb 2026d, is not
    /// New York before 1966, while <c>/etc/localtime</c> links to America/New_York.
    /// </remarks>
    public static string FindMachineZone(string? localtimeLinkTarget, string? localZoneId, ITimeZoneRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        string? linked = null;
        if (!string.IsNullOrEmpty(localtimeLinkTarget))
        {
            string target = localtimeLinkTarget.Replace('\\', '/');
            int at = target.LastIndexOf(ZoneinfoMarker, StringComparison.Ordinal);
            if (at >= 0)
                linked = target[(at + ZoneinfoMarker.Length)..];
        }

        return FindOfferedZone(linked, rules) ?? FindOfferedZone(localZoneId, rules) ?? string.Empty;
    }

    /// <summary>
    /// Builds the sorted list of IANA timezone identifiers from the app's own
    /// time-zone data: zone.tab's "Area/City" identifiers, in ordinal order.
    /// </summary>
    private static IReadOnlyList<string> BuildTimezoneList() => new TzdbTimeZoneRules().ZoneIds;
}
