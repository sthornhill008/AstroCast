namespace AstroCast.Core.Models.Transit;

/// <summary>
/// A declination aspect (parallel or contraparallel) between a transiting
/// planet at an exact pass time and a natal planet. These represent hidden
/// transit activations that the longitudinal aspect table does not capture.
/// </summary>
public sealed class TransitDeclinationAspect
{
    /// <summary>Name of the transiting planet (e.g., "Jupiter").</summary>
    public required string TransitPlanetName { get; init; }

    /// <summary>Declination of the transiting planet at the exact pass time (decimal degrees).</summary>
    public required double TransitDeclination { get; init; }

    /// <summary>
    /// True if the aspect is a parallel (same declination sign);
    /// false if it is a contraparallel (opposite declination signs).
    /// </summary>
    public required bool IsParallel { get; init; }

    /// <summary>Name of the natal planet (e.g., "Saturn").</summary>
    public required string NatalPlanetName { get; init; }

    /// <summary>Declination of the natal planet (decimal degrees).</summary>
    public required double NatalDeclination { get; init; }

    /// <summary>The actual orb in degrees (always positive).</summary>
    public required double Orb { get; init; }

    /// <summary>Date of the exact pass that produced this declination aspect.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Display abbreviation: "Par" or "CPar".</summary>
    public string Abbreviation => IsParallel ? "Par" : "CPar";
}
