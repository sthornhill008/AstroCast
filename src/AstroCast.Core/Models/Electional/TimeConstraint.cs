namespace AstroCast.Core.Models.Electional;

/// <summary>
/// Time-of-day constraint for electional windows.
/// Only candidates whose local time falls between
/// <see cref="Earliest"/> and <see cref="Latest"/> are considered.
/// </summary>
public sealed class TimeConstraint
{
    /// <summary>Earliest acceptable local time.</summary>
    public required TimeOnly Earliest { get; init; }

    /// <summary>Latest acceptable local time.</summary>
    public required TimeOnly Latest { get; init; }
}
