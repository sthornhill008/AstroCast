namespace AstroCast.Core.Enums;

/// <summary>
/// Specifies the field used to sort chart query results.
/// </summary>
public enum ChartSortField
{
    /// <summary>Sort by chart subject name (alphabetical).</summary>
    Name = 0,

    /// <summary>Sort by the event date/time (local).</summary>
    Date = 1,

    /// <summary>Sort by the chart event type (Birth, Event, Horary, etc.).</summary>
    EventType = 2,

    /// <summary>Sort by the record creation timestamp.</summary>
    CreatedAt = 3
}
