using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// The horizon as a chart reads it (branch F6, 1.3.3): a body's geometric altitude, the side of the horizon a
/// longitude lies on, and day or night (ruling D3).
/// </summary>
/// <remarks>
/// <para>
/// The ecliptic crosses the horizon at the Ascendant and the Descendant, and the half above the horizon is the arc
/// from the Descendant forward to the Ascendant, the half that holds the ecliptic's upper-meridian point. Inside the
/// polar circles that point can lie below the horizon. The half above is then the arc from the Ascendant forward to
/// the Descendant (S′), and an axis test that ignored it would call a chart with the Sun 20° up a night chart.
/// </para>
/// <para>
/// Until 1.3.3 a chart was a day chart when the Sun was in houses 7 to 12, which in Whole Sign and Morinus is not
/// the half above the horizon, and hayz read its hemisphere from the house number.
/// </para>
/// </remarks>
internal static class ChartSect
{
    /// <summary>Degrees per radian.</summary>
    private const double DegreesPerRadian = 180.0 / Math.PI;

    /// <summary>
    /// A body's geometric altitude, without refraction: sin h = sin φ sin δ + cos φ cos δ cos(ARMC − α).
    /// </summary>
    /// <param name="rightAscension">The body's right ascension α, in degrees.</param>
    /// <param name="declination">The body's declination δ, in degrees.</param>
    /// <param name="armc">The right ascension of the upper meridian (ARMC), in degrees.</param>
    /// <param name="latitude">The geographic latitude φ, in degrees, north positive.</param>
    /// <returns>The altitude, in degrees: positive above the horizon.</returns>
    internal static double AltitudeOf(double rightAscension, double declination, double armc, double latitude)
    {
        double phi = latitude / DegreesPerRadian;
        double delta = declination / DegreesPerRadian;
        double hourAngle = (armc - rightAscension) / DegreesPerRadian;
        double sine = Math.Sin(phi) * Math.Sin(delta) + Math.Cos(phi) * Math.Cos(delta) * Math.Cos(hourAngle);
        return Math.Asin(Math.Clamp(sine, -1.0, 1.0)) * DegreesPerRadian;
    }

    /// <summary>
    /// Whether a longitude lies on the half of the ecliptic above the horizon: the arc from the Descendant forward
    /// to the Ascendant, or, where the ecliptic's upper-meridian point is below the horizon, the arc from the
    /// Ascendant forward to the Descendant.
    /// </summary>
    /// <param name="longitude">The ecliptic longitude, in degrees.</param>
    /// <param name="houses">
    /// The chart's houses: the Ascendant, the ARMC, and the <see cref="HouseData.Obliquity"/> and
    /// <see cref="HouseData.GeographicLatitude"/> the flip reads.
    /// </param>
    /// <returns>
    /// True above the horizon. A longitude exactly on the Descendant is above and one exactly on the Ascendant below,
    /// on either arc.
    /// </returns>
    /// <remarks>
    /// The upper-meridian point's declination is δ_M = atan(sin ARMC · tan ε); it is below the horizon when
    /// |φ − δ_M| exceeds 90°, which happens only at latitudes beyond 90° − ε. With a NaN obliquity or latitude, as a
    /// fixture that leaves them unset has, there is no flip.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="houses"/> is null.</exception>
    internal static bool IsAboveHorizonByLongitude(double longitude, HouseData houses)
    {
        ArgumentNullException.ThrowIfNull(houses);

        double ascendant = houses.Ascendant;
        double descendant = houses.Descendant;
        bool onDescendantToAscendant = descendant < ascendant
            ? longitude >= descendant && longitude < ascendant
            : longitude >= descendant || longitude < ascendant;

        if (!IsUpperMeridianPointBelowHorizon(houses))
        {
            return onDescendantToAscendant;
        }

        return longitude == descendant || (!onDescendantToAscendant && longitude != ascendant);
    }

    /// <summary>
    /// Whether the chart is a day chart: the Sun's longitude on the half of the ecliptic above the horizon
    /// (ruling D3, with S′ inside the polar circles), in every house system.
    /// </summary>
    /// <param name="sunLongitude">The Sun's ecliptic longitude, in degrees.</param>
    /// <param name="houses">The chart's houses.</param>
    /// <returns>True for a day chart.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="houses"/> is null.</exception>
    internal static bool IsDayChart(double sunLongitude, HouseData houses) =>
        IsAboveHorizonByLongitude(sunLongitude, houses);

    /// <summary>
    /// Whether the ecliptic's upper-meridian point, the point whose right ascension is the ARMC, lies below the
    /// horizon: |φ − δ_M| &gt; 90°, with δ_M = atan(sin ARMC · tan ε).
    /// </summary>
    /// <param name="houses">The chart's houses.</param>
    /// <returns>True when it lies below; false when the obliquity or the latitude is NaN.</returns>
    private static bool IsUpperMeridianPointBelowHorizon(HouseData houses)
    {
        if (double.IsNaN(houses.Obliquity) || double.IsNaN(houses.GeographicLatitude))
        {
            return false;
        }

        double declination = Math.Atan(Math.Sin(houses.Armc / DegreesPerRadian) * Math.Tan(houses.Obliquity / DegreesPerRadian))
            * DegreesPerRadian;
        return Math.Abs(houses.GeographicLatitude - declination) > 90.0;
    }
}
