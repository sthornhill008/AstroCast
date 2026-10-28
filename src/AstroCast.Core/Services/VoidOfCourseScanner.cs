using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Finds the Moon's void-of-course periods. The Moon is void of course from the last exact
/// major aspect it makes during its stay in a sign until it leaves that sign.
/// </summary>
/// <remarks>
/// <para>
/// <b>The definition</b> (1.3.2's). The aspects are the conjunction, sextile, square, trine and
/// opposition, either side; the targets are the Sun and Mercury through Pluto, every one in
/// motion. A stay in which the Moon makes no such aspect is void for the whole stay, from its
/// sign entry, and an aspect exact at the sign exit itself does not count. At every instant this
/// is the void of course of <see cref="MoonConditionService.GetMoonCondition"/>: void when no
/// exact major aspect perfects strictly after the instant and before the sign exit.
/// </para>
/// <para>
/// <b>Event times.</b> Exact aspects come from
/// <see cref="AspectPerfectionFinder.FindFirstExactMajorAspect"/>, the finder the Moon condition
/// uses, with the same targets and angles; there is no detection orb and no sampling grid for the
/// start. The sign entry and exit are found to <see cref="AspectPerfectionFinder.TimePrecisionDays"/>
/// (one second), the exit as the first instant known to lie in the next sign, as the Moon
/// condition finds it. Instants are taken to the second
/// (<see cref="JulianDayUtility.ToDateTimeUtc"/>) and read on the zone's clock.
/// </para>
/// <para>
/// <b>The range.</b> <see cref="Scan"/> returns every period that touches the range it is
/// given, each with its true start and end: a stay under way at the range's start is searched
/// from its own sign entry, and one under way at its end to its own sign exit. The ephemeris is
/// never read outside <see cref="EphemerisSpan"/>.
/// </para>
/// <para>
/// Until 1.3.3 a period started at the last hourly sample at which the Moon was within 1° of any
/// major aspect, applying or separating, a stay with no sample in orb had no period, and the
/// exit was found to about a minute. Against Tier B over 1900–2050 the start was a median 81
/// minutes late, from 59 minutes early to 52 hours late, and 6,323.6 of 8,561.3 void days were
/// reported. A period that straddled either end of the range was dropped.
/// </para>
/// </remarks>
public sealed class VoidOfCourseScanner
{
    /// <summary>
    /// Days before the range's start from which the first stay that can touch it is found: the
    /// Moon crosses a sign in under 2.6 days, so the stay under way this long before the start has
    /// left its sign by then.
    /// </summary>
    private const double StayLookBackDays = 3.0;

    /// <summary>Major aspect angles for void-of-course detection.</summary>
    private static readonly double[] MajorAspectAngles = [0.0, 60.0, 90.0, 120.0, 180.0];

    /// <summary>Planets to check for aspects against the Moon (Sun through Pluto, excluding Moon).</summary>
    private static readonly CelestialBodyId[] AspectTargets =
    [
        CelestialBodyId.Sun,
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
        CelestialBodyId.Uranus,
        CelestialBodyId.Neptune,
        CelestialBodyId.Pluto,
    ];

    private readonly IEphemerisService _ephemeris;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="VoidOfCourseScanner"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for position calculations.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules instants are read on a zone's clock with. Optional: when omitted,
    /// the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public VoidOfCourseScanner(IEphemerisService ephemeris, ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules();
    }

    /// <summary>
    /// Scans the date range and returns every void-of-course period that touches it: each period
    /// whose end is after <paramref name="startJd"/> and whose start is before <paramref name="endJd"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each period keeps its true start and end, even where they lie outside the range: a stay
    /// under way at <paramref name="startJd"/> is searched from its own sign entry, and one under
    /// way at <paramref name="endJd"/> to its own sign exit. Periods come in time order, once each.
    /// No caller needs to pad or look back.
    /// </para>
    /// <para>
    /// The ephemeris is never read outside <see cref="EphemerisSpan"/>. A range partly outside the
    /// span is cut to it, and a range wholly outside has no period. A stay that begins before
    /// <see cref="EphemerisSpan.FirstJulianDay"/> is searched from there; if it makes no aspect
    /// after it, its period starts there, the Moon being void at every instant from then on. A
    /// stay whose exit lies after <see cref="EphemerisSpan.LastJulianDay"/> has no period, since its
    /// last aspect cannot be known.
    /// </para>
    /// </remarks>
    /// <param name="startJd">Start of range as Julian Day (UT).</param>
    /// <param name="endJd">End of range as Julian Day (UT).</param>
    /// <param name="timeZoneId">IANA timezone for local time conversion.</param>
    /// <returns>Every void-of-course period touching the range, with local times, in time order.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="startJd"/> is greater than or equal to <paramref name="endJd"/>.
    /// </exception>
    public IReadOnlyList<VoidOfCoursePeriod> Scan(
        double startJd, double endJd, string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        if (startJd >= endJd)
        {
            throw new ArgumentException("startJd must be less than endJd.");
        }

        List<VoidOfCoursePeriod> result = [];

        // The range, cut to the span: nothing is read outside it.
        double fromJd = Math.Max(startJd, EphemerisSpan.FirstJulianDay);
        double toJd = Math.Min(endJd, EphemerisSpan.LastJulianDay);
        if (fromJd >= toJd)
        {
            return result.AsReadOnly();
        }

        // The first stay that can touch the range begins at the exit of the one under way
        // StayLookBackDays before it, which has left its sign by the range's start; near the
        // span's start it is the stay under way at FirstJulianDay, searched from there.
        double lookBackJd = fromJd - StayLookBackDays;
        double? searchFromJd = lookBackJd > EphemerisSpan.FirstJulianDay
            ? FindSignExit(lookBackJd)
            : EphemerisSpan.FirstJulianDay;

        while (searchFromJd is double stayStartJd && stayStartJd < toJd)
        {
            // A stay that leaves its sign after the span has no period: its last aspect cannot be known.
            if (FindSignExit(stayStartJd) is not double exitJd)
            {
                break;
            }

            if (exitJd > fromJd)
            {
                double vocStartJd = FindLastExactAspect(stayStartJd, exitJd) ?? stayStartJd;

                if (vocStartJd < toJd)
                {
                    DateTime vocStartUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, vocStartJd);
                    DateTime vocEndUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, exitJd);

                    DateTime vocStartLocal = _timeZoneRules.ToLocal(vocStartUtc, timeZoneId).LocalDateTime;
                    DateTime vocEndLocal = _timeZoneRules.ToLocal(vocEndUtc, timeZoneId).LocalDateTime;

                    result.Add(new VoidOfCoursePeriod
                    {
                        StartLocal = vocStartLocal,
                        EndLocal = vocEndLocal,
                        MoonSign = GetMoonSign(stayStartJd),
                        StartUtc = vocStartUtc,
                        EndUtc = vocEndUtc,
                    });
                }
            }

            searchFromJd = exitJd;
        }

        return result.AsReadOnly();
    }

    /// <summary>
    /// Finds when the Moon leaves the sign it occupies at <paramref name="julianDayUt"/>: the
    /// first instant known to lie in the next sign, to
    /// <see cref="AspectPerfectionFinder.TimePrecisionDays"/>.
    /// </summary>
    /// <remarks>
    /// The Moon is sampled on an hourly grid fixed in time, at whole multiples of
    /// <see cref="AspectPerfectionFinder.SampleStepDays"/> from Julian Day 0, cut to the span, and
    /// the hour in which its sign changes is bisected by
    /// <see cref="AspectPerfectionFinder.FindSignExit"/>. That hour does not depend on the instant
    /// the search starts from, so a stay's exit, and with it the next stay's entry, is the same
    /// instant in every scan that reaches it. The Moon never retrogrades and spends days in a
    /// sign, so the hour's first sample lies in the sign the search starts in even when it
    /// precedes <paramref name="julianDayUt"/>.
    /// </remarks>
    /// <param name="julianDayUt">An instant in the stay, inside the span (Julian Day, UT).</param>
    /// <returns>
    /// The sign exit (Julian Day, UT), or null if the Moon is still in its sign at
    /// <see cref="EphemerisSpan.LastJulianDay"/>.
    /// </returns>
    private double? FindSignExit(double julianDayUt)
    {
        string sign = GetMoonSign(julianDayUt);

        for (long hour = (long)Math.Floor(julianDayUt / AspectPerfectionFinder.SampleStepDays) + 1; ; hour++)
        {
            double sampleJd = Math.Min(hour * AspectPerfectionFinder.SampleStepDays, EphemerisSpan.LastJulianDay);

            if (GetMoonSign(sampleJd) != sign)
            {
                return AspectPerfectionFinder.FindSignExit(
                    _ephemeris,
                    CelestialBodyId.Moon,
                    Math.Max((hour - 1) * AspectPerfectionFinder.SampleStepDays, EphemerisSpan.FirstJulianDay),
                    sampleJd);
            }

            if (sampleJd >= EphemerisSpan.LastJulianDay)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Finds the Moon's last exact major aspect strictly after <paramref name="fromJd"/> and
    /// before <paramref name="exitJd"/>, its sign exit: the instant from which no aspect is left.
    /// </summary>
    /// <remarks>
    /// The Moon is followed from one first exact aspect to the next
    /// (<see cref="FindFirstExactAspect"/>) until none is left before the exit. Each instant found
    /// lies strictly after the one before, and the Moon makes a handful of aspects in a stay, so
    /// the walk ends. An aspect the finder brackets on the near side of its crossing is found once
    /// more from there, so the instant returned lies on the far side, at most half a second after
    /// the crossing: from it, as from any later instant, the Moon condition finds no aspect left.
    /// </remarks>
    /// <param name="fromJd">The instant the stay is searched from (Julian Day, UT).</param>
    /// <param name="exitJd">The stay's sign exit (Julian Day, UT).</param>
    /// <returns>The last exact aspect (Julian Day, UT), or null if the Moon makes none.</returns>
    private double? FindLastExactAspect(double fromJd, double exitJd)
    {
        double? lastAspectJd = null;

        while (FindFirstExactAspect(lastAspectJd ?? fromJd, exitJd) is double nextAspectJd)
        {
            lastAspectJd = nextAspectJd;
        }

        return lastAspectJd;
    }

    /// <summary>
    /// Finds the Moon's first exact major aspect to any target strictly after
    /// <paramref name="fromJd"/> and before <paramref name="exitJd"/>, as
    /// <see cref="MoonConditionService.GetMoonCondition"/> finds its first applying aspect: each
    /// target's first exact aspect from <see cref="AspectPerfectionFinder.FindFirstExactMajorAspect"/>,
    /// every later target searched only up to the earliest found so far.
    /// </summary>
    /// <param name="fromJd">The instant searched from (Julian Day, UT); an aspect exact at it does not count.</param>
    /// <param name="exitJd">The stay's sign exit (Julian Day, UT); an aspect exact at it does not count.</param>
    /// <returns>The first exact aspect (Julian Day, UT), or null if there is none.</returns>
    private double? FindFirstExactAspect(double fromJd, double exitJd)
    {
        double? firstAspectJd = null;

        foreach (CelestialBodyId target in AspectTargets)
        {
            ExactAspectEvent? hit = AspectPerfectionFinder.FindFirstExactMajorAspect(
                _ephemeris, CelestialBodyId.Moon, target, fromJd, firstAspectJd ?? exitJd);

            if (hit is not null && hit.JulianDay < (firstAspectJd ?? exitJd))
            {
                firstAspectJd = hit.JulianDay;
            }
        }

        return firstAspectJd;
    }

    /// <summary>
    /// Gets the zodiac sign name for the Moon at the given Julian Day.
    /// </summary>
    /// <param name="jd">Julian Day.</param>
    /// <returns>Sign name (e.g., "Aries", "Taurus").</returns>
    private string GetMoonSign(double jd)
    {
        double longitude = _ephemeris.CalculatePositionLongitudeOnly(jd, CelestialBodyId.Moon).Longitude;
        return SignFromLongitude(longitude);
    }

    /// <summary>
    /// Converts an ecliptic longitude to a zodiac sign name.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in degrees (0–360).</param>
    /// <returns>Zodiac sign name.</returns>
    internal static string SignFromLongitude(double longitude)
    {
        ZodiacSign sign = (ZodiacSign)((int)(longitude % 360.0 / 30.0) + 1);
        return sign.ToString();
    }

}
