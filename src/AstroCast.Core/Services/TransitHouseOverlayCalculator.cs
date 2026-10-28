using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates which natal house each transiting planet occupies at the report
/// midpoint date, with interception detection.
/// </summary>
/// <remarks>
/// A sign is intercepted in a house when neither the house cusp nor the next
/// house cusp falls in that sign — the entire 30° arc of the sign is contained
/// within the house. This is significant in transit interpretation because an
/// intercepted transit planet's energy is considered "hidden" or restricted
/// in expression within that house's domain.
/// </remarks>
public sealed class TransitHouseOverlayCalculator
{
    private readonly TransitEphemerisHelper _ephemeris;

    /// <summary>
    /// Initialises a new instance of <see cref="TransitHouseOverlayCalculator"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris helper for position lookups.</param>
    public TransitHouseOverlayCalculator(TransitEphemerisHelper ephemeris)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
    }

    /// <summary>
    /// Calculates house overlays for all active transit bodies at the given date.
    /// </summary>
    /// <param name="transitBodies">Transit bodies to include in the overlay snapshot.</param>
    /// <param name="snapshotDate">The date at which to calculate the overlay (typically report midpoint).</param>
    /// <param name="natalHouseCusps">
    /// Natal house cusp longitudes (index 0 = 1st house cusp, index 11 = 12th house cusp).
    /// </param>
    /// <returns>List of house overlays, one per transit body.</returns>
    public IReadOnlyList<TransitHouseOverlay> Calculate(
        IReadOnlyList<TransitBody> transitBodies,
        DateOnly snapshotDate,
        IReadOnlyList<double> natalHouseCusps)
    {
        ArgumentNullException.ThrowIfNull(transitBodies);
        ArgumentNullException.ThrowIfNull(natalHouseCusps);

        double jd = _ephemeris.DateToJulianDay(snapshotDate);
        List<TransitHouseOverlay> overlays = new(transitBodies.Count);

        foreach (TransitBody body in transitBodies)
        {
            double longitude = _ephemeris.GetLongitude(body, jd);
            int houseNumber = HousePlacement.HouseOf(longitude, natalHouseCusps) ?? 1;
            bool isIntercepted = IsInterceptedSign(longitude, houseNumber, natalHouseCusps);

            overlays.Add(new TransitHouseOverlay
            {
                Planet = body,
                HouseNumber = houseNumber,
                IsIntercepted = isIntercepted,
            });
        }

        return overlays.AsReadOnly();
    }

    /// <summary>
    /// Determines whether the zodiac sign at the given longitude is intercepted
    /// in the specified house. A sign is intercepted when neither the house cusp
    /// nor the next house cusp falls in that sign.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude to check (0–360).</param>
    /// <param name="houseNumber">House number the longitude falls in (1–12).</param>
    /// <param name="cusps">Natal house cusp longitudes (index 0 = house 1 cusp).</param>
    /// <returns>True if the sign at the longitude is intercepted in this house.</returns>
    public static bool IsInterceptedSign(
        double longitude, int houseNumber, IReadOnlyList<double> cusps)
    {
        int signIndex = (int)(longitude / 30.0) % 12;
        int houseIdx = houseNumber - 1;
        int cuspSignIndex = (int)(cusps[houseIdx] / 30.0) % 12;
        int nextCuspSignIndex = (int)(cusps[(houseIdx + 1) % 12] / 30.0) % 12;

        return signIndex != cuspSignIndex && signIndex != nextCuspSignIndex;
    }
}
