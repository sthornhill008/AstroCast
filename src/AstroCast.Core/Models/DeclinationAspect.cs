namespace AstroCast.Core.Models;

/// <summary>
/// Represents a declination aspect (parallel or contraparallel) between two
/// celestial bodies. Parallels occur when two bodies on the same side of the
/// celestial equator share the same declination value (equivalent in strength to
/// a conjunction). Contraparallels occur when two bodies on opposite sides have
/// equal but opposite declinations (equivalent to an opposition). A declination
/// of exactly 0° counts as north, so a pair forms at most one of the two.
/// </summary>
public sealed class DeclinationAspect
{
    /// <summary>Name of the first body (e.g., "Sun", "Moon").</summary>
    public required string Body1Name { get; init; }

    /// <summary>Declination of the first body in decimal degrees.</summary>
    public required double Body1Declination { get; init; }

    /// <summary>Name of the second body.</summary>
    public required string Body2Name { get; init; }

    /// <summary>Declination of the second body in decimal degrees.</summary>
    public required double Body2Declination { get; init; }

    /// <summary>
    /// True if the aspect is a parallel (both bodies on the same side of the
    /// celestial equator, declinations within the orb of each other); false if it
    /// is a contraparallel (on opposite sides, declinations within the orb of equal
    /// and opposite). A declination of exactly 0° counts as north.
    /// </summary>
    public required bool IsParallel { get; init; }

    /// <summary>The actual orb in degrees (always positive).</summary>
    public required double Orb { get; init; }

    /// <summary>Display name: "Parallel" or "Contraparallel".</summary>
    public string AspectName => IsParallel ? "Parallel" : "Contraparallel";

    /// <summary>Abbreviation: "Par" or "CPar".</summary>
    public string Abbreviation => IsParallel ? "Par" : "CPar";
}
