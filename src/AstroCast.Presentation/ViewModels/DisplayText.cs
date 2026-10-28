using System.Globalization;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models.PrimaryDirections;

namespace AstroCast.ViewModels;

/// <summary>
/// The text the screen shows for a value the view models expose as an enum or a stored record (1.4.0,
/// E3-6). Both heads reach it through their <c>DisplayTextConverter</c>, so the bound value stays the enum
/// or the entity and only the text shown changes.
/// </summary>
/// <remarks>
/// <para>
/// The Core enums' names live in Core (<see cref="DisplayNames"/>), where the exports read them; the names
/// here are the screen's own: the planetary-return find modes and the saved-charts sort fields, and the
/// saved-chart row. Every name is English under every culture, as the exports' are.
/// </para>
/// <para>
/// Until 1.4.0 the Houses, Relationship, Electional Type, Find by and Sort boxes showed the enum names
/// ("WholeSign", "ParentChild", "BusinessLaunch", "ByYear", "EventType") and the saved-chart row the stored
/// round-trip reading ("1947-10-05T09:30:00.0000000").
/// </para>
/// </remarks>
public static class DisplayText
{
    /// <summary>
    /// Gets the text the screen shows for a value: a name in words for the enums that have one, the
    /// saved-chart row for a <see cref="ChartEntity"/>, otherwise the value's own text.
    /// </summary>
    /// <param name="value">The bound value.</param>
    /// <returns>The text to show; empty for <see langword="null"/>.</returns>
    public static string Of(object? value) => value switch
    {
        null => string.Empty,
        HouseSystem system => system.DisplayName,
        RelationshipType relationship => relationship.DisplayName,
        ElectionType election => election.DisplayName,
        PrimaryDirectionKey key => key.DisplayName,
        ReturnFindMode mode => NameOf(mode),
        ChartSortField field => NameOf(field),
        ChartEntity chart => SavedChartDetail(chart),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>
    /// Gets a saved chart's second line in the saved-charts list: its event type by name, its place and its
    /// reading as typed, "Birth — Harrisburg, Pennsylvania, United States (1947-10-05 09:30:00)".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stored local reading is the chart's own wall clock as typed (CLAUDE.md, "A stored local time is a
    /// wall-clock reading"), read by <see cref="ChartMapper.ParseStoredLocalDateTime"/>, which ignores a
    /// legacy row's offset suffix, and printed as <c>yyyy-MM-dd HH:mm:ss</c> in the invariant culture. It does
    /// not go through <c>CalendarDateFormatter</c>: a typed reading prints as typed, and a stored reading
    /// before 1582-10-15 is already on the Julian calendar, so formatting it as an instant would move it a
    /// second time. A reading that does not parse prints as stored.
    /// </para>
    /// <para>
    /// The event type is <see cref="EventTypeHelper.GetDisplayName"/>'s when the stored string names an
    /// <see cref="EventType"/> ("JobStart" reads "Job / Career Start"); otherwise it prints as stored.
    /// </para>
    /// </remarks>
    /// <param name="chart">The saved chart.</param>
    /// <returns>The row's text.</returns>
    public static string SavedChartDetail(ChartEntity chart)
    {
        ArgumentNullException.ThrowIfNull(chart);

        return $"{EventTypeName(chart.EventType)} — {chart.LocationName} ({StoredReading(chart.LocalDateTime)})";
    }

    /// <summary>
    /// Names a chart's event type as stored: <see cref="EventTypeHelper.GetDisplayName"/>'s name when the string
    /// names an <see cref="EventType"/> ("JobStart" reads "Job / Career Start"), otherwise the string itself.
    /// </summary>
    /// <param name="storedEventType">The event type as a chart stores it.</param>
    /// <returns>The name to show.</returns>
    internal static string EventTypeName(string storedEventType) =>
        EventTypeHelper.TryParse(storedEventType, out EventType parsed)
            ? EventTypeHelper.GetDisplayName(parsed)
            : storedEventType;

    /// <summary>
    /// Prints a stored local reading as typed: read by <see cref="ChartMapper.ParseStoredLocalDateTime"/>, which
    /// ignores a legacy row's offset suffix, then <see cref="Reading"/>; a reading that does not parse prints
    /// as stored.
    /// </summary>
    /// <param name="storedLocalDateTime">The reading as a chart stores it.</param>
    /// <returns>The reading to show.</returns>
    internal static string StoredReading(string storedLocalDateTime)
    {
        try
        {
            return Reading(ChartMapper.ParseStoredLocalDateTime(storedLocalDateTime));
        }
        catch (FormatException)
        {
            return storedLocalDateTime;
        }
    }

    /// <summary>
    /// Prints a chart's local reading as typed, <c>yyyy-MM-dd HH:mm:ss</c> in the invariant culture, never
    /// through <c>CalendarDateFormatter</c>: a reading before 1582-10-15 is already on the Julian calendar.
    /// </summary>
    /// <param name="reading">The chart's local reading.</param>
    /// <returns>The reading to show.</returns>
    internal static string Reading(DateTime reading) =>
        reading.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>Names a planetary-return find mode as the Find by box's tooltip does.</summary>
    /// <param name="mode">The find mode.</param>
    /// <returns>"By Year" or "By Return #".</returns>
    private static string NameOf(ReturnFindMode mode) => mode switch
    {
        ReturnFindMode.ByYear => "By Year",
        ReturnFindMode.ByReturnNumber => "By Return #",
        _ => mode.ToString(),
    };

    /// <summary>Names a saved-charts sort field.</summary>
    /// <param name="field">The sort field.</param>
    /// <returns>"Name", "Date", "Event Type" or "Created".</returns>
    private static string NameOf(ChartSortField field) => field switch
    {
        ChartSortField.Name => "Name",
        ChartSortField.Date => "Date",
        ChartSortField.EventType => "Event Type",
        ChartSortField.CreatedAt => "Created",
        _ => field.ToString(),
    };
}
