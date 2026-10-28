namespace AstroCast.Core.Models;

/// <summary>
/// An aspect between a body in one chart and a body/angle in the other chart.
/// Unlike natal <see cref="AspectResult"/>, inter-chart aspects have no
/// applying/separating concept (static chart comparison).
/// </summary>
public sealed class InterChartAspect
{
    /// <summary>Name of the body from Person A's chart (e.g., "A Sun").</summary>
    public required string Body1Name { get; init; }

    /// <summary>Ecliptic longitude of Body 1.</summary>
    public required double Body1Longitude { get; init; }

    /// <summary>Name of the body/angle from Person B's chart (e.g., "B Venus", "B ASC").</summary>
    public required string Body2Name { get; init; }

    /// <summary>Ecliptic longitude of Body 2.</summary>
    public required double Body2Longitude { get; init; }

    /// <summary>The aspect definition that matched.</summary>
    public required AspectDefinition Aspect { get; init; }

    /// <summary>The actual orb in degrees (always positive).</summary>
    public required double Orb { get; init; }

    /// <summary>Optional note (e.g., "exact" if orb &lt; 0°10', "wide" if orb &gt; 5°).</summary>
    public string? Note { get; init; }
}
