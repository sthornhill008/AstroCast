using System.Collections.Concurrent;

using NodaTime;
using NodaTime.TimeZones;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// <see cref="ITimeZoneRules"/> over the IANA time-zone database the app carries: the tzdb
/// release embedded in NodaTime (3.3.5 embeds 2026e).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the app carries its own data.</b> .NET reads time-zone rules from the operating
/// system: the system's zoneinfo files on macOS, which lag IANA by however long the OS
/// update takes, and the Windows registry, which keeps no history at all. The same chart
/// therefore cast to different instants on different machines. With the data inside the
/// app, every machine converts every chart time the same way, as IANA does. The data is
/// updated by rebuilding with a newer NodaTime; there is no data file to ship beside it.
/// </para>
/// <para>
/// <b>D17.</b> A wall-clock reading that occurs twice denotes the later instant; one that
/// never occurs denotes the first instant after the gap. The mapping is reported with the
/// instant, so a caller can say what happened, together with the other side of the change
/// of clock: the earlier instant of a repeated reading, and the offset and name in force
/// before the clocks changed.
/// </para>
/// <para>
/// <b>Identifiers</b> resolve in this order: a tzdb identifier or alias, matched without
/// regard to case as the platforms' own lookups always were; then a Windows identifier,
/// through CLDR's mapping, with one log line the first time each is translated; otherwise
/// <see cref="TimeZoneNotFoundException"/>, naming the identifier and the data version.
/// </para>
/// <para>
/// The instance is safe to share across threads. Its only state is caches: the zone cache,
/// and the set of Windows identifiers already logged.
/// </para>
/// </remarks>
public sealed class TzdbTimeZoneRules : ITimeZoneRules
{
    private readonly TzdbDateTimeZoneSource _source;
    private readonly IDateTimeZoneProvider _provider;
    private readonly Dictionary<string, string> _idsIgnoringCase;
    private readonly ConcurrentDictionary<string, string> _translatedWindowsIds = new(StringComparer.Ordinal);
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises the rules over the tzdb data embedded in NodaTime.
    /// </summary>
    /// <param name="logger">
    /// Optional Serilog logger, for the line written when a Windows identifier is
    /// translated. Without one the translation still happens, silently.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the embedded data carries no zone.tab, so no zones could be offered.
    /// </exception>
    public TzdbTimeZoneRules(ILogger? logger = null)
    {
        _source = TzdbDateTimeZoneSource.Default;
        _provider = new DateTimeZoneCache(_source);
        _logger = logger?.ForContext<TzdbTimeZoneRules>();

        IList<TzdbZoneLocation> zoneTab = _source.ZoneLocations
            ?? throw new InvalidOperationException(
                $"The embedded time-zone data (tzdb {_source.TzdbVersion}) carries no zone.tab.");

        ZoneIds = zoneTab
            .Select(location => location.ZoneId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

        _idsIgnoringCase = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in _source.GetIds())
            _idsIgnoringCase.TryAdd(id, id);
    }

    /// <inheritdoc />
    public string DataVersion => _source.TzdbVersion;

    /// <inheritdoc />
    public IReadOnlyList<string> ZoneIds { get; }

    /// <inheritdoc />
    public bool IsKnown(string zoneId) =>
        !string.IsNullOrWhiteSpace(zoneId) && _idsIgnoringCase.ContainsKey(zoneId);

    /// <inheritdoc />
    public string? Canonicalize(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            return null;

        if (_idsIgnoringCase.TryGetValue(zoneId, out string? id))
            return _source.CanonicalIdMap[id];

        if (_source.WindowsMapping.PrimaryMapping.TryGetValue(zoneId, out string? mapped))
            return _source.CanonicalIdMap.TryGetValue(mapped, out string? canonical) ? canonical : mapped;

        return null;
    }

    /// <inheritdoc />
    public ZoneInstant ToUniversal(DateTime localDateTime, string zoneId)
    {
        DateTimeZone zone = ResolveZone(zoneId);
        ZoneLocalMapping mapping = zone.MapLocal(LocalDateTime.FromDateTime(localDateTime));

        // D17: the later of two instants; for none, the first instant after the gap, which
        // is where the interval after the gap begins.
        (ZonedDateTime zoned, LocalTimeMapping kind) = mapping.Count switch
        {
            1 => (mapping.Single(), LocalTimeMapping.Unique),
            2 => (mapping.Last(), LocalTimeMapping.Repeated),
            _ => (new ZonedDateTime(mapping.LateInterval.Start, zone), LocalTimeMapping.Skipped),
        };

        ZoneInterval interval = zone.GetZoneInterval(zoned.ToInstant());

        // The other side of the change of clock, from the zone's own record: the earlier
        // instant of a repeated reading, and the interval in force before the clocks changed.
        DateTime? earlierUtc = kind == LocalTimeMapping.Repeated ? mapping.First().ToDateTimeUtc() : null;
        ZoneInterval? before = kind == LocalTimeMapping.Unique ? null : mapping.EarlyInterval;

        return new ZoneInstant(
            zoned.ToDateTimeUtc(),
            zoned.Offset.ToTimeSpan(),
            interval.Name,
            kind,
            zoned.LocalDateTime.ToDateTimeUnspecified(),
            earlierUtc,
            before?.WallOffset.ToTimeSpan(),
            before?.Name);
    }

    /// <inheritdoc />
    public ZoneReading ToLocal(DateTime utcDateTime, string zoneId)
    {
        DateTimeZone zone = ResolveZone(zoneId);
        Instant instant = Instant.FromDateTimeUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc));
        ZoneInterval interval = zone.GetZoneInterval(instant);

        return new ZoneReading(
            instant.InZone(zone).LocalDateTime.ToDateTimeUnspecified(),
            interval.WallOffset.ToTimeSpan(),
            interval.Name);
    }

    /// <summary>
    /// Finds the zone an identifier names: a tzdb identifier or alias in any case, then a
    /// Windows identifier through CLDR's mapping.
    /// </summary>
    /// <param name="zoneId">The identifier.</param>
    /// <returns>The zone.</returns>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when <paramref name="zoneId"/> is blank or names no zone the data holds.
    /// </exception>
    private DateTimeZone ResolveZone(string zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            throw new TimeZoneNotFoundException("No time zone was given.");

        if (_idsIgnoringCase.TryGetValue(zoneId, out string? id))
            return _provider[id];

        if (_source.WindowsMapping.PrimaryMapping.TryGetValue(zoneId, out string? mapped))
        {
            if (_translatedWindowsIds.TryAdd(zoneId, mapped))
            {
                _logger?.Information(
                    "Time zone '{WindowsId}' is a Windows identifier; converting it as '{IanaId}' through the CLDR mapping in tzdb {DataVersion}",
                    zoneId, mapped, DataVersion);
            }

            return _provider[mapped];
        }

        throw new TimeZoneNotFoundException(
            $"The time zone '{zoneId}' is not in the app's time-zone data (tzdb {DataVersion}).");
    }
}
