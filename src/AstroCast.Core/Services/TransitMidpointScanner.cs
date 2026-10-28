using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Scans for transit activations of natal midpoints on the 45° dial.
/// A midpoint activation occurs when a transiting planet's 45°-reduced longitude
/// falls within <see cref="TransitOrbs.MidpointOrb"/> (10 arc-minutes) of a
/// natal midpoint position on the same dial.
/// </summary>
/// <remarks>
/// <para>
/// The 45° dial collapses the full 360° ecliptic into eight 45° sectors,
/// making all Ebertin harmonic contacts (0°, 45°, 90°, 135°, 180°) visible
/// as conjunctions on the reduced dial. This follows the standard cosmobiology
/// method from Reinhold Ebertin's "The Combination of Stellar Influences".
/// </para>
/// <para>
/// An activation is one passage of the body over one longitude (branch D, 1.3.4; the transit
/// rules of <see cref="TransitAspectFinder"/> applied to midpoints): a natal midpoint's dial
/// position m stands for the eight longitudes m + 45k, and the body's in-orb periods at one of them
/// whose gaps are shorter than <see cref="PassageGapDays"/> belong to one activation. It is listed
/// when the body is in orb at a 12:00 UT report sample, the in-orb samples at one longitude grouped
/// by the same gap; its entry and exit are found by walks from the passage's first and last in-orb
/// sample and bisected to the second; its passes are the crossings of the longitude, found on a
/// half-day grid from the sample before the entry instant to the one after the exit and kept
/// within [entry, exit]. Until 1.3.4 one activation covered every in-orb report day on the dial,
/// its entry and exit were the noons of the first and last in-orb samples, its passes were scanned
/// only between those noons, so a fast body's pass before its first in-orb noon was missed
/// (Jupiter: 79 of 568 in the real exports), and a signed separation on the dial joined the eight
/// longitudes (audit finding T1-8).
/// </para>
/// </remarks>
public sealed class TransitMidpointScanner
{
    #region Constants

    /// <summary>
    /// The gap that ends an activation, in 12:00 UT samples out of orb: two in-orb samples at one
    /// longitude with fewer than this many out-of-orb samples between them belong to one activation,
    /// and a walk for an entry or exit stops after this many consecutive samples out of orb. The
    /// transit rule's value (<see cref="TransitAspectFinder"/>).
    /// </summary>
    private const int PassageGapDays = 365;

    /// <summary>
    /// How far a walk for an entry or exit samples from the passage's first (last) in-orb report
    /// sample, in days; the transit rule's value. A walk that reaches it, or the span's edge, while
    /// in orb leaves the entry (exit) at that sample's noon, as the refinement's fallback does.
    /// </summary>
    private const int MaxWalkDays = 3653;

    /// <summary>
    /// A pass is a sign change of the signed separation between consecutive samples with both within
    /// this many degrees of the longitude: the guard against the change of sign at the longitude
    /// opposite, where the separation wraps from +180° to −180°.
    /// </summary>
    private const double PassGuardDegrees = 90.0;

    /// <summary>The pass scan's step in days: half a day, the 00:00 and 12:00 UT samples.</summary>
    private const double PassScanStepDays = 0.5;

    /// <summary>
    /// Number of binary search iterations for the entry, exit and exact pass refinements.
    /// 24 hours / 2^20 ≈ 0.08 seconds precision (~1 arc-second for outer planets).
    /// </summary>
    private const int BisectionIterations = 20;

    #endregion

    private readonly TransitEphemerisHelper _ephemeris;

    /// <summary>
    /// Initialises a new instance of <see cref="TransitMidpointScanner"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris helper for position lookups.</param>
    public TransitMidpointScanner(TransitEphemerisHelper ephemeris)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
    }

    /// <summary>
    /// Finds all midpoint activations for a single transiting body against
    /// the natal midpoint tree within the report period.
    /// </summary>
    /// <param name="transitBody">The transiting planet.</param>
    /// <param name="natalMidpoints">
    /// List of natal midpoints reduced to the 45° dial.
    /// Each entry contains the body pair, dial position, and priority flag.
    /// </param>
    /// <param name="startDate">Report start date.</param>
    /// <param name="endDate">Report end date.</param>
    /// <returns>
    /// The activations found during the report period, in midpoint order and, within one midpoint, by
    /// entry instant: one per passage of the body over one of the midpoint's eight dial longitudes
    /// with an in-orb 12:00 UT report sample.
    /// </returns>
    public IReadOnlyList<TransitMidpointActivation> FindActivations(
        TransitBody transitBody,
        IReadOnlyList<NatalMidpointTarget> natalMidpoints,
        DateOnly startDate,
        DateOnly endDate)
    {
        ArgumentNullException.ThrowIfNull(natalMidpoints);

        List<TransitMidpointActivation> results = [];

        // The body's longitude at 12:00 UT on every report date, read once for every midpoint and longitude
        double[] reportLongitudes = ReadReportLongitudes(transitBody, startDate, endDate);

        foreach (NatalMidpointTarget midpoint in natalMidpoints)
        {
            List<TransitMidpointActivation> midpointActivations = [];

            foreach (double longitude in DialLongitudes(midpoint.Dial45Position))
            {
                // One activation per passage over this longitude with an in-orb report sample
                foreach (List<DateOnly> passageDays in GroupByPassage(FindInOrbDays(reportLongitudes, startDate, longitude)))
                {
                    midpointActivations.Add(BuildActivation(
                        transitBody, midpoint, longitude, passageDays, startDate, endDate));
                }
            }

            // The midpoint's activations in time order, whichever longitude each passes
            midpointActivations.Sort((a, b) => a.Lifecycle[0].Date.CompareTo(b.Lifecycle[0].Date));
            results.AddRange(midpointActivations);
        }

        return results.AsReadOnly();
    }

    #region 45° Dial Helpers (Public Static)

    /// <summary>
    /// Calculates the shortest separation between two positions on the 45° dial.
    /// Returns a value in the range [0, 22.5].
    /// </summary>
    /// <param name="pos1">First position on the 45° dial (0–45).</param>
    /// <param name="pos2">Second position on the 45° dial (0–45).</param>
    /// <returns>Shortest separation in degrees (0–22.5).</returns>
    public static double Dial45Separation(double pos1, double pos2)
    {
        double diff = Math.Abs(pos1 - pos2);
        if (diff > 22.5)
            diff = 45.0 - diff;
        return diff;
    }

    /// <summary>
    /// Reduces an ecliptic longitude to its position on the 45° dial.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude (0–360).</param>
    /// <returns>Position on the 45° dial (0–45).</returns>
    public static double ReduceToDial45(double longitude)
    {
        return ((longitude % 45.0) + 45.0) % 45.0;
    }

    #endregion

    #region Private — Longitudes and Signed Separation

    /// <summary>A longitude brought into [0°, 360°).</summary>
    private static double Normalize(double longitude) => ((longitude % 360.0) + 360.0) % 360.0;

    /// <summary>
    /// The eight ecliptic longitudes a 45° dial position stands for: m + 45k for k = 0 to 7.
    /// </summary>
    /// <param name="dial45Position">The natal midpoint's position on the 45° dial.</param>
    /// <returns>The longitudes, in order of k.</returns>
    private static double[] DialLongitudes(double dial45Position)
    {
        double[] longitudes = new double[8];
        for (int k = 0; k < 8; k++)
            longitudes[k] = Normalize(ReduceToDial45(dial45Position) + 45.0 * k);

        return longitudes;
    }

    /// <summary>
    /// The signed separation of a longitude from a point, ((lon − point + 180) mod 360) − 180, in
    /// [−180°, 180°): negative behind the point in the order of the signs, positive past it. It
    /// wraps only at the point opposite, never in orb.
    /// </summary>
    /// <param name="longitude">The body's longitude.</param>
    /// <param name="point">The point's longitude.</param>
    /// <returns>The signed separation in degrees.</returns>
    private static double SignedSeparation(double longitude, double point) =>
        Normalize(longitude - point + 180.0) - 180.0;

    /// <summary>Whether a longitude is within <see cref="TransitOrbs.MidpointOrb"/> of a point.</summary>
    /// <param name="longitude">The body's longitude.</param>
    /// <param name="point">The point's longitude.</param>
    /// <returns>Whether the longitude is within orb.</returns>
    private static bool IsWithinOrb(double longitude, double point) =>
        Math.Abs(SignedSeparation(longitude, point)) <= TransitOrbs.MidpointOrb;

    #endregion

    #region Private — Report Period Scanning

    /// <summary>
    /// The body's longitude at 12:00 UT on every date of the report period, index 0 the start date.
    /// </summary>
    private double[] ReadReportLongitudes(TransitBody body, DateOnly startDate, DateOnly endDate)
    {
        int days = endDate.DayNumber - startDate.DayNumber + 1;
        double[] longitudes = new double[days];
        for (int i = 0; i < days; i++)
            longitudes[i] = _ephemeris.GetLongitude(body, _ephemeris.DateToJulianDay(startDate.AddDays(i)));

        return longitudes;
    }

    /// <summary>
    /// The report dates on which the body is within orb of the longitude at 12:00 UT.
    /// </summary>
    private static List<DateOnly> FindInOrbDays(double[] reportLongitudes, DateOnly startDate, double point)
    {
        List<DateOnly> days = [];
        for (int i = 0; i < reportLongitudes.Length; i++)
        {
            if (IsWithinOrb(reportLongitudes[i], point))
                days.Add(startDate.AddDays(i));
        }

        return days;
    }

    /// <summary>
    /// Groups the in-orb report samples at one longitude into passages: two consecutive in-orb
    /// samples with fewer than <see cref="PassageGapDays"/> samples out of orb between them belong
    /// to one passage, as the walks keep an entry or exit until that many consecutive samples out
    /// of orb.
    /// </summary>
    private static List<List<DateOnly>> GroupByPassage(List<DateOnly> inOrbDays)
    {
        List<List<DateOnly>> passages = [];
        foreach (DateOnly day in inOrbDays)
        {
            if (passages.Count > 0 && day.DayNumber - passages[^1][^1].DayNumber - 1 < PassageGapDays)
                passages[^1].Add(day);
            else
                passages.Add([day]);
        }

        return passages;
    }

    #endregion

    #region Private — Activation Construction

    /// <summary>
    /// Builds a complete <see cref="TransitMidpointActivation"/> with lifecycle for one passage of
    /// the body over one of the midpoint's dial longitudes, from the passage's in-orb report samples.
    /// </summary>
    private TransitMidpointActivation BuildActivation(
        TransitBody transitBody,
        NatalMidpointTarget midpoint,
        double longitude,
        List<DateOnly> passageDays,
        DateOnly reportStart,
        DateOnly reportEnd)
    {
        // Find entry and exit dates (may extend beyond report period)
        DateOnly entryDate = ScanForEntry(transitBody, longitude, passageDays[0]);
        DateOnly exitDate = ScanForExit(transitBody, longitude, passageDays[^1]);

        // Refine entry/exit to the second via binary search
        (DateTime entryTime, double entryJd) = RefineEntryTime(transitBody, longitude, entryDate);
        (DateTime exitTime, double exitJd) = RefineExitTime(transitBody, longitude, exitDate);

        // The crossings of the longitude between the entry and exit instants
        List<(DateTime DateTimeUtc, int PassNumber, double ResidualOrb)> exactPasses = FindExactPasses(
            transitBody, longitude, entryJd, exitJd, entryTime, exitTime);

        // Assemble lifecycle
        IReadOnlyList<TransitLifecycleEvent> lifecycle = BuildLifecycle(
            entryTime, exitTime, exactPasses, reportStart, reportEnd);

        // Get transit position at report start for orb calculation
        double startJd = _ephemeris.DateToJulianDay(reportStart);
        double transitLon = _ephemeris.GetLongitude(transitBody, startJd);
        double transitDial45 = ReduceToDial45(transitLon);

        // Calculate orb at report start
        double orbAtStart = Dial45Separation(transitDial45, midpoint.Dial45Position);

        return new TransitMidpointActivation
        {
            TransitPlanet = transitBody,
            MidpointBody1 = midpoint.Body1,
            MidpointBody2 = midpoint.Body2,
            TransitDial45Position = transitDial45,
            NatalMidpointDial45Position = midpoint.Dial45Position,
            OrbAtStartDegrees = orbAtStart,
            IsPriority = midpoint.IsPriority,
            PriorityReason = midpoint.PriorityReason,
            Lifecycle = lifecycle,
        };
    }

    #endregion

    #region Private — Entry / Exit Scanning

    /// <summary>
    /// Walks back from the passage's first in-orb report sample, a 12:00 UT sample a day, to the first
    /// sample of the passage: the earliest in-orb sample before which <see cref="PassageGapDays"/>
    /// consecutive samples are out of orb. The walk goes no further than <see cref="MaxWalkDays"/>
    /// and never samples before <see cref="EphemerisSpan.FirstJulianDay"/>.
    /// </summary>
    private DateOnly ScanForEntry(TransitBody body, double point, DateOnly firstInOrbDay)
    {
        DateOnly entryCandidate = firstInOrbDay;
        DateOnly currentDate = firstInOrbDay.AddDays(-1);
        DateOnly scanLimit = firstInOrbDay.AddDays(-MaxWalkDays);
        int consecutiveOutOfOrb = 0;

        while (currentDate >= scanLimit)
        {
            double jd = _ephemeris.DateToJulianDay(currentDate);
            if (jd < EphemerisSpan.FirstJulianDay)
                break; // the files show nothing earlier

            if (IsWithinOrb(_ephemeris.GetLongitude(body, jd), point))
            {
                entryCandidate = currentDate;
                consecutiveOutOfOrb = 0;
            }
            else
            {
                consecutiveOutOfOrb++;
                if (consecutiveOutOfOrb >= PassageGapDays)
                    return entryCandidate;
            }

            currentDate = currentDate.AddDays(-1);
        }

        return entryCandidate;
    }

    /// <summary>
    /// Walks forward from the passage's last in-orb report sample to the last sample of the passage:
    /// the latest in-orb sample after which <see cref="PassageGapDays"/> consecutive samples are out
    /// of orb. The walk goes no further than <see cref="MaxWalkDays"/> and never samples after
    /// <see cref="EphemerisSpan.LastJulianDay"/>.
    /// </summary>
    private DateOnly ScanForExit(TransitBody body, double point, DateOnly lastInOrbDay)
    {
        DateOnly lastInOrb = lastInOrbDay;
        DateOnly currentDate = lastInOrbDay.AddDays(1);
        DateOnly scanLimit = lastInOrbDay.AddDays(MaxWalkDays);
        int consecutiveOutOfOrb = 0;

        while (currentDate <= scanLimit)
        {
            double jd = _ephemeris.DateToJulianDay(currentDate);
            if (jd > EphemerisSpan.LastJulianDay)
                break; // the files show nothing later

            if (IsWithinOrb(_ephemeris.GetLongitude(body, jd), point))
            {
                lastInOrb = currentDate;
                consecutiveOutOfOrb = 0;
            }
            else
            {
                consecutiveOutOfOrb++;
                if (consecutiveOutOfOrb >= PassageGapDays)
                    return lastInOrb;
            }

            currentDate = currentDate.AddDays(1);
        }

        return lastInOrb;
    }

    /// <summary>
    /// Refines the entry date to the second by binary search between the 12:00 UT sample before the
    /// entry date, out of orb, and the entry date's, in orb.
    /// </summary>
    /// <returns>
    /// The entry moment to the whole second, and its Julian Day, taken to that second; or, where the
    /// bracket is invalid or its outside sample lies before the span, noon UT of the entry date and
    /// the scan's Julian Day for it.
    /// </returns>
    private (DateTime Time, double JulianDay) RefineEntryTime(TransitBody body, double point, DateOnly entryDate)
    {
        double jdOutside = _ephemeris.DateToJulianDay(entryDate.AddDays(-1));
        double jdInside = _ephemeris.DateToJulianDay(entryDate);

        bool outsideInOrb = jdOutside < EphemerisSpan.FirstJulianDay
            || IsWithinOrb(_ephemeris.GetLongitude(body, jdOutside), point);
        bool insideInOrb = IsWithinOrb(_ephemeris.GetLongitude(body, jdInside), point);

        if (outsideInOrb || !insideInOrb)
            return (entryDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), jdInside);

        for (int i = 0; i < BisectionIterations; i++)
        {
            double jdMid = (jdOutside + jdInside) / 2.0;
            if (IsWithinOrb(_ephemeris.GetLongitude(body, jdMid), point))
                jdInside = jdMid;
            else
                jdOutside = jdMid;
        }

        double jdEntry = (jdOutside + jdInside) / 2.0;
        return (_ephemeris.JulianDayToDateTime(jdEntry), JulianDayUtility.InstantToJulianDay(JulianDayUtility.JulianDayToInstant(jdEntry)));
    }

    /// <summary>
    /// Refines the exit date to the second by binary search between the exit date's 12:00 UT sample,
    /// in orb, and the one after, out of orb.
    /// </summary>
    /// <returns>
    /// The exit moment to the whole second, and its Julian Day, taken to that second; or, where the
    /// bracket is invalid or its outside sample lies after the span, noon UT of the exit date and the
    /// scan's Julian Day for it.
    /// </returns>
    private (DateTime Time, double JulianDay) RefineExitTime(TransitBody body, double point, DateOnly exitDate)
    {
        double jdInside = _ephemeris.DateToJulianDay(exitDate);
        double jdOutside = _ephemeris.DateToJulianDay(exitDate.AddDays(1));

        bool insideInOrb = IsWithinOrb(_ephemeris.GetLongitude(body, jdInside), point);
        bool outsideInOrb = jdOutside > EphemerisSpan.LastJulianDay
            || IsWithinOrb(_ephemeris.GetLongitude(body, jdOutside), point);

        if (!insideInOrb || outsideInOrb)
            return (exitDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), jdInside);

        for (int i = 0; i < BisectionIterations; i++)
        {
            double jdMid = (jdInside + jdOutside) / 2.0;
            if (IsWithinOrb(_ephemeris.GetLongitude(body, jdMid), point))
                jdInside = jdMid;
            else
                jdOutside = jdMid;
        }

        double jdExit = (jdInside + jdOutside) / 2.0;
        return (_ephemeris.JulianDayToDateTime(jdExit), JulianDayUtility.InstantToJulianDay(JulianDayUtility.JulianDayToInstant(jdExit)));
    }

    #endregion

    #region Private — Exact Pass Detection

    /// <summary>
    /// Finds the passes of the activation: the crossings of its longitude between the entry and exit
    /// instants. The samples are the 00:00 and 12:00 UT grid, from the sample at or before the entry
    /// instant to the one at or after the exit, within the span; a pass is a sign change of the
    /// signed separation between consecutive samples with both within <see cref="PassGuardDegrees"/>
    /// of the longitude, bisected to the second and kept only if its instant lies within
    /// [entry, exit].
    /// </summary>
    private List<(DateTime DateTimeUtc, int PassNumber, double ResidualOrb)> FindExactPasses(
        TransitBody body, double point, double entryJd, double exitJd, DateTime entryTime, DateTime exitTime)
    {
        List<(DateTime, int, double)> passes = [];
        int passNumber = 0;

        double scanStart = Math.Floor(entryJd / PassScanStepDays) * PassScanStepDays;
        while (scanStart < EphemerisSpan.FirstJulianDay)
            scanStart += PassScanStepDays;

        double scanEnd = Math.Ceiling(exitJd / PassScanStepDays) * PassScanStepDays;
        while (scanEnd > EphemerisSpan.LastJulianDay)
            scanEnd -= PassScanStepDays;

        double jdPrev = scanStart;
        double prevDelta = SignedSeparation(_ephemeris.GetLongitude(body, jdPrev), point);

        for (double jdCurr = scanStart + PassScanStepDays; jdCurr <= scanEnd; jdCurr += PassScanStepDays)
        {
            double currDelta = SignedSeparation(_ephemeris.GetLongitude(body, jdCurr), point);

            bool signChanged = (prevDelta > 0 && currDelta < 0) || (prevDelta < 0 && currDelta > 0);
            if (signChanged && Math.Abs(prevDelta) < PassGuardDegrees && Math.Abs(currDelta) < PassGuardDegrees)
            {
                (DateTime exactTime, double residualOrb) = RefineExactPassTime(jdPrev, prevDelta, jdCurr, body, point);
                if (exactTime >= entryTime && exactTime <= exitTime)
                {
                    passNumber++;
                    passes.Add((exactTime, passNumber, residualOrb));
                }
            }

            prevDelta = currDelta;
            jdPrev = jdCurr;
        }

        return passes;
    }

    /// <summary>
    /// Uses binary search (bisection) to refine a crossing of the longitude to the second within a
    /// half-day bracket.
    /// </summary>
    /// <param name="jdLow">Julian Day at the earlier sample (sign change bracket start).</param>
    /// <param name="deltaLow">Signed separation from the longitude at <paramref name="jdLow"/>.</param>
    /// <param name="jdHigh">Julian Day at the later sample (sign change bracket end).</param>
    /// <param name="body">The transiting body.</param>
    /// <param name="point">The longitude.</param>
    /// <returns>
    /// Tuple of (UTC DateTime of the exact pass, residual orb in degrees at the refined moment).
    /// </returns>
    private (DateTime DateTimeUtc, double ResidualOrb) RefineExactPassTime(
        double jdLow, double deltaLow, double jdHigh, TransitBody body, double point)
    {
        for (int i = 0; i < BisectionIterations; i++)
        {
            double jdMid = (jdLow + jdHigh) / 2.0;
            double deltaMid = SignedSeparation(_ephemeris.GetLongitude(body, jdMid), point);

            if (Math.Sign(deltaMid) == Math.Sign(deltaLow))
            {
                jdLow = jdMid;
                deltaLow = deltaMid;
            }
            else
            {
                jdHigh = jdMid;
            }
        }

        double jdExact = (jdLow + jdHigh) / 2.0;
        double residualOrb = Math.Abs(SignedSeparation(_ephemeris.GetLongitude(body, jdExact), point));
        return (_ephemeris.JulianDayToDateTime(jdExact), residualOrb);
    }

    #endregion

    #region Private — Lifecycle Assembly

    /// <summary>
    /// Assembles lifecycle events for a midpoint activation.
    /// Includes Entry, Exact passes, and Exit (no station tracking for midpoints).
    /// </summary>
    /// <remarks>
    /// Every event carries a refined instant: the entry and exit the crossings of the orb and the
    /// passes the crossings of the longitude, each bisected to the second. Only an entry or exit
    /// whose refinement bracket was invalid is 12:00 UT of its date. No event carries a longitude
    /// (<see cref="TransitLifecycleEvent.TransitLongitude"/> stays null).
    /// </remarks>
    private static IReadOnlyList<TransitLifecycleEvent> BuildLifecycle(
        DateTime entryTime,
        DateTime exitTime,
        List<(DateTime DateTimeUtc, int PassNumber, double ResidualOrb)> exactPasses,
        DateOnly reportStart,
        DateOnly reportEnd)
    {
        List<TransitLifecycleEvent> events = [];

        events.Add(new TransitLifecycleEvent
        {
            EventType = TransitLifecycleEventType.Entry,
            Date = entryTime,
            BeforeReportPeriod = DateOnly.FromDateTime(entryTime) < reportStart,
        });

        foreach ((DateTime dateTimeUtc, int passNumber, double residualOrb) in exactPasses)
        {
            DateOnly passDate = DateOnly.FromDateTime(dateTimeUtc);
            events.Add(new TransitLifecycleEvent
            {
                EventType = TransitLifecycleEventType.Exact,
                Date = dateTimeUtc,
                PassNumber = passNumber,
                ExactOrbDegrees = residualOrb,
                BeforeReportPeriod = passDate < reportStart,
                BeyondReportPeriod = passDate > reportEnd,
            });
        }

        events.Add(new TransitLifecycleEvent
        {
            EventType = TransitLifecycleEventType.Exit,
            Date = exitTime,
            BeyondReportPeriod = DateOnly.FromDateTime(exitTime) > reportEnd,
        });

        // Sort: Entry first, Exit last, exact passes by date in between
        events.Sort((a, b) =>
        {
            if (a.EventType == TransitLifecycleEventType.Entry) return -1;
            if (b.EventType == TransitLifecycleEventType.Entry) return 1;
            if (a.EventType == TransitLifecycleEventType.Exit) return 1;
            if (b.EventType == TransitLifecycleEventType.Exit) return -1;
            return a.Date.CompareTo(b.Date);
        });

        return events.AsReadOnly();
    }

    #endregion
}
