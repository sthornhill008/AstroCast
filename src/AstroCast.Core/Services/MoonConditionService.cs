using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Analyses Moon conditions for chart interpretation.
/// Calculates phase, Void of Course status, first applying Ptolemaic aspect,
/// and Via Combusta detection.
/// </summary>
/// <remarks>
/// <para>
/// This service is stateless — it requires no external dependencies and can
/// be instantiated directly via <c>new MoonConditionService()</c>. The
/// ephemeris it follows the Moon in is passed with each call.
/// </para>
/// <para>
/// <b>Phase calculation:</b> The Sun-Moon angular separation
/// <c>(moonLongitude - sunLongitude + 360) % 360</c> is divided into
/// eight 45° segments corresponding to the eight traditional Moon phases.
/// </para>
/// <para>
/// <b>Void of Course (VoC):</b> The Moon is void of course when it will
/// form no major Ptolemaic aspect (conjunction, opposition, trine, square,
/// sextile) to any visible planet (Sun through Pluto) that perfects before
/// the Moon leaves its current sign; otherwise the first such aspect to
/// perfect is its first applying aspect. There is no orb limit, so an
/// aspect is found however far from exact it is at the chart instant.
/// The targets move while the Moon crosses its sign, so
/// <see cref="GetMoonCondition"/> follows every body in the ephemeris: the
/// window ends at the Moon's sign exit, found by bisection on its longitude
/// (the Moon never retrogrades), the first exact aspect to each target
/// inside it comes from <see cref="AspectPerfectionFinder.FindFirstExactMajorAspect"/>,
/// and the earliest across the targets is the answer. The orb reported is
/// the separation from exact at the chart instant.
/// </para>
/// <para>
/// <b>Fixed targets:</b> <see cref="GetMoonConditionWithFixedTargets"/>
/// holds every target at its given longitude: for each planet, for each
/// major aspect angle, the Moon longitude that would form an exact aspect
/// is computed and checked against the remaining arc of the Moon's sign.
/// It is for positions no ephemeris can move, such as a composite chart's
/// midpoints. On charts cast at an instant it gave the wrong first applying
/// aspect on 2.21% of instants and the wrong VoC status on 0.55%
/// (2000–2030, every 12 hours), which is why the ephemeris overload exists.
/// </para>
/// <para>
/// <b>Via Combusta:</b> The Moon is in Via Combusta when its ecliptic
/// longitude falls between 195.0° and 225.0° (15° Libra to 15° Scorpio).
/// </para>
/// </remarks>
public sealed class MoonConditionService : IMoonConditionService
{
    #region Constants

    /// <summary>
    /// Via Combusta start: 15° Libra = 195° ecliptic longitude.
    /// </summary>
    private const double ViaCombustaStart = 195.0;

    /// <summary>
    /// Via Combusta end: 15° Scorpio = 225° ecliptic longitude.
    /// </summary>
    private const double ViaCombustaEnd = 225.0;

    /// <summary>
    /// Major Ptolemaic aspect angles in degrees, used for VoC forward scanning.
    /// </summary>
    private static readonly double[] MajorAspectAngles = [0.0, 60.0, 90.0, 120.0, 180.0];

    /// <summary>
    /// Maps each major aspect angle to its corresponding <see cref="AspectType"/>
    /// for result reporting.
    /// </summary>
    private static readonly Dictionary<double, AspectType> AspectTypeForAngle = new()
    {
        [0.0] = AspectType.Conjunction,
        [60.0] = AspectType.Sextile,
        [90.0] = AspectType.Square,
        [120.0] = AspectType.Trine,
        [180.0] = AspectType.Opposition,
    };

    /// <summary>
    /// Days after the chart instant that the sign-exit search brackets: the
    /// Moon crosses a sign in under 2.6 days, so it has always left by then.
    /// </summary>
    private const double SignExitBracketDays = 3.0;

    #endregion

    /// <inheritdoc />
    public MoonConditionInfo? GetMoonCondition(
        IReadOnlyList<PlanetPosition> positions,
        IReadOnlyList<AspectResult> aspects,
        IEphemerisService ephemeris,
        double julianDayUt)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);

        return Analyze(positions, aspects,
            moon => FindFirstApplyingAspect(moon, positions, ephemeris, julianDayUt));
    }

    /// <inheritdoc />
    public MoonConditionInfo? GetMoonConditionWithFixedTargets(
        IReadOnlyList<PlanetPosition> positions,
        IReadOnlyList<AspectResult> aspects) =>
        Analyze(positions, aspects,
            moon => DetermineVoidOfCourse(moon.Longitude, 30.0 - (moon.Longitude % 30.0), positions));

    /// <summary>
    /// Builds the Moon condition from the chart positions, taking the first
    /// applying aspect and the void-of-course status from
    /// <paramref name="findFirstApplying"/>, which receives the Moon's position.
    /// </summary>
    /// <param name="positions">All planetary positions from the chart calculation.</param>
    /// <param name="aspects">All detected aspects from the chart calculation.</param>
    /// <param name="findFirstApplying">The scan for the first applying aspect: moving or fixed targets.</param>
    /// <returns>The Moon condition, or null if the positions hold no Moon or no Sun.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="positions"/> or <paramref name="aspects"/> is null.
    /// </exception>
    private static MoonConditionInfo? Analyze(
        IReadOnlyList<PlanetPosition> positions,
        IReadOnlyList<AspectResult> aspects,
        Func<PlanetPosition, (bool IsVoC, string? Planet, AspectType? Aspect, double? Orb)> findFirstApplying)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(aspects);

        PlanetPosition? moon = positions.FirstOrDefault(
            p => string.Equals(p.Name, "Moon", StringComparison.OrdinalIgnoreCase));

        if (moon is null)
            return null;

        PlanetPosition? sun = positions.FirstOrDefault(
            p => string.Equals(p.Name, "Sun", StringComparison.OrdinalIgnoreCase));

        if (sun is null)
            return null;

        // Phase calculation
        double phaseAngle = (moon.Longitude - sun.Longitude + 360.0) % 360.0;
        MoonPhaseType phase = ClassifyPhase(phaseAngle);

        // Via Combusta
        bool isViaCombusta = moon.Longitude >= ViaCombustaStart
                          && moon.Longitude < ViaCombustaEnd;

        // VoC and first applying aspect
        (bool isVoC, string? firstPlanet, AspectType? firstAspect, double? firstOrb) =
            findFirstApplying(moon);

        return new MoonConditionInfo
        {
            Phase = phase,
            PhaseAngle = phaseAngle,
            PhasePosition = GetPhasePosition(phaseAngle),
            IsVoidOfCourse = isVoC,
            FirstApplyingPlanet = firstPlanet,
            FirstApplyingAspectType = firstAspect,
            FirstApplyingOrb = firstOrb,
            IsViaCombusta = isViaCombusta,
            MoonSign = moon.Sign,
            MoonLongitude = moon.Longitude,
        };
    }

    #region Phase Classification

    /// <summary>
    /// Classifies the Sun-Moon angular separation into one of the eight
    /// traditional Moon phases. Each phase spans 45°.
    /// </summary>
    /// <param name="phaseAngle">
    /// Sun-Moon angular separation in degrees (0-360).
    /// </param>
    /// <returns>The Moon phase classification.</returns>
    private static MoonPhaseType ClassifyPhase(double phaseAngle) => phaseAngle switch
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

    /// <summary>
    /// Returns the position within the current lunar phase: "early", "mid", or "late".
    /// Each named phase in the Dane Rudhyar 8-phase system spans 45°.
    /// Early = first 15° of the phase (defining theme at full strength),
    /// mid = middle 15° (theme active but evolving),
    /// late = final 15° (transitioning toward next phase's quality).
    /// </summary>
    /// <param name="phaseAngle">
    /// The Sun-Moon elongation in degrees (0°-360°), as computed in
    /// <see cref="GetMoonCondition"/>.
    /// </param>
    /// <returns>"early", "mid", or "late".</returns>
    public static string GetPhasePosition(double phaseAngle)
    {
        double withinPhase = phaseAngle % 45.0;
        return withinPhase switch
        {
            < 15.0 => "early",
            < 30.0 => "mid",
            _ => "late",
        };
    }

    #endregion

    #region Void of Course

    /// <summary>
    /// Finds the first exact major Ptolemaic aspect the Moon makes to a target
    /// before it leaves its current sign, following the Moon and every target
    /// in the ephemeris.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The window runs from the chart instant to the Moon's sign exit
    /// (<see cref="FindSignExit"/>). For each target,
    /// <see cref="AspectPerfectionFinder.FindFirstExactMajorAspect"/> returns
    /// the first exact aspect of all five angles, in both orientations,
    /// strictly after the chart instant; the earliest across the targets is
    /// the first applying aspect, and none before the exit means void of
    /// course. Nothing after the earliest hit so far can come first, so each
    /// later target is searched only up to it.
    /// </para>
    /// <para>
    /// Body list: the same as <see cref="DetermineVoidOfCourse"/> — the Sun
    /// and Mercury through Pluto among the positions given.
    /// </para>
    /// </remarks>
    /// <param name="moon">The Moon's position at the chart instant.</param>
    /// <param name="positions">All planetary positions from the chart calculation.</param>
    /// <param name="ephemeris">Ephemeris the Moon and the targets are followed in.</param>
    /// <param name="julianDayUt">The chart instant (Julian Day, UT).</param>
    /// <returns>
    /// A tuple of (isVoC, firstApplyingPlanet, firstAspectType, firstOrb), as
    /// <see cref="DetermineVoidOfCourse"/> returns it. The orb is the
    /// separation from exact at the chart instant, from the chart positions.
    /// </returns>
    private static (bool IsVoC, string? Planet, AspectType? Aspect, double? Orb)
        FindFirstApplyingAspect(
            PlanetPosition moon,
            IReadOnlyList<PlanetPosition> positions,
            IEphemerisService ephemeris,
            double julianDayUt)
    {
        double signExitJd = FindSignExit(ephemeris, julianDayUt);

        ExactAspectEvent? first = null;
        PlanetPosition? firstTarget = null;

        foreach (PlanetPosition planet in positions)
        {
            // Skip Moon itself
            if (planet.Body == CelestialBodyId.Moon)
                continue;

            // Only check Sun through Pluto (skip TrueNode, Chiron)
            if (!IsAspectTarget(planet.Body))
                continue;

            ExactAspectEvent? hit = AspectPerfectionFinder.FindFirstExactMajorAspect(
                ephemeris, CelestialBodyId.Moon, planet.Body, julianDayUt, first?.JulianDay ?? signExitJd);

            if (hit is not null && hit.JulianDay < signExitJd
                && (first is null || hit.JulianDay < first.JulianDay))
            {
                first = hit;
                firstTarget = planet;
            }
        }

        if (first is null || firstTarget is null)
        {
            return (true, null, null, null);
        }

        double orb = Math.Abs(Norm180(moon.Longitude - firstTarget.Longitude - first.SignedTargetAngle));
        return (false, firstTarget.Name, first.AspectType, Math.Round(orb, 4));
    }

    /// <summary>
    /// Finds when the Moon leaves the sign it occupies at
    /// <paramref name="julianDayUt"/>, bisecting its forward travel against
    /// the arc left in that sign until the bracket is no wider than
    /// <see cref="AspectPerfectionFinder.TimePrecisionDays"/>.
    /// </summary>
    /// <remarks>
    /// The Moon never retrogrades, so its travel from the chart instant grows
    /// monotonically and crosses the remaining arc once inside
    /// <see cref="SignExitBracketDays"/>.
    /// </remarks>
    /// <param name="ephemeris">Ephemeris the Moon is followed in.</param>
    /// <param name="julianDayUt">The chart instant (Julian Day, UT).</param>
    /// <returns>The first instant (Julian Day, UT) at which the Moon is in the next sign.</returns>
    private static double FindSignExit(IEphemerisService ephemeris, double julianDayUt)
    {
        double start = MoonLongitude(ephemeris, julianDayUt);
        double remaining = 30.0 - (start % 30.0);

        double inSign = julianDayUt;
        double left = julianDayUt + SignExitBracketDays;

        while (left - inSign > AspectPerfectionFinder.TimePrecisionDays)
        {
            double mid = (inSign + left) / 2.0;
            double travel = (MoonLongitude(ephemeris, mid) - start + 360.0) % 360.0;

            if (travel < remaining)
                inSign = mid;
            else
                left = mid;
        }

        return left;
    }

    /// <summary>The Moon's ecliptic longitude at an instant (Julian Day, UT).</summary>
    private static double MoonLongitude(IEphemerisService ephemeris, double julianDayUt) =>
        ephemeris.CalculatePositionLongitudeOnly(julianDayUt, CelestialBodyId.Moon).Longitude;

    /// <summary>Normalises an angle to (−180, 180].</summary>
    private static double Norm180(double degrees)
    {
        double x = ((degrees % 360.0) + 360.0) % 360.0;
        return x > 180.0 ? x - 360.0 : x;
    }

    /// <summary>
    /// Determines whether the Moon is void of course by performing a forward
    /// geometric scan through the remaining arc of the Moon's current sign,
    /// with every target held at its given longitude — the scan behind
    /// <see cref="GetMoonConditionWithFixedTargets"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For each planet (Sun through Pluto), for each major Ptolemaic aspect
    /// angle (0°, 60°, 90°, 120°, 180°), the method computes the Moon
    /// longitude(s) that would form an exact aspect to that planet's current
    /// position. If any such longitude falls ahead of the Moon and before
    /// the sign boundary, the aspect will perfect and the Moon is not void.
    /// </para>
    /// <para>
    /// This approach is independent of the pre-computed aspect list and its
    /// orb limits. A sextile that is currently 10° from exact but will
    /// perfect within the sign is correctly detected, whereas the previous
    /// orb-limited approach would miss it entirely.
    /// </para>
    /// <para>
    /// Body list: Sun, Mercury, Venus, Mars, Jupiter, Saturn, Uranus,
    /// Neptune, Pluto (9 targets). Matches <c>VoidOfCourseScanner</c> for
    /// consistency. Chiron and True Node are excluded.
    /// </para>
    /// </remarks>
    /// <param name="moonLongitude">
    /// Moon's ecliptic longitude in degrees (0–360).
    /// </param>
    /// <param name="degreesRemainingInSign">
    /// Degrees the Moon has left in its current sign (0–30).
    /// </param>
    /// <param name="positions">
    /// All planetary positions from the chart calculation.
    /// </param>
    /// <returns>
    /// A tuple of (isVoC, firstApplyingPlanet, firstAspectType, firstOrb).
    /// If void, firstApplyingPlanet/firstAspectType/firstOrb are null.
    /// The orb represents the forward distance in degrees the Moon must
    /// travel to perfect the aspect.
    /// </returns>
    private static (bool IsVoC, string? Planet, AspectType? Aspect, double? Orb)
        DetermineVoidOfCourse(
            double moonLongitude,
            double degreesRemainingInSign,
            IReadOnlyList<PlanetPosition> positions)
    {
        double bestForwardDistance = double.MaxValue;
        string? bestPlanet = null;
        AspectType? bestAspect = null;

        foreach (PlanetPosition planet in positions)
        {
            // Skip Moon itself
            if (planet.Body == CelestialBodyId.Moon)
                continue;

            // Only check Sun through Pluto (skip TrueNode, Chiron)
            if (!IsAspectTarget(planet.Body))
                continue;

            foreach (double angle in MajorAspectAngles)
            {
                // Candidate Moon longitude that forms an exact aspect to this planet.
                // For aspect angle A to planet at longitude P, the Moon forms the
                // aspect when it reaches (P + A) mod 360 or (P - A) mod 360.
                double c1 = (planet.Longitude + angle) % 360.0;
                CheckCandidate(moonLongitude, degreesRemainingInSign, c1,
                    planet.Name, angle,
                    ref bestForwardDistance, ref bestPlanet, ref bestAspect);

                // For non-symmetric angles (not 0° or 180°), check the mirror position
                if (angle > 0.0 && angle < 180.0)
                {
                    double c2 = (planet.Longitude - angle + 360.0) % 360.0;
                    CheckCandidate(moonLongitude, degreesRemainingInSign, c2,
                        planet.Name, angle,
                        ref bestForwardDistance, ref bestPlanet, ref bestAspect);
                }
            }
        }

        if (bestPlanet is null)
        {
            return (true, null, null, null);
        }

        return (false, bestPlanet, bestAspect, Math.Round(bestForwardDistance, 4));
    }

    /// <summary>
    /// Checks whether a candidate Moon longitude is ahead of the current Moon
    /// position and before the sign boundary. If so, and if it is closer than
    /// the current best, updates the best candidate.
    /// </summary>
    /// <param name="moonLongitude">Moon's current ecliptic longitude.</param>
    /// <param name="degreesRemainingInSign">
    /// Degrees remaining before the Moon leaves its current sign.
    /// </param>
    /// <param name="candidateLongitude">
    /// The Moon longitude that would form an exact aspect.
    /// </param>
    /// <param name="planetName">Name of the target planet.</param>
    /// <param name="aspectAngle">The major aspect angle in degrees.</param>
    /// <param name="bestForwardDistance">
    /// Current best forward distance (updated if this candidate is closer).
    /// </param>
    /// <param name="bestPlanet">
    /// Current best planet name (updated if this candidate is closer).
    /// </param>
    /// <param name="bestAspect">
    /// Current best aspect type (updated if this candidate is closer).
    /// </param>
    private static void CheckCandidate(
        double moonLongitude,
        double degreesRemainingInSign,
        double candidateLongitude,
        string planetName,
        double aspectAngle,
        ref double bestForwardDistance,
        ref string? bestPlanet,
        ref AspectType? bestAspect)
    {
        // Forward distance: how far the Moon must travel (in increasing longitude)
        // to reach the candidate. The modular arithmetic handles the 360°/0° wrap.
        double forwardDistance = (candidateLongitude - moonLongitude + 360.0) % 360.0;

        // Must be strictly ahead of the Moon (not zero = currently exact/separating)
        // and must perfect before the Moon leaves its current sign.
        if (forwardDistance > 0.0 && forwardDistance < degreesRemainingInSign
            && forwardDistance < bestForwardDistance)
        {
            bestForwardDistance = forwardDistance;
            bestPlanet = planetName;
            bestAspect = AspectTypeForAngle[aspectAngle];
        }
    }

    /// <summary>
    /// Returns true if the body is a valid VoC aspect target (Sun through Pluto).
    /// Excludes Moon, True Node, and Chiron — consistent with
    /// <see cref="VoidOfCourseScanner"/>.
    /// </summary>
    /// <param name="body">The celestial body identifier to check.</param>
    /// <returns>True if the body should be scanned for VoC aspects.</returns>
    private static bool IsAspectTarget(CelestialBodyId body)
    {
        return body is CelestialBodyId.Sun
            or CelestialBodyId.Mercury
            or CelestialBodyId.Venus
            or CelestialBodyId.Mars
            or CelestialBodyId.Jupiter
            or CelestialBodyId.Saturn
            or CelestialBodyId.Uranus
            or CelestialBodyId.Neptune
            or CelestialBodyId.Pluto;
    }

    #endregion
}
