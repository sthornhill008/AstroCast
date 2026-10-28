using AstroCast.Core.Enums;

namespace AstroCast.Core.MedicalAstrology;

/// <summary>
/// Aggregate result of a medical astrology health profile analysis.
/// Contains the temperament assessment, zodiacal anatomy highlights,
/// significant medical aspects, and 6th/8th/12th house analysis.
/// Consumed by the WPF Medical panel for display.
/// </summary>
/// <remarks>
/// This profile is purely informational and based on traditional
/// astrological correspondences. It is not a medical diagnosis.
/// </remarks>
public sealed class HealthProfile
{
    /// <summary>Name of the chart subject.</summary>
    public required string SubjectName { get; init; }

    /// <summary>The calculated temperament profile.</summary>
    public required TemperamentProfile Temperament { get; init; }

    /// <summary>
    /// Zodiac signs with significant planetary emphasis in this chart
    /// (Ascendant sign, Sun sign, Moon sign, and any sign with 2+ planets).
    /// Each entry pairs the sign with the body regions it rules.
    /// </summary>
    public required IReadOnlyList<SignEmphasis> SignEmphases { get; init; }

    /// <summary>
    /// Hard aspects (conjunction, opposition, square, quincunx) interpreted
    /// through medical correspondences. Sorted by significance then orb.
    /// </summary>
    public required IReadOnlyList<MedicalAspectInfo> MedicalAspects { get; init; }

    /// <summary>
    /// Analysis of the 6th house: sign on cusp, planets in 6th, and ruler condition.
    /// The 6th house represents daily health habits and acute illness in traditional astrology.
    /// </summary>
    public required string SixthHouseAnalysis { get; init; }

    /// <summary>
    /// The zodiac sign on the 6th house cusp.
    /// </summary>
    public required ZodiacSign SixthHouseSign { get; init; }

    /// <summary>
    /// Planets located in the 6th house, if any.
    /// </summary>
    public required IReadOnlyList<string> PlanetsInSixth { get; init; }

    /// <summary>
    /// Brief analysis of the 8th house (chronic illness, surgery, regeneration).
    /// </summary>
    public required string EighthHouseAnalysis { get; init; }

    /// <summary>
    /// Brief analysis of the 12th house (hidden illness, hospitals, immune system).
    /// </summary>
    public required string TwelfthHouseAnalysis { get; init; }
}

/// <summary>
/// A zodiac sign that has significant planetary emphasis in the chart,
/// paired with its traditional body region correspondences.
/// </summary>
public sealed class SignEmphasis
{
    /// <summary>The emphasised zodiac sign.</summary>
    public required ZodiacSign Sign { get; init; }

    /// <summary>Why this sign is emphasised (e.g., "Ascendant", "Sun sign", "3 planets").</summary>
    public required string Reason { get; init; }

    /// <summary>Body regions ruled by this sign.</summary>
    public required IReadOnlyList<BodyRegion> BodyRegions { get; init; }

    /// <summary>Health summary for this sign.</summary>
    public required string HealthSummary { get; init; }
}
