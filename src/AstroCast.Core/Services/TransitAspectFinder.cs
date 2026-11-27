using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Finds active transit-to-natal aspects within a date range and calculates
/// their full lifecycle (entry, exact passes, stations, exit).
/// </summary>
/// <remarks>
/// <para>
/// A transit is one passage of the body over one point (ruling T-D3, branch D, 1.3.4). For a natal
/// point of longitude n and an aspect of angle a, the aspect's points are n + a and n − a, one point
/// when a is 0° or 180°; the body's in-orb periods at one point whose gaps are shorter than
/// <see cref="PassageGapDays"/> belong to one transit, and a gap of that many days or more ends it.
/// For each transiting body the report period is sampled at 12:00 UT, read once per date; a transit is
/// listed when the body is in orb at one of those samples, the in-orb samples at one point grouped by
/// the same gap, so a report may list two transits of one body, point and aspect. Its lifecycle is
/// found by walking back from the passage's first in-orb sample and forward from its last for the
/// entry and exit, each bisected to the second, then between them for the passes, crossings of the
/// transit's own point, and the stations at whose instant the body is in orb.
/// </para>
/// <para>
/// Until 1.3.4 one <see cref="ActiveTransit"/> covered every in-orb report day of an aspect at either
/// point, the walks stopped after 90 days out of orb and 365 days from the report, a pass was a sign
/// change of the separation to the nearer point (so a body passing the natal point or the point
/// opposite was given an exact pass of 30° to 120°), and the stations were the report period's
/// (audit findings T1-1, T1-2, T1-9, T1-10).
/// </para>
/// </remarks>
public sealed class TransitAspectFinder
{
    #region Aspect Definitions

    /// <summary>Compact representation of a transit aspect type.</summary>
    private readonly record struct AspectDef(string Name, string Abbreviation, double Angle, bool IsMinor);

    /// <summary>Major Ptolemaic aspects checked for all transit calculations.</summary>
    private static readonly AspectDef[] MajorAspects =
    [
        new("Conjunction", "cnj", 0.0, false),
        new("Sextile", "sxt", 60.0, false),
        new("Square", "sqr", 90.0, false),
        new("Trine", "tri", 120.0, false),
        new("Opposition", "opp", 180.0, false),
    ];

    /// <summary>Minor / Ebertin aspects included when the user enables them.</summary>
    private static readonly AspectDef[] MinorAspects =
    [
        new("Semisextile", "ssx", 30.0, true),
        new("Semisquare", "ssq", 45.0, true),
        new("Sesquiquintile", "sqn", 108.0, true),
        new("Sesquiquadrate", "ses", 135.0, true),
        new("Quincunx", "qnx", 150.0, true),
    ];

    /// <summary>
    /// Looks up a transit aspect by the name an <see cref="ActiveTransit"/> carries in
    /// <see cref="ActiveTransit.AspectName"/>, in the finder's own table.
    /// </summary>
    /// <param name="aspectName">The aspect's name as the finder writes it ("Opposition").</param>
    /// <param name="angle">The aspect's angle in degrees; 0 when the name is not one of the finder's.</param>
    /// <param name="isMinor">Whether it is one of the minor aspects; false when the name is not one of the finder's.</param>
    /// <returns>Whether the name is one of the finder's aspects.</returns>
    public static bool TryGetAspect(string aspectName, out double angle, out bool isMinor)
    {
        foreach (AspectDef aspect in MajorAspects.Concat(MinorAspects))
        {
            if (string.Equals(aspect.Name, aspectName, StringComparison.Ordinal))
            {
                angle = aspect.Angle;
                isMinor = aspect.IsMinor;
                return true;
            }
        }

        angle = 0.0;
        isMinor = false;
        return false;
    }

    #endregion

    #region Constants

    /// <summary>
    /// The gap that ends a transit, in 12:00 UT samples out of orb: two in-orb samples at one point with
    /// fewer than this many out-of-orb samples between them belong to one transit, and a walk for an
    /// entry or exit stops after this many consecutive samples out of orb. Measured over the span
    /// 1200–2399 (gaps_d_span.out): the longest gap inside one passage is 287 days (Pluto) and the
    /// shortest between two passages of the same point 516 days (Mars), so no body's gap falls near
    /// the rule.
    /// </summary>
    private const int PassageGapDays = 365;

    /// <summary>
    /// How far a walk for an entry or exit samples from the passage's first (last) in-orb report
    /// sample, in days: ten years, against the longest passage over the span, 2047 days (Pluto). A
    /// walk that reaches it, or the span's edge, while in orb leaves the entry (exit) at that sample's
    /// noon, as the refinement's fallback does; inside the span that takes a report more than ten
    /// years from the span's edges and does not happen.
    /// </summary>
    private const int MaxWalkDays = 3653;

    /// <summary>
    /// A pass is a sign change of the signed separation between consecutive samples with both within
    /// this many degrees of the point: the guard <see cref="TransitToTransitAspectFinder"/> keeps
    /// against the change of sign at the point opposite, where the separation wraps from +180° to −180°.
    /// </summary>
    private const double PassGuardDegrees = 90.0;

    /// <summary>Threshold in degrees for considering an aspect "exact" at report start.</summary>
    private const double ExactThreshold = 0.01;

    #endregion

    private readonly TransitEphemerisHelper _ephemeris;
    private readonly TransitStationDetector _stationDetector;

    /// <summary>
    /// Initialises a new instance of <see cref="TransitAspectFinder"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris helper for position and motion lookups.</param>
    /// <param name="stationDetector">Station detector for finding in-orb stations.</param>
    public TransitAspectFinder(
        TransitEphemerisHelper ephemeris,
        TransitStationDetector stationDetector)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(stationDetector);
        _ephemeris = ephemeris;
        _stationDetector = stationDetector;
    }

    /// <summary>
    /// Finds all active transit-to-natal aspects for a single transiting body
    /// against all natal targets within the report period.
    /// </summary>
    /// <param name="transitBody">The transiting planet.</param>
    /// <param name="natalTargets">
    /// List of natal targets: name, symbol, longitude, and house number.
    /// Includes all 12 planets/points plus ASC and MC.
    /// </param>
    /// <param name="startDate">Report start date.</param>
    /// <param name="endDate">Report end date.</param>
    /// <param name="includeMinorAspects">Whether to check minor aspects too.</param>
    /// <param name="natalHouseCusps">
    /// Natal house cusp longitudes (index 0 = 1st house cusp, etc.) for determining
    /// which house the transit planet occupies.
    /// </param>
    /// <param name="cancellationToken">
    /// Token used to abort the outer natal-target / aspect-def scan loops between
    /// iterations. The tight bisection refinement loops are not polled because
    /// each iteration is bounded and short.
    /// </param>
    /// <returns>
    /// The active transits, in natal target order, then aspect order, then, within one aspect, by
    /// entry instant: one per passage of the body over one of the aspect's points with an in-orb
    /// 12:00 UT report sample.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="startDate"/> is after <paramref name="endDate"/>,
    /// if either boundary is <see cref="DateOnly.MinValue"/> or <see cref="DateOnly.MaxValue"/>,
    /// or if the range exceeds 100 years. The cap defends against malformed inputs
    /// (e.g. default-constructed or NaN-derived dates) that could otherwise cause the scan
    /// to run for an unbounded duration.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown if <paramref name="cancellationToken"/> is signalled while iterating.
    /// </exception>
    public IReadOnlyList<ActiveTransit> FindActiveTransits(
        TransitBody transitBody,
        IReadOnlyList<NatalTarget> natalTargets,
        DateOnly startDate,
        DateOnly endDate,
        bool includeMinorAspects,
        IReadOnlyList<double> natalHouseCusps,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalTargets);
        ArgumentNullException.ThrowIfNull(natalHouseCusps);
        ValidateScanRange(startDate, endDate);

        List<ActiveTransit> results = [];

        AspectDef[] aspects = includeMinorAspects
            ? [.. MajorAspects, .. MinorAspects]
            : MajorAspects;

        // The body's longitude at 12:00 UT on every report date, read once for every target, aspect and point
        double[] reportLongitudes = ReadReportLongitudes(transitBody, startDate, endDate);

        foreach (NatalTarget natal in natalTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (AspectDef aspect in aspects)
            {
                double orb = TransitOrbs.GetOrb(transitBody, aspect.Angle, aspect.IsMinor);
                List<ActiveTransit> aspectTransits = [];

                foreach (double point in AspectPoints(natal.Longitude, aspect.Angle))
                {
                    // One transit per passage over this point with an in-orb report sample
                    foreach (List<DateOnly> passageDays in GroupByPassage(FindInOrbDays(reportLongitudes, startDate, point, orb)))
                    {
                        aspectTransits.Add(BuildActiveTransit(
                            transitBody, natal, aspect, orb, point, passageDays, startDate, endDate, natalHouseCusps));
                    }
                }

                // The aspect's transits in time order, whichever point each passes
                aspectTransits.Sort((a, b) => a.Lifecycle[0].Date.CompareTo(b.Lifecycle[0].Date));
                results.AddRange(aspectTransits);
            }
        }

        return results.AsReadOnly();
    }

    #region Input Validation

    /// <summary>
    /// Maximum scan-window width accepted by <see cref="FindActiveTransits"/>.
    /// Expressed as a span of days corresponding to 100 years.
    /// </summary>
    /// <remarks>
    /// 100 years far exceeds any legitimate transit report window (typical reports
    /// cover weeks to a few years). The cap defends against malformed inputs — e.g.
    /// a caller accidentally passing a default-constructed date, or a NaN-derived
    /// JD round-trip — that would otherwise run an unbounded day-by-day scan.
    /// </remarks>
    private const int MaxScanRangeDays = 365 * 100;

    /// <summary>
    /// Validates that the scan range is well-formed: its end not before its start, not using
    /// sentinel boundary values, and not exceeding the <see cref="MaxScanRangeDays"/> cap.
    /// </summary>
    /// <remarks>
    /// Both bounds are whole days of the report, so a start and an end on the same date are a report of one
    /// day, with its one 12:00 UT sample (branch InputChecks, 1.7.2, finding N6). Until 1.7.2 the start had to
    /// be strictly before the end, and a one-day report was refused.
    /// </remarks>
    /// <param name="startDate">Inclusive start of the scan window.</param>
    /// <param name="endDate">Inclusive end of the scan window.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if the range is inverted, uses <see cref="DateOnly.MinValue"/>/<see cref="DateOnly.MaxValue"/>
    /// boundaries, or exceeds <see cref="MaxScanRangeDays"/> days.
    /// </exception>
    private static void ValidateScanRange(DateOnly startDate, DateOnly endDate)
    {
        if (startDate > endDate)
        {
            throw new ArgumentException(
                $"Scan range start ({startDate:O}) must not be after end ({endDate:O}).",
                nameof(startDate));
        }

        if (startDate == DateOnly.MinValue || endDate == DateOnly.MaxValue)
        {
            throw new ArgumentException(
                "Scan range must not use DateOnly.MinValue or DateOnly.MaxValue boundaries.",
                nameof(startDate));
        }

        int rangeDays = endDate.DayNumber - startDate.DayNumber;
        if (rangeDays > MaxScanRangeDays)
        {
            throw new ArgumentException(
                $"Scan range exceeds 100 years ({rangeDays} days). " +
                "This is likely a malformed input; narrow the window.",
                nameof(endDate));
        }
    }

    #endregion

    #region Angular Separation (Public Static)

    /// <summary>
    /// Calculates the shortest angular separation between two ecliptic longitudes.
    /// Returns a value in the range [0, 180].
    /// </summary>
    /// <param name="lon1">First longitude (0–360).</param>
    /// <param name="lon2">Second longitude (0–360).</param>
    /// <returns>Angular separation in degrees (0–180).</returns>
    public static double ShortestArc(double lon1, double lon2)
    {
        double diff = Math.Abs(lon1 - lon2) % 360.0;
        return diff > 180.0 ? 360.0 - diff : diff;
    }

    /// <summary>
    /// Checks if two longitudes are within orb of a specific aspect angle.
    /// </summary>
    /// <param name="transitLon">Transit planet longitude (0–360).</param>
    /// <param name="natalLon">Natal target longitude (0–360).</param>
    /// <param name="aspectAngle">Exact aspect angle (e.g., 0, 60, 90, 120, 180).</param>
    /// <param name="orb">Maximum orb allowance in degrees.</param>
    /// <returns>True if the angular separation is within orb of the aspect angle.</returns>
    public static bool IsWithinOrb(double transitLon, double natalLon, double aspectAngle, double orb)
    {
        double separation = ShortestArc(transitLon, natalLon);
        return Math.Abs(separation - aspectAngle) <= orb;
    }

    #endregion

    #region Private — Points and Signed Separation

    /// <summary>A longitude brought into [0°, 360°).</summary>
    private static double Normalize(double longitude) => ((longitude % 360.0) + 360.0) % 360.0;

    /// <summary>
    /// The ecliptic points of an aspect to a natal longitude: n + a and n − a, or the one point n + a
    /// when a is 0° or 180°.
    /// </summary>
    /// <param name="natalLon">The natal point's longitude.</param>
    /// <param name="aspectAngle">The aspect's angle.</param>
    /// <returns>The points, n + a first.</returns>
    private static double[] AspectPoints(double natalLon, double aspectAngle)
    {
        double ahead = Normalize(natalLon + aspectAngle);
        if (aspectAngle == 0.0 || aspectAngle == 180.0)
            return [ahead];

        return [ahead, Normalize(natalLon - aspectAngle)];
    }

    /// <summary>
    /// The signed separation of a longitude from a point, ((lon − point + 180) mod 360) − 180, in
    /// [−180°, 180°): negative behind the point in the order of the signs, positive past it, so a body
    /// moving forward over the point takes it from negative to positive and one retrograding the
    /// reverse. It wraps only at the point opposite, never at the natal point or anywhere in orb.
    /// </summary>
    /// <param name="longitude">The body's longitude.</param>
    /// <param name="point">The point's longitude.</param>
    /// <returns>The signed separation in degrees.</returns>
    private static double SignedSeparation(double longitude, double point) =>
        Normalize(longitude - point + 180.0) - 180.0;

    /// <summary>Whether a longitude is within orb of a point: |signed separation| ≤ orb.</summary>
    /// <param name="longitude">The body's longitude.</param>
    /// <param name="point">The point's longitude.</param>
    /// <param name="orb">The orb in degrees.</param>
    /// <returns>Whether the longitude is within orb.</returns>
    private static bool IsWithinOrbOfPoint(double longitude, double point, double orb) =>
        Math.Abs(SignedSeparation(longitude, point)) <= orb;

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
    /// The report dates on which the body is within orb of the point at 12:00 UT.
    /// </summary>
    private static List<DateOnly> FindInOrbDays(double[] reportLongitudes, DateOnly startDate, double point, double orb)
    {
        List<DateOnly> days = [];
        for (int i = 0; i < reportLongitudes.Length; i++)
        {
            if (IsWithinOrbOfPoint(reportLongitudes[i], point, orb))
                days.Add(startDate.AddDays(i));
        }

        return days;
    }

    /// <summary>
    /// Groups the in-orb report samples at one point into passages: two consecutive in-orb samples
    /// with fewer than <see cref="PassageGapDays"/> samples out of orb between them belong to one
    /// passage, as the walks keep an entry or exit until that many consecutive samples out of orb.
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

    #region Private — Active Transit Construction

    /// <summary>
    /// Builds a complete <see cref="ActiveTransit"/> with full lifecycle for one passage of the body
    /// over one point of an aspect, from the passage's in-orb report samples.
    /// </summary>
    private ActiveTransit BuildActiveTransit(
        TransitBody transitBody,
        NatalTarget natal,
        AspectDef aspect,
        double orb,
        double point,
        List<DateOnly> passageDays,
        DateOnly reportStart,
        DateOnly reportEnd,
        IReadOnlyList<double> natalHouseCusps)
    {
        // Find entry and exit dates (may extend beyond report period)
        DateOnly entryDate = ScanForEntry(transitBody, point, orb, passageDays[0]);
        DateOnly exitDate = ScanForExit(transitBody, point, orb, passageDays[^1]);

        // Refine entry/exit to the second via binary search
        (DateTime refinedEntryTime, double entryJd) = RefineEntryTime(transitBody, point, orb, entryDate);
        (DateTime refinedExitTime, double exitJd) = RefineExitTime(transitBody, point, orb, exitDate);

        // Compute transit longitude at entry and exit for position display, at the Julian Day the
        // refinement holds (RT6): the printed instant read back through the date's calendar switch
        // was a Julian date before 1582-10-15, and the longitude the body's 7 to 10 days later.
        double entryLongitude = _ephemeris.GetLongitude(transitBody, entryJd);
        double exitLongitude = _ephemeris.GetLongitude(transitBody, exitJd);

        // The crossings of the point between the entry and exit instants
        List<(DateTime DateTimeUtc, int PassNumber, double ResidualOrb)> exactPasses = FindExactPasses(
            transitBody, point, entryDate, exitDate, refinedEntryTime, refinedExitTime);

        // The stations between the entry and exit instants at which the body is in orb
        IReadOnlyList<TransitStation> stationsWithinOrb = FindStationsWithinOrb(
            transitBody, point, orb, refinedEntryTime, refinedExitTime);

        // Assemble lifecycle events in chronological order
        IReadOnlyList<TransitLifecycleEvent> lifecycle = BuildLifecycle(
            refinedEntryTime, refinedExitTime, exactPasses, stationsWithinOrb,
            reportStart, reportEnd, entryLongitude, exitLongitude);

        // Get transit position at report start
        double startJd = _ephemeris.DateToJulianDay(reportStart);
        double transitLonAtStart = _ephemeris.GetLongitude(transitBody, startJd);

        // Determine orb direction (applying / separating / exact) at report start
        string orbDirection = DetermineOrbDirection(
            transitBody, natal.Longitude, aspect.Angle, reportStart);

        // Calculate orb at report start
        double sepAtStart = ShortestArc(transitLonAtStart, natal.Longitude);
        double orbAtStart = Math.Abs(sepAtStart - aspect.Angle);

        // Determine which natal house the transit planet occupies
        int transitHouse = HousePlacement.HouseOf(transitLonAtStart, natalHouseCusps) ?? 1;

        return new ActiveTransit
        {
            TransitPlanet = transitBody,
            NatalTarget = natal.Name,
            NatalTargetSymbol = natal.Symbol,
            AspectName = aspect.Name,
            AspectAbbreviation = aspect.Abbreviation,
            TransitLongitudeAtStart = transitLonAtStart,
            TransitSignAtStart = TransitEphemerisHelper.LongitudeToSign(transitLonAtStart),
            NatalLongitude = natal.Longitude,
            NatalSign = TransitEphemerisHelper.LongitudeToSign(natal.Longitude),
            NatalHouseNumber = natal.HouseNumber,
            OrbAtStartDegrees = orbAtStart,
            OrbDirection = orbDirection,
            TransitHouseNumber = transitHouse,
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
    /// <param name="body">The transiting planet.</param>
    /// <param name="point">The point's longitude.</param>
    /// <param name="orb">Orb allowance.</param>
    /// <param name="firstInOrbDay">The passage's first in-orb report sample.</param>
    /// <returns>The entry date: the passage's first in-orb sample.</returns>
    private DateOnly ScanForEntry(TransitBody body, double point, double orb, DateOnly firstInOrbDay)
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

            if (IsWithinOrbOfPoint(_ephemeris.GetLongitude(body, jd), point, orb))
            {
                // An earlier in-orb sample of the same passage; the out-of-orb samples since are a gap
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
    /// <param name="body">The transiting planet.</param>
    /// <param name="point">The point's longitude.</param>
    /// <param name="orb">Orb allowance.</param>
    /// <param name="lastInOrbDay">The passage's last in-orb report sample.</param>
    /// <returns>The exit date: the passage's last in-orb sample.</returns>
    private DateOnly ScanForExit(TransitBody body, double point, double orb, DateOnly lastInOrbDay)
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

            if (IsWithinOrbOfPoint(_ephemeris.GetLongitude(body, jd), point, orb))
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
    /// Refines the entry date to the second using binary search. The entry moment is when the
    /// separation from the point equals the orb (transitioning from out-of-orb to in-orb). Searches
    /// between the 12:00 UT sample before the entry date and the entry date's.
    /// </summary>
    /// <param name="body">The transiting planet.</param>
    /// <param name="point">The point's longitude.</param>
    /// <param name="orb">Orb allowance in degrees.</param>
    /// <param name="entryDate">The day-level entry date from <see cref="ScanForEntry"/>.</param>
    /// <returns>
    /// The entry moment to the whole second, and its Julian Day: the refined crossing's, taken to that
    /// second, or, where the bracket is invalid or its outside sample lies before the span, noon UT of
    /// the entry date and the scan's Julian Day for it.
    /// </returns>
    private (DateTime Time, double JulianDay) RefineEntryTime(
        TransitBody body, double point, double orb, DateOnly entryDate)
    {
        // Bracket: day before entry (out of orb) to entry date (in orb)
        double jdOutside = _ephemeris.DateToJulianDay(entryDate.AddDays(-1));
        double jdInside = _ephemeris.DateToJulianDay(entryDate);

        // Verify bracket: outside should be out of orb, inside should be in orb; the outside sample is
        // not read before the span.
        bool outsideInOrb = jdOutside < EphemerisSpan.FirstJulianDay
            || IsWithinOrbOfPoint(_ephemeris.GetLongitude(body, jdOutside), point, orb);
        bool insideInOrb = IsWithinOrbOfPoint(_ephemeris.GetLongitude(body, jdInside), point, orb);

        if (outsideInOrb || !insideInOrb)
        {
            // Bracket invalid — fall back to noon UTC
            return (entryDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), jdInside);
        }

        // Binary search for the crossing point
        for (int i = 0; i < BisectionIterations; i++)
        {
            double jdMid = (jdOutside + jdInside) / 2.0;

            if (IsWithinOrbOfPoint(_ephemeris.GetLongitude(body, jdMid), point, orb))
                jdInside = jdMid;
            else
                jdOutside = jdMid;
        }

        double jdEntry = (jdOutside + jdInside) / 2.0;
        return (_ephemeris.JulianDayToDateTime(jdEntry), JulianDayUtility.InstantToJulianDay(JulianDayUtility.JulianDayToInstant(jdEntry)));
    }

    /// <summary>
    /// Refines the exit date to the second using binary search. The exit moment is when the
    /// separation from the point equals the orb (transitioning from in-orb to out-of-orb). Searches
    /// between the exit date's 12:00 UT sample and the one after.
    /// </summary>
    /// <param name="body">The transiting planet.</param>
    /// <param name="point">The point's longitude.</param>
    /// <param name="orb">Orb allowance in degrees.</param>
    /// <param name="exitDate">The day-level exit date from <see cref="ScanForExit"/>.</param>
    /// <returns>
    /// The exit moment to the whole second, and its Julian Day: the refined crossing's, taken to that
    /// second, or, where the bracket is invalid or its outside sample lies after the span, noon UT of
    /// the exit date and the scan's Julian Day for it.
    /// </returns>
    private (DateTime Time, double JulianDay) RefineExitTime(
        TransitBody body, double point, double orb, DateOnly exitDate)
    {
        // Bracket: exit date (in orb) to day after exit (out of orb)
        double jdInside = _ephemeris.DateToJulianDay(exitDate);
        double jdOutside = _ephemeris.DateToJulianDay(exitDate.AddDays(1));

        // Verify bracket; the outside sample is not read after the span.
        bool insideInOrb = IsWithinOrbOfPoint(_ephemeris.GetLongitude(body, jdInside), point, orb);
        bool outsideInOrb = jdOutside > EphemerisSpan.LastJulianDay
            || IsWithinOrbOfPoint(_ephemeris.GetLongitude(body, jdOutside), point, orb);

        if (!insideInOrb || outsideInOrb)
        {
            // Bracket invalid — fall back to noon UTC
            return (exitDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), jdInside);
        }

        // Binary search for the crossing point
        for (int i = 0; i < BisectionIterations; i++)
        {
            double jdMid = (jdInside + jdOutside) / 2.0;

            if (IsWithinOrbOfPoint(_ephemeris.GetLongitude(body, jdMid), point, orb))
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
    /// Number of binary search iterations for exact pass refinement.
    /// 24 hours / 2^20 ≈ 0.08 seconds precision (~1 arc-second for outer planets).
    /// </summary>
    private const int BisectionIterations = 20;

    /// <summary>
    /// Finds the passes of the transit: the crossings of its point between the entry and exit
    /// instants. A pass is a sign change of the signed separation between consecutive 12:00 UT samples
    /// with both samples within <see cref="PassGuardDegrees"/> of the point, bisected to the second.
    /// The scan runs from the sample before the entry instant (the entry lies after the sample before
    /// its date's, which the refinement bracketed it against) to the sample after the exit instant,
    /// within the span, and keeps a pass only if its instant lies within [entry, exit]: a fast body
    /// can pass the point between its entry and the first in-orb sample (T1-10).
    /// </summary>
    private List<(DateTime DateTimeUtc, int PassNumber, double ResidualOrb)> FindExactPasses(
        TransitBody body, double point, DateOnly entryDate, DateOnly exitDate,
        DateTime entryTime, DateTime exitTime)
    {
        List<(DateTime, int, double)> passes = [];
        int passNumber = 0;

        DateOnly scanStart = entryDate.AddDays(-1);
        if (_ephemeris.DateToJulianDay(scanStart) < EphemerisSpan.FirstJulianDay)
            scanStart = entryDate;

        DateOnly scanEnd = exitDate.AddDays(1);
        if (_ephemeris.DateToJulianDay(scanEnd) > EphemerisSpan.LastJulianDay)
            scanEnd = exitDate;

        double jdPrev = _ephemeris.DateToJulianDay(scanStart);
        double prevDelta = SignedSeparation(_ephemeris.GetLongitude(body, jdPrev), point);

        for (DateOnly currentDate = scanStart.AddDays(1); currentDate <= scanEnd; currentDate = currentDate.AddDays(1))
        {
            double jdCurr = _ephemeris.DateToJulianDay(currentDate);
            double currDelta = SignedSeparation(_ephemeris.GetLongitude(body, jdCurr), point);

            // A sign change between the two samples, both near the point, is a crossing of it
            bool signChanged = (prevDelta > 0 && currDelta < 0) || (prevDelta < 0 && currDelta > 0);
            if (signChanged && Math.Abs(prevDelta) < PassGuardDegrees && Math.Abs(currDelta) < PassGuardDegrees)
            {
                // Refine to the second within the noon-to-noon bracket
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
    /// Uses binary search (bisection) to refine a crossing of the point to the second within a
    /// noon-to-noon bracket.
    /// </summary>
    /// <param name="jdLow">Julian Day at the earlier noon (sign change bracket start).</param>
    /// <param name="deltaLow">Signed separation from the point at <paramref name="jdLow"/>.</param>
    /// <param name="jdHigh">Julian Day at the later noon (sign change bracket end).</param>
    /// <param name="body">The transiting body.</param>
    /// <param name="point">The point's longitude.</param>
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

    #region Private — Stations Within Orb

    /// <summary>
    /// Finds every station of the body between the entry and exit instants at whose instant the body
    /// is in orb of the point (T1-9), with <see cref="TransitStationDetector.FindStations"/> over the
    /// transit's own dates, not the report's: the detector samples 12:00 UT from the entry instant's
    /// date to the exit instant's, so the half-days before the first in-orb noon and after the last are
    /// covered. The true node's reversals of motion are oscillation, not stations: it has none (T-D4).
    /// </summary>
    private IReadOnlyList<TransitStation> FindStationsWithinOrb(
        TransitBody body, double point, double orb, DateTime entryTime, DateTime exitTime)
    {
        if (body == TransitBody.NorthNode)
            return [];

        IReadOnlyList<TransitStation> stations = _stationDetector.FindStations(
            body, DateOnly.FromDateTime(entryTime), DateOnly.FromDateTime(exitTime));

        List<TransitStation> withinOrb = [];
        foreach (TransitStation station in stations)
        {
            DateTime instant = station.DateTimeUtc
                ?? station.Date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
            if (instant < entryTime || instant > exitTime)
                continue;

            // In orb at the station's own instant: a station inside the transit's dates but in a
            // gap between its in-orb periods belongs to the gap, not to the transit
            double longitude = _ephemeris.GetLongitude(body, JulianDayUtility.InstantToJulianDay(instant));
            if (IsWithinOrbOfPoint(longitude, point, orb))
                withinOrb.Add(station);
        }

        return withinOrb;
    }

    #endregion

    #region Private — Lifecycle Assembly

    /// <summary>
    /// Assembles all lifecycle events into chronological order:
    /// Entry first, then interleaved exact passes and stations by date, then Exit last.
    /// </summary>
    /// <remarks>
    /// Every event carries a refined instant: the entry and exit the crossings of the orb, the
    /// passes the crossings of the point and the stations the zeros of the daily motion, each
    /// bisected to the second. Only an entry or exit whose refinement bracket was invalid, which
    /// inside the span takes a walk that reaches its cap while in orb, is 12:00 UT of its date.
    /// </remarks>
    private static IReadOnlyList<TransitLifecycleEvent> BuildLifecycle(
        DateTime refinedEntryTime,
        DateTime refinedExitTime,
        List<(DateTime DateTimeUtc, int PassNumber, double ResidualOrb)> exactPasses,
        IReadOnlyList<TransitStation> stations,
        DateOnly reportStart,
        DateOnly reportEnd,
        double entryLongitude,
        double exitLongitude)
    {
        List<TransitLifecycleEvent> events = [];

        // Entry event (sub-minute UTC from binary search refinement)
        DateOnly entryDatePart = DateOnly.FromDateTime(refinedEntryTime);
        events.Add(new TransitLifecycleEvent
        {
            EventType = TransitLifecycleEventType.Entry,
            Date = refinedEntryTime,
            BeforeReportPeriod = entryDatePart < reportStart,
            TransitLongitude = entryLongitude,
        });

        // Exact pass events (sub-minute UTC precision from binary search)
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

        // Station events within orb (the detector's refined instant; noon UTC only if it has none)
        foreach (TransitStation station in stations)
        {
            DateTime stationTime = station.DateTimeUtc
                ?? station.Date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

            events.Add(new TransitLifecycleEvent
            {
                EventType = TransitLifecycleEventType.Station,
                Date = stationTime,
                StationType = station.StationType,
                BeforeReportPeriod = station.Date < reportStart,
                BeyondReportPeriod = station.Date > reportEnd,
            });
        }

        // Exit event (sub-minute UTC from binary search refinement)
        DateOnly exitDatePart = DateOnly.FromDateTime(refinedExitTime);
        events.Add(new TransitLifecycleEvent
        {
            EventType = TransitLifecycleEventType.Exit,
            Date = refinedExitTime,
            BeyondReportPeriod = exitDatePart > reportEnd,
            TransitLongitude = exitLongitude,
        });

        // Sort: Entry always first, Exit always last, others by date
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

    #region Private — Orb Direction

    /// <summary>
    /// Determines whether the transit is applying, separating, or exact at a given date.
    /// Within <see cref="ExactThreshold"/> of exact it is "exact"; otherwise it is applying
    /// when the orb is decreasing at 12:00 UT, by the transiting body's speed at that instant
    /// with the natal point held still (<see cref="AspectDirection"/>).
    /// </summary>
    /// <remarks>
    /// Until 1.3.3 this compared the orb at 12:00 UT with the orb 24 hours later, so an
    /// aspect that perfected within the next 12 hours had opened again by then and was
    /// called separating, and a true node reversing within the day could invert the label
    /// either way (finding T1-3).
    /// </remarks>
    private string DetermineOrbDirection(
        TransitBody body, double natalLon, double aspectAngle, DateOnly date)
    {
        double jdToday = _ephemeris.DateToJulianDay(date);
        double transitLonToday = _ephemeris.GetLongitude(body, jdToday);

        double sepToday = ShortestArc(transitLonToday, natalLon);
        double deltaToday = Math.Abs(sepToday - aspectAngle);

        if (deltaToday < ExactThreshold)
            return "exact";

        // The natal point does not move: speed 0
        double transitSpeedToday = _ephemeris.GetDailyMotion(body, jdToday);

        return AspectDirection.IsApplying(transitLonToday, transitSpeedToday, natalLon, 0.0, aspectAngle)
            ? "applying" : "separating";
    }

    #endregion
}
