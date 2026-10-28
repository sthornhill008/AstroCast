using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates the Moon's lunar mansion from its ecliptic longitude using
/// 28 equal divisions of the tropical zodiac (~12 degrees 51 arcminutes 26 arcseconds each).
/// </summary>
/// <remarks>
/// The formula is: <c>mansion = floor(longitude / (360 / 28)) + 1</c>.
/// This is a pure mathematical operation with no external dependencies.
/// The catalog of mansion names, natures, and keywords is stored in
/// <see cref="MoonMansionInfo.Catalog"/>.
/// </remarks>
public sealed class MoonMansionService : IMoonMansionService
{
    /// <summary>Width of each mansion in degrees: 360 / 28 = 12.857142...</summary>
    private const double MansionWidth = 360.0 / 28.0;

    /// <inheritdoc />
    public MoonMansionInfo GetMansion(double moonLongitude)
    {
        double normalized = ((moonLongitude % 360.0) + 360.0) % 360.0;
        int index = (int)(normalized / MansionWidth);

        // Clamp to valid range (handles exactly 360.0)
        if (index >= 28)
            index = 0;

        // Catalog is 0-based internally, mansions are 1-based
        return MoonMansionInfo.Catalog[index];
    }
}
