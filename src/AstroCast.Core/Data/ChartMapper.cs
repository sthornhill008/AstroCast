using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

namespace AstroCast.Core.Data;

/// <summary>
/// Converts between <see cref="ChartInput"/> (calculation model) and
/// <see cref="ChartEntity"/> (database entity). Handles timezone conversion
/// and Julian Day computation during the mapping.
/// </summary>
public static class ChartMapper
{
    /// <summary>
    /// Convert a <see cref="ChartInput"/> to a <see cref="ChartEntity"/>,
    /// computing the UTC date/time and Julian Day from the local datetime and timezone.
    /// </summary>
    /// <remarks>
    /// The local date/time is stored as the chart's own wall-clock reading in
    /// round-trip (<c>"o"</c>) form with <see cref="DateTimeKind.Unspecified"/>,
    /// so no UTC offset is written: <c>2026-07-19T09:06:30.0000000</c>. Formatting
    /// the value with its own kind stamped the saving machine's offset on any
    /// <see cref="DateTimeKind.Local"/> value — what a reloaded chart carried — so a
    /// London chart saved in New York read <c>…09:06:30.0000000-04:00</c>, an offset
    /// that belongs to neither the chart nor the instant. Rows written that way are
    /// still read correctly by <see cref="ParseStoredLocalDateTime"/>.
    /// <para>
    /// The UTC date/time is stored as UTC, with a <c>Z</c>, whether the chart is cast through
    /// its zone or at an explicit offset, whatever the input's <see cref="DateTimeKind"/>. An
    /// explicit-offset chart wrote the input's own kind: no suffix, or the saving machine's
    /// offset, which as an ISO instant is hours wrong (known issue 3). Rows written that way
    /// keep their text until they are saved again; nothing reads the column back.
    /// </para>
    /// <para>
    /// The instant is converted by <c>ChartEngine</c>'s own conversion, on the same
    /// time-zone rules and by the same rule (D17): a repeated reading takes the later
    /// instant and a skipped one the first instant after the gap, and the reading and an
    /// explicit offset are taken to the whole second. The stored Julian Day is therefore
    /// the instant the chart was calculated at, and a chart in a skipped hour saves rather
    /// than throwing.
    /// </para>
    /// <para>
    /// The minor-points switch, <see cref="ChartInput.IncludeMinorPoints"/>, is not mapped: it is a session
    /// value, and the row has no column for it (branch Asteroids, 1.6.0). <see cref="ToInput"/> takes it
    /// from its caller.
    /// </para>
    /// </remarks>
    /// <param name="input">The chart input with local datetime and timezone.</param>
    /// <param name="ephemeris">Ephemeris service for Julian Day computation.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules to convert with. Optional: when omitted, the IANA data the app
    /// carries (<see cref="TzdbTimeZoneRules"/>), the data <c>ChartEngine</c> uses by default.
    /// </param>
    /// <returns>A fully populated chart entity ready for database storage.</returns>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when the timezone identifier in <paramref name="input"/> is not valid.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the local date is one of 1582-10-05 to 1582-10-14, which never occurred,
    /// or the instant falls on 29 February UT of 1300, 1400 or 1500 on the Julian calendar,
    /// as <c>ChartEngine</c> refuses them.
    /// </exception>
    public static ChartEntity ToEntity(
        ChartInput input, IEphemerisService ephemeris, ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(ephemeris);

        // The chart engine's own conversion, so the stored instant and Julian Day are the
        // ones the chart is calculated at, to the whole second.
        (_, DateTime utcDateTime, double julianDay, _, _) =
            ChartEngine.ResolveInstant(input, ephemeris, timeZoneRules ?? new TzdbTimeZoneRules());

        string now = DateTime.UtcNow.ToString("o");

        return new ChartEntity
        {
            Name = input.Name,
            EventType = input.EventType,
            EventDescription = input.EventDescription,
            LocalDateTime = DateTime.SpecifyKind(input.LocalDateTime, DateTimeKind.Unspecified)
                .ToString("o", CultureInfo.InvariantCulture),
            TimeZone = input.TimeZone,
            UtcDateTime = CalendarDateFormatter.Iso(utcDateTime),
            JulianDay = julianDay,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            LocationName = input.LocationName,
            Gender = input.Gender.ToString(),
            SourceNotes = input.SourceNotes,
            UtcOffsetHours = input.UtcOffsetHours,
            UseExplicitOffset = input.UseExplicitOffset,
            Notes = input.Notes,
            Keywords = input.Keywords,
            HoraryQuestion = input.HoraryQuestion,
            HoraryCategory = input.HoraryCategory,
            HouseSystem = input.HouseSystem == Enums.HouseSystem.Placidus
                ? null
                : ((char)input.HouseSystem).ToString(),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>
    /// Convert a <see cref="ChartEntity"/> back to a <see cref="ChartInput"/>.
    /// </summary>
    /// <remarks>
    /// The stored local date/time is read by <see cref="ParseStoredLocalDateTime"/>:
    /// the wall-clock reading exactly as written, with
    /// <see cref="DateTimeKind.Unspecified"/>, whatever UTC offset an older row
    /// carries and whatever time zone this machine is in.
    /// </remarks>
    /// <param name="entity">The database entity to convert.</param>
    /// <param name="includeMinorPoints">
    /// The caller's session value for <see cref="ChartInput.IncludeMinorPoints"/>, never read from the row:
    /// the switch is not stored, so a loaded chart carries the minor points only when the session has it on
    /// (branch Asteroids, 1.6.0). Off by default.
    /// </param>
    /// <returns>A chart input suitable for recalculation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entity"/> is null.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when the stored local date/time is not a valid date/time string.
    /// </exception>
    public static ChartInput ToInput(ChartEntity entity, bool includeMinorPoints = false)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return new ChartInput
        {
            Name = entity.Name,
            LocalDateTime = ParseStoredLocalDateTime(entity.LocalDateTime),
            TimeZone = entity.TimeZone,
            Latitude = entity.Latitude,
            Longitude = entity.Longitude,
            LocationName = entity.LocationName,
            EventType = entity.EventType,
            EventDescription = entity.EventDescription,
            Gender = Enum.TryParse<Gender>(entity.Gender, out Gender g) ? g : Gender.Unspecified,
            SourceNotes = entity.SourceNotes,
            UtcOffsetHours = entity.UtcOffsetHours,
            UseExplicitOffset = entity.UseExplicitOffset,
            Notes = entity.Notes,
            Keywords = entity.Keywords,
            HoraryQuestion = entity.HoraryQuestion,
            HoraryCategory = entity.HoraryCategory,
            HouseSystem = string.IsNullOrEmpty(entity.HouseSystem)
                ? Enums.HouseSystem.Placidus
                : (Enums.HouseSystem)entity.HouseSystem[0],
            IncludeMinorPoints = includeMinorPoints,
        };
    }

    /// <summary>
    /// Read a stored <see cref="ChartEntity.LocalDateTime"/> as the chart's own
    /// wall-clock reading, exactly as written, ignoring any UTC offset suffix.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The clock part of the stored string is the chart's local date and time in
    /// its own time zone (<see cref="ChartEntity.TimeZone"/>). Rows written before
    /// 1.3.2 may also carry an offset suffix such as <c>-04:00</c>. That suffix is
    /// the offset of the machine that saved the row, stamped because the value was
    /// a <see cref="DateTimeKind.Local"/> date/time; it is not the chart's offset,
    /// and it is ignored. <c>2026-07-19T09:06:30.0000000-04:00</c>,
    /// <c>…+09:00</c> and <c>2026-07-19T09:06:30.0000000</c> all read as 09:06:30.
    /// </para>
    /// <para>
    /// Parsing with <see cref="DateTime.Parse(string)"/> instead converted a
    /// suffixed value into this machine's time zone. Outside the zone that saved
    /// it, the chart loaded hours off, and a re-save persisted the shift.
    /// Unsuffixed strings are read as universal so that no machine zone takes part
    /// at all.
    /// </para>
    /// </remarks>
    /// <param name="storedLocalDateTime">
    /// The stored local date/time, in round-trip (<c>"o"</c>) form with or without
    /// an offset suffix.
    /// </param>
    /// <returns>
    /// The wall-clock date/time as written, with <see cref="DateTimeKind.Unspecified"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="storedLocalDateTime"/> is null.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="storedLocalDateTime"/> is not a valid date/time string.
    /// </exception>
    public static DateTime ParseStoredLocalDateTime(string storedLocalDateTime)
    {
        ArgumentNullException.ThrowIfNull(storedLocalDateTime);

        return DateTimeOffset.Parse(
            storedLocalDateTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal).DateTime;
    }
}
