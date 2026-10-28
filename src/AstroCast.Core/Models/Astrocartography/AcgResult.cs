namespace AstroCast.Core.Models.Astrocartography;

/// <summary>
/// Complete astrocartography result containing all angular lines for a natal chart.
/// </summary>
/// <remarks>
/// Contains 48 lines (12 celestial bodies x 4 line types: MC, IC, ASC, DSC).
/// An ASC/DSC line has fewer than the full 171 whole-degree points when the body
/// stops rising and setting short of 85 degrees; it then ends at the latitude where
/// it stops.
/// </remarks>
public sealed class AcgResult
{
    /// <summary>The natal chart used for computation.</summary>
    public required ChartResult NatalChart { get; init; }

    /// <summary>All astrocartography lines (up to 48: 12 bodies x 4 types).</summary>
    public required IReadOnlyList<AcgLine> Lines { get; init; }

    /// <summary>True obliquity of the ecliptic used for computation (degrees).</summary>
    public required double Obliquity { get; init; }

    /// <summary>Greenwich Apparent Sidereal Time used for computation (degrees, 0-360).</summary>
    public required double GastDegrees { get; init; }

    /// <summary>
    /// Southernmost latitude of any line (degrees): the MC/IC lines start here, and so does
    /// an ASC/DSC line whose body rises and sets this far south.
    /// </summary>
    public required double LatitudeMin { get; init; }

    /// <summary>
    /// Northernmost latitude of any line (degrees): the MC/IC lines end here, and so does
    /// an ASC/DSC line whose body rises and sets this far north.
    /// </summary>
    public required double LatitudeMax { get; init; }

    /// <summary>Latitude step between the ASC/DSC points, between a line's ends (degrees).</summary>
    public required double LatitudeStep { get; init; }
}
