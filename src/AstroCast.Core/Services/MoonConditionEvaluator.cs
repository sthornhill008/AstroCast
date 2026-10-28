using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Evaluates the Moon's condition at a moment for electional scoring. Its next applying
/// aspect and void of course are <see cref="MoonConditionService"/>'s (H3): the first exact
/// major aspect (0°, 60°, 90°, 120°, 180°, either side) to the Sun or Mercury through Pluto,
/// targets in motion, strictly after the instant and before the Moon leaves its sign; none
/// means void of course.
/// </summary>
/// <remarks>
/// <para>
/// The evaluator calls <see cref="MoonConditionService.GetMoonCondition"/> at the instant
/// instead of applying a rule of its own, so a candidate's header Moon and its chart's Moon
/// Conditions block are one reading, and an instant gives the same answer whatever was
/// evaluated before. The pre-computed void-of-course calendar supplies only the bounds of
/// the void the instant falls in.
/// </para>
/// <para>
/// Until 1.3.3 the evaluator showed the nearest applying aspect within 8°, judged applying
/// from the orb 0.01 day (14.4 minutes) later, with Jupiter through Pluto held for up to half
/// a day, and took void of course from the calendar.
/// </para>
/// </remarks>
public class MoonConditionEvaluator
{
    /// <summary>
    /// The bodies besides the Moon whose positions are passed to the service: the Sun and
    /// Mercury through Pluto, its targets.
    /// </summary>
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

    /// <summary>Via Combusta start longitude: 15° Libra = 195°.</summary>
    private const double ViaCombustStart = 195.0;

    /// <summary>Via Combusta end longitude: 15° Scorpio = 225°.</summary>
    private const double ViaCombustEnd = 225.0;

    private readonly IEphemerisService _ephemeris;

    /// <summary>
    /// The service the Moon's next applying aspect and void of course are read from. It is
    /// stateless; the ephemeris it follows the Moon in is passed with each call.
    /// </summary>
    private readonly IMoonConditionService _moonConditionService = new MoonConditionService();

    /// <summary>
    /// The time-zone rules the evaluator was given. Unread since 1.3.3, when the void period that
    /// holds an instant came to be found by the instant itself; kept with the constructor's
    /// signature.
    /// </summary>
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="MoonConditionEvaluator"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for position calculations.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules of the search. Optional: when omitted, the IANA data the app carries
    /// (<see cref="TzdbTimeZoneRules"/>). Unread since 1.3.3: the void period is found by the
    /// instant.
    /// </param>
    public MoonConditionEvaluator(IEphemerisService ephemeris, ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules();
    }

    /// <summary>
    /// Evaluates the Moon's condition at the given Julian Day.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The next applying aspect and the void of course are those
    /// <see cref="MoonConditionService.GetMoonCondition"/> gives at the same Julian Day for the
    /// positions of the Moon, the Sun and Mercury through Pluto there. The orb is the service's,
    /// the separation from exact at the instant, and the description reads
    /// "Moon {aspect} {planet} (applying, {orb})" with the orb in degrees and minutes. The Moon is
    /// void of course exactly when the service finds no aspect.
    /// </para>
    /// <para>
    /// The void's start and end come from the period in <paramref name="voidPeriods"/> that holds
    /// the instant — found by the instant, not its local reading — and are null when none does.
    /// </para>
    /// </remarks>
    /// <param name="julianDay">Julian Day (UT) to evaluate.</param>
    /// <param name="voidPeriods">Pre-computed void-of-course periods, for the bounds of the void the instant falls in.</param>
    /// <param name="timeZoneId">
    /// IANA timezone of the search. Checked for content, not read: since 1.3.3 the void period
    /// is found by the instant.
    /// </param>
    /// <returns>Complete Moon condition snapshot.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="voidPeriods"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="timeZoneId"/> is null or white space.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the service gives no condition, which it does only without a Moon or a Sun position.
    /// </exception>
    public MoonCondition Evaluate(
        double julianDay,
        IReadOnlyList<VoidOfCoursePeriod> voidPeriods,
        string timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(voidPeriods);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        // Get Moon and Sun positions (longitude-only: no equatorial needed)
        PlanetPosition moonPos = _ephemeris.CalculatePositionLongitudeOnly(julianDay, CelestialBodyId.Moon);
        PlanetPosition sunPos = _ephemeris.CalculatePositionLongitudeOnly(julianDay, CelestialBodyId.Sun);

        // Determine sign
        string sign = VoidOfCourseScanner.SignFromLongitude(moonPos.Longitude);

        // Calculate phase
        double separation = (moonPos.Longitude - sunPos.Longitude + 360.0) % 360.0;
        MoonPhaseType phase = ClassifyPhase(separation);
        double phasePercent = separation / 360.0 * 100.0;

        // Next applying aspect and void of course: the service's, at this instant, from the
        // positions of the Sun, the Moon and Mercury through Pluto
        List<PlanetPosition> positions = [sunPos, moonPos];
        foreach (CelestialBodyId target in AspectTargets)
        {
            if (target != CelestialBodyId.Sun)
            {
                positions.Add(_ephemeris.CalculatePositionLongitudeOnly(julianDay, target));
            }
        }

        MoonConditionInfo next = _moonConditionService.GetMoonCondition(positions, [], _ephemeris, julianDay)
            ?? throw new InvalidOperationException("MoonConditionService gave no Moon condition for positions holding the Moon and the Sun.");

        // The bounds of the void-of-course period the instant falls in, if any: found by the
        // instant, since a local reading the clocks repeat names two
        VoidOfCoursePeriod? activeVoid = FindActiveVoidPeriod(
            JulianDayUtility.ToDateTimeUtc(_ephemeris, julianDay), voidPeriods);

        // Check Via Combusta
        bool isInViaCombusta = moonPos.Longitude >= ViaCombustStart
                            && moonPos.Longitude < ViaCombustEnd;

        string? description = next.FirstApplyingPlanet is string planet
            && next.FirstApplyingAspectType is AspectType aspectType
            && next.FirstApplyingOrb is double orb
                ? $"Moon {aspectType.ToString().ToLowerInvariant()} {planet} (applying, {PositionFormatter.FormatOrb(orb)})"
                : null;

        return new MoonCondition
        {
            Longitude = moonPos.Longitude,
            Sign = sign,
            Phase = phase,
            PhasePercent = phasePercent,
            PhasePosition = MoonConditionService.GetPhasePosition(separation),
            IsVoidOfCourse = next.IsVoidOfCourse,
            VoidStartLocal = activeVoid?.StartLocal,
            VoidEndLocal = activeVoid?.EndLocal,
            IsInViaCombusta = isInViaCombusta,
            NextApplyingAspectDescription = description,
            NextApplyingPlanet = next.FirstApplyingPlanet,
            NextApplyingAspectType = next.FirstApplyingAspectType,
            NextApplyingOrb = next.FirstApplyingOrb,
            DailyMotion = moonPos.SpeedInLongitude,
        };
    }

    /// <summary>
    /// Classifies the Moon phase based on Sun-Moon angular separation.
    /// </summary>
    /// <param name="separation">
    /// Sun-Moon angular separation in degrees (0-360), measured as
    /// Moon longitude minus Sun longitude (forward direction).
    /// </param>
    /// <returns>The Moon phase classification.</returns>
    internal static MoonPhaseType ClassifyPhase(double separation)
    {
        return separation switch
        {
            < 45.0 => MoonPhaseType.NewMoon,
            < 90.0 => MoonPhaseType.WaxingCrescent,
            < 135.0 => MoonPhaseType.FirstQuarter,
            < 180.0 => MoonPhaseType.WaxingGibbous,
            < 225.0 => MoonPhaseType.FullMoon,
            < 270.0 => MoonPhaseType.WaningGibbous,
            < 315.0 => MoonPhaseType.LastQuarter,
            _ => MoonPhaseType.WaningCrescent,
        };
    }

    /// <summary>
    /// Finds the void-of-course period that holds the given instant:
    /// [<see cref="VoidOfCoursePeriod.StartUtc"/>, <see cref="VoidOfCoursePeriod.EndUtc"/>).
    /// </summary>
    /// <remarks>
    /// By the instant, not its local reading: in an hour the clocks repeat a reading names two
    /// instants, and until 1.3.3 a sample in one pass was looked up by the other pass's side of a
    /// bound.
    /// </remarks>
    /// <param name="instant">The instant to check, <see cref="DateTimeKind.Utc"/>, to the second.</param>
    /// <param name="voidPeriods">Pre-computed void-of-course periods.</param>
    /// <returns>The matching period, or null if not void-of-course.</returns>
    private static VoidOfCoursePeriod? FindActiveVoidPeriod(
        DateTime instant, IReadOnlyList<VoidOfCoursePeriod> voidPeriods)
    {
        foreach (VoidOfCoursePeriod period in voidPeriods)
        {
            if (instant >= period.StartUtc && instant < period.EndUtc)
            {
                return period;
            }
        }

        return null;
    }

}
