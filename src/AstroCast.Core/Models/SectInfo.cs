namespace AstroCast.Core.Models;

/// <summary>
/// Planetary sect analysis for a single planet. Describes whether the planet
/// is operating in its preferred sect (day or night) and its resulting
/// accidental dignity or debility.
/// </summary>
/// <remarks>
/// <para>
/// The diurnal sect comprises Sun, Jupiter, and Saturn. The nocturnal sect
/// comprises Moon, Venus, and Mars. Mercury adapts: it is diurnal when
/// oriental of the Sun (rises before the Sun) and nocturnal when occidental.
/// </para>
/// <para>
/// A planet in its correct sect in a matching chart (day planet in day chart,
/// night planet in night chart) gains accidental dignity. The out-of-sect
/// malefic is the most dangerous planet in the chart.
/// </para>
/// <para>
/// <b>Hayz</b> is the strongest form of accidental sect dignity: a diurnal
/// planet above the horizon in a day chart in a masculine sign, or a nocturnal
/// planet below the horizon in a night chart in a feminine sign. Above or below
/// the horizon is the planet's true altitude (<see cref="PlanetPosition.Altitude"/>);
/// a composite planet, which has none, goes by its longitude.
/// </para>
/// </remarks>
public sealed class SectInfo
{
    /// <summary>Name of the planet (e.g., "Sun", "Moon", "Mars").</summary>
    public required string PlanetName { get; init; }

    /// <summary>
    /// The planet's natural sect: "Diurnal" or "Nocturnal".
    /// Mercury's sect is determined by its position relative to the Sun.
    /// </summary>
    public required string NaturalSect { get; init; }

    /// <summary>
    /// True if this planet's natural sect matches the chart sect
    /// (day planet in day chart, or night planet in night chart).
    /// </summary>
    public required bool IsInSect { get; init; }

    /// <summary>
    /// True if this planet is a benefic (Jupiter or Venus).
    /// </summary>
    public required bool IsBenefic { get; init; }

    /// <summary>
    /// True if this planet is a malefic (Mars or Saturn).
    /// </summary>
    public required bool IsMalefic { get; init; }

    /// <summary>
    /// True if this planet is the in-sect malefic (less harmful malefic
    /// for this chart type). Saturn in a day chart, Mars in a night chart.
    /// </summary>
    public required bool IsInSectMalefic { get; init; }

    /// <summary>
    /// True if this planet is the out-of-sect malefic (most harmful malefic
    /// for this chart type). Mars in a day chart, Saturn in a night chart.
    /// </summary>
    public required bool IsOutOfSectMalefic { get; init; }

    /// <summary>
    /// True if this planet is the in-sect benefic (most effective benefic
    /// for this chart type). Jupiter in a day chart, Venus in a night chart.
    /// </summary>
    public required bool IsInSectBenefic { get; init; }

    /// <summary>
    /// True if the planet meets the full hayz condition: correct sect,
    /// correct hemisphere, and correct sign gender (masculine/feminine).
    /// This is the strongest form of accidental sect dignity.
    /// </summary>
    public required bool IsHayz { get; init; }

    /// <summary>
    /// The house of joy for this planet, where it functions most comfortably.
    /// Mercury=1, Moon=3, Venus=5, Mars=6, Sun=9, Jupiter=11, Saturn=12.
    /// Null for non-classical planets.
    /// </summary>
    public int? HouseOfJoy { get; init; }

    /// <summary>
    /// Human-readable summary (e.g., "Jupiter — Diurnal, In Sect, In-Sect Benefic").
    /// </summary>
    public string Summary
    {
        get
        {
            List<string> parts = [PlanetName, NaturalSect];
            parts.Add(IsInSect ? "In Sect" : "Out of Sect");
            if (IsInSectBenefic) parts.Add("In-Sect Benefic");
            if (IsInSectMalefic) parts.Add("In-Sect Malefic");
            if (IsOutOfSectMalefic) parts.Add("Out-of-Sect Malefic");
            if (IsHayz) parts.Add("Hayz");
            return string.Join(" — ", parts);
        }
    }
}
