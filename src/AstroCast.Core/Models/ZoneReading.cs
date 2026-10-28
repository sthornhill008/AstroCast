namespace AstroCast.Core.Models;

/// <summary>
/// An instant read on a time zone's clock, with the offset and the zone record's name in
/// force at that instant.
/// </summary>
/// <param name="LocalDateTime">The wall clock, with <see cref="DateTimeKind.Unspecified"/>.</param>
/// <param name="Offset">The zone's offset from UTC at the instant, to the second.</param>
/// <param name="Abbreviation">
/// The zone record's name for that offset, exactly as the data gives it ("EDT", "LMT", "+06").
/// </param>
public sealed record ZoneReading(
    DateTime LocalDateTime,
    TimeSpan Offset,
    string Abbreviation);
