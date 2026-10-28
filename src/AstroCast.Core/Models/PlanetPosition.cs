using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Represents the calculated position and motion of a celestial body
/// at a specific moment in time. Contains both ecliptic and optional
/// equatorial coordinates.
/// </summary>
public sealed class PlanetPosition
{
    /// <summary>The celestial body identifier.</summary>
    public required CelestialBodyId Body { get; init; }

    /// <summary>The display name of the body (e.g., "Sun", "Moon").</summary>
    public required string Name { get; init; }

    /// <summary>Ecliptic longitude in decimal degrees (0–360, tropical zodiac).</summary>
    public required double Longitude { get; init; }

    /// <summary>Ecliptic latitude in decimal degrees.</summary>
    public required double Latitude { get; init; }

    /// <summary>Distance from Earth in astronomical units (AU).</summary>
    public required double Distance { get; init; }

    /// <summary>Daily speed in longitude (degrees/day). Negative indicates retrograde motion.</summary>
    public required double SpeedInLongitude { get; init; }

    /// <summary>Daily speed in latitude (degrees/day).</summary>
    public required double SpeedInLatitude { get; init; }

    /// <summary>Daily speed in distance (AU/day).</summary>
    public required double SpeedInDistance { get; init; }

    /// <summary>Right ascension in decimal degrees (0–360). Populated when equatorial coordinates are calculated.</summary>
    public double? RightAscension { get; init; }

    /// <summary>Declination in decimal degrees (-90 to +90). Populated when equatorial coordinates are calculated.</summary>
    public double? Declination { get; init; }

    /// <summary>
    /// House number (1–12) this body occupies. Null if houses have not been calculated.
    /// </summary>
    public int? House { get; set; }

    /// <summary>
    /// Precise house position as a decimal: the house plus the fraction of its arc, from its cusp to the next in
    /// the direction the cusps run (e.g., 10.45 = house 10, 45% through). Its whole part is always
    /// <see cref="House"/>. Null if houses have not been calculated.
    /// </summary>
    public double? HouseExact { get; set; }

    /// <summary>
    /// Geometric altitude above the horizon in decimal degrees, without refraction: positive above the horizon,
    /// negative below. Null for a composite position, whose averaged right ascension and declination are not a
    /// sky, or when the right ascension or declination is unknown.
    /// </summary>
    /// <remarks>
    /// Set by the chart engine from the position's right ascension and declination, the chart's ARMC and the
    /// place's latitude. Hayz reads its hemisphere from it. It is not printed.
    /// </remarks>
    public double? Altitude { get; set; }

    /// <summary>True if the body is retrograde (negative speed in longitude).</summary>
    public bool IsRetrograde => SpeedInLongitude < 0;

    /// <summary>Direction indicator: "D" for direct, "R" for retrograde.</summary>
    public string DirectionCode => IsRetrograde ? "R" : "D";

    /// <summary>The zodiac sign the body occupies, derived from ecliptic longitude.</summary>
    public ZodiacSign Sign => (ZodiacSign)((int)(Longitude % 360.0 / 30.0) + 1);

    /// <summary>Degrees within the current zodiac sign (0–29).</summary>
    public int SignDegrees => (int)(Longitude % 30.0);

    /// <summary>Arc-minutes within the current degree (0–59).</summary>
    public int SignMinutes => (int)((Longitude % 1.0) * 60.0);

    /// <summary>Arc-seconds within the current minute (0–59).</summary>
    public int SignSeconds => (int)(((Longitude % 1.0) * 60.0 % 1.0) * 60.0);
}
