using System.ComponentModel;
using System.Reflection;

namespace AstroCast.Core.Enums;

/// <summary>
/// Utility methods for working with <see cref="EventType"/> enum values.
/// Provides display name lookups, string-to-enum conversion, and the
/// ordered list of event types for UI dropdowns.
/// </summary>
public static class EventTypeHelper
{
    /// <summary>
    /// Ordered list of all event types for UI display.
    /// Core chart types appear first, followed by life events,
    /// business/legal, creative/technology, political, and general.
    /// </summary>
    private static readonly EventType[] s_allEventTypes = Enum.GetValues<EventType>();

    /// <summary>
    /// Cached display names keyed by enum value.
    /// </summary>
    private static readonly Dictionary<EventType, string> s_displayNames = BuildDisplayNameCache();

    /// <summary>
    /// Cached reverse lookup: lowercase enum name string to EventType.
    /// </summary>
    private static readonly Dictionary<string, EventType> s_nameLookup = BuildNameLookup();

    /// <summary>
    /// Gets the display-friendly name for an event type, drawn from
    /// the <see cref="DescriptionAttribute"/> on each enum member.
    /// </summary>
    /// <param name="eventType">The event type enum value.</param>
    /// <returns>Human-readable display name (e.g., "Wedding / Marriage").</returns>
    public static string GetDisplayName(EventType eventType) =>
        s_displayNames.TryGetValue(eventType, out string? name) ? name : eventType.ToString();

    /// <summary>
    /// Gets all event types in display order as (value, displayName) pairs.
    /// Suitable for binding to ComboBox ItemsSource with
    /// <c>SelectedValuePath="Value"</c> and <c>DisplayMemberPath="DisplayName"</c>.
    /// The <see cref="EventTypeDisplayItem.Value"/> is the enum name string
    /// (e.g., "BusinessLaunch") matching the string stored in <c>ChartInput.EventType</c>.
    /// </summary>
    /// <returns>Ordered collection of event type display items.</returns>
    public static IReadOnlyList<EventTypeDisplayItem> GetDisplayItems()
    {
        return s_allEventTypes
            .Select(et => new EventTypeDisplayItem(et.ToString(), GetDisplayName(et)))
            .ToList();
    }

    /// <summary>
    /// Gets the core chart types in display order as (value, displayName) pairs.
    /// Returns only Birth, Event, Horary, and Electional -- the four primary
    /// chart categories used in UI dropdowns. Extended event types (Wedding,
    /// Surgery, etc.) remain in the enum for backward compatibility but are
    /// not shown in dropdowns; users should capture those details in the
    /// Notes or Keywords fields instead.
    /// </summary>
    /// <returns>Ordered collection of core event type display items.</returns>
    public static IReadOnlyList<EventTypeDisplayItem> GetCoreDisplayItems()
    {
        EventType[] coreTypes = [EventType.Birth, EventType.Event, EventType.Horary, EventType.Electional];
        return coreTypes
            .Select(et => new EventTypeDisplayItem(et.ToString(), GetDisplayName(et)))
            .ToList();
    }

    /// <summary>
    /// Attempts to parse a string event type (as stored in the database) to
    /// the <see cref="EventType"/> enum. Matching is case-insensitive.
    /// </summary>
    /// <param name="value">The string event type (e.g., "Wedding", "BusinessLaunch").</param>
    /// <param name="result">The parsed enum value, or <see cref="EventType.Event"/> if not found.</param>
    /// <returns><c>true</c> if the string was successfully parsed.</returns>
    public static bool TryParse(string? value, out EventType result)
    {
        if (value is not null && s_nameLookup.TryGetValue(value.ToLowerInvariant(), out result))
            return true;

        result = EventType.Event;
        return false;
    }

    /// <summary>
    /// Builds the display name cache from Description attributes.
    /// </summary>
    private static Dictionary<EventType, string> BuildDisplayNameCache()
    {
        Dictionary<EventType, string> cache = [];

        foreach (EventType value in Enum.GetValues<EventType>())
        {
            FieldInfo? field = typeof(EventType).GetField(value.ToString());
            DescriptionAttribute? attr = field?.GetCustomAttribute<DescriptionAttribute>();
            cache[value] = attr?.Description ?? value.ToString();
        }

        return cache;
    }

    /// <summary>
    /// Builds the reverse name lookup dictionary (lowercase name to enum value).
    /// </summary>
    private static Dictionary<string, EventType> BuildNameLookup()
    {
        Dictionary<string, EventType> lookup = new(StringComparer.OrdinalIgnoreCase);

        foreach (EventType value in Enum.GetValues<EventType>())
        {
            lookup[value.ToString().ToLowerInvariant()] = value;
        }

        return lookup;
    }
}

/// <summary>
/// Display item for an <see cref="EventType"/> value, suitable for data binding
/// in UI ComboBox controls. The <see cref="Value"/> is the enum name string
/// (e.g., "BusinessLaunch") that is stored in <c>ChartInput.EventType</c>.
/// </summary>
/// <param name="Value">The enum name string (used as the stored value).</param>
/// <param name="DisplayName">The human-readable display name for the UI.</param>
public sealed record EventTypeDisplayItem(string Value, string DisplayName)
{
    /// <inheritdoc />
    public override string ToString() => DisplayName;
}
