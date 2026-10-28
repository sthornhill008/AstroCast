using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Transit;

/// <summary>
/// A single active transit-to-natal aspect with its full lifecycle.
/// </summary>
public sealed class ActiveTransit
{
    /// <summary>The transiting body.</summary>
    public required TransitBody TransitPlanet { get; init; }

    /// <summary>The natal target being aspected.</summary>
    public required string NatalTarget { get; init; }

    /// <summary>The natal target's symbol (SU, MO, ME, etc.).</summary>
    public required string NatalTargetSymbol { get; init; }

    /// <summary>The aspect type (conjunction, opposition, etc.).</summary>
    public required string AspectName { get; init; }

    /// <summary>The aspect abbreviation (cnj, opp, tri, sqr, sxt, etc.).</summary>
    public required string AspectAbbreviation { get; init; }

    /// <summary>Transit planet longitude at report start (degrees).</summary>
    public required double TransitLongitudeAtStart { get; init; }

    /// <summary>Transit planet sign at report start.</summary>
    public required string TransitSignAtStart { get; init; }

    /// <summary>Natal target longitude (degrees).</summary>
    public required double NatalLongitude { get; init; }

    /// <summary>Natal target sign.</summary>
    public required string NatalSign { get; init; }

    /// <summary>Natal house number the target occupies (1–12).</summary>
    public required int NatalHouseNumber { get; init; }

    /// <summary>Orb at report start in degrees.</summary>
    public required double OrbAtStartDegrees { get; init; }

    /// <summary>Whether the transit is applying, separating, or exact at report start.</summary>
    public required string OrbDirection { get; init; }

    /// <summary>
    /// Natal house the transiting planet occupies (1–12).
    /// </summary>
    public required int TransitHouseNumber { get; init; }

    /// <summary>
    /// Ordered lifecycle events (entry, exact passes, stations, exit).
    /// </summary>
    public required IReadOnlyList<TransitLifecycleEvent> Lifecycle { get; init; }
}
