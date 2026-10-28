using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Transit;

/// <summary>
/// A transit planet activating a natal midpoint on the 45° dial.
/// </summary>
public sealed class TransitMidpointActivation
{
    /// <summary>The transiting planet.</summary>
    public required TransitBody TransitPlanet { get; init; }

    /// <summary>First body of the natal midpoint pair.</summary>
    public required string MidpointBody1 { get; init; }

    /// <summary>Second body of the natal midpoint pair.</summary>
    public required string MidpointBody2 { get; init; }

    /// <summary>Transit planet position on 45° dial (degrees).</summary>
    public required double TransitDial45Position { get; init; }

    /// <summary>Natal midpoint position on 45° dial (degrees).</summary>
    public required double NatalMidpointDial45Position { get; init; }

    /// <summary>Orb at report start in degrees.</summary>
    public required double OrbAtStartDegrees { get; init; }

    /// <summary>
    /// True if this is a priority midpoint (involves Sun/Moon, ASC/MC,
    /// or chart ruler).
    /// </summary>
    public required bool IsPriority { get; init; }

    /// <summary>
    /// Reason for priority flag (e.g., "Sun/Moon midpoint", "involves natal Sun").
    /// Null if not a priority midpoint.
    /// </summary>
    public string? PriorityReason { get; init; }

    /// <summary>
    /// Ordered lifecycle events (entry, exact, exit).
    /// </summary>
    public required IReadOnlyList<TransitLifecycleEvent> Lifecycle { get; init; }
}
