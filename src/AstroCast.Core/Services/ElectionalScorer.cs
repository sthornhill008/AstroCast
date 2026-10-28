using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;

namespace AstroCast.Core.Services;

/// <summary>
/// Scores a candidate electional chart across seven dimensions, producing a
/// weighted <see cref="ScoreBreakdown"/>. Each dimension is scored 0–100 and
/// the overall score is a weighted sum using the weight profile for the
/// specified <see cref="ElectionType"/>.
/// </summary>
/// <remarks>
/// <para>
/// The seven scoring dimensions are:
/// </para>
/// <list type="number">
///   <item><description>Moon — phase, void-of-course, Via Combusta, dignity, applying aspects</description></item>
///   <item><description>Ascendant — rising sign appropriateness for the election type</description></item>
///   <item><description>Significator — dignity and condition of the election's ruling planet</description></item>
///   <item><description>Natal harmony — aspects between election and natal chart (if provided)</description></item>
///   <item><description>Malefic avoidance — Mars and Saturn not within 5° of the chart angles (shared <see cref="AngularityService"/> routine)</description></item>
///   <item><description>Mercury — retrograde, combust, and dignity checks</description></item>
///   <item><description>Planetary hour — hour ruler alignment with election significator</description></item>
/// </list>
/// <para>
/// The scorer consumes pre-computed data (<see cref="ChartResult"/>,
/// <see cref="MoonCondition"/>, <see cref="PlanetaryHour"/>) and does not
/// access the ephemeris directly.
/// </para>
/// </remarks>
public class ElectionalScorer
{
    #region Traditional Rulership Tables

    /// <summary>
    /// Traditional domicile rulers: planet name → signs where it has domicile dignity.
    /// Uses traditional rulerships (Mars for Scorpio, Saturn for Aquarius, Jupiter for Pisces).
    /// </summary>
    private static readonly Dictionary<string, ZodiacSign[]> DomicileRulers = new()
    {
        ["Sun"]     = [ZodiacSign.Leo],
        ["Moon"]    = [ZodiacSign.Cancer],
        ["Mercury"] = [ZodiacSign.Gemini, ZodiacSign.Virgo],
        ["Venus"]   = [ZodiacSign.Taurus, ZodiacSign.Libra],
        ["Mars"]    = [ZodiacSign.Aries, ZodiacSign.Scorpio],
        ["Jupiter"] = [ZodiacSign.Sagittarius, ZodiacSign.Pisces],
        ["Saturn"]  = [ZodiacSign.Capricorn, ZodiacSign.Aquarius],
    };

    /// <summary>
    /// Exaltation: planet name → sign where it is exalted.
    /// </summary>
    private static readonly Dictionary<string, ZodiacSign> ExaltationSigns = new()
    {
        ["Sun"]     = ZodiacSign.Aries,
        ["Moon"]    = ZodiacSign.Taurus,
        ["Mercury"] = ZodiacSign.Virgo,
        ["Venus"]   = ZodiacSign.Pisces,
        ["Mars"]    = ZodiacSign.Capricorn,
        ["Jupiter"] = ZodiacSign.Cancer,
        ["Saturn"]  = ZodiacSign.Libra,
    };

    /// <summary>
    /// Detriment: planet name → signs where it is in detriment (opposite domicile).
    /// </summary>
    private static readonly Dictionary<string, ZodiacSign[]> DetrimentSigns = new()
    {
        ["Sun"]     = [ZodiacSign.Aquarius],
        ["Moon"]    = [ZodiacSign.Capricorn],
        ["Mercury"] = [ZodiacSign.Sagittarius, ZodiacSign.Pisces],
        ["Venus"]   = [ZodiacSign.Aries, ZodiacSign.Scorpio],
        ["Mars"]    = [ZodiacSign.Taurus, ZodiacSign.Libra],
        ["Jupiter"] = [ZodiacSign.Gemini, ZodiacSign.Virgo],
        ["Saturn"]  = [ZodiacSign.Cancer, ZodiacSign.Leo],
    };

    /// <summary>
    /// Fall: planet name → sign where it is in fall (opposite exaltation).
    /// </summary>
    private static readonly Dictionary<string, ZodiacSign> FallSigns = new()
    {
        ["Sun"]     = ZodiacSign.Libra,
        ["Moon"]    = ZodiacSign.Scorpio,
        ["Mercury"] = ZodiacSign.Pisces,
        ["Venus"]   = ZodiacSign.Virgo,
        ["Mars"]    = ZodiacSign.Cancer,
        ["Jupiter"] = ZodiacSign.Capricorn,
        ["Saturn"]  = ZodiacSign.Aries,
    };

    #endregion

    #region Significator Lookup

    /// <summary>
    /// Maps each election type to its natural significator planet name.
    /// </summary>
    private static readonly Dictionary<ElectionType, string> Significators = new()
    {
        [ElectionType.BusinessLaunch]    = "Jupiter",
        [ElectionType.Contract]          = "Mercury",
        [ElectionType.Wedding]           = "Venus",
        [ElectionType.Surgery]           = "Mars",
        [ElectionType.RealEstateListing] = "Venus",
        [ElectionType.RealEstateClosing] = "Saturn",
        [ElectionType.CreativeLaunch]    = "Sun",
        [ElectionType.Travel]            = "Mercury",
        [ElectionType.Other]             = "Moon",
    };

    #endregion

    #region Preferred Rising Signs

    /// <summary>
    /// Maps each election type to its preferred rising signs.
    /// A preferred sign scores higher; other signs score neutrally.
    /// </summary>
    private static readonly Dictionary<ElectionType, ZodiacSign[]> PreferredRisingSigns = new()
    {
        [ElectionType.BusinessLaunch]    = [ZodiacSign.Leo, ZodiacSign.Sagittarius, ZodiacSign.Aries],
        [ElectionType.Contract]          = [ZodiacSign.Gemini, ZodiacSign.Virgo, ZodiacSign.Libra],
        [ElectionType.Wedding]           = [ZodiacSign.Taurus, ZodiacSign.Libra, ZodiacSign.Cancer, ZodiacSign.Pisces],
        [ElectionType.Surgery]           = [ZodiacSign.Scorpio, ZodiacSign.Aries, ZodiacSign.Capricorn],
        [ElectionType.RealEstateListing] = [ZodiacSign.Taurus, ZodiacSign.Cancer, ZodiacSign.Leo],
        [ElectionType.RealEstateClosing] = [ZodiacSign.Capricorn, ZodiacSign.Taurus, ZodiacSign.Virgo],
        [ElectionType.CreativeLaunch]    = [ZodiacSign.Leo, ZodiacSign.Pisces, ZodiacSign.Libra],
        [ElectionType.Travel]            = [ZodiacSign.Sagittarius, ZodiacSign.Gemini, ZodiacSign.Aquarius],
        [ElectionType.Other]             = [],
    };

    /// <summary>
    /// Election types where a waxing Moon is preferred (new beginnings).
    /// </summary>
    private static readonly HashSet<ElectionType> WaxingPreferred =
    [
        ElectionType.BusinessLaunch,
        ElectionType.Wedding,
        ElectionType.CreativeLaunch,
        ElectionType.Travel,
        ElectionType.RealEstateListing,
    ];

    #endregion

    #region Angular Houses

    /// <summary>Angular house numbers (1st, 4th, 7th, 10th).</summary>
    private static readonly HashSet<int> AngularHouses = [1, 4, 7, 10];

    /// <summary>Cadent house numbers (3rd, 6th, 9th, 12th).</summary>
    private static readonly HashSet<int> CadentHouses = [3, 6, 9, 12];

    /// <summary>Combustion threshold in degrees from the Sun.</summary>
    private const double CombustionOrb = 8.0;

    #endregion

    /// <summary>
    /// Scores a candidate election chart across all seven dimensions.
    /// </summary>
    /// <param name="chart">The calculated chart for the candidate election time.</param>
    /// <param name="moonCondition">Moon condition snapshot at the election time.</param>
    /// <param name="planetaryHour">Planetary hour at the election time.</param>
    /// <param name="electionType">The category of election being scored.</param>
    /// <param name="natalChart">
    /// Optional natal chart for the person initiating the election.
    /// If null, the natal harmony dimension scores a neutral 50.
    /// </param>
    /// <returns>A <see cref="ScoreBreakdown"/> with all dimension scores and the weighted overall.</returns>
    public ScoreBreakdown Score(
        ChartResult chart,
        MoonCondition moonCondition,
        PlanetaryHour planetaryHour,
        ElectionType electionType,
        ChartResult? natalChart = null)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(moonCondition);
        ArgumentNullException.ThrowIfNull(planetaryHour);

        ScoreWeights weights = ElectionalWeights.GetWeights(electionType);

        double moonScore = ScoreMoon(moonCondition, electionType);
        double ascendantScore = ScoreAscendant(chart, electionType);
        double significatorScore = ScoreSignificator(chart, electionType);
        double natalHarmonyScore = ScoreNatalHarmony(chart, natalChart, electionType);
        double maleficAvoidanceScore = ScoreMaleficAvoidance(chart);
        double mercuryScore = ScoreMercury(chart);
        double planetaryHourScore = ScorePlanetaryHour(planetaryHour, electionType);

        return new ScoreBreakdown
        {
            MoonScore = moonScore,
            AscendantScore = ascendantScore,
            SignificatorScore = significatorScore,
            NatalHarmonyScore = natalHarmonyScore,
            MaleficAvoidanceScore = maleficAvoidanceScore,
            MercuryScore = mercuryScore,
            PlanetaryHourScore = planetaryHourScore,
            Weights = weights,
            ElectionTypeName = electionType.ToString(),
        };
    }

    #region Score Ceiling

    /// <summary>
    /// The Moon's highest score, 100: 50, +15 for a waxing Moon where the type prefers one, +25 in Cancer and +10 for a
    /// trine or sextile applying to Jupiter or Venus; where the type prefers a waning Moon, 50, +10 waning, +5 for a
    /// Full Moon, +25 in Cancer and +10 for such an aspect. The score is held to 100.
    /// </summary>
    private const double HighestMoonScore = 100.0;

    /// <summary>The Ascendant's highest score: a rising sign among the type's preferred signs, 85.</summary>
    private const double HighestAscendantScore = 85.0;

    /// <summary>
    /// The Ascendant's highest score for a type with no preferred rising signs (Other): every rising sign scores 50.
    /// </summary>
    private const double HighestAscendantScoreWithoutPreferredSigns = 50.0;

    /// <summary>
    /// The significator's highest score: 50, +25 in its domicile and +15 in an angular house, direct and not combust:
    /// 90.
    /// </summary>
    private const double HighestSignificatorScore = 90.0;

    /// <summary>
    /// The natal harmony's highest score: 50, +10 for a trine or sextile within 5° to natal Jupiter and +10 to natal
    /// Venus, with no aspect to natal Mars or Saturn: 70.
    /// </summary>
    private const double HighestNatalHarmonyScore = 70.0;

    /// <summary>The malefic avoidance's highest score: 100, neither Mars nor Saturn within 5° of an angle.</summary>
    private const double HighestMaleficAvoidanceScore = 100.0;

    /// <summary>
    /// Mercury's highest score: 80, +25 in Gemini or Virgo, direct and not combust: 105, held to 100.
    /// </summary>
    private const double HighestMercuryScore = 100.0;

    /// <summary>The planetary hour's highest score: the hour of the type's significator, 100.</summary>
    private const double HighestPlanetaryHourScore = 100.0;

    /// <summary>
    /// Returns the ceiling of the overall score for an election type: each of the seven dimensions at its highest
    /// score, weighted by the type's profile and summed.
    /// </summary>
    /// <param name="electionType">The election type.</param>
    /// <returns>
    /// The ceiling: BusinessLaunch 93.5, Contract 94, Wedding 93, Surgery 95.5, RealEstateListing 93.5,
    /// RealEstateClosing 93.5, CreativeLaunch 92.75, Travel 94, Other 90.5.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The ceiling is below 100 because three dimensions stop short of it: the Ascendant at 85 (50 for a type with no
    /// preferred rising signs), the significator at 90 and the natal harmony at 70. It is summed in the order of
    /// <see cref="ScoreBreakdown.OverallScore"/>, so a sample with every dimension at its highest would score exactly
    /// this. The electional export prints it in its header (branch Q, 1.3.3).
    /// </para>
    /// <para>
    /// No moment need reach it, and for five types none can, since the dimensions share bodies: for Contract, Surgery,
    /// RealEstateClosing and Other the Moon's highest score needs a Full Moon in Cancer, with the Sun in Sagittarius or
    /// Capricorn, where Mercury cannot be in Gemini or Virgo; for CreativeLaunch the Sun's best sign (Leo) and the
    /// Moon's (Cancer, waxing) exclude each other.
    /// </para>
    /// </remarks>
    internal static double ScoreCeiling(ElectionType electionType)
    {
        ScoreWeights weights = ElectionalWeights.GetWeights(electionType);

        double highestAscendantScore =
            PreferredRisingSigns.TryGetValue(electionType, out ZodiacSign[]? preferred) && preferred.Length > 0
                ? HighestAscendantScore
                : HighestAscendantScoreWithoutPreferredSigns;

        return HighestMoonScore * weights.Moon +
            highestAscendantScore * weights.Ascendant +
            HighestSignificatorScore * weights.Significator +
            HighestNatalHarmonyScore * weights.NatalHarmony +
            HighestMaleficAvoidanceScore * weights.MaleficAvoidance +
            HighestMercuryScore * weights.Mercury +
            HighestPlanetaryHourScore * weights.PlanetaryHour;
    }

    #endregion

    #region Dimension 1: Moon

    /// <summary>
    /// Scores the Moon's condition for electional purposes.
    /// </summary>
    /// <param name="moon">Moon condition snapshot.</param>
    /// <param name="electionType">Election type (determines waxing preference).</param>
    /// <returns>Score from 0 to 100.</returns>
    internal static double ScoreMoon(MoonCondition moon, ElectionType electionType)
    {
        double score = 50.0;

        // Phase scoring: waxing phases preferred for new beginnings
        bool isWaxing = moon.Phase is MoonPhaseType.NewMoon
            or MoonPhaseType.WaxingCrescent
            or MoonPhaseType.FirstQuarter
            or MoonPhaseType.WaxingGibbous;

        if (isWaxing && WaxingPreferred.Contains(electionType))
        {
            score += 15.0;
        }
        else if (!isWaxing && !WaxingPreferred.Contains(electionType))
        {
            score += 10.0; // Waning acceptable for completion-type elections
        }
        else if (!isWaxing && WaxingPreferred.Contains(electionType))
        {
            score -= 10.0; // Waning for new beginnings is less ideal
        }

        // Full Moon phase gets a small bonus for any type (peak energy)
        if (moon.Phase == MoonPhaseType.FullMoon)
        {
            score += 5.0;
        }

        // Void-of-course: major penalty
        if (moon.IsVoidOfCourse)
        {
            score -= 40.0;
        }

        // Via Combusta: penalty
        if (moon.IsInViaCombusta)
        {
            score -= 20.0;
        }

        // Moon sign dignity
        ZodiacSign moonSign = ParseSign(moon.Sign);
        score += GetDignityBonus("Moon", moonSign);

        // Applying aspect quality (parse from description string)
        if (moon.NextApplyingAspectDescription is not null)
        {
            score += ScoreApplyingAspect(moon.NextApplyingAspectDescription);
        }

        return Math.Clamp(score, 0.0, 100.0);
    }

    /// <summary>
    /// Scores the quality of the Moon's next applying aspect from its description string.
    /// </summary>
    /// <param name="description">
    /// Format: "Moon {aspectName} {planetName} (applying, {orb})".
    /// </param>
    /// <returns>Positive bonus for benefic aspects, negative for malefic.</returns>
    private static double ScoreApplyingAspect(string description)
    {
        bool isBeneficAspect = description.Contains("trine", StringComparison.OrdinalIgnoreCase)
                            || description.Contains("sextile", StringComparison.OrdinalIgnoreCase);

        bool isMaleficAspect = description.Contains("square", StringComparison.OrdinalIgnoreCase)
                            || description.Contains("opposition", StringComparison.OrdinalIgnoreCase);

        bool toBenefic = description.Contains("Jupiter", StringComparison.OrdinalIgnoreCase)
                      || description.Contains("Venus", StringComparison.OrdinalIgnoreCase);

        bool toMalefic = description.Contains("Mars", StringComparison.OrdinalIgnoreCase)
                      || description.Contains("Saturn", StringComparison.OrdinalIgnoreCase);

        double bonus = 0.0;

        if (isBeneficAspect && toBenefic)
        {
            bonus += 10.0;
        }
        else if (isBeneficAspect)
        {
            bonus += 5.0;
        }

        if (isMaleficAspect && toMalefic)
        {
            bonus -= 10.0;
        }
        else if (isMaleficAspect)
        {
            bonus -= 5.0;
        }

        return bonus;
    }

    #endregion

    #region Dimension 2: Ascendant

    /// <summary>
    /// Scores the rising sign's appropriateness for the election type.
    /// </summary>
    /// <param name="chart">The election chart containing house data.</param>
    /// <param name="electionType">The election type being scored.</param>
    /// <returns>Score from 0 to 100.</returns>
    internal static double ScoreAscendant(ChartResult chart, ElectionType electionType)
    {
        ZodiacSign risingSign = (ZodiacSign)((int)(chart.Houses.Ascendant % 360.0 / 30.0) + 1);

        if (!PreferredRisingSigns.TryGetValue(electionType, out ZodiacSign[]? preferred)
            || preferred.Length == 0)
        {
            return 50.0; // Neutral for "Other" type
        }

        if (Array.IndexOf(preferred, risingSign) >= 0)
        {
            return 85.0; // Preferred sign
        }

        return 45.0; // Non-preferred sign
    }

    #endregion

    #region Dimension 3: Significator

    /// <summary>
    /// Scores the significator planet's dignity and condition.
    /// </summary>
    /// <param name="chart">The election chart.</param>
    /// <param name="electionType">The election type (determines which planet is the significator).</param>
    /// <returns>Score from 0 to 100.</returns>
    internal static double ScoreSignificator(ChartResult chart, ElectionType electionType)
    {
        string significatorName = Significators[electionType];
        PlanetPosition? significator = FindPlanet(chart, significatorName);

        if (significator is null)
        {
            return 50.0; // Neutral if planet not found
        }

        double score = 50.0;

        // Dignity
        score += GetDignityBonus(significatorName, significator.Sign);

        // Retrograde penalty
        if (significator.IsRetrograde)
        {
            score -= 30.0;
        }

        // Combustion check (within 8° of Sun)
        if (significatorName != "Sun")
        {
            PlanetPosition? sun = FindPlanet(chart, "Sun");
            if (sun is not null)
            {
                double separation = AspectService.AngularSeparation(
                    significator.Longitude, sun.Longitude);
                if (separation < CombustionOrb)
                {
                    score -= 15.0;
                }
            }
        }

        // House placement
        if (significator.House.HasValue)
        {
            if (AngularHouses.Contains(significator.House.Value))
            {
                score += 15.0;
            }
            else if (CadentHouses.Contains(significator.House.Value))
            {
                score -= 10.0;
            }
        }

        return Math.Clamp(score, 0.0, 100.0);
    }

    #endregion

    #region Dimension 4: Natal Harmony

    /// <summary>
    /// Scores the harmony between the election chart and a natal chart.
    /// Returns a neutral 50 if no natal chart is provided.
    /// </summary>
    /// <param name="chart">The election chart.</param>
    /// <param name="natalChart">Optional natal chart.</param>
    /// <param name="electionType">Election type (determines significator).</param>
    /// <returns>Score from 0 to 100.</returns>
    internal static double ScoreNatalHarmony(
        ChartResult chart, ChartResult? natalChart, ElectionType electionType)
    {
        if (natalChart is null)
        {
            return 50.0;
        }

        string significatorName = Significators[electionType];
        PlanetPosition? electionSignificator = FindPlanet(chart, significatorName);

        if (electionSignificator is null)
        {
            return 50.0;
        }

        double score = 50.0;

        // Check aspects between election significator and natal benefics/malefics
        foreach (PlanetPosition natalPlanet in natalChart.Positions)
        {
            double separation = AspectService.AngularSeparation(
                electionSignificator.Longitude, natalPlanet.Longitude);

            // Check for trine (120°) or sextile (60°) to natal benefics
            bool isBeneficTarget = natalPlanet.Name is "Jupiter" or "Venus";
            bool isMaleficTarget = natalPlanet.Name is "Mars" or "Saturn";

            double trineOrb = Math.Abs(separation - 120.0);
            double sextileOrb = Math.Abs(separation - 60.0);
            double squareOrb = Math.Abs(separation - 90.0);
            double oppositionOrb = Math.Abs(separation - 180.0);
            double conjunctionOrb = separation; // conjunction = 0°

            if (isBeneficTarget && (trineOrb <= 5.0 || sextileOrb <= 5.0))
            {
                score += 10.0;
            }
            else if (isBeneficTarget && conjunctionOrb <= 5.0)
            {
                score += 8.0;
            }

            if (isMaleficTarget && (squareOrb <= 5.0 || oppositionOrb <= 5.0))
            {
                score -= 10.0;
            }
            else if (isMaleficTarget && conjunctionOrb <= 5.0)
            {
                score -= 8.0;
            }
        }

        return Math.Clamp(score, 0.0, 100.0);
    }

    #endregion

    #region Dimension 5: Malefic Avoidance

    /// <summary>Penalty applied for each malefic within the angular orb.</summary>
    private const double AngularMaleficBasePenalty = 30.0;

    /// <summary>Additional penalty when the angular malefic is the out-of-sect malefic.</summary>
    private const double OutOfSectMaleficPenalty = 20.0;

    /// <summary>Additional penalty when the angular malefic is essentially debilitated.</summary>
    private const double DebilitatedMaleficPenalty = 10.0;

    /// <summary>Additional penalty when the angular malefic is the chart ruler.</summary>
    private const double ChartRulerMaleficPenalty = 10.0;

    /// <summary>Orb (degrees) up to which the full angular-malefic penalty applies.</summary>
    private const double FullPenaltyOrb = 3.0;

    /// <summary>Penalty multiplier for orbs beyond <see cref="FullPenaltyOrb"/> (up to 5°).</summary>
    private const double WideOrbPenaltyFactor = 0.6;

    /// <summary>Essential dignity score at or below which a malefic counts as debilitated (fall or detriment).</summary>
    private const int DebilitatedDignityThreshold = -4;

    /// <summary>Essential dignity score at or above which a malefic counts as well dignified (triplicity or better).</summary>
    private const int WellDignifiedThreshold = 3;

    /// <summary>The two traditional malefics evaluated by this dimension.</summary>
    private static readonly string[] MaleficNames = ["Mars", "Saturn"];

    /// <summary>
    /// Scores how well the chart avoids Mars and Saturn on the chart angles.
    /// Angularity is determined by ecliptic-longitude proximity to the
    /// ASC, MC, DSC, or IC via <see cref="AngularityService"/> — the same
    /// routine and 5° orb that populate the export's Angular Planets table —
    /// never by Placidus house number.
    /// </summary>
    /// <remarks>
    /// <para>Penalty model (2026-06-10 live-run review, Defect A):</para>
    /// <list type="bullet">
    ///   <item><description>Start at 100. For each malefic within 5° of any angle:</description></item>
    ///   <item><description>Base angular-malefic penalty: 30.</description></item>
    ///   <item><description>+20 when that malefic is the out-of-sect malefic
    ///   (Mars in a day chart, Saturn in a night chart).</description></item>
    ///   <item><description>+10 when its essential dignity score is <= -4 (fall or detriment).</description></item>
    ///   <item><description>+10 when it is the chart ruler.</description></item>
    ///   <item><description>Orb scaling: full penalty at orb <= 3°; 60% of the
    ///   penalty for orb &gt; 3° and <= 5°.</description></item>
    ///   <item><description>Classical exception: an angular malefic with essential
    ///   dignity >= +3 (triplicity or better) has its total penalty halved,
    ///   supporting Mars elections such as surgery.</description></item>
    ///   <item><description>Result clamped to [0, 100].</description></item>
    /// </list>
    /// <para>
    /// Sect, dignity, and chart-ruler data are read from the chart's computed
    /// context (<see cref="ChartResult.ChartContext"/>, <see cref="ChartResult.Dignities"/>)
    /// when available, with table-based fallbacks for synthetic fixtures.
    /// </para>
    /// </remarks>
    /// <param name="chart">The election chart.</param>
    /// <returns>Score from 0 to 100.</returns>
    internal static double ScoreMaleficAvoidance(ChartResult chart)
    {
        double score = 100.0;

        IReadOnlyList<AngularPlanet> angularPlanets =
            AngularityService.FindAngularPlanets(chart.Positions, chart.Houses);

        string outOfSectMalefic = IsDayChart(chart) ? "Mars" : "Saturn";
        string chartRuler = GetChartRuler(chart);

        foreach (string malefic in MaleficNames)
        {
            AngularPlanet? tightest = angularPlanets
                .Where(a => string.Equals(a.PlanetName, malefic, StringComparison.OrdinalIgnoreCase))
                .OrderBy(a => a.Orb)
                .FirstOrDefault();

            if (tightest is null)
            {
                continue;
            }

            double penalty = AngularMaleficBasePenalty;

            if (string.Equals(malefic, outOfSectMalefic, StringComparison.OrdinalIgnoreCase))
            {
                penalty += OutOfSectMaleficPenalty;
            }

            int dignityScore = GetEssentialDignityScore(chart, malefic);
            if (dignityScore <= DebilitatedDignityThreshold)
            {
                penalty += DebilitatedMaleficPenalty;
            }

            if (string.Equals(malefic, chartRuler, StringComparison.OrdinalIgnoreCase))
            {
                penalty += ChartRulerMaleficPenalty;
            }

            // Orb scaling: full penalty within 3°, reduced beyond
            if (tightest.Orb > FullPenaltyOrb)
            {
                penalty *= WideOrbPenaltyFactor;
            }

            // Well-dignified-malefic exception (triplicity or better halves the penalty)
            if (dignityScore >= WellDignifiedThreshold)
            {
                penalty /= 2.0;
            }

            score -= penalty;
        }

        return Math.Clamp(score, 0.0, 100.0);
    }

    /// <summary>
    /// Determines chart sect: a day chart when the Sun's longitude lies on the
    /// half of the ecliptic above the horizon (<see cref="ChartSect.IsDayChart"/>).
    /// Prefers the pre-computed <see cref="ChartResult.ChartContext"/> sect;
    /// falls back to the rule on the chart's houses for charts without context
    /// (synthetic fixtures).
    /// </summary>
    /// <param name="chart">The election chart.</param>
    /// <returns>True for a day chart, false for a night chart.</returns>
    private static bool IsDayChart(ChartResult chart)
    {
        if (chart.ChartContext is not null)
        {
            return chart.ChartContext.IsDayChart;
        }

        PlanetPosition? sun = FindPlanet(chart, "Sun");
        return sun is not null && ChartSect.IsDayChart(sun.Longitude, chart.Houses);
    }

    /// <summary>
    /// Resolves the chart ruler (traditional ruler of the Ascendant sign).
    /// Prefers the pre-computed <see cref="ChartResult.ChartContext"/> ruler;
    /// falls back to the traditional rulership table for charts without
    /// context (synthetic fixtures).
    /// </summary>
    /// <param name="chart">The election chart.</param>
    /// <returns>The chart ruler planet name.</returns>
    private static string GetChartRuler(ChartResult chart)
    {
        if (chart.ChartContext is not null)
        {
            return chart.ChartContext.ChartRuler;
        }

        ZodiacSign ascSign = (ZodiacSign)((int)(chart.Houses.Ascendant % 360.0 / 30.0) + 1);

        foreach (KeyValuePair<string, ZodiacSign[]> ruler in DomicileRulers)
        {
            if (Array.IndexOf(ruler.Value, ascSign) >= 0)
            {
                return ruler.Key;
            }
        }

        return "Unknown";
    }

    /// <summary>
    /// Reads a planet's essential dignity score on the traditional Lilly point
    /// scale (domicile +5, exaltation +4, triplicity +3, decan +1,
    /// detriment -5, fall -4, peregrine 0). Prefers the chart's computed
    /// <see cref="ChartResult.Dignities"/>; falls back to the scorer's own
    /// sign tables (major dignities only — no triplicity or decan) for charts
    /// without computed dignities (synthetic fixtures).
    /// </summary>
    /// <param name="chart">The election chart.</param>
    /// <param name="planetName">The planet to evaluate.</param>
    /// <returns>The essential dignity score.</returns>
    private static int GetEssentialDignityScore(ChartResult chart, string planetName)
    {
        DignityInfo? dignity = chart.Dignities.FirstOrDefault(d =>
            string.Equals(d.PlanetName, planetName, StringComparison.OrdinalIgnoreCase));

        if (dignity is not null)
        {
            return dignity.Score;
        }

        PlanetPosition? planet = FindPlanet(chart, planetName);
        if (planet is null)
        {
            return 0;
        }

        if (DomicileRulers.TryGetValue(planetName, out ZodiacSign[]? domicileSigns)
            && Array.IndexOf(domicileSigns, planet.Sign) >= 0)
        {
            return 5;
        }

        if (ExaltationSigns.TryGetValue(planetName, out ZodiacSign exaltation)
            && planet.Sign == exaltation)
        {
            return 4;
        }

        if (DetrimentSigns.TryGetValue(planetName, out ZodiacSign[]? detrimentSignList)
            && Array.IndexOf(detrimentSignList, planet.Sign) >= 0)
        {
            return -5;
        }

        if (FallSigns.TryGetValue(planetName, out ZodiacSign fallSign)
            && planet.Sign == fallSign)
        {
            return -4;
        }

        return 0;
    }

    #endregion

    #region Dimension 6: Mercury

    /// <summary>
    /// Scores Mercury's condition (retrograde, combust, dignity).
    /// </summary>
    /// <param name="chart">The election chart.</param>
    /// <returns>Score from 0 to 100.</returns>
    internal static double ScoreMercury(ChartResult chart)
    {
        PlanetPosition? mercury = FindPlanet(chart, "Mercury");

        if (mercury is null)
        {
            return 50.0;
        }

        double score = 80.0;

        // Retrograde: major penalty
        if (mercury.IsRetrograde)
        {
            score -= 50.0;
        }

        // Combustion check
        PlanetPosition? sun = FindPlanet(chart, "Sun");
        if (sun is not null)
        {
            double separation = AspectService.AngularSeparation(
                mercury.Longitude, sun.Longitude);
            if (separation < CombustionOrb)
            {
                score -= 15.0;
            }
        }

        // Dignity
        score += GetDignityBonus("Mercury", mercury.Sign);

        return Math.Clamp(score, 0.0, 100.0);
    }

    #endregion

    #region Dimension 7: Planetary Hour

    /// <summary>
    /// Scores how well the planetary hour ruler aligns with the election type.
    /// </summary>
    /// <param name="hour">The planetary hour at the election time.</param>
    /// <param name="electionType">The election type being scored.</param>
    /// <returns>Score from 0 to 100.</returns>
    internal static double ScorePlanetaryHour(PlanetaryHour hour, ElectionType electionType)
    {
        string significator = Significators[electionType];

        // Hour ruler matches significator: best possible
        if (string.Equals(hour.RulingPlanet, significator, StringComparison.OrdinalIgnoreCase))
        {
            return 100.0;
        }

        // Hour ruler is a natural benefic (Jupiter, Venus)
        if (hour.RulingPlanet is "Jupiter" or "Venus")
        {
            return 75.0;
        }

        // Hour ruler is a luminary (Sun, Moon) or neutral (Mercury)
        if (hour.RulingPlanet is "Sun" or "Moon" or "Mercury")
        {
            return 50.0;
        }

        // Hour ruler is a natural malefic (Mars, Saturn)
        return 25.0;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Calculates the dignity bonus/penalty for a planet in a given sign.
    /// </summary>
    /// <param name="planetName">The planet name.</param>
    /// <param name="sign">The zodiac sign the planet occupies.</param>
    /// <returns>
    /// +25 for domicile, +20 for exaltation, -20 for detriment, -25 for fall, 0 for peregrine.
    /// </returns>
    internal static double GetDignityBonus(string planetName, ZodiacSign sign)
    {
        // Check domicile first (strongest dignity)
        if (DomicileRulers.TryGetValue(planetName, out ZodiacSign[]? domicileSigns)
            && Array.IndexOf(domicileSigns, sign) >= 0)
        {
            return 25.0;
        }

        // Check exaltation
        if (ExaltationSigns.TryGetValue(planetName, out ZodiacSign exaltation)
            && sign == exaltation)
        {
            return 20.0;
        }

        // Check detriment
        if (DetrimentSigns.TryGetValue(planetName, out ZodiacSign[]? detrimentSignList)
            && Array.IndexOf(detrimentSignList, sign) >= 0)
        {
            return -20.0;
        }

        // Check fall
        if (FallSigns.TryGetValue(planetName, out ZodiacSign fallSign)
            && sign == fallSign)
        {
            return -25.0;
        }

        // Peregrine (no essential dignity)
        return 0.0;
    }

    /// <summary>
    /// Finds a planet by name in the chart's position list.
    /// </summary>
    /// <param name="chart">The chart result to search.</param>
    /// <param name="name">The planet name (e.g., "Mercury", "Mars").</param>
    /// <returns>The matching <see cref="PlanetPosition"/>, or null.</returns>
    private static PlanetPosition? FindPlanet(ChartResult chart, string name)
    {
        foreach (PlanetPosition position in chart.Positions)
        {
            if (string.Equals(position.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return position;
            }
        }

        return null;
    }

    /// <summary>
    /// Parses a zodiac sign name string to its <see cref="ZodiacSign"/> enum value.
    /// </summary>
    /// <param name="signName">Sign name (e.g., "Aries", "Cancer").</param>
    /// <returns>The parsed sign, defaulting to Aries if parsing fails.</returns>
    private static ZodiacSign ParseSign(string signName)
    {
        if (Enum.TryParse<ZodiacSign>(signName, ignoreCase: true, out ZodiacSign result))
        {
            return result;
        }

        System.Diagnostics.Debug.WriteLine(
            $"ElectionalScorer.ParseSign: unrecognised sign '{signName}', defaulting to Aries");
        return ZodiacSign.Aries;
    }

    #endregion
}
