using System.Globalization;

using AstroCast.Core.Configuration;
using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Exceptions;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Orchestrates the full electional calculation pipeline: builds
/// disqualification maps, scans for candidate windows, scores them,
/// and returns ranked results.
/// </summary>
/// <remarks>
/// <para>
/// Follows the TransitService composition pattern: one public
/// <see cref="Calculate"/> method that coordinates all sub-services.
/// </para>
/// <para>
/// The algorithm has five steps:
/// </para>
/// <list type="number">
///   <item><description>Build disqualification map (Mercury retrograde)</description></item>
///   <item><description>Build Moon void-of-course calendar</description></item>
///   <item><description>Generate candidate grid: every eligible sample, with its Moon</description></item>
///   <item><description>Score every eligible sample (all 7 dimensions) from the scorer's inputs
///   alone (<see cref="IChartEngine.CalculateElectionalScoringInputs"/>)</description></item>
///   <item><description>Select top N with the minimum separation, cast each one's full chart,
///   and return ranked results</description></item>
/// </list>
/// <para>
/// The best-scoring eligible sample of the grid is always the first candidate instant, the
/// earliest on a tie (S-D10(b), branch O2, 1.3.3). Until 1.3.3 only a pool chosen by a
/// Moon-only quick score was fully scored, and a better moment outside it was never seen.
/// </para>
/// <para>
/// Before step 1 the natal chart's house system is cast once at the place: a system with no
/// houses there (Placidus or Koch beyond the polar circles) refuses the search with the system
/// and the latitude named (Steve's ruling 2 of 2026-10-09, branch O3, 1.3.3).
/// </para>
/// <para>
/// In step 4 a sample on a day when the Sun does not rise or set at the place has no planetary
/// hour: it is not scored, and <see cref="ElectionalResult.SamplesWithoutPlanetaryHour"/> counts
/// it (the same ruling). Every window reported has its hour.
/// </para>
/// </remarks>
public class ElectionalService : IElectionalService
{
    /// <summary>Orb threshold below which a synastry aspect is annotated "exact".</summary>
    private const double ExactOrbThreshold = 10.0 / 60.0; // 10 arcminutes

    /// <summary>Orb threshold above which a synastry aspect is annotated "wide".</summary>
    private const double WideOrbThreshold = 5.0;

    /// <summary>Scan step for date ranges ≤ 90 days (15 minutes in JD).</summary>
    private const double ShortRangeStep = 15.0 / 1440.0;

    /// <summary>Scan step for date ranges > 90 days (30 minutes in JD).</summary>
    private const double LongRangeStep = 30.0 / 1440.0;

    /// <summary>Day threshold for switching from fine to coarse scanning.</summary>
    private const int LongRangeDayThreshold = 90;

    private readonly IEphemerisService _ephemeris;
    private readonly IChartEngine _chartEngine;
    private readonly VoidOfCourseScanner _vocScanner;
    private readonly MoonConditionEvaluator _moonEvaluator;
    private readonly MercuryRetroDetector _mercuryDetector;
    private readonly PlanetaryHourCalculator _hourCalculator;
    private readonly ElectionalScorer _scorer;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="ElectionalService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for all astronomical calculations.</param>
    /// <param name="chartEngine">Chart engine for full chart calculation at candidate times.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules the range, the samples and the disqualifying periods are read on
    /// the event's clock with. Optional: when omitted, the IANA data the app carries
    /// (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    /// <remarks>
    /// Sub-services (<see cref="VoidOfCourseScanner"/>, <see cref="MoonConditionEvaluator"/>,
    /// <see cref="MercuryRetroDetector"/>, <see cref="PlanetaryHourCalculator"/>,
    /// <see cref="ElectionalScorer"/>) are intentionally constructed here with
    /// <c>new</c> rather than injected via DI. These are internal implementation
    /// details of the electional engine — they share the same
    /// <see cref="IEphemerisService"/> and <see cref="ITimeZoneRules"/> instances and
    /// have no independent lifecycle.
    /// Injecting them would expose an implementation detail and provide no
    /// testability benefit, since the electional sub-services are always tested
    /// through <see cref="IElectionalService.Calculate"/>.
    /// </remarks>
    public ElectionalService(IEphemerisService ephemeris, IChartEngine chartEngine, ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(chartEngine);

        _ephemeris = ephemeris;
        _chartEngine = chartEngine;
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules();
        _vocScanner = new VoidOfCourseScanner(ephemeris, _timeZoneRules);
        _moonEvaluator = new MoonConditionEvaluator(ephemeris, _timeZoneRules);
        _mercuryDetector = new MercuryRetroDetector(ephemeris, _timeZoneRules);
        _hourCalculator = new PlanetaryHourCalculator(ephemeris, _timeZoneRules);
        _scorer = new ElectionalScorer();
    }

    /// <inheritdoc />
    public ElectionalResult Calculate(ElectionalInput input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Midnight-wrapping time windows (e.g. 22:00-06:00) are not supported
        // by the sampling filter; reject rather than silently exclude everything.
        if (input.TimeConstraint is not null
            && input.TimeConstraint.Earliest > input.TimeConstraint.Latest)
        {
            throw new ArgumentException(
                "Time constraint window may not wrap midnight: earliest "
                + $"({input.TimeConstraint.Earliest:HH\\:mm}) must not be later than latest "
                + $"({input.TimeConstraint.Latest:HH\\:mm}).",
                nameof(input));
        }

        // Convert the requested calendar range to Julian Days, through the event's own
        // timezone. RangeStart and RangeEnd are dates the user picked on a calendar at
        // the event location, so their boundaries are local midnights; passing them to
        // DateToJulianDay with hour 0.0 would instead denote midnight in Greenwich and
        // displace the entire scan by the location's UTC offset.
        //
        // The end bound is local midnight opening the day *after* RangeEnd, which makes
        // the half-open scan interval cover the whole of RangeEnd through 23:59:59 local
        // -- RangeEnd is documented as the last *acceptable* date, so an event may fall
        // anywhere within it. Each bound converts on its own date so that a range
        // spanning a daylight-saving transition is neither displaced nor resized.
        //
        // Each bound is the first instant its date's midnight reads: where the clocks go back
        // at midnight, the earlier of the two, so the hour between them belongs to that date.
        // Until 1.3.3 the bounds took the later (D17, the rule for a chart's reading), and
        // Havana's range ending 31 October 2026 scanned 49 hours, an hour into 1 November.
        //
        // The typed bounds are read once to the days they denote (D21; branch R3, 1.3.4, R-D7): a
        // bound on or before 1582-10-04 is a Julian date, one in 1582-10-05 to 1582-10-14 is refused.
        DateOnly firstDay = CalendarDateFormatter.ProlepticDay(input.RangeStart);
        DateOnly lastDay = CalendarDateFormatter.ProlepticDay(input.RangeEnd);
        double startJd = JulianDayUtility.LocalDayStartToJulianDay(
            _ephemeris, firstDay, _timeZoneRules, input.TimeZone);
        double endJd = JulianDayUtility.LocalDayStartToJulianDay(
            _ephemeris, lastDay.AddDays(1), _timeZoneRules, input.TimeZone);

        // Candidates are cast in the natal chart's house system, and some systems have no houses
        // at some places (Placidus and Koch beyond the polar circles). The engine decides: the
        // houses are cast once, at the range's start, and the search is refused before it scans.
        // Until 1.3.3 the first candidate failed the whole search with the engine's house error.
        try
        {
            _ephemeris.CalculateHouses(
                startJd, input.Latitude, input.Longitude, (char)input.NatalChart.Input.HouseSystem);
        }
        catch (EphemerisCalculationException ex)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture,
                    $"Electional candidates are cast in the natal chart's house system, {input.NatalChart.HouseSystemName}, which has no houses "
                    + $"at latitude {Math.Abs(input.Latitude):F4}° {(input.Latitude >= 0 ? "N" : "S")}. Choose a natal chart in a house system that exists there, such "
                    + $"as Equal, Whole Sign or Regiomontanus."),
                nameof(input),
                ex);
        }

        // Step 1: Build disqualification map
        List<DisqualifiedPeriod> disqualified = [];
        if (input.AvoidMercuryRetrograde)
        {
            IReadOnlyList<DisqualifiedPeriod> mercuryRx =
                _mercuryDetector.FindRetrogradePeriods(startJd, endJd, input.TimeZone);
            disqualified.AddRange(mercuryRx);
        }

        // Step 2: Build Moon void-of-course calendar
        IReadOnlyList<VoidOfCoursePeriod> vocPeriods =
            _vocScanner.Scan(startJd, endJd, input.TimeZone);

        foreach (VoidOfCoursePeriod voc in vocPeriods)
        {
            disqualified.Add(new DisqualifiedPeriod
            {
                Start = voc.StartLocal,
                End = voc.EndLocal,
                Reason = $"Moon void-of-course in {voc.MoonSign}",
                StartUtc = voc.StartUtc,
                EndUtc = voc.EndUtc,
            });
        }

        // Step 3: Generate candidate grid: every eligible sample, with its Moon
        int totalDays = lastDay.DayNumber - firstDay.DayNumber;
        double scanStep = totalDays > LongRangeDayThreshold ? LongRangeStep : ShortRangeStep;

        List<(double Jd, DateTime LocalDt, MoonCondition Moon)> eligible = [];

        // Each sample's Julian Day is computed from its index rather than accumulated.
        // Adding scanStep several hundred times drifts by a fraction of a second, which
        // is enough for the accumulated value to fall just below endJd on the step that
        // should have ended the loop and admit one extra sample sitting on the exclusive
        // bound. That bound is local midnight opening the day after RangeEnd, so the
        // stray sample is a moment dated outside the range the user asked for.
        //
        // The index alone does not prevent it: the last index's Julian Day can still land a
        // few ulps below endJd and round, as an instant, to that midnight. So a sample is
        // admitted only while its instant, taken to the second, is before the end bound's.
        // Until 1.3.3 the stray sample was reported in searches such as 2026-10-28 to
        // 2026-11-03 at Richmond, inside a Mercury retrograde to be avoided.
        int sampleCount = (int)Math.Ceiling((endJd - startJd) / scanStep);
        DateTime endUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, endJd);

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            double jd = startJd + (sampleIndex * scanStep);
            DateTime sampleUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jd);
            if (jd >= endJd || sampleUtc >= endUtc)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            DateTime localDt = JulianDayUtility.ToLocalDateTime(_ephemeris, jd, _timeZoneRules, input.TimeZone);

            // Skip if outside AllowedDays
            if (input.AllowedDays is not null && !input.AllowedDays.Contains(localDt.DayOfWeek))
            {
                continue;
            }

            // Skip if outside TimeConstraint
            if (input.TimeConstraint is not null)
            {
                TimeOnly localTime = TimeOnly.FromDateTime(localDt);
                if (localTime < input.TimeConstraint.Earliest
                    || localTime > input.TimeConstraint.Latest)
                {
                    continue;
                }
            }

            // Skip if inside any disqualified period: by the sample's instant, since a local
            // reading the clocks repeat names two instants
            if (IsDisqualified(sampleUtc, disqualified))
            {
                continue;
            }

            // Moon condition (the evaluator's reading at the sample's instant)
            MoonCondition moonCondition = _moonEvaluator.Evaluate(
                jd, vocPeriods, input.TimeZone);

            eligible.Add((jd, localDt, moonCondition));
        }

        // Step 4: Score every eligible sample, from only what the scorer reads: the chart's
        // positions, houses, dignities and context at the sample's instant
        // (CalculateElectionalScoringInputs), its Moon and its planetary hour. These equal the
        // full chart's, so each sample scores exactly as a full chart would; the full chart is
        // cast only for the windows reported. Until 1.3.3 only a pool chosen by a Moon-only quick
        // score was scored, and a moment outside it was never seen however well it scored.
        //
        // A sample on a day when the Sun does not rise or set at the place has no planetary hour:
        // it is not scored, and is counted. Until 1.3.3 the first such sample ended the whole search.
        List<CandidateWindow> scored = [];
        int samplesWithoutPlanetaryHour = 0;

        foreach ((double jd, DateTime localDt, MoonCondition moonCondition) in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Planetary hour, first. The two exceptions are the ones the chart engine catches
            // around the same call: no sunrise or sunset that day, and the calculator's own.
            PlanetaryHour planetaryHour;
            try
            {
                planetaryHour = _hourCalculator.Calculate(
                    jd, input.Latitude, input.Longitude, input.TimeZone);
            }
            catch (Exception ex) when (ex is SunDoesNotRiseOrSetException or InvalidOperationException)
            {
                samplesWithoutPlanetaryHour++;
                continue;
            }

            // Cast at the instant sampled and scored, not at the one its wall clock converts
            // back to: in an hour the clocks repeat, the wall clock names two instants (TZ-1).
            ChartResult scoringInputs = _chartEngine.CalculateElectionalScoringInputs(
                BuildCandidateChartInput(input, localDt), JulianDayUtility.ToDateTimeUtc(_ephemeris, jd));

            // Scoring (all 7 dimensions)
            ScoreBreakdown breakdown = _scorer.Score(
                scoringInputs, moonCondition, planetaryHour, input.ElectionType, input.NatalChart);

            scored.Add(new CandidateWindow
            {
                Rank = 0,
                LocalDateTime = localDt,
                Chart = scoringInputs,
                Moon = moonCondition,
                PlanetaryHour = planetaryHour,
                Score = breakdown.OverallScore,
                ScoreBreakdown = breakdown,
                NatalHarmonyAspects = [],
                Strengths = [],
                Compromises = [],
            });
        }

        // Step 5: Select top N with minimum separation. The sort is stable, so on a tie the
        // earlier sample comes first.
        List<CandidateWindow> sorted = scored
            .OrderByDescending(c => c.Score)
            .ToList();

        List<CandidateWindow> selected = ApplySeparationFilter(
            sorted, input.CandidateCount, input.MinimumCandidateSeparationHours);

        // Each window reported, in rank order, takes the full chart at the same instant: the
        // scoring inputs' UtcDateTime is the instant the full chart is cast at. Its Moon and
        // planetary hour are the sample's.
        List<CandidateWindow> ranked = [];

        foreach (CandidateWindow window in selected)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Full chart calculation
            ChartResult chart = _chartEngine.CalculateForElectional(
                BuildCandidateChartInput(input, window.LocalDateTime), window.Chart.UtcDateTime);

            // Full scoring (all 7 dimensions)
            ScoreBreakdown breakdown = _scorer.Score(
                chart, window.Moon, window.PlanetaryHour, input.ElectionType, input.NatalChart);

            // Natal harmony aspects (inter-chart)
            IReadOnlyList<InterChartAspect> natalAspects =
                CalculateNatalHarmonyAspects(chart, input.NatalChart);

            // Generate strengths and compromises
            IReadOnlyList<string> strengths = GenerateStrengths(
                breakdown, window.Moon, window.PlanetaryHour, chart, input.ElectionType);
            IReadOnlyList<string> compromises = GenerateCompromises(
                breakdown, window.Moon, chart);

            ranked.Add(new CandidateWindow
            {
                Rank = 0,
                LocalDateTime = window.LocalDateTime,
                Chart = chart,
                Moon = window.Moon,
                PlanetaryHour = window.PlanetaryHour,
                Score = breakdown.OverallScore,
                ScoreBreakdown = breakdown,
                NatalHarmonyAspects = natalAspects,
                Strengths = strengths,
                Compromises = compromises,
            });
        }

        for (int i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
        }

        return new ElectionalResult
        {
            Input = input,
            NatalChart = input.NatalChart,
            Candidates = ranked.AsReadOnly(),
            RequestedCandidateCount = input.CandidateCount,
            SamplesWithoutPlanetaryHour = samplesWithoutPlanetaryHour,
            DisqualifiedPeriods = disqualified.AsReadOnly(),
            VoidOfCoursePeriods = vocPeriods,
            CalculatedAt = DateTime.UtcNow,
        };
    }

    #region Candidate Separation

    /// <summary>
    /// Selects up to <paramref name="count"/> candidates from a score-descending list,
    /// ensuring each selected candidate is at least <paramref name="separationHours"/>
    /// hours apart from all previously selected candidates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses a greedy algorithm: select the highest-scoring remaining candidate,
    /// then exclude all others within the separation window before selecting the next.
    /// When <paramref name="separationHours"/> is 0, no filtering is applied (backward compatibility).
    /// If fewer than <paramref name="count"/> candidates survive the filter,
    /// the result contains however many remain.
    /// </para>
    /// <para>
    /// The separation is the time between the candidates' instants, their charts'
    /// <see cref="ChartResult.UtcDateTime"/>, not between their local clock readings, which
    /// differ from it by any change of the clocks between them. Until 1.3.3 the clock was
    /// measured: across a spring-forward night a pair 5–6 hours apart in time was kept as
    /// "6 hours apart", and across a fall-back night a pair 6–7 hours apart was rejected.
    /// </para>
    /// </remarks>
    /// <param name="sortedCandidates">Candidates sorted by score descending.</param>
    /// <param name="count">Maximum number of candidates to select.</param>
    /// <param name="separationHours">Minimum hours between selected candidates. 0 disables filtering.</param>
    /// <returns>Selected candidates in score-descending order.</returns>
    internal static List<CandidateWindow> ApplySeparationFilter(
        List<CandidateWindow> sortedCandidates,
        int count,
        double separationHours)
    {
        ArgumentNullException.ThrowIfNull(sortedCandidates);

        if (separationHours <= 0.0)
        {
            return sortedCandidates.Take(count).ToList();
        }

        List<CandidateWindow> selected = [];

        foreach (CandidateWindow candidate in sortedCandidates)
        {
            if (selected.Count >= count)
            {
                break;
            }

            bool tooClose = false;
            foreach (CandidateWindow already in selected)
            {
                double hoursDiff = Math.Abs(
                    (candidate.Chart.UtcDateTime - already.Chart.UtcDateTime).TotalHours);
                if (hoursDiff < separationHours)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Returns true if the given instant falls within any disqualified period:
    /// [<see cref="DisqualifiedPeriod.StartUtc"/>, <see cref="DisqualifiedPeriod.EndUtc"/>).
    /// </summary>
    /// <remarks>
    /// Instants, not local readings: in an hour the clocks repeat, a reading names two
    /// instants, and until 1.3.3 the samples of one pass were judged by the other pass's side
    /// of a bound (void samples admitted at St. John's and Havana in November 2028; samples
    /// an hour clear of a void excluded at St. John's, Sydney and Cairo).
    /// </remarks>
    /// <param name="sampleUtc">The sample's instant, <see cref="DateTimeKind.Utc"/>, to the second.</param>
    /// <param name="periods">List of disqualified periods.</param>
    /// <returns>True if disqualified.</returns>
    private static bool IsDisqualified(DateTime sampleUtc, List<DisqualifiedPeriod> periods)
    {
        foreach (DisqualifiedPeriod period in periods)
        {
            if (sampleUtc >= period.StartUtc && sampleUtc < period.EndUtc)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Builds the chart input of a candidate sample: an "Event" chart at the election's place,
    /// on its zone's clock, in the natal chart's house system. Both the scoring inputs and the
    /// full chart of a reported window are cast from it.
    /// </summary>
    /// <param name="input">The electional search.</param>
    /// <param name="localDt">The sample's reading on the election's clock.</param>
    /// <returns>The chart input.</returns>
    private static ChartInput BuildCandidateChartInput(ElectionalInput input, DateTime localDt) => new()
    {
        Name = $"Election {CalendarDateFormatter.Minutes(localDt)}",
        LocalDateTime = localDt,
        TimeZone = input.TimeZone,
        Latitude = input.Latitude,
        Longitude = input.Longitude,
        LocationName = input.LocationName,
        EventType = "Event",
        HouseSystem = input.NatalChart.Input.HouseSystem,
    };

    /// <summary>
    /// Calculates inter-chart aspects between the candidate election chart
    /// and the natal chart, reusing the synastry aspect pattern.
    /// </summary>
    /// <remarks>
    /// The orb of each pair is the synastry orb of its points' own categories
    /// (<see cref="SynastryOrbs.GetOrb"/> on "Sun", "Venus", "ASC"), so a luminary takes the
    /// luminary orbs and a natal angle is an angle; the aspects are named "E Sun", "N ASC".
    /// Until 1.3.3 the lookup saw the prefixed names, which no category recognises, and every
    /// pair took the Outer orbs. The Natal Harmony score does not read this table.
    /// </remarks>
    /// <param name="candidate">The election candidate chart.</param>
    /// <param name="natal">The client's natal chart.</param>
    /// <returns>List of inter-chart aspects sorted by orb.</returns>
    internal static IReadOnlyList<InterChartAspect> CalculateNatalHarmonyAspects(
        ChartResult candidate, ChartResult natal)
    {
        // Guard: natal chart may be absent (user hasn't loaded one)
        if (natal.Positions is null || natal.Positions.Count == 0)
        {
            return Array.Empty<InterChartAspect>();
        }

        // Build point lists with their own names: the orb lookup reads the category from them,
        // and the "E " and "N " prefixes are added to the aspects found
        List<(string Name, double Longitude)> electionPoints = candidate.Positions
            .Select(p => (p.Name, p.Longitude))
            .ToList();

        List<(string Name, double Longitude)> natalPoints = natal.Positions
            .Select(p => (p.Name, p.Longitude))
            .ToList();

        // Add natal angles
        natalPoints.Add(("ASC", natal.Houses.Ascendant));
        natalPoints.Add(("MC", natal.Houses.Midheaven));

        List<InterChartAspect> results = [];

        foreach ((string eName, double eLon) in electionPoints)
        {
            foreach ((string nName, double nLon) in natalPoints)
            {
                InterChartAspect? aspect = FindInterChartAspect(eName, eLon, nName, nLon);
                if (aspect is not null)
                {
                    results.Add(aspect);
                }
            }
        }

        results.Sort((a, b) => a.Orb.CompareTo(b.Orb));
        return results.AsReadOnly();
    }

    /// <summary>
    /// Check all aspect definitions against the angular separation of two points,
    /// using synastry-specific orbs based on planet category.
    /// </summary>
    /// <param name="name1">Name of the election point (e.g., "Sun"); the aspect names it "E Sun".</param>
    /// <param name="lon1">Ecliptic longitude of the first point.</param>
    /// <param name="name2">Name of the natal point (e.g., "Venus", "ASC"); the aspect names it "N Venus".</param>
    /// <param name="lon2">Ecliptic longitude of the second point.</param>
    /// <returns>An <see cref="InterChartAspect"/> if found, otherwise null.</returns>
    private static InterChartAspect? FindInterChartAspect(
        string name1, double lon1,
        string name2, double lon2)
    {
        double separation = AspectService.AngularSeparation(lon1, lon2);

        foreach (AspectDefinition definition in AspectDefinition.All)
        {
            double synastryOrb = SynastryOrbs.GetOrb(definition.Type, name1, name2);
            double orbActual = Math.Abs(separation - definition.Angle);

            if (orbActual <= synastryOrb)
            {
                string? note = null;
                if (orbActual < ExactOrbThreshold)
                {
                    note = "exact";
                }
                else if (orbActual > WideOrbThreshold)
                {
                    note = "wide";
                }

                return new InterChartAspect
                {
                    Body1Name = $"E {name1}",
                    Body1Longitude = lon1,
                    Body2Name = $"N {name2}",
                    Body2Longitude = lon2,
                    Aspect = definition,
                    Orb = orbActual,
                    Note = note,
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Generates 2–4 human-readable strength strings based on high-scoring dimensions.
    /// </summary>
    /// <param name="breakdown">Score breakdown for this candidate.</param>
    /// <param name="moon">Moon condition at this time.</param>
    /// <param name="hour">Planetary hour at this time.</param>
    /// <param name="chart">Full chart result.</param>
    /// <param name="electionType">Election type being scored.</param>
    /// <returns>List of strength descriptions.</returns>
    private static IReadOnlyList<string> GenerateStrengths(
        ScoreBreakdown breakdown,
        MoonCondition moon,
        PlanetaryHour hour,
        ChartResult chart,
        ElectionType electionType)
    {
        List<string> strengths = [];

        if (breakdown.MoonScore >= 70.0)
        {
            strengths.Add($"Moon in {moon.Sign}, {moon.Phase}, well-aspected");
        }

        if (breakdown.AscendantScore >= 70.0)
        {
            ZodiacSign ascSign = (ZodiacSign)((int)(chart.Houses.Ascendant % 360.0 / 30.0) + 1);
            strengths.Add($"Strong Ascendant in {ascSign}");
        }

        if (breakdown.SignificatorScore >= 70.0)
        {
            strengths.Add($"Significator well-placed for {electionType}");
        }

        if (breakdown.PlanetaryHourScore >= 70.0)
        {
            strengths.Add($"Favorable planetary hour ({hour.RulingPlanet})");
        }

        if (breakdown.MaleficAvoidanceScore >= 90.0)
        {
            strengths.Add("No malefic interference");
        }

        // Ensure at least one strength
        if (strengths.Count == 0)
        {
            strengths.Add("Acceptable conditions for the election");
        }

        return strengths.AsReadOnly();
    }

    /// <summary>
    /// Generates 1–3 human-readable compromise strings based on low-scoring dimensions.
    /// </summary>
    /// <param name="breakdown">Score breakdown for this candidate.</param>
    /// <param name="moon">Moon condition at this time.</param>
    /// <param name="chart">Full chart result.</param>
    /// <returns>List of compromise descriptions.</returns>
    private static IReadOnlyList<string> GenerateCompromises(
        ScoreBreakdown breakdown,
        MoonCondition moon,
        ChartResult chart)
    {
        List<string> compromises = [];

        if (breakdown.MoonScore < 50.0)
        {
            string condition = moon.IsVoidOfCourse ? "void-of-course" :
                               moon.IsInViaCombusta ? "in Via Combusta" :
                               $"in {moon.Sign}";
            compromises.Add($"Moon {condition} is not ideal");
        }

        if (breakdown.MercuryScore < 50.0)
        {
            PlanetPosition? mercury = chart.Positions
                .FirstOrDefault(p => p.Name == "Mercury");
            string condition = mercury?.IsRetrograde == true ? "retrograde" : "weakly placed";
            compromises.Add($"Mercury {condition}");
        }

        if (breakdown.MaleficAvoidanceScore < 60.0)
        {
            compromises.Add("Mars/Saturn angular \u2014 potential friction");
        }

        if (breakdown.NatalHarmonyScore < 40.0)
        {
            compromises.Add("Limited natal chart harmony");
        }

        return compromises.AsReadOnly();
    }

    // JulianDayToLocalDateTime removed — replaced by JulianDayUtility.ToLocalDateTime

    #endregion
}
