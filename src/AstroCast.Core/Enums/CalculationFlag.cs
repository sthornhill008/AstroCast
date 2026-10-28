namespace AstroCast.Core.Enums;

/// <summary>
/// Flags controlling Swiss Ephemeris calculation behaviour.
/// Values map directly to SEFLG_* constants in swephexp.h.
/// Combine with bitwise OR for multiple options.
/// </summary>
[Flags]
public enum CalculationFlag
{
    /// <summary>No flags set. Uses default Swiss Ephemeris with apparent geocentric positions.</summary>
    None = 0,

    /// <summary>Use Swiss Ephemeris data files (SEFLG_SWIEPH = 2).</summary>
    SwissEphemeris = 2,

    /// <summary>Calculate high-precision daily speed (SEFLG_SPEED = 256).</summary>
    Speed = 256,

    /// <summary>Return equatorial coordinates (RA/Declination) instead of ecliptic (SEFLG_EQUATORIAL = 2048).</summary>
    Equatorial = 2048,

    /// <summary>Topocentric position adjusted for observer location (SEFLG_TOPOCTR = 32768).</summary>
    TopoCentric = 32768
}
