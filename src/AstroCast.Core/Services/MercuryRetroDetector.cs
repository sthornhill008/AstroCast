using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Detects the Mercury retrograde periods that touch a date range.
/// Mercury is retrograde when its speed in longitude is negative.
/// A retrograde period runs from the Rx station (speed crosses from
/// positive to negative) to the Direct station (speed crosses from
/// negative to positive), whether or not either lies inside the range.
/// </summary>
public sealed class MercuryRetroDetector
{
    /// <summary>Step size for retrograde scanning (~12 hours).</summary>
    private const double ScanStep = 0.5;

    /// <summary>
    /// The cell a station is bisected to: 1e-6 day (0.0864 s), on a lattice fixed in time (whole multiples of it from
    /// Julian Day 0), so the station found does not depend on the bracket a scan hands over.
    /// </summary>
    private const double StationPrecision = 0.000001;

    private readonly IEphemerisService _ephemeris;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="MercuryRetroDetector"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for position calculations.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules instants are read on a zone's clock with. Optional: when omitted,
    /// the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public MercuryRetroDetector(IEphemerisService ephemeris, ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules();
    }

    /// <summary>
    /// Finds every Mercury retrograde period that touches the given date range: each period whose
    /// end is after <paramref name="startJd"/> and whose start is before <paramref name="endJd"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each period runs from its own station retrograde to its own station direct, even where
    /// they lie outside the range: a retrograde under way at <paramref name="startJd"/> is searched
    /// back to its station, and one under way at <paramref name="endJd"/> forward to its station,
    /// as the void-of-course scanner keeps its periods. Until 1.3.3 such a period was cut to the
    /// range, so the export printed the range's bounds as Mercury's stations.
    /// </para>
    /// <para>
    /// The range is sampled every half day from <paramref name="startJd"/>, and the last part step
    /// up to <paramref name="endJd"/>; a sign change of the speed is bisected to a cell of 1e-6 day
    /// (0.0864 s) on a lattice fixed in time, the cell's middle is the station, and each period's
    /// instants are its stations' taken to the second. So a station prints on its own minute, and the
    /// same station gives the same second from any range that reaches it. Until the O1 fix-up the
    /// scan's own bracket was halved to 0.0007 day and its midpoint taken: up to half a minute out,
    /// by where the scan began.
    /// </para>
    /// <para>
    /// The ephemeris is never read outside <see cref="EphemerisSpan"/>. A range partly outside the
    /// span is cut to it, and a range wholly outside has no period. A retrograde under way at
    /// <see cref="EphemerisSpan.FirstJulianDay"/> starts there, and one under way at
    /// <see cref="EphemerisSpan.LastJulianDay"/> ends there: the files show nothing further.
    /// </para>
    /// </remarks>
    /// <param name="startJd">Start of range as Julian Day (UT).</param>
    /// <param name="endJd">End of range as Julian Day (UT).</param>
    /// <param name="timeZoneId">IANA timezone for local time conversion.</param>
    /// <returns>
    /// Every retrograde period touching the range, in time order, as disqualified periods with
    /// reason "Mercury retrograde".
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="startJd"/> is greater than or equal to <paramref name="endJd"/>.
    /// </exception>
    public IReadOnlyList<DisqualifiedPeriod> FindRetrogradePeriods(
        double startJd, double endJd, string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (startJd >= endJd)
        {
            throw new ArgumentException("startJd must be less than endJd.");
        }

        List<DisqualifiedPeriod> periods = [];

        // The range, cut to the span: nothing is read outside it.
        double fromJd = Math.Max(startJd, EphemerisSpan.FirstJulianDay);
        double toJd = Math.Min(endJd, EphemerisSpan.LastJulianDay);
        if (fromJd >= toJd)
        {
            return periods.AsReadOnly();
        }

        double prevSpeed = GetMercurySpeed(fromJd);

        // A retrograde under way at the range's start began at its own station, searched back from there.
        double? retroStartJd = prevSpeed < 0 ? FindStationBefore(fromJd) : null;

        // One sample step: a station between the previous sample and this one opens or closes a period.
        void Step(double lowJd, double highJd)
        {
            double speed = GetMercurySpeed(highJd);

            if (prevSpeed >= 0 && speed < 0)
            {
                // Rx station: direct → retrograde
                retroStartJd = BinarySearchStation(lowJd, highJd, lowIsPositive: true);
            }
            else if (prevSpeed < 0 && speed >= 0)
            {
                // Direct station: retrograde → direct
                double stationJd = BinarySearchStation(lowJd, highJd, lowIsPositive: false);
                if (retroStartJd is double openedJd)
                {
                    periods.Add(BuildPeriod(openedJd, stationJd, timeZoneId));
                }

                retroStartJd = null;
            }

            prevSpeed = speed;
        }

        double lastJd = fromJd;
        for (double jd = fromJd + ScanStep; jd <= toJd; jd += ScanStep)
        {
            Step(jd - ScanStep, jd);
            lastJd = jd;
        }

        // The last part step, from the last half-day sample to the range's end.
        if (lastJd < toJd)
        {
            Step(lastJd, toJd);
        }

        // A retrograde still under way at the range's end ends at its own station, searched on from there.
        if (retroStartJd is double stillOpenJd)
        {
            periods.Add(BuildPeriod(stillOpenJd, FindStationAfter(toJd), timeZoneId));
        }

        return periods.AsReadOnly();
    }

    /// <summary>
    /// Builds one retrograde period from its two stations: their instants, taken to the second
    /// (<see cref="JulianDayUtility.ToDateTimeUtc"/>), and those same instants read on the zone's clock
    /// (<see cref="JulianDayUtility.ToLocalDateTime"/> reads the instant it takes to the second).
    /// </summary>
    /// <param name="retroStationJd">The station retrograde (Julian Day, UT).</param>
    /// <param name="directStationJd">The station direct (Julian Day, UT).</param>
    /// <param name="timeZoneId">IANA timezone for local time conversion.</param>
    /// <returns>The period, with reason "Mercury retrograde".</returns>
    private DisqualifiedPeriod BuildPeriod(double retroStationJd, double directStationJd, string timeZoneId)
    {
        return new DisqualifiedPeriod
        {
            Start = JulianDayUtility.ToLocalDateTime(_ephemeris, retroStationJd, _timeZoneRules, timeZoneId),
            End = JulianDayUtility.ToLocalDateTime(_ephemeris, directStationJd, _timeZoneRules, timeZoneId),
            Reason = "Mercury retrograde",
            StartUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, retroStationJd),
            EndUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, directStationJd),
        };
    }

    /// <summary>
    /// Finds the station retrograde before an instant at which Mercury is retrograde, stepping back
    /// half a day at a time, never before <see cref="EphemerisSpan.FirstJulianDay"/>.
    /// </summary>
    /// <param name="retrogradeJd">An instant at which Mercury is retrograde (Julian Day, UT), inside the span.</param>
    /// <returns>The station (to a 1e-6-day cell), or the span's first instant when Mercury is retrograde there.</returns>
    private double FindStationBefore(double retrogradeJd)
    {
        double highJd = retrogradeJd;
        while (highJd > EphemerisSpan.FirstJulianDay)
        {
            double lowJd = Math.Max(highJd - ScanStep, EphemerisSpan.FirstJulianDay);
            if (GetMercurySpeed(lowJd) >= 0)
            {
                return BinarySearchStation(lowJd, highJd, lowIsPositive: true);
            }

            highJd = lowJd;
        }

        return EphemerisSpan.FirstJulianDay;
    }

    /// <summary>
    /// Finds the station direct after an instant at which Mercury is retrograde, stepping on half
    /// a day at a time, never after <see cref="EphemerisSpan.LastJulianDay"/>.
    /// </summary>
    /// <param name="retrogradeJd">An instant at which Mercury is retrograde (Julian Day, UT), inside the span.</param>
    /// <returns>The station (to a 1e-6-day cell), or the span's last instant when Mercury is still retrograde there.</returns>
    private double FindStationAfter(double retrogradeJd)
    {
        double lowJd = retrogradeJd;
        while (lowJd < EphemerisSpan.LastJulianDay)
        {
            double highJd = Math.Min(lowJd + ScanStep, EphemerisSpan.LastJulianDay);
            if (GetMercurySpeed(highJd) >= 0)
            {
                return BinarySearchStation(lowJd, highJd, lowIsPositive: false);
            }

            lowJd = highJd;
        }

        return EphemerisSpan.LastJulianDay;
    }

    /// <summary>
    /// Gets Mercury's speed in longitude at the given Julian Day.
    /// </summary>
    /// <param name="jd">Julian Day (UT).</param>
    /// <returns>Speed in longitude (degrees/day). Negative = retrograde.</returns>
    private double GetMercurySpeed(double jd)
    {
        return _ephemeris.CalculatePositionLongitudeOnly(jd, CelestialBodyId.Mercury).SpeedInLongitude;
    }

    /// <summary>
    /// Binary searches for the Julian Day when Mercury's speed crosses zero (station point), on the
    /// lattice of <see cref="StationPrecision"/> fixed in time: the cell between the two lattice points
    /// either side of the sign change, whichever bracket the search starts from.
    /// </summary>
    /// <remarks>
    /// Only lattice points strictly inside the bracket are read. The bracket's ends stand for the lattice
    /// points just outside them, which share their signs, since no other station lies within a cell of
    /// them; so nothing outside the bracket, and nothing outside <see cref="EphemerisSpan"/>, is read.
    /// Until the O1 fix-up the bracket itself was halved to 0.0007 day and its midpoint taken.
    /// </remarks>
    /// <param name="lowJd">JD where speed has the sign indicated by <paramref name="lowIsPositive"/>.</param>
    /// <param name="highJd">JD where speed has the opposite sign.</param>
    /// <param name="lowIsPositive">True if speed at <paramref name="lowJd"/> is positive (direct).</param>
    /// <returns>The JD of the station: the middle of its lattice cell, within 0.5e-6 day (0.0432 s) of the sign change.</returns>
    private double BinarySearchStation(double lowJd, double highJd, bool lowIsPositive)
    {
        long lowStep = (long)Math.Floor(lowJd / StationPrecision);
        long highStep = (long)Math.Ceiling(highJd / StationPrecision);

        while (highStep - lowStep > 1)
        {
            long midStep = lowStep + ((highStep - lowStep) / 2);
            double midSpeed = GetMercurySpeed(midStep * StationPrecision);
            bool midIsPositive = midSpeed >= 0;

            if (midIsPositive == lowIsPositive)
            {
                lowStep = midStep;
            }
            else
            {
                highStep = midStep;
            }
        }

        return (lowStep + 0.5) * StationPrecision;
    }

}
