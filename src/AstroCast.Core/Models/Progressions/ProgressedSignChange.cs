namespace AstroCast.Core.Models.Progressions;

/// <summary>
/// A progressed body changing zodiac sign within 6 months of the report period.
/// </summary>
public sealed class ProgressedSignChange
{
    /// <summary>The progressed body name.</summary>
    public required string BodyName { get; init; }

    /// <summary>The sign being left.</summary>
    public required string FromSign { get; init; }

    /// <summary>The sign being entered.</summary>
    public required string ToSign { get; init; }

    /// <summary>
    /// The date of the sign change, exact: the UT date of its instant (branch G, 1.3.4). The
    /// export's column header keeps "Approximate Date".
    /// </summary>
    public required DateOnly Date { get; init; }
}
