using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Options for filtering and sorting chart queries.
/// All filter properties are optional — null or empty values are ignored.
/// </summary>
public sealed class ChartQueryOptions
{
    /// <summary>
    /// Keyword search across all text fields: Name, LocationName, Notes,
    /// EventDescription, SourceNotes, and Keywords (case-insensitive LIKE %keyword%).
    /// </summary>
    public string? KeywordSearch { get; init; }

    /// <summary>Exact event type filter (case-insensitive).</summary>
    public string? EventType { get; init; }

    /// <summary>Filter charts on or after this local date (inclusive).</summary>
    public DateTime? DateFrom { get; init; }

    /// <summary>Filter charts on or before this local date (inclusive, end-of-day).</summary>
    public DateTime? DateTo { get; init; }

    /// <summary>
    /// The field used to sort results.
    /// Defaults to <see cref="ChartSortField.Name"/>.
    /// </summary>
    public ChartSortField SortField { get; init; } = ChartSortField.Name;

    /// <summary>
    /// Whether to sort in ascending order.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool SortAscending { get; init; } = true;
}
