using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Builds the daily horoscope's days from a transit result: for each calendar day of the report,
/// the Moon's sign and phase, its sign change and void-of-course periods, and a snapshot of each
/// transit. <see cref="Formatting.DailyHoroscopeMarkdownFormatter"/> renders the days as Markdown.
/// </summary>
/// <remarks>
/// <para>
/// <b>A day is its own instants</b>: from the first instant its date reads on the zone's clock to
/// the next date's (<see cref="JulianDayUtility.LocalDayStartToJulianDay"/>). Every exact pass,
/// sign change and void bound is filed on the day its instant lies in, instants compared, and
/// printed as its reading on that clock, "HH:mm", with the zone's name where the clocks repeat the
/// reading. A date the zone skipped has no instant and is left out.
/// </para>
/// <para>
/// <b>Each day is read at 12:00 local</b>: the Moon's sign and phase, and each transit's orb and
/// phase, the phase by <see cref="AspectDirection"/> with the natal point still. A transit is listed
/// when it is in orb at noon; one in orb for only part of the day enters or leaves orb at an instant
/// found from its own positions.
/// </para>
/// <para>
/// Until 1.3.3 this was <c>DailyHoroscopeMarkdownFormatter.BuildDailyData</c>. It reads the
/// ephemeris, the time-zone rules and the void-of-course scanner, which is a service's work, not a
/// document formatter's: the same reason <see cref="Formatting.ChartTimeFormatter.FormatLocalTime"/>
/// left <c>ChartMarkdownFormatter</c> in 1.3.2. Until 1.3.3 the orb was estimated from the orb at
/// the report's start scaled by days from the nearest pass, the phase from the passes' UT dates, and
/// a transit listed on every day between its entry's and exit's UT dates (finding T1-6); a pass was
/// filed under its UT date (T1-13); and the Moon's sign change was noted only when it fell between
/// noon less 12 hours and noon (T1-7).
/// </para>
/// </remarks>
public sealed class DailyHoroscopeService
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// The largest residual orb, in degrees, at which a lifecycle pass is a pass. A real pass is
    /// bisected to about 0.0001°; the transit lifecycle also reports "passes" where the signed
    /// separation jumps sign far from exact (a sextile's at the opposition, residual 120°), which
    /// are not passes (finding T1-1, whose cause branch D removes).
    /// </summary>
    private const double ExactPassResidualDegrees = 0.01;

    /// <summary>
    /// Halvings of the interval in which a transit's orb crosses its limit: half a day / 2^24 is
    /// about 2.6 ms, well inside the second the crossing is printed to.
    /// </summary>
    private const int CrossingBisectionSteps = 24;

    private readonly IEphemerisService _ephemeris;
    private readonly VoidOfCourseScanner _vocScanner;
    private readonly ITimeZoneRules? _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="DailyHoroscopeService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for Moon position calculations.</param>
    /// <param name="vocScanner">
    /// Void of Course scanner instance. Optional: when omitted, a scanner on
    /// <paramref name="ephemeris"/> and <paramref name="timeZoneRules"/>.
    /// </param>
    /// <param name="timeZoneRules">
    /// The time-zone rules the report's days are converted with. Optional: when omitted,
    /// the rules the natal chart was cast with, or failing those the IANA data the app
    /// carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ephemeris"/> is null.</exception>
    public DailyHoroscopeService(
        IEphemerisService ephemeris,
        VoidOfCourseScanner? vocScanner = null,
        ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
        _vocScanner = vocScanner ?? new VoidOfCourseScanner(ephemeris, timeZoneRules);
        _timeZoneRules = timeZoneRules;
    }

    /// <summary>
    /// Builds per-day data from a transit result by iterating each calendar day in the
    /// report range and collecting the Moon at noon, its sign change and VoC periods, and each
    /// transit's snapshot: its orb and phase at noon, its exact passes, and its entry into or exit
    /// from orb during the day.
    /// </summary>
    /// <param name="result">The transit calculation result.</param>
    /// <param name="timeZoneId">IANA timezone identifier for local time conversion.</param>
    /// <returns>Ordered list of daily data, one entry per calendar day that occurred on the zone's clock.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a transit's aspect is not one of <see cref="TransitAspectFinder"/>'s.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="timeZoneId"/> is null or blank.</exception>
    public IReadOnlyList<DailyHoroscopeDay> BuildDays(TransitResult result, string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        ITimeZoneRules rules = _timeZoneRules ?? result.NatalChart.TimeZoneRules ?? new TzdbTimeZoneRules();

        // The typed bounds, read once to the days they denote (D21; branch R3, 1.3.4, R-D7): the
        // days from here are proleptic days, each printed on the calendar in force by the
        // formatter, so a range across October 1582 steps 1582-10-04 to 1582-10-15 and one over
        // a Julian 29 February has that day. Until 1.3.4 the loop stepped the typed date and read
        // each day back through the calendar switch: ten days twice, and no 29 February.
        DateOnly firstDay = CalendarDateFormatter.ProlepticDay(result.ReportStartDate);
        DateOnly lastDay = CalendarDateFormatter.ProlepticDay(result.ReportEndDate);

        // Scan VoC periods across the report's own instants: from the first instant of its
        // first day to the first instant of the day after its last (DayStartJd). The scanner
        // returns every period that touches the range with its true start and end, so nothing
        // is padded.
        double scanStartJd = DayStartJd(firstDay, rules, timeZoneId);
        double scanEndJd = DayStartJd(lastDay.AddDays(1), rules, timeZoneId);

        IReadOnlyList<VoidOfCoursePeriod> allVocPeriods =
            _vocScanner.Scan(scanStartJd, scanEndJd, timeZoneId);

        List<DailyHoroscopeDay> days = [];

        for (DateOnly date = firstDay;
             date <= lastDay;
             date = date.AddDays(1))
        {
            double dayStartJd = DayStartJd(date, rules, timeZoneId);
            double dayEndJd = DayStartJd(date.AddDays(1), rules, timeZoneId);

            // A date the zone skipped whole (Pacific/Apia, 2011-12-30) has no instant: its start
            // is the next date's. It is left out, and the report counts the days that occurred.
            if (dayEndJd <= dayStartJd)
                continue;

            // Moon position at local noon
            DateTime localNoon = date.ToDateTime(new TimeOnly(12, 0, 0));
            DateTime utcNoon = rules.ToUniversal(localNoon, timeZoneId).UtcDateTime;
            double noonJd = JulianDayUtility.InstantToJulianDay(utcNoon);

            PlanetPosition moonPos = _ephemeris.CalculatePositionLongitudeOnly(noonJd, CelestialBodyId.Moon);
            PlanetPosition sunPos = _ephemeris.CalculatePositionLongitudeOnly(noonJd, CelestialBodyId.Sun);

            ZodiacSign moonSign = LongitudeToSign(moonPos.Longitude);
            string moonPhaseLabel = ComputePhaseLabel(moonPos.Longitude, sunPos.Longitude);

            // The Moon's sign change in the day, at any hour: the end of the void period that ends
            // in the day. Every stay of the Moon has a void period, which ends at the scanner's sign
            // exit, so the note and the printed end of that period are one instant.
            string? moonSignChange = null;
            foreach (VoidOfCoursePeriod voc in allVocPeriods)
            {
                if (InDay(voc.EndUtc, dayStartJd, dayEndJd))
                {
                    ZodiacSign entered = NextSign(Enum.Parse<ZodiacSign>(voc.MoonSign));
                    moonSignChange = $"Moon enters {entered} at {LocalTime(voc.EndUtc, rules, timeZoneId)}";
                }
            }

            // VoC periods overlapping this day
            List<string> vocStrings = FormatVocPeriodsForDay(dayStartJd, dayEndJd, allVocPeriods, rules, timeZoneId);

            // Active transits for this day
            List<DailyTransitSnapshot> activeTransits = [];
            List<DailyTransitSnapshot> exactToday = [];
            List<DailyTransitSnapshot> enteringToday = [];
            List<DailyTransitSnapshot> exitingToday = [];

            // Each body's longitude and speed at the day's start, noon and end, read once for every
            // transit of that body.
            Dictionary<TransitBody, BodyDay> bodyDays = [];

            foreach (ActiveTransit transit in result.ActiveTransits)
            {
                // Find exact passes on this day: a pass whose instant lies in the day, printed on
                // the zone's clock. A "pass" far from exact is not one.
                List<TransitLifecycleEvent> exactPasses = transit.Lifecycle
                    .Where(e => e.EventType == TransitLifecycleEventType.Exact
                        && (e.ExactOrbDegrees is null or <= ExactPassResidualDegrees)
                        && InDay(e.Date, dayStartJd, dayEndJd))
                    .ToList();

                bool isExact = exactPasses.Count > 0;
                List<string> exactTimes = exactPasses
                    .Select(e => LocalTime(e.Date, rules, timeZoneId))
                    .ToList();

                if (!bodyDays.TryGetValue(transit.TransitPlanet, out BodyDay bodyDay))
                {
                    bodyDay = BodyDayAt(transit.TransitPlanet, dayStartJd, noonJd, dayEndJd);
                    bodyDays[transit.TransitPlanet] = bodyDay;
                }

                if (!TransitAspectFinder.TryGetAspect(transit.AspectName, out double angle, out bool isMinor))
                {
                    throw new InvalidOperationException($"Unknown transit aspect '{transit.AspectName}'.");
                }

                // The orb at the day's start, noon and end, against the transit's orb limit. The
                // transit is listed when it is in orb at noon, with its noon orb.
                double limit = TransitOrbs.GetOrb(transit.TransitPlanet, angle, isMinor);
                double natalLongitude = transit.NatalLongitude;
                double orbNoon = Orb(bodyDay.LongitudeNoon, natalLongitude, angle);
                bool inOrbAtStart = Orb(bodyDay.LongitudeStart, natalLongitude, angle) <= limit;
                bool inOrbAtNoon = orbNoon <= limit;
                bool inOrbAtEnd = Orb(bodyDay.LongitudeEnd, natalLongitude, angle) <= limit;

                // The phase at noon by F3's rule, the natal point still; "Exact" on a day with a pass.
                string phase = isExact
                    ? "Exact"
                    : AspectDirection.IsApplying(bodyDay.LongitudeNoon, bodyDay.SpeedNoon, natalLongitude, 0.0, angle)
                        ? "Applying"
                        : "Separating";

                // Crossings of the orb limit, between the day's start and noon and between noon and
                // its end: a transit that enters and leaves orb on one day around noon is seen twice.
                List<(double JulianDay, bool Enters)> crossings = [];
                if (inOrbAtStart != inOrbAtNoon)
                {
                    crossings.Add((Crossing(transit.TransitPlanet, natalLongitude, angle, limit, dayStartJd, noonJd, inOrbAtStart), !inOrbAtStart));
                }

                if (inOrbAtNoon != inOrbAtEnd)
                {
                    crossings.Add((Crossing(transit.TransitPlanet, natalLongitude, angle, limit, noonJd, dayEndJd, inOrbAtNoon), !inOrbAtNoon));
                }

                bool entering = crossings.Any(c => c.Enters);
                bool exiting = crossings.Any(c => !c.Enters);
                string? entryTime = entering
                    ? LocalTime(JulianDayUtility.ToDateTimeUtc(_ephemeris, crossings.First(c => c.Enters).JulianDay), rules, timeZoneId)
                    : null;
                string? exitTime = exiting
                    ? LocalTime(JulianDayUtility.ToDateTimeUtc(_ephemeris, crossings.Last(c => !c.Enters).JulianDay), rules, timeZoneId)
                    : null;

                if (!inOrbAtNoon && !entering && !exiting && !isExact)
                    continue;

                DailyTransitSnapshot snapshot = new()
                {
                    TransitPlanet = $"TR {FormatPlanetName(transit.TransitPlanet)}",
                    Aspect = transit.AspectName.ToLowerInvariant(),
                    AspectAbbrev = transit.AspectAbbreviation,
                    NatalTarget = $"NA {transit.NatalTarget}",
                    NatalHouse = transit.NatalHouseNumber,
                    TransitHouse = transit.TransitHouseNumber,
                    OrbDegrees = orbNoon,
                    Phase = phase,
                    IsExactToday = isExact,
                    ExactTimes = exactTimes,
                    EntryTime = entryTime,
                    ExitTime = exitTime,
                };

                if (inOrbAtNoon)
                    activeTransits.Add(snapshot);

                if (isExact)
                    exactToday.Add(snapshot);

                if (entering)
                    enteringToday.Add(snapshot);

                if (exiting)
                    exitingToday.Add(snapshot);
            }

            // Sort active transits: outermost planet first, then by natal target
            activeTransits.Sort(CompareSnapshots);

            days.Add(new DailyHoroscopeDay
            {
                Date = date,
                MoonSign = moonSign,
                MoonLongitude = moonPos.Longitude,
                MoonPhaseLabel = moonPhaseLabel,
                MoonSignChange = moonSignChange,
                VoidOfCoursePeriods = vocStrings,
                ActiveTransits = activeTransits,
                ExactToday = exactToday,
                EnteringToday = enteringToday,
                ExitingToday = exitingToday,
            });
        }

        return days;
    }

    #region BuildDays Helpers

    /// <summary>
    /// The first instant of a local date: the instant its midnight first reads, the earlier of a
    /// repeated midnight and the end of a gap that swallows midnight
    /// (<see cref="JulianDayUtility.LocalDayStartToJulianDay"/>). A day is the instants from its
    /// own first instant to the next date's.
    /// </summary>
    /// <param name="date">The local date.</param>
    /// <param name="rules">The time-zone rules the date is read on.</param>
    /// <param name="timeZoneId">The IANA zone the date is on.</param>
    /// <returns>The Julian Day (UT) of the date's first instant.</returns>
    private double DayStartJd(DateOnly date, ITimeZoneRules rules, string timeZoneId) =>
        JulianDayUtility.LocalDayStartToJulianDay(_ephemeris, date, rules, timeZoneId);

    /// <summary>
    /// Whether an instant lies in the day [start, end): instants are compared, not readings, so an
    /// instant falls on the day its reading names, in a repeated hour as in any other.
    /// </summary>
    /// <param name="utc">The instant, UTC.</param>
    /// <param name="dayStartJd">The day's first instant (Julian Day, UT).</param>
    /// <param name="dayEndJd">The next day's first instant (Julian Day, UT).</param>
    /// <returns>Whether the instant is in the day.</returns>
    private static bool InDay(DateTime utc, double dayStartJd, double dayEndJd)
    {
        double jd = JulianDayUtility.InstantToJulianDay(utc);
        return jd >= dayStartJd && jd < dayEndJd;
    }

    /// <summary>
    /// An instant's reading on the zone's clock as the daily prints it: "HH:mm", 24-hour and
    /// invariant, followed by a space and the zone's name where the clocks repeat that reading
    /// (<see cref="ChartTimeFormatter.RepeatedReadingOffset"/>): "21:30", "01:30 EST".
    /// </summary>
    /// <param name="utc">The instant, UTC.</param>
    /// <param name="rules">The time-zone rules the instant is read on.</param>
    /// <param name="timeZoneId">The IANA zone the reading is on.</param>
    /// <returns>The printed time.</returns>
    private static string LocalTime(DateTime utc, ITimeZoneRules rules, string timeZoneId)
    {
        DateTime local = rules.ToLocal(utc, timeZoneId).LocalDateTime;
        return local.ToString("HH:mm", Inv) + ChartTimeFormatter.RepeatedReadingOffset(local, utc, rules, timeZoneId);
    }

    /// <summary>
    /// A transiting body's longitude at a day's start, noon and end, and its speed at noon.
    /// </summary>
    /// <param name="LongitudeStart">Longitude at the day's first instant, degrees.</param>
    /// <param name="LongitudeNoon">Longitude at local noon, degrees.</param>
    /// <param name="SpeedNoon">Speed in longitude at local noon, degrees per day.</param>
    /// <param name="LongitudeEnd">Longitude at the next day's first instant, degrees.</param>
    private readonly record struct BodyDay(double LongitudeStart, double LongitudeNoon, double SpeedNoon, double LongitudeEnd);

    /// <summary>
    /// Reads a transiting body at a day's start, noon and end.
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="dayStartJd">The day's first instant (Julian Day, UT).</param>
    /// <param name="noonJd">Local noon (Julian Day, UT).</param>
    /// <param name="dayEndJd">The next day's first instant (Julian Day, UT).</param>
    /// <returns>The body's longitudes and noon speed.</returns>
    private BodyDay BodyDayAt(TransitBody body, double dayStartJd, double noonJd, double dayEndJd)
    {
        (double longitudeNoon, double speedNoon) = LongitudeAt(body, noonJd);
        return new BodyDay(LongitudeAt(body, dayStartJd).Longitude, longitudeNoon, speedNoon, LongitudeAt(body, dayEndJd).Longitude);
    }

    /// <summary>
    /// A transiting body's longitude and speed in longitude at an instant.
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="julianDay">The instant (Julian Day, UT).</param>
    /// <returns>The longitude in degrees and the speed in degrees per day.</returns>
    private (double Longitude, double Speed) LongitudeAt(TransitBody body, double julianDay)
    {
        PlanetPosition position = _ephemeris.CalculatePositionLongitudeOnly(
            julianDay, TransitEphemerisHelper.TransitBodyToCelestialBodyId(body));
        return (position.Longitude, position.SpeedInLongitude);
    }

    /// <summary>
    /// The orb of an aspect: the separation's distance from the aspect's angle.
    /// </summary>
    /// <param name="longitude">The transiting body's longitude, degrees.</param>
    /// <param name="natalLongitude">The natal point's longitude, degrees.</param>
    /// <param name="angle">The aspect's angle, degrees.</param>
    /// <returns>The orb, degrees.</returns>
    private static double Orb(double longitude, double natalLongitude, double angle) =>
        Math.Abs(TransitAspectFinder.ShortestArc(longitude, natalLongitude) - angle);

    /// <summary>
    /// The instant between two instants at which a transit's orb crosses its limit, by bisection
    /// to <see cref="CrossingBisectionSteps"/> halvings (well under a second over half a day).
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="natalLongitude">The natal point's longitude, degrees.</param>
    /// <param name="angle">The aspect's angle, degrees.</param>
    /// <param name="limit">The transit's orb limit, degrees.</param>
    /// <param name="fromJd">The earlier instant (Julian Day, UT).</param>
    /// <param name="toJd">The later instant (Julian Day, UT).</param>
    /// <param name="inOrbAtFrom">Whether the transit is in orb at <paramref name="fromJd"/> (and so out of orb at <paramref name="toJd"/>).</param>
    /// <returns>The crossing (Julian Day, UT).</returns>
    private double Crossing(
        TransitBody body, double natalLongitude, double angle, double limit, double fromJd, double toJd, bool inOrbAtFrom)
    {
        double low = fromJd;
        double high = toJd;
        for (int i = 0; i < CrossingBisectionSteps; i++)
        {
            double mid = (low + high) / 2.0;
            if ((Orb(LongitudeAt(body, mid).Longitude, natalLongitude, angle) <= limit) == inOrbAtFrom)
                low = mid;
            else
                high = mid;
        }

        return (low + high) / 2.0;
    }

    /// <summary>
    /// Formats the VoC periods that meet a day, instants compared: a period meets the day when it
    /// starts before the day's end and ends after its start. A bound that lies in the day prints as
    /// its local time (<see cref="LocalTime"/>, "00:18 CDT" where the reading repeats); a period that
    /// runs past the day's start or end prints "00:00" or "23:59" there.
    /// </summary>
    /// <param name="dayStartJd">The day's first instant (Julian Day, UT).</param>
    /// <param name="dayEndJd">The next day's first instant (Julian Day, UT).</param>
    /// <param name="allPeriods">The void periods of the report.</param>
    /// <param name="rules">The time-zone rules the bounds are read on.</param>
    /// <param name="timeZoneId">The IANA zone the day is on.</param>
    /// <returns>The day's void strings, "HH:mm – HH:mm (Sign)".</returns>
    private static List<string> FormatVocPeriodsForDay(
        double dayStartJd, double dayEndJd, IReadOnlyList<VoidOfCoursePeriod> allPeriods, ITimeZoneRules rules, string timeZoneId)
    {
        List<string> result = [];

        foreach (VoidOfCoursePeriod voc in allPeriods)
        {
            double startJd = JulianDayUtility.InstantToJulianDay(voc.StartUtc);
            double endJd = JulianDayUtility.InstantToJulianDay(voc.EndUtc);

            // Check if VoC period overlaps with this day
            if (startJd >= dayEndJd || endJd <= dayStartJd)
                continue;

            // Format the times that fall on this day
            string startTime = startJd >= dayStartJd
                ? LocalTime(voc.StartUtc, rules, timeZoneId)
                : "00:00";
            string endTime = endJd < dayEndJd
                ? LocalTime(voc.EndUtc, rules, timeZoneId)
                : "23:59";

            result.Add($"{startTime} \u2013 {endTime} ({voc.MoonSign})");
        }

        return result;
    }

    /// <summary>
    /// Computes the Moon phase label from Sun and Moon longitudes.
    /// </summary>
    private static string ComputePhaseLabel(double moonLongitude, double sunLongitude)
    {
        double phaseAngle = (moonLongitude - sunLongitude + 360.0) % 360.0;

        string phaseName = phaseAngle switch
        {
            < 45.0 => "New Moon",
            < 90.0 => "Waxing Crescent",
            < 135.0 => "First Quarter",
            < 180.0 => "Waxing Gibbous",
            < 225.0 => "Full Moon",
            < 270.0 => "Waning Gibbous",
            < 315.0 => "Last Quarter",
            _ => "Waning Crescent",
        };

        string position = MoonConditionService.GetPhasePosition(phaseAngle);

        return $"{phaseName} ({position})";
    }

    /// <summary>
    /// Converts an ecliptic longitude to a <see cref="ZodiacSign"/>. Shared with
    /// <see cref="Formatting.DailyHoroscopeMarkdownFormatter"/>, which reads it for the natal
    /// summary's positions.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in degrees.</param>
    /// <returns>The sign the longitude falls in.</returns>
    internal static ZodiacSign LongitudeToSign(double longitude)
    {
        return (ZodiacSign)((int)(longitude % 360.0 / 30.0) + 1);
    }

    /// <summary>
    /// The sign after a sign in zodiacal order, Pisces followed by Aries: the sign the Moon enters
    /// when it leaves one (it never retrogrades).
    /// </summary>
    /// <param name="sign">The sign left.</param>
    /// <returns>The sign entered.</returns>
    private static ZodiacSign NextSign(ZodiacSign sign) => (ZodiacSign)((int)sign % 12 + 1);

    /// <summary>
    /// Comparison function for sorting transit snapshots: outermost planet first,
    /// then by natal target name.
    /// </summary>
    private static int CompareSnapshots(DailyTransitSnapshot a, DailyTransitSnapshot b)
    {
        int orderA = GetSortOrderFromName(a.TransitPlanet);
        int orderB = GetSortOrderFromName(b.TransitPlanet);

        int cmp = orderA.CompareTo(orderB);
        if (cmp != 0) return cmp;

        return string.Compare(a.NatalTarget, b.NatalTarget, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the sort order from a transit planet display name like "TR Jupiter".
    /// </summary>
    private static int GetSortOrderFromName(string transitPlanet)
    {
        string name = transitPlanet.StartsWith("TR ", StringComparison.Ordinal)
            ? transitPlanet[3..]
            : transitPlanet;

        return name switch
        {
            "Pluto" => 0,
            "Neptune" => 1,
            "Uranus" => 2,
            "Saturn" => 3,
            "Jupiter" => 4,
            "Chiron" => 5,
            "North Node" => 6,
            "Mars" => 7,
            _ => 99,
        };
    }

    /// <summary>
    /// Formats a <see cref="TransitBody"/> enum to its display name. Shared with
    /// <see cref="Formatting.DailyHoroscopeMarkdownFormatter"/>, which reads it for the
    /// slow-moving backdrop.
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <returns>The body's display name ("North Node" for the node).</returns>
    internal static string FormatPlanetName(TransitBody body) => body switch
    {
        TransitBody.Jupiter => "Jupiter",
        TransitBody.Saturn => "Saturn",
        TransitBody.Uranus => "Uranus",
        TransitBody.Neptune => "Neptune",
        TransitBody.Pluto => "Pluto",
        TransitBody.Chiron => "Chiron",
        TransitBody.NorthNode => "North Node",
        TransitBody.Mars => "Mars",
        TransitBody.Mercury => "Mercury",
        TransitBody.Venus => "Venus",
        _ => body.ToString(),
    };

    #endregion
}
