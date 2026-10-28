namespace AstroCast.Core.Enums;

/// <summary>
/// Identifies a celestial body for ephemeris calculations.
/// Values map directly to Swiss Ephemeris body constants (swephexp.h).
/// </summary>
public enum CelestialBodyId
{
    /// <summary>The Sun (SE_SUN).</summary>
    Sun = 0,

    /// <summary>The Moon (SE_MOON).</summary>
    Moon = 1,

    /// <summary>Mercury (SE_MERCURY).</summary>
    Mercury = 2,

    /// <summary>Venus (SE_VENUS).</summary>
    Venus = 3,

    /// <summary>Mars (SE_MARS).</summary>
    Mars = 4,

    /// <summary>Jupiter (SE_JUPITER).</summary>
    Jupiter = 5,

    /// <summary>Saturn (SE_SATURN).</summary>
    Saturn = 6,

    /// <summary>Uranus (SE_URANUS).</summary>
    Uranus = 7,

    /// <summary>Neptune (SE_NEPTUNE).</summary>
    Neptune = 8,

    /// <summary>Pluto (SE_PLUTO).</summary>
    Pluto = 9,

    /// <summary>True Lunar Node, not Mean Node (SE_TRUE_NODE).</summary>
    TrueNode = 11,

    /// <summary>
    /// Black Moon Lilith, the mean lunar apogee (SE_MEAN_APOG), named "Lilith" in every list: the fifth of the
    /// minor points a chart carries behind <see cref="Models.ChartInput.IncludeMinorPoints"/> (branch Asteroids,
    /// 1.6.0). The mean apogee, never the osculating or interpolated one; it is never retrograde.
    /// </summary>
    MeanApogee = 12,

    /// <summary>Chiron (SE_CHIRON).</summary>
    Chiron = 15,

    /// <summary>
    /// Ceres (SE_CERES), the first of the minor points a chart carries behind
    /// <see cref="Models.ChartInput.IncludeMinorPoints"/>: Ceres, Pallas, Juno, Vesta, Lilith, in that order
    /// everywhere (branch Asteroids, 1.6.0).
    /// </summary>
    Ceres = 17,

    /// <summary>Pallas (SE_PALLAS), the second minor point.</summary>
    Pallas = 18,

    /// <summary>Juno (SE_JUNO), the third minor point.</summary>
    Juno = 19,

    /// <summary>Vesta (SE_VESTA), the fourth minor point.</summary>
    Vesta = 20
}
