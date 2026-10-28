using AstroCast.Core.Models;
using AstroCast.Core.Models.SolarArcs;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Calculates solar arc directions: directed positions and
/// directed-to-natal aspects.
/// </summary>
public interface ISolarArcService
{
    /// <summary>
    /// Calculates complete solar arc direction data.
    /// </summary>
    /// <param name="natalChart">The natal <see cref="ChartResult"/>.</param>
    /// <param name="solarArcValue">
    /// Solar arc value in degrees (from ProgressionResult.SolarArcValue).
    /// </param>
    /// <returns>Complete <see cref="SolarArcResult"/>.</returns>
    SolarArcResult Calculate(ChartResult natalChart, double solarArcValue);
}
