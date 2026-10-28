using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Records the moment a single celestial body crosses one of the four local-space
/// angles (rising, culminating, setting, anti-culminating) at a specific geographic
/// location on a given date.
/// </summary>
/// <remarks>
/// Angular crossings are the building blocks of paran detection. Each body may
/// cross each angle once per sidereal day (~23h 56m), producing up to four
/// crossings per body per day. Parans are found by comparing crossing times
/// across body pairs and identifying simultaneities within the configured orb.
/// </remarks>
public sealed class AngularCrossing
{
    /// <summary>The celestial body that crosses the angle.</summary>
    public required CelestialBodyId Body { get; init; }

    /// <summary>The display name of the body (e.g., "Sun", "Moon", "Chiron").</summary>
    public required string BodyName { get; init; }

    /// <summary>Which angle the body crosses.</summary>
    public required AngleType Angle { get; init; }

    /// <summary>
    /// Julian Day (Universal Time) of the angular crossing.
    /// For rise/set events this incorporates atmospheric refraction.
    /// For MC/IC crossings this is the geometric meridian transit time.
    /// </summary>
    public required double JulianDayUt { get; init; }

    /// <summary>
    /// The body's ecliptic longitude at the moment of crossing (decimal degrees, 0–360).
    /// Useful for identifying zodiacal context (sign and degree) of the paran event.
    /// </summary>
    public required double Longitude { get; init; }
}
