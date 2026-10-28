using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Calculates the Moon's lunar mansion from its ecliptic longitude.
/// </summary>
public interface IMoonMansionService
{
    /// <summary>
    /// Determines which of the 28 lunar mansions the Moon occupies
    /// at the given ecliptic longitude.
    /// </summary>
    /// <param name="moonLongitude">Moon's ecliptic longitude in degrees (0-360).</param>
    /// <returns>
    /// The mansion information including number, name, nature, and keywords.
    /// </returns>
    MoonMansionInfo GetMansion(double moonLongitude);
}
