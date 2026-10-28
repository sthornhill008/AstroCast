namespace AstroCast.Core.Data;

/// <summary>
/// Database entity representing a saved chart's input data.
/// Positions and calculations are computed on demand from the stored
/// Julian Day and coordinates — they are never persisted.
/// </summary>
public sealed class ChartEntity
{
    /// <summary>Auto-incrementing primary key.</summary>
    public long Id { get; set; }

    /// <summary>Person or event name.</summary>
    public required string Name { get; set; }

    /// <summary>Chart type: Birth, Event, Horary, etc.</summary>
    public required string EventType { get; set; }

    /// <summary>Optional event description.</summary>
    public string? EventDescription { get; set; }

    /// <summary>Local date/time as ISO 8601 string.</summary>
    public required string LocalDateTime { get; set; }

    /// <summary>IANA timezone identifier (e.g., "America/New_York").</summary>
    public required string TimeZone { get; set; }

    /// <summary>Computed UTC date/time as ISO 8601 string.</summary>
    public required string UtcDateTime { get; set; }

    /// <summary>Computed Julian Day (Universal Time).</summary>
    public required double JulianDay { get; set; }

    /// <summary>Geographic latitude in decimal degrees (positive = North).</summary>
    public required double Latitude { get; set; }

    /// <summary>Geographic longitude in decimal degrees (positive = East).</summary>
    public required double Longitude { get; set; }

    /// <summary>Human-readable location name.</summary>
    public required string LocationName { get; set; }

    /// <summary>Gender of the chart subject (nullable for non-natal charts).</summary>
    public string? Gender { get; set; }

    /// <summary>Source of the birth/event data (e.g., "birth certificate", "from memory").</summary>
    public string? SourceNotes { get; set; }

    /// <summary>Explicit UTC offset in hours (null if using IANA timezone conversion).</summary>
    public double? UtcOffsetHours { get; set; }

    /// <summary>Whether the explicit offset was used instead of IANA timezone conversion.</summary>
    public bool UseExplicitOffset { get; set; }

    /// <summary>Optional notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Comma-separated keywords for categorisation and filtering.</summary>
    public string? Keywords { get; set; }

    /// <summary>The horary question (null for non-horary charts).</summary>
    public string? HoraryQuestion { get; set; }

    /// <summary>Horary question category for house mapping.</summary>
    public string? HoraryCategory { get; set; }

    /// <summary>
    /// House system used for this chart's calculation.
    /// Stored as the single-character Swiss Ephemeris code (e.g., "P" for Placidus,
    /// "R" for Regiomontanus). Null defaults to Placidus for backward compatibility.
    /// </summary>
    public string? HouseSystem { get; set; }

    /// <summary>Record creation timestamp (ISO 8601 UTC).</summary>
    public required string CreatedAt { get; set; }

    /// <summary>Record last-updated timestamp (ISO 8601 UTC).</summary>
    public required string UpdatedAt { get; set; }
}
