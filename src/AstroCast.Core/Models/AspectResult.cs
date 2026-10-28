namespace AstroCast.Core.Models;

/// <summary>
/// Represents a detected aspect between two chart points (planet-to-planet
/// or planet-to-angle). Contains the matched aspect definition, the actual orb,
/// and whether the aspect is applying or separating.
/// </summary>
public sealed class AspectResult
{
    /// <summary>Name of the first body/point (e.g., "Sun", "ASC").</summary>
    public required string Body1Name { get; init; }

    /// <summary>Ecliptic longitude of the first body/point.</summary>
    public required double Body1Longitude { get; init; }

    /// <summary>Name of the second body/point.</summary>
    public required string Body2Name { get; init; }

    /// <summary>Ecliptic longitude of the second body/point.</summary>
    public required double Body2Longitude { get; init; }

    /// <summary>The aspect definition that matched.</summary>
    public required AspectDefinition Aspect { get; init; }

    /// <summary>The actual orb in degrees (always positive).</summary>
    public required double Orb { get; init; }

    /// <summary>True if the aspect is applying (bodies moving toward exact).</summary>
    public required bool IsApplying { get; init; }

    /// <summary>"Ap" for applying, "Sp" for separating.</summary>
    public string ApplyingSeparating => IsApplying ? "Ap" : "Sp";
}
