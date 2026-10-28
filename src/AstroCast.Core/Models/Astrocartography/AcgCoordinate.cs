namespace AstroCast.Core.Models.Astrocartography;

/// <summary>
/// A geographic coordinate point on an astrocartography line.
/// </summary>
/// <param name="Latitude">Geographic latitude in degrees (-90 to +90, north positive).</param>
/// <param name="Longitude">Geographic longitude in degrees (-180 to +180, west negative).</param>
public sealed record AcgCoordinate(double Latitude, double Longitude);
