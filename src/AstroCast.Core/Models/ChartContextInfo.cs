using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Interpretive context for a chart, combining positional data with
/// traditional astrological framework elements. Identifies the chart ruler,
/// sect, event significator, angular planets, and mutual receptions.
/// </summary>
public sealed class ChartContextInfo
{
    /// <summary>
    /// The planet that rules the sign on the Ascendant (the chart ruler).
    /// In event charts, represents the initiator or owner of the event.
    /// Uses traditional (Ptolemaic) rulership.
    /// </summary>
    public required string ChartRuler { get; init; }

    /// <summary>
    /// The zodiac sign on the Ascendant.
    /// </summary>
    public required ZodiacSign AscendantSign { get; init; }

    /// <summary>
    /// The dignity status of the chart ruler.
    /// </summary>
    public required DignityInfo ChartRulerDignity { get; init; }

    /// <summary>
    /// The house the chart ruler occupies.
    /// </summary>
    public required int ChartRulerHouse { get; init; }

    /// <summary>
    /// True if the chart ruler is retrograde.
    /// </summary>
    public required bool IsChartRulerRetrograde { get; init; }

    /// <summary>
    /// True if this is a day chart: the Sun's longitude on the half of the ecliptic above the
    /// horizon, the arc from the Descendant to the Ascendant, or the other arc where the
    /// ecliptic's upper-meridian point is below the horizon, in every house system. False if
    /// this is a night chart. Sect determines which planets are "in favor" for the chart.
    /// </summary>
    public required bool IsDayChart { get; init; }

    /// <summary>
    /// The sect label: "Day" or "Night".
    /// </summary>
    public string SectLabel => IsDayChart ? "Day" : "Night";

    /// <summary>
    /// The natural planetary significator for this event type.
    /// Null for birth charts or when the event type has no specific significator.
    /// </summary>
    public required string? EventSignificator { get; init; }

    /// <summary>
    /// The dignity status of the event significator.
    /// Null when there is no event significator.
    /// </summary>
    public required DignityInfo? EventSignificatorDignity { get; init; }

    /// <summary>
    /// Key houses for this event type (e.g., [1, 7, 5] for a wedding).
    /// Empty for birth charts.
    /// </summary>
    public required IReadOnlyList<int> KeyHouses { get; init; }

    /// <summary>
    /// Planets within 5 degrees of an angular cusp (ASC, MC, DSC, IC).
    /// These are the most powerful and visible forces in the chart.
    /// </summary>
    public required IReadOnlyList<AngularPlanet> AngularPlanets { get; init; }

    /// <summary>
    /// Pairs of planets in mutual reception, each named by its strongest type:
    /// domicile, exaltation, mixed domicile–exaltation, triplicity, mixed
    /// domicile–triplicity or mixed exaltation–triplicity. Creates a
    /// cooperative link between the planets.
    /// </summary>
    public required IReadOnlyList<MutualReception> MutualReceptions { get; init; }

    /// <summary>
    /// Sect analysis for each classical planet, describing in-sect/out-of-sect
    /// status, benefic/malefic classification, and hayz condition.
    /// Empty list for charts where sect is indeterminate.
    /// </summary>
    public IReadOnlyList<SectInfo> PlanetarySect { get; init; } = [];

    /// <summary>
    /// Triplicity rulers for the Ascendant sign's element, using the
    /// Dorothean system. The active ruler is selected based on chart sect.
    /// Null if chart context was not fully computed.
    /// </summary>
    public TriplicityRulerInfo? AscendantTriplicityRulers { get; init; }
}
