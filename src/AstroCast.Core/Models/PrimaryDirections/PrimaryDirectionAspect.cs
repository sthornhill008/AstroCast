namespace AstroCast.Core.Models.PrimaryDirections;

/// <summary>
/// The relation a promissor point bears to the body it is taken from (branch Primary, 1.5.0, rule R4): the body's own
/// place, one of its zodiacal aspect points, or its antiscion or contra-antiscion.
/// </summary>
public enum PrimaryDirectionAspect
{
    /// <summary>The body's own place, with its ecliptic latitude; a fixed star's place.</summary>
    Conjunction,

    /// <summary>The body's longitude plus or minus 60°, at latitude 0.</summary>
    Sextile,

    /// <summary>The body's longitude plus or minus 90°, at latitude 0.</summary>
    Square,

    /// <summary>The body's longitude plus or minus 120°, at latitude 0.</summary>
    Trine,

    /// <summary>The body's longitude plus 180°, at latitude 0.</summary>
    Opposition,

    /// <summary>The body's antiscion, 180° less its longitude, at latitude 0.</summary>
    Antiscion,

    /// <summary>The body's contra-antiscion, 360° less its longitude, at latitude 0.</summary>
    ContraAntiscion,
}
