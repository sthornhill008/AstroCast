using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// The antiscion and contra-antiscion points for a single planet.
/// The antiscion is the planet's mirror reflection across the solstice
/// axis (0° Cancer / 0° Capricorn). The contra-antiscion mirrors across
/// the equinoctial axis (0° Aries / 0° Libra) and is always 180° from
/// the antiscion.
/// </summary>
/// <remarks>
/// Two planets whose positions coincide with each other's antiscia points
/// (within orb) have a hidden connection equivalent to a conjunction (for
/// antiscia) or opposition (for contra-antiscia). These connections are
/// invisible to standard aspect scanning and are essential in horary
/// perfection analysis.
/// </remarks>
public sealed class AntisciaPoint
{
    /// <summary>Name of the planet (e.g., "Mars", "Venus").</summary>
    public required string PlanetName { get; init; }

    /// <summary>The planet's actual ecliptic longitude in degrees (0–360).</summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// The antiscion longitude — the planet's reflection across the
    /// solstice axis. Formula: (540 − longitude) % 360.
    /// </summary>
    public required double AntisciaLongitude { get; init; }

    /// <summary>The zodiac sign of the antiscion point.</summary>
    public ZodiacSign AntisciaSign => ToSign(AntisciaLongitude);

    /// <summary>
    /// The contra-antiscion longitude — the antiscion + 180°,
    /// equivalent to reflection across the equinoctial axis.
    /// </summary>
    public required double ContraAntisciaLongitude { get; init; }

    /// <summary>The zodiac sign of the contra-antiscion point.</summary>
    public ZodiacSign ContraAntisciaSign => ToSign(ContraAntisciaLongitude);

    /// <summary>
    /// Converts a longitude to a zodiac sign.
    /// </summary>
    private static ZodiacSign ToSign(double longitude)
    {
        double normalized = ((longitude % 360.0) + 360.0) % 360.0;
        int index = (int)(normalized / 30.0);
        if (index >= 12) index = 0;
        return (ZodiacSign)(index + 1);
    }
}
