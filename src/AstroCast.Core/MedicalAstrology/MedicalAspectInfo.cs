namespace AstroCast.Core.MedicalAstrology;

/// <summary>
/// A hard aspect between two planets interpreted through the lens of medical
/// astrology. Identifies the body systems potentially involved based on the
/// planetary correspondences of the two bodies in aspect.
/// </summary>
public sealed class MedicalAspectInfo
{
    /// <summary>Description of the aspect (e.g., "Sun square Saturn").</summary>
    public required string AspectDescription { get; init; }

    /// <summary>The aspect type name (e.g., "Square", "Opposition").</summary>
    public required string AspectType { get; init; }

    /// <summary>The orb of the aspect in degrees.</summary>
    public required double Orb { get; init; }

    /// <summary>Whether the aspect is applying (bodies moving toward exact).</summary>
    public required bool IsApplying { get; init; }

    /// <summary>Traditional medical interpretation of this aspect combination.</summary>
    public required string MedicalInterpretation { get; init; }

    /// <summary>Body regions associated with the first planet.</summary>
    public required IReadOnlyList<BodyRegion> Body1Regions { get; init; }

    /// <summary>Body regions associated with the second planet.</summary>
    public required IReadOnlyList<BodyRegion> Body2Regions { get; init; }

    /// <summary>
    /// Relative significance: "High" for conjunctions/oppositions/squares,
    /// "Moderate" for quincunxes, "Low" for semi-squares/sesquiquadrates.
    /// </summary>
    public required string Significance { get; init; }
}
