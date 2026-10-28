using System.Globalization;

namespace AstroCast.Core.Models;

/// <summary>
/// Records a planet's house change between the natal and relocated charts.
/// Used in the relocation comparison to highlight which life areas are
/// activated or deactivated by the move.
/// </summary>
public sealed class HouseChange
{
    /// <summary>Name of the planet (e.g., "Venus", "Mars").</summary>
    public required string PlanetName { get; init; }

    /// <summary>House number in the natal chart (1–12).</summary>
    public required int NatalHouse { get; init; }

    /// <summary>House number in the relocated chart (1–12).</summary>
    public required int RelocatedHouse { get; init; }

    /// <summary>
    /// True if the planet is angular in the relocated chart, within
    /// <see cref="Services.AngularityService.AngularOrb"/> (5°) of an angle, and was
    /// not angular in the natal chart (Q11, branch K2, 1.3.4). This is the
    /// most significant type of house change — the planet gains prominence.
    /// </summary>
    public required bool BecameAngular { get; init; }

    /// <summary>
    /// True if the planet was angular in the natal chart, within 5° of an
    /// angle, and is no longer angular in the relocated chart. The planet
    /// loses prominence at the new location.
    /// </summary>
    public required bool LeftAngle { get; init; }

    /// <summary>
    /// Human-readable description of the change
    /// (e.g., "Venus: 5th → 10th (became angular)").
    /// </summary>
    public string Description
    {
        get
        {
            string suffix = "";
            if (BecameAngular) suffix = " (became angular)";
            else if (LeftAngle) suffix = " (left angle)";
            return $"{PlanetName}: {OrdinalHouse(NatalHouse)} \u2192 {OrdinalHouse(RelocatedHouse)}{suffix}";
        }
    }

    /// <summary>Formats a house number as ordinal (e.g., "1st", "2nd", "10th").</summary>
    private static string OrdinalHouse(int house) => house switch
    {
        1 => "1st", 2 => "2nd", 3 => "3rd",
        _ => string.Create(CultureInfo.InvariantCulture, $"{house}th"),
    };
}
