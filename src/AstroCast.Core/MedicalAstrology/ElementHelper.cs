using AstroCast.Core.Enums;

namespace AstroCast.Core.MedicalAstrology;

/// <summary>
/// Maps zodiac signs to their classical elements (Fire, Earth, Air, Water)
/// and provides element-to-temperament conversion. Used by the
/// <see cref="TemperamentCalculator"/> for humoral scoring.
/// </summary>
public static class ElementHelper
{
    /// <summary>
    /// Gets the classical element for the specified zodiac sign.
    /// </summary>
    /// <param name="sign">The zodiac sign.</param>
    /// <returns>The element name: "Fire", "Earth", "Air", or "Water".</returns>
    public static string GetElement(ZodiacSign sign) => sign switch
    {
        ZodiacSign.Aries or ZodiacSign.Leo or ZodiacSign.Sagittarius => "Fire",
        ZodiacSign.Taurus or ZodiacSign.Virgo or ZodiacSign.Capricorn => "Earth",
        ZodiacSign.Gemini or ZodiacSign.Libra or ZodiacSign.Aquarius => "Air",
        ZodiacSign.Cancer or ZodiacSign.Scorpio or ZodiacSign.Pisces => "Water",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the temperament associated with the specified element.
    /// </summary>
    /// <param name="element">The element name.</param>
    /// <returns>The corresponding temperament.</returns>
    public static Temperament GetTemperament(string element) => element switch
    {
        "Fire" => Temperament.Choleric,
        "Earth" => Temperament.Melancholic,
        "Air" => Temperament.Sanguine,
        "Water" => Temperament.Phlegmatic,
        _ => Temperament.Sanguine
    };

    /// <summary>
    /// Gets the traditional planetary ruler of the specified zodiac sign.
    /// Uses the Ptolemaic (traditional) rulership system -- no modern outer planet rulers.
    /// </summary>
    /// <param name="sign">The zodiac sign.</param>
    /// <returns>The traditional ruler's <see cref="CelestialBodyId"/>.</returns>
    public static CelestialBodyId GetTraditionalRuler(ZodiacSign sign) => sign switch
    {
        ZodiacSign.Aries => CelestialBodyId.Mars,
        ZodiacSign.Taurus => CelestialBodyId.Venus,
        ZodiacSign.Gemini => CelestialBodyId.Mercury,
        ZodiacSign.Cancer => CelestialBodyId.Moon,
        ZodiacSign.Leo => CelestialBodyId.Sun,
        ZodiacSign.Virgo => CelestialBodyId.Mercury,
        ZodiacSign.Libra => CelestialBodyId.Venus,
        ZodiacSign.Scorpio => CelestialBodyId.Mars,
        ZodiacSign.Sagittarius => CelestialBodyId.Jupiter,
        ZodiacSign.Capricorn => CelestialBodyId.Saturn,
        ZodiacSign.Aquarius => CelestialBodyId.Saturn,
        ZodiacSign.Pisces => CelestialBodyId.Jupiter,
        _ => CelestialBodyId.Sun
    };
}
