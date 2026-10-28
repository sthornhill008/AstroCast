namespace AstroCast.Core.MedicalAstrology;

/// <summary>
/// The four classical temperaments derived from Galenic humoral theory.
/// Each temperament corresponds to a pair of elemental qualities,
/// a humor, an element, and a season.
/// </summary>
public enum Temperament
{
    /// <summary>
    /// Hot and Moist. Humor: Blood. Element: Air. Season: Spring.
    /// Associated with Gemini, Libra, Aquarius emphasis.
    /// Characterised by warmth, sociability, and optimism.
    /// </summary>
    Sanguine,

    /// <summary>
    /// Hot and Dry. Humor: Yellow Bile. Element: Fire. Season: Summer.
    /// Associated with Aries, Leo, Sagittarius emphasis.
    /// Characterised by energy, ambition, and decisiveness.
    /// </summary>
    Choleric,

    /// <summary>
    /// Cold and Dry. Humor: Black Bile. Element: Earth. Season: Autumn.
    /// Associated with Taurus, Virgo, Capricorn emphasis.
    /// Characterised by thoughtfulness, persistence, and caution.
    /// </summary>
    Melancholic,

    /// <summary>
    /// Cold and Moist. Humor: Phlegm. Element: Water. Season: Winter.
    /// Associated with Cancer, Scorpio, Pisces emphasis.
    /// Characterised by calmness, empathy, and receptivity.
    /// </summary>
    Phlegmatic
}
