using AstroCast.Core.Models.Electional;

namespace AstroCast.Core.Models;

/// <summary>
/// The planetary hour at the chart moment, with radicality analysis for
/// horary charts. Wraps the existing <see cref="PlanetaryHour"/> from
/// the electional system and adds fields for comparing the hour ruler
/// to the chart ruler.
/// </summary>
/// <remarks>
/// <para>
/// In horary astrology, a chart is considered <b>radical</b> (fit to be
/// judged) when the planetary hour ruler matches or is connected to the
/// chart ruler (Ascendant sign ruler). The radicality levels are:
/// </para>
/// <list type="bullet">
///   <item><description><b>Radical:</b> Hour ruler IS the chart ruler.</description></item>
///   <item><description><b>Supportive:</b> Hour ruler and chart ruler share the same triplicity element.</description></item>
///   <item><description><b>Weak:</b> Hour ruler is the chart ruler's dispositor.</description></item>
///   <item><description><b>Not Radical:</b> No connection between hour ruler and chart ruler.</description></item>
/// </list>
/// <para>
/// Radicality is computed only for Horary charts. For other chart types,
/// the planetary hour is still reported but the radicality fields are
/// null/empty.
/// </para>
/// </remarks>
public sealed class PlanetaryHourInfo
{
    /// <summary>The underlying planetary hour data (ruler, hour number, boundaries).</summary>
    public required PlanetaryHour Hour { get; init; }

    /// <summary>
    /// The ruling planet of this hour (convenience accessor, same as
    /// <see cref="Hour"/>.<see cref="PlanetaryHour.RulingPlanet"/>).
    /// </summary>
    public string RulingPlanet => Hour.RulingPlanet;

    /// <summary>
    /// True if the planetary hour ruler is connected to the chart ruler,
    /// confirming the chart as radical. Only computed for Horary charts.
    /// Null for non-Horary charts.
    /// </summary>
    public bool? IsRadical { get; init; }

    /// <summary>
    /// The radicality level: "Radical", "Supportive", "Weak", or "Not Radical".
    /// Null for non-Horary charts.
    /// </summary>
    public string? RadicalityLevel { get; init; }

    /// <summary>
    /// Human-readable explanation of the radicality determination
    /// (e.g., "Hour ruler Jupiter matches chart ruler Jupiter -- chart is radical.").
    /// Null for non-Horary charts.
    /// </summary>
    public string? RadicalityNote { get; init; }
}
