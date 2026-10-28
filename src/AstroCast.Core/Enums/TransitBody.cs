namespace AstroCast.Core.Enums;

/// <summary>
/// Celestial bodies tracked in transit calculations.
/// Excludes inner planets (Sun, Moon, Mercury, Venus) which move
/// too fast for meaningful date-range interpretation, except that
/// Mercury and Venus are tracked for their stations (card O-D6).
/// </summary>
public enum TransitBody
{
    /// <summary>Jupiter — expansion cycles, ~1 year per sign.</summary>
    Jupiter = 0,

    /// <summary>Saturn — structure/discipline, ~2.5 years per sign.</summary>
    Saturn = 1,

    /// <summary>Uranus — disruption/awakening, ~7 years per sign.</summary>
    Uranus = 2,

    /// <summary>Neptune — dissolution/spiritual, ~14 years per sign.</summary>
    Neptune = 3,

    /// <summary>Pluto — transformation/power, ~12–31 years per sign.</summary>
    Pluto = 4,

    /// <summary>Chiron — wound/healing cycles, ~2–8 years per sign.</summary>
    Chiron = 5,

    /// <summary>North Node — karmic direction, ~1.5 years per sign.</summary>
    NorthNode = 6,

    /// <summary>
    /// Mars — included only when stationing retrograde/direct within the
    /// report period, or moving slower than 0.3°/day near station.
    /// </summary>
    Mars = 7,

    /// <summary>
    /// Mercury — used for stations only: its stations are listed with the others,
    /// and it is never an active transit body (card O-D6).
    /// </summary>
    Mercury = 8,

    /// <summary>
    /// Venus — used for stations only: its stations are listed with the others,
    /// and it is never an active transit body (card O-D6).
    /// </summary>
    Venus = 9
}
