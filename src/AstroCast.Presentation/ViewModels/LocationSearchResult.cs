namespace AstroCast.ViewModels;

/// <summary>
/// Represents a location result from the Nominatim geocoding API.
/// Used by the location lookup feature to populate chart coordinates.
/// </summary>
public sealed class LocationSearchResult
{
    /// <summary>Gets the human-readable display name of the location.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the geographic latitude in decimal degrees (north positive).</summary>
    public required double Latitude { get; init; }

    /// <summary>Gets the geographic longitude in decimal degrees (east positive).</summary>
    public required double Longitude { get; init; }

    /// <inheritdoc />
    public override string ToString() => DisplayName;
}
