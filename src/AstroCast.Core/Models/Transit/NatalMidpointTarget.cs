namespace AstroCast.Core.Models.Transit;

/// <summary>
/// A natal midpoint target for transit midpoint activation scanning.
/// Represents a midpoint pair reduced to the 45° dial for Ebertin-style analysis.
/// </summary>
public sealed class NatalMidpointTarget
{
    /// <summary>First body name (e.g., "Sun").</summary>
    public required string Body1 { get; init; }

    /// <summary>Second body name (e.g., "Moon").</summary>
    public required string Body2 { get; init; }

    /// <summary>Position on the 45° dial (0–45 degrees).</summary>
    public required double Dial45Position { get; init; }

    /// <summary>Whether this is a priority midpoint.</summary>
    public required bool IsPriority { get; init; }

    /// <summary>Reason for priority (null if not priority).</summary>
    public string? PriorityReason { get; init; }
}
