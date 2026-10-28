using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// The one rule that finds the house an ecliptic longitude lies in: house <c>i</c> is the arc from cusp <c>i</c>
/// to cusp <c>i + 1</c> (house 12 runs to cusp 1), taken in the direction the cusps run, cusp <c>i</c> included
/// and cusp <c>i + 1</c> excluded (rulings D2 and S-D1, branch F6, 1.3.3).
/// </summary>
/// <remarks>
/// <para>
/// The cusps run forward when their twelve forward spans add up to a whole circle, and backward when their twelve
/// backward spans do. Regiomontanus and Campanus run backward inside the polar circles, where the ecliptic's
/// upper-meridian point is below the horizon and the library flips their Midheaven to keep houses 7–12 above it.
/// Every other cusp set the library returns runs forward.
/// </para>
/// <para>
/// Membership is decided by comparisons only, so no arithmetic can round a longitude onto a cusp. A longitude of
/// exactly 360.0 is read as 0.0; nothing else is normalised. An arc of zero length holds nothing.
/// </para>
/// <para>
/// Until 1.3.3 four helpers placed a longitude between cusps, each with its own edge cases, and natal bodies were
/// placed by the library's house position with their ecliptic latitude, which can differ from the longitude's
/// house.
/// </para>
/// </remarks>
internal static class HousePlacement
{
    /// <summary>The number of house cusps.</summary>
    private const int CuspCount = 12;

    /// <summary>How far twelve spans may add up from a whole circle and still be read as one, in degrees.</summary>
    private const double CircleTolerance = 1e-9;

    /// <summary>
    /// The house whose arc holds the longitude, in the direction the cusps run.
    /// </summary>
    /// <param name="longitude">The ecliptic longitude, in degrees, 0 to 360.</param>
    /// <param name="cusps">The twelve cusp longitudes, cusp 1 first, in degrees, 0 to under 360.</param>
    /// <returns>
    /// The house, 1 to 12, or <see langword="null"/> when no arc holds the longitude: a NaN, or cusps that run in
    /// neither direction (all equal). No chart the library casts produces this.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cusps"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cusps"/> does not hold twelve cusps.</exception>
    internal static int? HouseOf(double longitude, IReadOnlyList<double> cusps)
    {
        int index = FindArc(longitude, cusps, out _);
        return index < 0 ? null : index + 1;
    }

    /// <summary>
    /// The house plus the distance from its cusp along its arc, divided by the arc's length, in the direction the
    /// cusps run: 10.45 is 45% of the way through the 10th house.
    /// </summary>
    /// <param name="longitude">The ecliptic longitude, in degrees, 0 to 360.</param>
    /// <param name="cusps">The twelve cusp longitudes, cusp 1 first, in degrees, 0 to under 360.</param>
    /// <returns>
    /// The house and its fraction, whose whole part always equals <see cref="HouseOf"/>; or
    /// <see langword="null"/> where <see cref="HouseOf"/> is.
    /// </returns>
    /// <remarks>
    /// A fraction that rounds to 1 is held at the largest double below 1, and a sum that rounds up to the next
    /// house is held at the largest double below it, so the whole part never names the next house.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cusps"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cusps"/> does not hold twelve cusps.</exception>
    internal static double? HouseExactOf(double longitude, IReadOnlyList<double> cusps)
    {
        int index = FindArc(longitude, cusps, out bool forward);
        if (index < 0)
        {
            return null;
        }

        double point = longitude == 360.0 ? 0.0 : longitude;
        double cusp = cusps[index];
        double nextCusp = cusps[(index + 1) % CuspCount];
        double along = forward ? ForwardSpan(cusp, point) : ForwardSpan(point, cusp);
        double length = forward ? ForwardSpan(cusp, nextCusp) : ForwardSpan(nextCusp, cusp);

        double fraction = along / length;
        if (fraction >= 1.0)
        {
            fraction = Math.BitDecrement(1.0);
        }

        double house = index + 1;
        double exact = house + fraction;
        return exact < house + 1.0 ? exact : Math.BitDecrement(house + 1.0);
    }

    /// <summary>
    /// Sets each position's <see cref="PlanetPosition.House"/> and <see cref="PlanetPosition.HouseExact"/> from its
    /// longitude: <see cref="HouseOf"/> and <see cref="HouseExactOf"/>.
    /// </summary>
    /// <param name="positions">The positions to place.</param>
    /// <param name="houses">The chart's houses.</param>
    /// <remarks>
    /// Where no arc holds a longitude both are left as they are, null on a freshly calculated position, as a failed
    /// placement left them before. No chart the library casts produces this.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="positions"/> or <paramref name="houses"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the houses do not hold twelve cusps.</exception>
    internal static void Assign(IReadOnlyList<PlanetPosition> positions, HouseData houses)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(houses);

        foreach (PlanetPosition position in positions)
        {
            if (HouseOf(position.Longitude, houses.Cusps) is int house)
            {
                position.House = house;
                position.HouseExact = HouseExactOf(position.Longitude, houses.Cusps);
            }
        }
    }

    /// <summary>
    /// The index (0 to 11) of the arc that holds the longitude, in the direction the cusps run.
    /// </summary>
    /// <param name="longitude">The ecliptic longitude, in degrees.</param>
    /// <param name="cusps">The twelve cusp longitudes, cusp 1 first.</param>
    /// <param name="forward">True when the cusps run forward through the zodiac.</param>
    /// <returns>The arc's index, or −1 when no arc holds the longitude.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="cusps"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cusps"/> does not hold twelve cusps.</exception>
    private static int FindArc(double longitude, IReadOnlyList<double> cusps, out bool forward)
    {
        ArgumentNullException.ThrowIfNull(cusps);
        if (cusps.Count != CuspCount)
        {
            throw new ArgumentException($"A chart has {CuspCount} house cusps; {cusps.Count} were given.", nameof(cusps));
        }

        double forwardTotal = 0.0;
        double backwardTotal = 0.0;
        for (int i = 0; i < CuspCount; i++)
        {
            forwardTotal += ForwardSpan(cusps[i], cusps[(i + 1) % CuspCount]);
            backwardTotal += ForwardSpan(cusps[(i + 1) % CuspCount], cusps[i]);
        }

        forward = Math.Abs(forwardTotal - 360.0) <= CircleTolerance;
        bool backward = !forward && Math.Abs(backwardTotal - 360.0) <= CircleTolerance;
        if (!forward && !backward)
        {
            return -1;
        }

        double point = longitude == 360.0 ? 0.0 : longitude;
        for (int i = 0; i < CuspCount; i++)
        {
            double cusp = cusps[i];
            double nextCusp = cusps[(i + 1) % CuspCount];
            if (forward ? IsOnForwardArc(point, cusp, nextCusp) : IsOnBackwardArc(point, cusp, nextCusp))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Whether the arc from <paramref name="from"/> forward through the zodiac to <paramref name="to"/> holds the
    /// point: <paramref name="from"/> included, <paramref name="to"/> excluded. Comparisons only.
    /// </summary>
    /// <param name="point">The longitude to test.</param>
    /// <param name="from">The arc's first cusp.</param>
    /// <param name="to">The next cusp.</param>
    /// <returns>True when the arc holds the point; an arc of zero length holds nothing.</returns>
    private static bool IsOnForwardArc(double point, double from, double to) =>
        from < to ? point >= from && point < to : from > to && (point >= from || point < to);

    /// <summary>
    /// Whether the arc from <paramref name="from"/> backward through the zodiac to <paramref name="to"/> holds the
    /// point: <paramref name="from"/> included, <paramref name="to"/> excluded. Comparisons only.
    /// </summary>
    /// <param name="point">The longitude to test.</param>
    /// <param name="from">The arc's first cusp.</param>
    /// <param name="to">The next cusp, behind it in the zodiac.</param>
    /// <returns>True when the arc holds the point; an arc of zero length holds nothing.</returns>
    private static bool IsOnBackwardArc(double point, double from, double to) =>
        to < from ? point <= from && point > to : to > from && (point <= from || point > to);

    /// <summary>
    /// The distance from <paramref name="from"/> forward through the zodiac to <paramref name="to"/>.
    /// </summary>
    /// <param name="from">The starting longitude, in degrees, 0 to under 360.</param>
    /// <param name="to">The ending longitude, in degrees, 0 to under 360.</param>
    /// <returns>The distance, in degrees, from 0 to under 360.</returns>
    private static double ForwardSpan(double from, double to)
    {
        double span = to - from;
        return span < 0.0 ? span + 360.0 : span;
    }
}
