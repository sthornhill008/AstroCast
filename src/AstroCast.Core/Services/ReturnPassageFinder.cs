using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// One passage of a planet over its natal longitude after birth: return #<see cref="Number"/>,
/// whose instant is its first pass (ruling T-D6, branch F, 1.3.4).
/// </summary>
/// <param name="Number">The passage's number, counted from the birth instant.</param>
/// <param name="PassJulianDays">
/// The passes' Julian Days (UT) in time order, each bisected to the second: one, or three when a
/// retrograde loop lies over the degree (direct, retrograde, direct).
/// </param>
internal sealed record ReturnPassage(int Number, IReadOnlyList<double> PassJulianDays)
{
    /// <summary>The first pass, the return's instant.</summary>
    public double FirstPass => PassJulianDays[0];
}

/// <summary>
/// Finds the passages of a planet over its natal longitude, for every consumer of a planetary
/// return: the return charts, the return and ingress timeline and the cycle timeline (branch F,
/// 1.3.4). The crossings of the natal longitude after birth, each direct or retrograde by the sign
/// of the planet's speed at the crossing, group as follows: a direct crossing begins a passage; a
/// retrograde crossing, and the direct crossing after it, belong to the passage in progress. The
/// birth's own passage is not a return: when the planet is retrograde at birth its first direct
/// crossing after birth belongs to it, and when the planet is direct at birth and its first
/// crossing after birth is retrograde, that crossing and the direct one after it belong to it.
/// Return #N is the Nth passage after birth; a passage has one pass or three.
/// </summary>
/// <remarks>
/// <para>
/// The scan steps a day from the first whole day after birth, as
/// <see cref="ReturnChartFinder.FindReturnJulianDays"/> does, and never reads beyond
/// <see cref="EphemerisSpan.LastJulianDay"/>. A passage is complete, and yielded, once the planet
/// lies more than <see cref="PassageCloseDegrees"/> past the degree after its last direct
/// crossing: no retrograde loop of Mercury (about 13°), Venus (17°), Mars (19.5°), Jupiter (10°)
/// or Saturn (7°) reaches back that far, so no later crossing can belong to it. A passage still in
/// progress when the span ends is yielded with the passes found.
/// </para>
/// <para>
/// Until 1.3.4 a return by number was the last crossing (Jupiter, Saturn) or the first (the rest)
/// in a window around birth + N × the sidereal period, so Mercury's and Venus's numbers counted
/// sidereal periods, not returns (T2-2); the timeline grouped crossings less than 120 days apart
/// (T2-5); and no consumer knew a return's other passes (T2-4, T5-3).
/// </para>
/// </remarks>
internal static class ReturnPassageFinder
{
    /// <summary>
    /// How far past the degree, in degrees of longitude, the planet must lie after a direct crossing
    /// for the passage to be complete: beyond the longest retrograde arc of the five planets.
    /// </summary>
    internal const double PassageCloseDegrees = 30.0;

    /// <summary>
    /// The passages of a planet over its natal longitude, in order, from birth to the end of the
    /// ephemeris span, each yielded once complete; the natal longitude, the birth instant and the
    /// planet's direction at birth are the natal chart's.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for longitude and speed lookups.</param>
    /// <param name="natalChart">The natal chart.</param>
    /// <param name="planet">The returning planet.</param>
    /// <param name="cancellationToken">Cancellation token, polled at every daily step.</param>
    /// <returns>The passages, lazily: the scan runs only as far as the caller reads.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the natal chart holds no position for the planet.</exception>
    internal static IEnumerable<ReturnPassage> EnumeratePassages(
        IEphemerisService ephemeris,
        ChartResult natalChart,
        CelestialBodyId planet,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(natalChart);

        PlanetPosition? natal = natalChart.Positions.FirstOrDefault(p => p.Body == planet)
            ?? throw new InvalidOperationException($"Natal chart does not contain a {planet} position.");

        return EnumeratePassages(
            ephemeris, planet, natal.Longitude, natalChart.JulianDay,
            retrogradeAtBirth: natal.SpeedInLongitude < 0.0, cancellationToken);
    }

    /// <summary>
    /// The passages of a planet over a natal longitude, in order, from birth to the end of the
    /// ephemeris span, each yielded once complete.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for longitude and speed lookups.</param>
    /// <param name="planet">The returning planet.</param>
    /// <param name="natalLongitude">The natal longitude, in degrees.</param>
    /// <param name="birthJd">The birth instant (Julian Day, UT).</param>
    /// <param name="retrogradeAtBirth">Whether the planet is retrograde at birth.</param>
    /// <param name="cancellationToken">Cancellation token, polled at every daily step.</param>
    /// <returns>The passages, lazily: the scan runs only as far as the caller reads.</returns>
    internal static IEnumerable<ReturnPassage> EnumeratePassages(
        IEphemerisService ephemeris,
        CelestialBodyId planet,
        double natalLongitude,
        double birthJd,
        bool retrogradeAtBirth,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);

        // The passage in progress: the birth's own while isBirthPassage, which is never a return
        List<double> passes = [];
        bool open = retrogradeAtBirth;
        bool isBirthPassage = retrogradeAtBirth;
        bool lastCrossingDirect = false;
        int number = 0;

        double jdPrev = birthJd + 1.0;
        double prevDelta = ReturnChartFinder.SignedLongitudeDelta(
            ephemeris.CalculatePositionLongitudeOnly(jdPrev, planet).Longitude, natalLongitude);

        for (double jdCurr = jdPrev + 1.0; jdCurr <= EphemerisSpan.LastJulianDay; jdCurr += 1.0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            double currDelta = ReturnChartFinder.SignedLongitudeDelta(
                ephemeris.CalculatePositionLongitudeOnly(jdCurr, planet).Longitude, natalLongitude);

            // A crossing of the natal longitude between the two days, not of the point opposite
            bool signChanged = (prevDelta > 0 && currDelta < 0) || (prevDelta < 0 && currDelta > 0);
            if (signChanged && Math.Abs(prevDelta) + Math.Abs(currDelta) < 90.0)
            {
                ReturnCrossing crossing = ReturnChartFinder.RefineCrossing(
                    ephemeris, planet, jdPrev, prevDelta, jdCurr, natalLongitude);

                if (crossing.IsDirect && open && !lastCrossingDirect)
                {
                    // The direct crossing after a retrograde one, or after a retrograde birth
                    passes.Add(crossing.JulianDay);
                }
                else if (crossing.IsDirect)
                {
                    // A direct crossing begins a passage
                    if (open && !isBirthPassage)
                    {
                        number++;
                        yield return new ReturnPassage(number, passes.AsReadOnly());
                    }

                    passes = [crossing.JulianDay];
                    open = true;
                    isBirthPassage = false;
                }
                else if (!open)
                {
                    // Direct at birth, and the first crossing retrograde: the birth's own passage
                    passes = [crossing.JulianDay];
                    open = true;
                    isBirthPassage = true;
                }
                else
                {
                    passes.Add(crossing.JulianDay);
                }

                lastCrossingDirect = crossing.IsDirect;
            }

            // Complete once the planet is past the degree by more than any retrograde arc
            if (open && lastCrossingDirect && currDelta > PassageCloseDegrees)
            {
                if (!isBirthPassage)
                {
                    number++;
                    yield return new ReturnPassage(number, passes.AsReadOnly());
                }

                passes = [];
                open = false;
                isBirthPassage = false;
            }

            prevDelta = currDelta;
            jdPrev = jdCurr;
        }

        // The passage in progress when the span ends: the passes found so far
        if (open && !isBirthPassage && passes.Count > 0)
        {
            number++;
            yield return new ReturnPassage(number, passes.AsReadOnly());
        }
    }
}
