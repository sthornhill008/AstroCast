namespace AstroCast.Core.Models.PrimaryDirections;

/// <summary>
/// The key that turns a primary direction's arc, in degrees of right ascension, into years of life
/// (branch Primary, 1.5.0, rule R6).
/// </summary>
public enum PrimaryDirectionKey
{
    /// <summary>Naibod's key: the Sun's mean daily motion, 360° over 365.2422 days, a year for each 0°59′08″ of arc.</summary>
    Naibod,

    /// <summary>Ptolemy's key: one degree of arc for each year of life.</summary>
    Ptolemy,

    /// <summary>
    /// The true solar arc in right ascension: the years are the days after birth it takes the Sun's right ascension to
    /// advance by the arc, the Sun read forward for a converse direction as for a direct one.
    /// </summary>
    SolarArcRightAscension,
}
