using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.MedicalAstrology;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Analyses horary chart perfection: determines whether the querent's
/// question will be answered affirmatively based on the relationship
/// between the querent's significator (L1) and quesited's significator (Lx).
/// </summary>
/// <remarks>
/// <para>
/// Consumes an assembled <see cref="ChartResult"/>, a question category, the
/// chart's Julian Day and the ephemeris. <c>ChartEngine</c> runs it for every
/// horary chart with a category and stores the result on
/// <see cref="ChartResult.HoraryPerfection"/>; the formatters only render it.
/// </para>
/// <para>
/// <b>Significators.</b> L1 is the Ascendant's ruler and Lx the quesited cusp's. When one
/// planet rules both, the Moon signifies the querent; when that planet is the Moon (the
/// Ascendant and the quesited cusp both in Cancer), the Moon signifies the querent and
/// Jupiter, exalted in Cancer, the quesited, and the notes say so (QI-1, ruling Q15).
/// </para>
/// <para>
/// <b>H-7 — Perfection rule precedence.</b> The classical horary literature
/// (Lilly, Bonatti, Al-Biruni) recognises several distinct <i>mechanisms of
/// perfection</i> by which a question can be "yes." These mechanisms are not
/// cumulative: the tradition recognises a primary mechanism and treats the
/// others as fallback pathways when the primary is absent. This implementation
/// encodes the following strict precedence, and the <i>first</i> match wins
/// (short-circuit evaluation inside <see cref="Analyze"/>):
/// </para>
/// <list type="number">
///   <item><b>Direct Aspect</b> — an applying major aspect (conjunction,
///     sextile, square, trine, opposition) between L1 and Lx <i>that actually
///     reaches exact</i>: the application is followed in the ephemeris by
///     <see cref="AspectPerfectionFinder.FollowFirstApproach"/>, and only an
///     application whose orb shrinks to zero perfects. One that turns around
///     first does not, and the mechanisms below are tried instead. The
///     application must also complete before either significator leaves the
///     sign it occupies at the chart, unless L1 and Lx receive each other by
///     domicile or exaltation in either direction; one that completes only out
///     of sign is noted, naming the sign changes, and the mechanisms below are
///     tried instead. This is the strongest and most unambiguous testimony
///     (Lilly, CA III, ch. 26).</item>
///   <item><b>Mutual Reception</b> — L1 and Lx each occupy a sign of the
///     other's domicile or exaltation, or one of each. A reception by
///     triplicity, alone or mixed, does not perfect. Treated here as
///     perfection in its own right when no direct aspect is present, following
///     Lilly's treatment of reception as a substitute mode of connection. Such a
///     reception is looked up once, and whatever the mechanism the Reception
///     line names its type, with each significator's kind of dignity taken from
///     the type (QI-2).</item>
///   <item><b>Translation of Light</b> — a faster intermediate planet
///     separates from one significator and applies to the other, carrying
///     the "light" between them. Only considered when no direct aspect or
///     mutual reception exists.</item>
///   <item><b>Collection of Light</b> — a slower third planet (to which both
///     L1 and Lx apply by major aspect) gathers their testimonies. Weakest
///     of the applying-aspect pathways and therefore evaluated last among
///     the classical mechanisms.</item>
///   <item><b>Antiscia Perfection</b> — an applying symmetry connection
///     (antiscion or contra-antiscion) between L1 and Lx: the sum of the two
///     longitudes, moving with the significators' signed speeds, approaches the
///     exact value (180° for an antiscion, 0° for a contra-antiscion). A
///     separating one is testimony and is noted, not perfection. Included as a
///     last-resort mechanism; non-classical authors such as Morin extend
///     perfection to antiscial links when no other connection obtains.</item>
/// </list>
/// <para>
/// Only the seven traditional planets translate or collect; the outer
/// planets, Chiron and the True Node never do. A retrograde intermediary
/// counts and is named in the notes. A translation or collection counts only
/// if every application it relies on completes — the translator's to the
/// significator it applies to, or both significators' to the collector — each
/// followed by <see cref="AspectPerfectionFinder.FollowFirstApproach"/>. One
/// that does not complete is noted, naming the application that failed, and
/// the mechanisms below it are tried instead, as for a direct aspect that
/// turns around.
/// </para>
/// <para>
/// Once a mechanism is identified, no lower-precedence mechanism is reported,
/// even if one also holds. The precedence above is preserved in the Analyze
/// method's if-chain; do not reorder without revisiting the underlying
/// tradition and the corresponding tests in
/// <c>HoraryPerfectionAnalyzerTests</c>.
/// </para>
/// <para>
/// <b>Denial rules are cumulative, not ranked.</b> Unlike perfection, every
/// applicable denial (Moon VoC, combustion, besiegement, prohibition,
/// refranation) is added to <see cref="HoraryPerfectionResult.DenialReasons"/>.
/// The overall judgment then weighs perfection against the set of denials
/// in <c>DetermineJudgment</c>.
/// </para>
/// <para>
/// <b>Refranation and prohibition are events, not speeds.</b> Refranation is
/// judged from the followed direct application and exists only when one does;
/// prohibition applies to a direct aspect that perfects and to a translation or
/// collection that completes (judged up to its last application, the
/// intermediary itself excepted). Refranation is a station of L1 or Lx, in
/// either direction, between the chart and the point where a failing
/// application turns around
/// (<see cref="StationWindowFinder"/>), or each significator's first station
/// after it when that falls before the linear perfection estimate, the instant
/// the chart's separation would close at the chart's relative speed (a speed
/// bounds that window; the denial is still the station). It is a full denial.
/// A station before an aspect that still perfects is an informational note. Prohibition is an
/// exact major aspect of Mars or Saturn to L1 or Lx strictly between the chart
/// and the perfection or completion (<see cref="AspectPerfectionFinder.FindFirstExactMajorAspect"/>),
/// the earliest such event per malefic; no speed or orb comparison is involved.
/// An application that neither perfects nor turns around within
/// <see cref="AspectPerfectionFinder.MaxFollowDays"/> is reported as unresolved
/// and is neither perfection nor refranation.
/// </para>
/// </remarks>
public static class HoraryPerfectionAnalyzer
{
    #region Exaltation Table

    /// <summary>
    /// Standard exaltation table mapping each planet to the sign of its exaltation.
    /// </summary>
    private static readonly Dictionary<CelestialBodyId, ZodiacSign> s_exaltations = new()
    {
        { CelestialBodyId.Sun, ZodiacSign.Aries },
        { CelestialBodyId.Moon, ZodiacSign.Taurus },
        { CelestialBodyId.Mercury, ZodiacSign.Virgo },
        { CelestialBodyId.Venus, ZodiacSign.Pisces },
        { CelestialBodyId.Mars, ZodiacSign.Capricorn },
        { CelestialBodyId.Jupiter, ZodiacSign.Cancer },
        { CelestialBodyId.Saturn, ZodiacSign.Libra },
    };

    #endregion

    /// <summary>
    /// Perform horary perfection analysis.
    /// </summary>
    /// <remarks>
    /// L1 is the Ascendant's ruler and Lx the quesited cusp's. When one planet rules both,
    /// the Moon signifies the querent; when that planet is the Moon itself (the Ascendant and
    /// the quesited cusp both in Cancer), the Moon signifies the querent and Jupiter, exalted
    /// in Cancer, the quesited, and the notes say so (QI-1, ruling Q15). Until 1.3.3 the Moon
    /// was then both significators.
    /// </remarks>
    /// <param name="chart">The horary chart calculation result.</param>
    /// <param name="category">
    /// The question category (e.g., "Career", "Relationship"). An unrecognised
    /// category takes the 7th house, and the result's notes name it.
    /// </param>
    /// <param name="julianDayUt">The chart instant as a Julian Day in Universal Time.</param>
    /// <param name="ephemeris">Ephemeris used to time perfections and stations after the chart.</param>
    /// <returns>A <see cref="HoraryPerfectionResult"/> with the complete horary judgment.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="chart"/> or <paramref name="ephemeris"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="category"/> is null or blank.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown on an internal invariant breach: the followed application is
    /// reported to perfect without its perfection instant or time, or to turn
    /// around without its turnaround time; an application a translation or
    /// collection relies on has lost its position or aspect when followed, or
    /// turns around without its turnaround time; or a significator or malefic
    /// name does not resolve to a celestial body. None of these can arise from a
    /// valid chart; the exception reports a defect, not a condition to handle.
    /// </exception>
    public static HoraryPerfectionResult Analyze(
        ChartResult chart, string category, double julianDayUt, IEphemerisService ephemeris)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(ephemeris);

        // Step 1 — Map category to quesited house; an unrecognised one takes the 7th
        //          ("the other party" / "the matter"), and the notes say so
        int? mappedHouseNumber = MapCategoryToHouse(category);
        int quesitedHouseNumber = mappedHouseNumber ?? 7;
        List<string> categoryNotes = mappedHouseNumber is null
            ? [$"Category '{category}' is not recognised; the 7th house is assumed."]
            : [];

        // Step 2 — Identify significators
        string l1Name = chart.ChartContext?.ChartRuler ?? GetRulerName(chart.Houses.Ascendant);
        int quesitedHouseIndex = quesitedHouseNumber - 1;
        double cuspLongitude = chart.Houses.Cusps[quesitedHouseIndex];
        ZodiacSign cuspSign = (ZodiacSign)((int)(cuspLongitude % 360.0 / 30.0) + 1);
        CelestialBodyId lxBody = ElementHelper.GetTraditionalRuler(cuspSign);
        string lxName = lxBody.ToString();

        // Special case: L1 == Lx (same planet rules both houses) → Moon becomes L1
        bool sameRuler = l1Name.Equals(lxName, StringComparison.OrdinalIgnoreCase);
        List<string> significatorNotes = [];
        if (sameRuler && lxName.Equals("Moon", StringComparison.OrdinalIgnoreCase))
        {
            // The Moon rules both (the Ascendant and the quesited cusp in Cancer): the Moon
            // signifies the querent and Jupiter, the exaltation ruler of Cancer, the quesited (Q15)
            lxName = CelestialBodyId.Jupiter.ToString();
            significatorNotes.Add(
                $"The Moon rules both the 1st and the {Ordinal(quesitedHouseNumber)} house; the Moon signifies the querent and Jupiter, exalted in Cancer, the quesited.");
        }
        else if (sameRuler)
        {
            l1Name = "Moon";
        }

        // Locate positions for L1 and Lx
        PlanetPosition? l1Pos = FindPosition(chart, l1Name);
        PlanetPosition? lxPos = FindPosition(chart, lxName);

        // Dignity info
        string l1Dignity = GetDignityLabel(chart, l1Name);
        string lxDignity = GetDignityLabel(chart, lxName);

        // The L1–Lx mutual reception that can perfect (domicile, exaltation, or mixed
        // domicile–exaltation), looked up once: it is a mechanism below (3b) and, whatever
        // the mechanism, the Reception line names it (QI-2)
        MutualReception? pairReception = chart.ChartContext?.MutualReceptions?.FirstOrDefault(r =>
            (r.Planet1.Equals(l1Name, StringComparison.OrdinalIgnoreCase) &&
             r.Planet2.Equals(lxName, StringComparison.OrdinalIgnoreCase)) ||
            (r.Planet1.Equals(lxName, StringComparison.OrdinalIgnoreCase) &&
             r.Planet2.Equals(l1Name, StringComparison.OrdinalIgnoreCase)));
        if (pairReception is not null && !IsPerfectingReception(pairReception.ReceptionType))
            pairReception = null;

        // Step 3 — Check perfection mechanisms
        string? perfectionMechanism = null;
        string? perfectionAspect = null;
        double? perfectionOrb = null;
        string? translator = null;
        string? collector = null;

        // 3a. Direct Aspect — counts only if its first approach reaches exact, and does so
        //     before either significator leaves its sign unless they receive each other
        AspectResult? directAspect = FindApplyingMajorAspect(chart, l1Name, lxName);
        DirectApplication? application = null;
        if (directAspect is not null && l1Pos is not null && lxPos is not null)
        {
            application = FollowDirectApplication(
                chart, l1Name, lxName, l1Pos, lxPos, directAspect, julianDayUt, ephemeris);

            if (application.Approach.Perfects && !application.PerfectsOutOfSign)
            {
                perfectionMechanism = "Direct Aspect";
                perfectionAspect = directAspect.Aspect.Type.ToString();
                perfectionOrb = directAspect.Orb;
            }
        }

        // 3b. Mutual Reception — by domicile, exaltation, or mixed domicile–exaltation
        if (perfectionMechanism is null && pairReception is not null)
        {
            perfectionMechanism = "Mutual Reception";
        }

        // 3c. Translation of Light — counts only if the translator's application completes;
        //     one that does not is noted and the mechanisms below are tried instead.
        List<string> mechanismNotes = [];
        IntermediaryOutcome? intermediary = null;
        if (perfectionMechanism is null && l1Pos is not null && lxPos is not null)
        {
            (string? translatorName, string? appliesTo) = FindTranslation(chart, l1Name, lxName, l1Pos, lxPos);
            if (translatorName is not null && appliesTo is not null)
            {
                IntermediaryOutcome outcome = FollowIntermediary(
                    chart, $"translation of light via {translatorName}", translatorName,
                    [(translatorName, appliesTo)], l1Name, lxName, julianDayUt, ephemeris);

                if (outcome.Completes)
                {
                    perfectionMechanism = "Translation of Light";
                    translator = translatorName;
                    intermediary = outcome;
                }
                else if (outcome.FailureNote is not null)
                {
                    mechanismNotes.Add(outcome.FailureNote);
                }
            }
        }

        // 3d. Collection of Light — counts only if both significators' applications to the
        //     collector complete; one that does not is noted and antiscia is tried instead.
        if (perfectionMechanism is null && l1Pos is not null && lxPos is not null)
        {
            string? collectorName = FindCollection(chart, l1Name, lxName, l1Pos, lxPos);
            if (collectorName is not null)
            {
                IntermediaryOutcome outcome = FollowIntermediary(
                    chart, $"collection of light by {collectorName}", collectorName,
                    [(l1Name, collectorName), (lxName, collectorName)], l1Name, lxName, julianDayUt, ephemeris);

                if (outcome.Completes)
                {
                    perfectionMechanism = "Collection of Light";
                    collector = collectorName;
                    intermediary = outcome;
                }
                else if (outcome.FailureNote is not null)
                {
                    mechanismNotes.Add(outcome.FailureNote);
                }
            }
        }

        // A retrograde translator or collector still counts; the notes name it.
        if ((translator ?? collector) is string via && FindPosition(chart, via) is { SpeedInLongitude: < 0.0 })
        {
            mechanismNotes.Add(translator is not null
                ? $"The light is translated by retrograde {via}."
                : $"The light is collected by retrograde {via}.");
        }

        // 3e. Antiscia Perfection — only while applying; a separating one is testimony
        if (perfectionMechanism is null && l1Pos is not null && lxPos is not null)
        {
            AntisciaConnection? antiscia = chart.AntisciaConnections.FirstOrDefault(a =>
                (a.Planet1.Equals(l1Name, StringComparison.OrdinalIgnoreCase) &&
                 a.Planet2.Equals(lxName, StringComparison.OrdinalIgnoreCase)) ||
                (a.Planet1.Equals(lxName, StringComparison.OrdinalIgnoreCase) &&
                 a.Planet2.Equals(l1Name, StringComparison.OrdinalIgnoreCase)));

            if (antiscia is not null && IsAntisciaApplying(antiscia.Type, l1Pos, lxPos))
            {
                perfectionMechanism = "Antiscia";
            }
            else if (antiscia is not null)
            {
                string kind = antiscia.Type == "Antiscia" ? "antiscion" : "contra-antiscion";
                mechanismNotes.Add(
                    $"The {l1Name}\u2013{lxName} {kind} (orb {PositionFormatter.FormatOrb(antiscia.Orb)}) is separating: testimony, not perfection.");
            }
        }

        bool isPerfected = perfectionMechanism is not null;

        // Step 4 — Check denial mechanisms
        List<string> denialReasons = [];

        // 4a. Moon VoC
        if (chart.MoonCondition?.IsVoidOfCourse == true)
        {
            denialReasons.Add(
                "Moon Void of Course \u2014 traditionally indicates nothing will come of this matter.");
        }

        // 4b. Combustion
        CheckCombustion(chart, l1Name, "L1", denialReasons);
        CheckCombustion(chart, lxName, "Lx", denialReasons);

        // 4c. Besiegement
        CheckBesiegement(chart, l1Name, "L1", denialReasons);
        CheckBesiegement(chart, lxName, "Lx", denialReasons);

        // 4d. Prohibition — when the direct aspect perfects, or a translation or collection
        //     completes: a malefic's exact aspect to either significator falling before then.
        if (application is { Approach.Perfects: true, PerfectsOutOfSign: false })
        {
            denialReasons.AddRange(application.ProhibitionReasons);
        }
        else if (intermediary is not null)
        {
            denialReasons.AddRange(intermediary.ProhibitionReasons);
        }

        // 4e. Refranation — only when a direct application exists and fails after a
        //     station of L1 or Lx. A station before an aspect that still perfects is
        //     a note (see FollowDirectApplication), never a denial.
        if (application is not null)
        {
            denialReasons.AddRange(application.RefranationReasons);
        }

        bool isDenied = denialReasons.Count > 0;

        // Step 5 — Assess reception quality
        (string receptionQuality, string? receptionDetail) = AssessReception(
            l1Name, lxName, l1Pos, lxPos, pairReception);

        // Step 6 — Moon's testimony
        (string? moonFirstApplying, bool moonVoC, string moonTestimony) =
            AssessMoonTestimony(chart, l1Name, lxName);

        // Step 7 — Overall judgment
        (string judgment, string judgmentNote) = DetermineJudgment(
            isPerfected, isDenied, perfectionMechanism, denialReasons, application);

        string question = chart.Input.HoraryQuestion ?? "(no question specified)";

        return new HoraryPerfectionResult
        {
            Question = question,
            QuestionCategory = category,
            QuesitedHouseNumber = quesitedHouseNumber,
            QuerentSignificator = l1Name,
            QuesitedSignificator = lxName,
            QuerentSign = l1Pos?.Sign.ToString() ?? "Unknown",
            QuesitedSign = lxPos?.Sign.ToString() ?? "Unknown",
            QuerentHouse = l1Pos?.House ?? 0,
            QuesitedHouse = lxPos?.House ?? 0,
            QuerentDignity = l1Dignity,
            QuesitedDignity = lxDignity,
            IsPerfected = isPerfected,
            PerfectionMechanism = perfectionMechanism,
            PerfectionAspect = perfectionAspect,
            PerfectionOrb = perfectionOrb,
            Translator = translator,
            Collector = collector,
            ApplyingPair = application?.Pair,
            ApplyingAspect = application?.AspectName,
            ApplyingAspectOrb = application?.Orb,
            Applier = application?.Applier,
            PerfectionTimeUtc = application is { PerfectsOutOfSign: false } ? application.Approach.PerfectionTimeUtc : null,
            ApplicationFailsTimeUtc = application is { Approach.Outcome: AspectApproachOutcome.TurnsAround }
                ? application.Approach.TurnaroundTimeUtc
                : null,
            ApplicationClosestOrb = application is { Approach.Outcome: AspectApproachOutcome.TurnsAround }
                ? application.Approach.ClosestOrb
                : null,
            ApplicationCapped = application is { Approach.Outcome: AspectApproachOutcome.Capped },
            SignificatorStations = application?.Stations ?? [],
            ProhibitionEvents = intermediary?.ProhibitionEvents ?? application?.ProhibitionEvents ?? [],
            Notes = [.. categoryNotes, .. significatorNotes, .. application?.Notes ?? [], .. mechanismNotes],
            IsDenied = isDenied,
            DenialReasons = denialReasons.AsReadOnly(),
            ReceptionQuality = receptionQuality,
            ReceptionDetail = receptionDetail,
            MoonFirstApplying = moonFirstApplying,
            MoonVoC = moonVoC,
            MoonTestimony = moonTestimony,
            Judgment = judgment,
            JudgmentNote = judgmentNote,
        };
    }

    #region Category Mapping

    /// <summary>
    /// Maps a horary question category to the relevant house number.
    /// </summary>
    /// <param name="category">The question category string.</param>
    /// <returns>
    /// The house number (1–12) governing the quesited matter, or null when the
    /// category is not recognised.
    /// </returns>
    private static int? MapCategoryToHouse(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "career" or "job" or "promotion" => 10,
            "money" or "finances" or "salary" => 2,
            "relationship" or "marriage" or "partner" => 7,
            "love" or "romance" or "dating" => 5,
            "home" or "property" or "real estate" or "move" => 4,
            "health" or "illness" => 6,
            "travel" or "relocation" => 9,
            "education" or "study" or "learning" => 9,
            "legal" or "lawsuit" or "court" => 7,
            "children" or "pregnancy" or "fertility" => 5,
            "siblings" or "neighbors" => 3,
            "death" or "inheritance" or "legacy" => 8,
            "death / inheritance" => 8,
            "friends" or "community" or "groups" => 11,
            "hidden" or "secrets" or "enemies" => 12,
            "hidden matters" => 12,
            "lost objects" or "lost" or "missing" => 2,
            "general" => 7,
            _ => null,
        };
    }

    #endregion

    #region Significator Helpers

    /// <summary>
    /// The ordinal of a house number as the notes print it: "1st", "2nd", "3rd", then "4th"
    /// to "12th".
    /// </summary>
    /// <param name="house">The house number, 1 to 12.</param>
    /// <returns>The ordinal, culture-independent.</returns>
    private static string Ordinal(int house) => house switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => house.ToString(CultureInfo.InvariantCulture) + "th",
    };

    /// <summary>
    /// Gets the traditional ruler name from an Ascendant longitude.
    /// </summary>
    private static string GetRulerName(double ascendantLongitude)
    {
        ZodiacSign ascSign = (ZodiacSign)((int)(ascendantLongitude % 360.0 / 30.0) + 1);
        CelestialBodyId ruler = ElementHelper.GetTraditionalRuler(ascSign);
        return ruler.ToString();
    }

    /// <summary>
    /// Finds a planet position by name (case-insensitive).
    /// </summary>
    private static PlanetPosition? FindPosition(ChartResult chart, string planetName)
    {
        return chart.Positions.FirstOrDefault(p =>
            p.Name.Equals(planetName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the dignity label for a planet, or "Unknown" if not found.
    /// </summary>
    private static string GetDignityLabel(ChartResult chart, string planetName)
    {
        DignityInfo? dignity = chart.Dignities.FirstOrDefault(d =>
            d.PlanetName.Equals(planetName, StringComparison.OrdinalIgnoreCase));
        return dignity is not null ? string.Create(CultureInfo.InvariantCulture, $"{dignity.Label} ({dignity.Score:+0;-0;0})") : "Unknown";
    }

    /// <summary>
    /// Parses a planet name to a <see cref="CelestialBodyId"/>, or null if not a standard body.
    /// </summary>
    private static CelestialBodyId? ParseBody(string name)
    {
        return Enum.TryParse<CelestialBodyId>(name, ignoreCase: true, out CelestialBodyId body)
            ? body
            : null;
    }

    #endregion

    #region Perfection Mechanisms

    /// <summary>
    /// Searches for an applying major aspect between L1 and Lx.
    /// </summary>
    private static AspectResult? FindApplyingMajorAspect(
        ChartResult chart, string l1Name, string lxName)
    {
        return chart.Aspects.FirstOrDefault(a =>
            a.IsApplying &&
            a.Aspect.Type.IsMajor() &&
            ((a.Body1Name.Equals(l1Name, StringComparison.OrdinalIgnoreCase) &&
              a.Body2Name.Equals(lxName, StringComparison.OrdinalIgnoreCase)) ||
             (a.Body1Name.Equals(lxName, StringComparison.OrdinalIgnoreCase) &&
              a.Body2Name.Equals(l1Name, StringComparison.OrdinalIgnoreCase))));
    }

    /// <summary>
    /// Searches for translation of light: one of the seven traditional planets,
    /// P, that separates from one significator and applies to the other, and is
    /// faster than both. A retrograde P qualifies; the speeds are compared by
    /// magnitude.
    /// </summary>
    /// <returns>
    /// The translator and the significator it applies to — the application the
    /// translation relies on — or nulls when there is no translation.
    /// </returns>
    private static (string? TranslatorName, string? AppliesTo) FindTranslation(
        ChartResult chart, string l1Name, string lxName,
        PlanetPosition l1Pos, PlanetPosition lxPos)
    {
        double l1Speed = Math.Abs(l1Pos.SpeedInLongitude);
        double lxSpeed = Math.Abs(lxPos.SpeedInLongitude);

        foreach (PlanetPosition p in chart.Positions)
        {
            string pName = p.Name;
            if (pName.Equals(l1Name, StringComparison.OrdinalIgnoreCase) ||
                pName.Equals(lxName, StringComparison.OrdinalIgnoreCase))
                continue;

            // Only the seven traditional planets carry light; the outer planets,
            // Chiron and the True Node never do.
            if (!IsTraditional(p.Body))
                continue;

            double pSpeed = Math.Abs(p.SpeedInLongitude);
            if (pSpeed <= l1Speed || pSpeed <= lxSpeed)
                continue;

            // Check: P separates from L1 and applies to Lx
            bool separatesFromL1 = HasAspect(chart, pName, l1Name, applying: false, majorOnly: true);
            bool appliesToLx = HasAspect(chart, pName, lxName, applying: true, majorOnly: true);

            if (separatesFromL1 && appliesToLx)
                return (pName, lxName);

            // Check: P separates from Lx and applies to L1
            bool separatesFromLx = HasAspect(chart, pName, lxName, applying: false, majorOnly: true);
            bool appliesToL1 = HasAspect(chart, pName, l1Name, applying: true, majorOnly: true);

            if (separatesFromLx && appliesToL1)
                return (pName, l1Name);
        }

        return (null, null);
    }

    /// <summary>
    /// Searches for collection of light: one of the seven traditional planets,
    /// P, to which both L1 and Lx apply by major aspect, and P is slower than
    /// both. A retrograde P qualifies; the speeds are compared by magnitude.
    /// </summary>
    private static string? FindCollection(
        ChartResult chart, string l1Name, string lxName,
        PlanetPosition l1Pos, PlanetPosition lxPos)
    {
        double l1Speed = Math.Abs(l1Pos.SpeedInLongitude);
        double lxSpeed = Math.Abs(lxPos.SpeedInLongitude);

        foreach (PlanetPosition p in chart.Positions)
        {
            string pName = p.Name;
            if (pName.Equals(l1Name, StringComparison.OrdinalIgnoreCase) ||
                pName.Equals(lxName, StringComparison.OrdinalIgnoreCase))
                continue;

            // Only the seven traditional planets collect light.
            if (!IsTraditional(p.Body))
                continue;

            double pSpeed = Math.Abs(p.SpeedInLongitude);
            if (pSpeed >= l1Speed || pSpeed >= lxSpeed)
                continue;

            bool l1AppliesToP = HasAspect(chart, l1Name, pName, applying: true, majorOnly: true);
            bool lxAppliesToP = HasAspect(chart, lxName, pName, applying: true, majorOnly: true);

            if (l1AppliesToP && lxAppliesToP)
                return pName;
        }

        return null;
    }

    /// <summary>
    /// True for the mutual-reception types that perfect: domicile, exaltation, or
    /// mixed domicile–exaltation. Triplicity and the mixed triplicity types do not.
    /// </summary>
    private static bool IsPerfectingReception(string receptionType) =>
        receptionType is "Domicile" or "Exaltation" or "Mixed (Domicile-Exaltation)";

    /// <summary>
    /// True when an antiscion (longitudes summing to 180°) or contra-antiscion
    /// (summing to 0°) is applying: the two signed speeds carry the sum toward
    /// the exact value.
    /// </summary>
    private static bool IsAntisciaApplying(string type, PlanetPosition a, PlanetPosition b)
    {
        double exact = type == "Antiscia" ? 180.0 : 0.0;
        double offset = Norm180(a.Longitude + b.Longitude - exact);
        double motion = a.SpeedInLongitude + b.SpeedInLongitude;
        return offset * motion < 0.0;
    }

    /// <summary>
    /// True when L1 and Lx receive each other, in either direction, by domicile
    /// or exaltation — the types the Reception line recognises.
    /// </summary>
    private static bool HasReception(PlanetPosition l1Pos, PlanetPosition lxPos) =>
        IsInDomicileOrExaltation(l1Pos.Sign, lxPos.Body) || IsInDomicileOrExaltation(lxPos.Sign, l1Pos.Body);

    /// <summary>True when a sign is the domicile or the exaltation of a planet.</summary>
    private static bool IsInDomicileOrExaltation(ZodiacSign sign, CelestialBodyId planet) =>
        ElementHelper.GetTraditionalRuler(sign) == planet ||
        (s_exaltations.TryGetValue(planet, out ZodiacSign exaltation) && exaltation == sign);

    /// <summary>The sign a body occupies at an instant (Julian Day, UT).</summary>
    private static ZodiacSign SignAt(IEphemerisService ephemeris, CelestialBodyId body, double julianDayUt)
    {
        double longitude = ephemeris.CalculatePositionLongitudeOnly(julianDayUt, body).Longitude;
        return (ZodiacSign)((int)(longitude % 360.0 / 30.0) + 1);
    }

    /// <summary>Normalises an angle to (−180, 180].</summary>
    private static double Norm180(double degrees)
    {
        double x = ((degrees % 360.0) + 360.0) % 360.0;
        return x > 180.0 ? x - 360.0 : x;
    }

    /// <summary>True for the seven traditional planets, the only bodies that translate or collect light.</summary>
    private static bool IsTraditional(CelestialBodyId body) => body is CelestialBodyId.Sun
        or CelestialBodyId.Moon or CelestialBodyId.Mercury or CelestialBodyId.Venus
        or CelestialBodyId.Mars or CelestialBodyId.Jupiter or CelestialBodyId.Saturn;

    /// <summary>
    /// Follows every application a translation or collection relies on and,
    /// when they all complete, looks for a prohibition before the mechanism does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each application — the translator's to the significator it applies to,
    /// or each significator's to the collector — is followed from its signed
    /// target by <see cref="AspectPerfectionFinder.FollowFirstApproach"/>. If any
    /// turns around or is still applying at the cap, the mechanism does not
    /// complete, and the note names every application that failed.
    /// </para>
    /// <para>
    /// Otherwise the mechanism completes when its last application perfects,
    /// and Mars or Saturn — neither a significator nor the intermediary itself,
    /// whose applications are the mechanism — prohibits it by an exact major
    /// aspect to L1 or Lx before then, as it would a direct aspect. The earliest
    /// such event per malefic is the denial.
    /// </para>
    /// </remarks>
    /// <param name="chart">The horary chart.</param>
    /// <param name="mechanism">The mechanism as the messages name it (e.g., "collection of light by Saturn").</param>
    /// <param name="via">The translator or collector.</param>
    /// <param name="applications">Each application the mechanism relies on, as (applying body, body applied to).</param>
    /// <param name="l1Name">L1's name.</param>
    /// <param name="lxName">Lx's name.</param>
    /// <param name="julianDayUt">The chart instant (Julian Day, UT).</param>
    /// <param name="ephemeris">Ephemeris the applications and malefics are followed in.</param>
    /// <returns>Whether the mechanism completes, the failure note if not, and its prohibitions if so.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a named body, position or applying aspect the mechanism was
    /// found from is missing from the chart — an invariant breach.
    /// </exception>
    private static IntermediaryOutcome FollowIntermediary(
        ChartResult chart, string mechanism, string via,
        IReadOnlyList<(string Applier, string Target)> applications,
        string l1Name, string lxName, double julianDayUt, IEphemerisService ephemeris)
    {
        List<string> failures = [];
        double completionJd = julianDayUt;

        foreach ((string applierName, string targetName) in applications)
        {
            PlanetPosition applierPos = FindPosition(chart, applierName)
                ?? throw new InvalidOperationException($"'{applierName}' has no position in the chart.");
            PlanetPosition targetPos = FindPosition(chart, targetName)
                ?? throw new InvalidOperationException($"'{targetName}' has no position in the chart.");
            AspectResult aspect = FindApplyingMajorAspect(chart, applierName, targetName)
                ?? throw new InvalidOperationException($"No applying major aspect between {applierName} and {targetName}.");

            double target = AspectPerfectionFinder.SignedTarget(
                aspect.Aspect.Angle, applierPos.Longitude, targetPos.Longitude);
            AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(
                ephemeris, applierPos.Body, targetPos.Body, target, julianDayUt);

            if (approach.PerfectionJulianDay is double perfectionJd)
            {
                completionJd = Math.Max(completionJd, perfectionJd);
                continue;
            }

            string fate;
            if (approach.Outcome == AspectApproachOutcome.TurnsAround)
            {
                DateTime turnaroundTimeUtc = approach.TurnaroundTimeUtc
                    ?? throw new InvalidOperationException(
                        $"{applierName}'s application to {targetName} is reported to turn around but carries no turnaround time.");
                fate = $"turns around at {PositionFormatter.FormatOrb(approach.ClosestOrb)} on {ChartTimeFormatter.FormatLocalTime(turnaroundTimeUtc, chart)}";
            }
            else
            {
                fate = string.Create(CultureInfo.InvariantCulture, $"is still applying {AspectPerfectionFinder.MaxFollowDays:F0} days after the chart");
            }

            failures.Add($"{applierName}'s {aspect.Aspect.Type.ToString().ToLowerInvariant()} to {targetName} {fate}");
        }

        if (failures.Count > 0)
        {
            return new IntermediaryOutcome
            {
                Completes = false,
                FailureNote = $"The {mechanism} does not complete: {string.Join("; ", failures)}.",
                ProhibitionReasons = [],
                ProhibitionEvents = [],
            };
        }

        string completesOn = ChartTimeFormatter.FormatLocalTime(
            JulianDayUtility.ToDateTimeUtc(ephemeris, completionJd), chart);
        CelestialBodyId l1Body = ParseBody(l1Name)
            ?? throw new InvalidOperationException($"'{l1Name}' is not a celestial body.");
        CelestialBodyId lxBody = ParseBody(lxName)
            ?? throw new InvalidOperationException($"'{lxName}' is not a celestial body.");
        List<string> reasons = [];
        List<HoraryProhibitionEvent> events = [];

        foreach (string malefic in new[] { "Mars", "Saturn" })
        {
            if (malefic.Equals(l1Name, StringComparison.OrdinalIgnoreCase) ||
                malefic.Equals(lxName, StringComparison.OrdinalIgnoreCase) ||
                malefic.Equals(via, StringComparison.OrdinalIgnoreCase))
                continue;

            CelestialBodyId maleficBody = ParseBody(malefic)
                ?? throw new InvalidOperationException($"'{malefic}' is not a celestial body.");
            (ExactAspectEvent Event, string Target, string Label)? earliest = null;

            foreach ((CelestialBodyId body, string name, string label) in new[] { (l1Body, l1Name, "L1"), (lxBody, lxName, "Lx") })
            {
                ExactAspectEvent? hit = AspectPerfectionFinder.FindFirstExactMajorAspect(
                    ephemeris, maleficBody, body, julianDayUt, completionJd);

                if (hit is not null && (earliest is null || hit.JulianDay < earliest.Value.Event.JulianDay))
                    earliest = (hit, name, label);
            }

            if (earliest is null)
                continue;

            (ExactAspectEvent evt, string significator, string significatorLabel) = earliest.Value;
            events.Add(new HoraryProhibitionEvent
            {
                Malefic = malefic,
                Target = significator,
                TargetLabel = significatorLabel,
                AspectType = evt.AspectType.ToString(),
                JulianDay = evt.JulianDay,
                ExactTimeUtc = evt.TimeUtc,
            });
            reasons.Add(
                $"Prohibition by {malefic} — {malefic}'s {evt.AspectType.ToString().ToLowerInvariant()} to " +
                $"{significator} ({significatorLabel}) is exact on {ChartTimeFormatter.FormatLocalTime(evt.TimeUtc, chart)}, " +
                $"before the {mechanism} completes on {completesOn}.");
        }

        return new IntermediaryOutcome
        {
            Completes = true,
            ProhibitionReasons = reasons,
            ProhibitionEvents = events,
        };
    }

    /// <summary>What following a translation's or collection's applications found.</summary>
    private sealed class IntermediaryOutcome
    {
        public required bool Completes { get; init; }
        public string? FailureNote { get; init; }
        public required IReadOnlyList<string> ProhibitionReasons { get; init; }
        public required IReadOnlyList<HoraryProhibitionEvent> ProhibitionEvents { get; init; }
    }

    /// <summary>
    /// Checks whether two bodies have a specific aspect relationship.
    /// </summary>
    private static bool HasAspect(
        ChartResult chart, string body1, string body2, bool applying, bool majorOnly)
    {
        return chart.Aspects.Any(a =>
            a.IsApplying == applying &&
            (!majorOnly || a.Aspect.Type.IsMajor()) &&
            ((a.Body1Name.Equals(body1, StringComparison.OrdinalIgnoreCase) &&
              a.Body2Name.Equals(body2, StringComparison.OrdinalIgnoreCase)) ||
             (a.Body1Name.Equals(body2, StringComparison.OrdinalIgnoreCase) &&
              a.Body2Name.Equals(body1, StringComparison.OrdinalIgnoreCase))));
    }

    #endregion

    #region Denial Mechanisms

    /// <summary>
    /// Checks if a significator is combust and adds a denial reason.
    /// </summary>
    private static void CheckCombustion(
        ChartResult chart, string planetName, string label, List<string> reasons)
    {
        PlanetCondition? condition = chart.PlanetConditions.FirstOrDefault(c =>
            c.PlanetName.Equals(planetName, StringComparison.OrdinalIgnoreCase));

        if (condition?.IsCombust == true)
        {
            reasons.Add(
                $"{label} ({planetName}) is combust \u2014 overwhelmed by the Sun, unable to act.");
        }
    }

    /// <summary>
    /// Checks if a significator is besieged and adds a denial reason.
    /// </summary>
    private static void CheckBesiegement(
        ChartResult chart, string planetName, string label, List<string> reasons)
    {
        PlanetCondition? condition = chart.PlanetConditions.FirstOrDefault(c =>
            c.PlanetName.Equals(planetName, StringComparison.OrdinalIgnoreCase));

        if (condition?.IsBesieged == true)
        {
            reasons.Add(
                $"{label} ({planetName}) is besieged between Mars and Saturn \u2014 trapped, unable to act freely.");
        }
    }

    /// <summary>
    /// Follows the direct applying aspect between L1 and Lx in the ephemeris and
    /// derives everything the judgment needs from its fate: whether and when it
    /// perfects, where it turns around if it fails, the significators' stations
    /// inside that window, the refranation denials, the prohibition denials and
    /// the informational notes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Refranation</b> — the application fails (its orb turns around before
    /// zero) and L1 or Lx stations, in either direction, between the chart and
    /// that turnaround; or a significator's first station after the turnaround
    /// falls before the linear perfection estimate — the instant the chart's
    /// separation would close at the chart's relative speed. Each such station
    /// is a denial, and joins the stations reported. A failing application with
    /// no such station is a note.
    /// </para>
    /// <para>
    /// <b>Station note</b> — the application perfects although L1 or Lx stations
    /// before it does (Venus turning retrograde two days before Mercury reaches
    /// her, say). A note, not a denial.
    /// </para>
    /// <para>
    /// <b>Sign test</b> — an application that perfects only after L1 or Lx has
    /// left the sign it occupies at the chart (<see cref="AspectPerfectionFinder.FindSignExit"/>)
    /// does not perfect, unless the significators receive each other by domicile
    /// or exaltation in either direction. The note names the sign changes, and
    /// no station note or prohibition is judged for it.
    /// </para>
    /// <para>
    /// <b>Prohibition</b> — the application perfects, and Mars or Saturn (not
    /// itself a significator) makes an exact major aspect to L1 or Lx strictly
    /// between the chart and the perfection. The earliest such event per
    /// malefic is a denial.
    /// </para>
    /// <para>
    /// Times in the messages are rendered on the chart's own clock by
    /// <see cref="ChartTimeFormatter.FormatLocalTime"/>, the same helper the
    /// Stationing table uses. A failed application's time is the finder's
    /// refined turnaround instant (<see cref="AspectApproach.TurnaroundTimeUtc"/>);
    /// the station window still ends at the closest hourly sample
    /// (<see cref="AspectApproach.ClosestJulianDay"/>), so refining the time
    /// cannot move a station into or out of it.
    /// </para>
    /// </remarks>
    private static DirectApplication FollowDirectApplication(
        ChartResult chart, string l1Name, string lxName,
        PlanetPosition l1Pos, PlanetPosition lxPos, AspectResult directAspect,
        double julianDayUt, IEphemerisService ephemeris)
    {
        CelestialBodyId l1Body = ParseBody(l1Name)
            ?? throw new InvalidOperationException($"'{l1Name}' is not a celestial body.");
        CelestialBodyId lxBody = ParseBody(lxName)
            ?? throw new InvalidOperationException($"'{lxName}' is not a celestial body.");

        double target = AspectPerfectionFinder.SignedTarget(
            directAspect.Aspect.Angle, l1Pos.Longitude, lxPos.Longitude);
        AspectApproach approach = AspectPerfectionFinder.FollowFirstApproach(
            ephemeris, l1Body, lxBody, target, julianDayUt);

        string pair = $"{l1Name}\u2013{lxName}";
        string aspectWord = directAspect.Aspect.Type.ToString().ToLowerInvariant();
        string applier = Math.Abs(l1Pos.SpeedInLongitude) >= Math.Abs(lxPos.SpeedInLongitude) ? l1Name : lxName;

        double windowEnd = approach.Outcome switch
        {
            AspectApproachOutcome.Perfects => PerfectionInstant(),
            // The closest hourly sample, not the refined turnaround instant: the refinement
            // moves the reported time only, never the window refranation is judged on.
            AspectApproachOutcome.TurnsAround => approach.ClosestJulianDay,
            _ => julianDayUt + AspectPerfectionFinder.MaxFollowDays,
        };

        List<HorarySignificatorStation> stations = [];
        foreach ((CelestialBodyId body, string name, string label) in new[] { (l1Body, l1Name, "L1"), (lxBody, lxName, "Lx") })
        {
            foreach (WindowStation station in StationWindowFinder.FindStations(ephemeris, body, julianDayUt, windowEnd))
            {
                stations.Add(new HorarySignificatorStation
                {
                    Significator = name,
                    Label = label,
                    IsRetrogradeStation = station.IsRetrogradeStation,
                    JulianDay = station.JulianDay,
                    TimeUtc = station.TimeUtc,
                });
            }
        }

        stations.Sort((a, b) => a.JulianDay.CompareTo(b.JulianDay));

        List<string> notes = [];
        List<string> refranationReasons = [];
        List<string> prohibitionReasons = [];
        List<HoraryProhibitionEvent> prohibitionEvents = [];
        bool perfectsOutOfSign = false;

        switch (approach.Outcome)
        {
            case AspectApproachOutcome.Perfects:
            {
                double perfectionJd = PerfectionInstant();
                DateTime perfectionTimeUtc = approach.PerfectionTimeUtc
                    ?? throw new InvalidOperationException(
                        $"The {pair} {aspectWord} is reported to perfect but carries no perfection time.");
                string perfectsOn = ChartTimeFormatter.FormatLocalTime(perfectionTimeUtc, chart);

                // An aspect that completes only after a significator has left its sign does not
                // perfect — unless the two receive each other. Nothing past it is judged.
                List<(string Name, CelestialBodyId Body, double ExitJd)> exits = [];
                if (!HasReception(l1Pos, lxPos))
                {
                    foreach ((CelestialBodyId body, string name) in new[] { (l1Body, l1Name), (lxBody, lxName) })
                    {
                        if (AspectPerfectionFinder.FindSignExit(ephemeris, body, julianDayUt, perfectionJd) is double exitJd
                            && exitJd < perfectionJd)
                            exits.Add((name, body, exitJd));
                    }
                }

                if (exits.Count > 0)
                {
                    perfectsOutOfSign = true;
                    IEnumerable<string> changes = exits.OrderBy(e => e.ExitJd).Select(e =>
                        $"{e.Name} enters {SignAt(ephemeris, e.Body, e.ExitJd)} on " +
                        $"{ChartTimeFormatter.FormatLocalTime(JulianDayUtility.ToDateTimeUtc(ephemeris, e.ExitJd), chart)}");
                    notes.Add(
                        $"The {pair} {aspectWord} is exact on {perfectsOn}, only after {string.Join(" and ", changes)}; " +
                        "with no reception between the significators it does not perfect.");
                    break;
                }

                foreach (HorarySignificatorStation station in stations)
                {
                    notes.Add(
                        $"{station.Significator} ({station.Label}) stations {StationWord(station)} on " +
                        $"{ChartTimeFormatter.FormatLocalTime(station.TimeUtc, chart)}; " +
                        $"the {pair} {aspectWord} still perfects on {perfectsOn}.");
                }

                foreach (string malefic in new[] { "Mars", "Saturn" })
                {
                    if (malefic.Equals(l1Name, StringComparison.OrdinalIgnoreCase) ||
                        malefic.Equals(lxName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    CelestialBodyId maleficBody = ParseBody(malefic)
                        ?? throw new InvalidOperationException($"'{malefic}' is not a celestial body.");
                    (ExactAspectEvent Event, string Target, string Label)? earliest = null;

                    foreach ((CelestialBodyId body, string name, string label) in new[] { (l1Body, l1Name, "L1"), (lxBody, lxName, "Lx") })
                    {
                        ExactAspectEvent? hit = AspectPerfectionFinder.FindFirstExactMajorAspect(
                            ephemeris, maleficBody, body, julianDayUt, perfectionJd);

                        if (hit is not null && (earliest is null || hit.JulianDay < earliest.Value.Event.JulianDay))
                            earliest = (hit, name, label);
                    }

                    if (earliest is null)
                        continue;

                    (ExactAspectEvent evt, string targetName, string targetLabel) = earliest.Value;
                    prohibitionEvents.Add(new HoraryProhibitionEvent
                    {
                        Malefic = malefic,
                        Target = targetName,
                        TargetLabel = targetLabel,
                        AspectType = evt.AspectType.ToString(),
                        JulianDay = evt.JulianDay,
                        ExactTimeUtc = evt.TimeUtc,
                    });
                    prohibitionReasons.Add(
                        $"Prohibition by {malefic} \u2014 {malefic}'s {evt.AspectType.ToString().ToLowerInvariant()} to " +
                        $"{targetName} ({targetLabel}) is exact on {ChartTimeFormatter.FormatLocalTime(evt.TimeUtc, chart)}, " +
                        $"before the {pair} {aspectWord} perfects on {perfectsOn}.");
                }

                break;
            }

            case AspectApproachOutcome.TurnsAround:
            {
                DateTime turnaroundTimeUtc = approach.TurnaroundTimeUtc
                    ?? throw new InvalidOperationException(
                        $"The {pair} {aspectWord} is reported to turn around but carries no turnaround time.");
                string closestOn = ChartTimeFormatter.FormatLocalTime(turnaroundTimeUtc, chart);

                // A station after the turnaround still refranates if it comes before the aspect
                // would have perfected at the chart's separation and relative speed: each
                // significator's first station in that window
                List<HorarySignificatorStation> lateStations = [];
                string? estimateOn = null;
                if (LinearPerfectionEstimate(l1Pos, lxPos, target, julianDayUt) is double estimateJd
                    && estimateJd > approach.ClosestJulianDay)
                {
                    estimateOn = ChartTimeFormatter.FormatLocalTime(JulianDayUtility.ToDateTimeUtc(ephemeris, estimateJd), chart);
                    foreach ((CelestialBodyId body, string name, string label) in new[] { (l1Body, l1Name, "L1"), (lxBody, lxName, "Lx") })
                    {
                        WindowStation? first = StationWindowFinder.FindStations(
                            ephemeris, body, approach.ClosestJulianDay, estimateJd).FirstOrDefault();
                        if (first is null)
                            continue;

                        lateStations.Add(new HorarySignificatorStation
                        {
                            Significator = name,
                            Label = label,
                            IsRetrogradeStation = first.IsRetrogradeStation,
                            JulianDay = first.JulianDay,
                            TimeUtc = first.TimeUtc,
                        });
                    }

                    lateStations.Sort((a, b) => a.JulianDay.CompareTo(b.JulianDay));
                }

                if (stations.Count == 0 && lateStations.Count == 0)
                {
                    notes.Add(
                        $"The {pair} {aspectWord} does not perfect: the application turns around at " +
                        $"{PositionFormatter.FormatOrb(approach.ClosestOrb)} on {closestOn} with neither significator stationing.");
                }

                foreach (HorarySignificatorStation station in stations)
                {
                    refranationReasons.Add(
                        string.Create(CultureInfo.InvariantCulture, $"Refranation \u2014 {station.Significator} ({station.Label}) stations {StationWord(station)} on " +
                        $"{ChartTimeFormatter.FormatLocalTime(station.TimeUtc, chart)}; " +
                        $"the {pair} {aspectWord} then fails, closest {PositionFormatter.FormatOrb(approach.ClosestOrb)} on {closestOn}."));
                }

                foreach (HorarySignificatorStation station in lateStations)
                {
                    refranationReasons.Add(
                        string.Create(CultureInfo.InvariantCulture, $"Refranation \u2014 {station.Significator} ({station.Label}) stations {StationWord(station)} on " +
                        $"{ChartTimeFormatter.FormatLocalTime(station.TimeUtc, chart)}, after the {pair} {aspectWord} " +
                        $"turns around at {PositionFormatter.FormatOrb(approach.ClosestOrb)} on {closestOn} but before its linear perfection " +
                        $"estimate of {estimateOn}."));
                }

                stations.AddRange(lateStations);
                break;
            }

            default:
            {
                notes.Add(
                    string.Create(CultureInfo.InvariantCulture, $"The {pair} {aspectWord} is still applying {AspectPerfectionFinder.MaxFollowDays:F0} days after the chart " +
                    $"(closest {PositionFormatter.FormatOrb(approach.ClosestOrb)} on {ChartTimeFormatter.FormatLocalTime(approach.ClosestTimeUtc, chart)}); ") +
                    "neither perfection nor refranation can be judged.");
                break;
            }
        }

        return new DirectApplication
        {
            Pair = pair,
            AspectName = directAspect.Aspect.Type.ToString(),
            Orb = directAspect.Orb,
            Applier = applier,
            Approach = approach,
            PerfectsOutOfSign = perfectsOutOfSign,
            Stations = stations,
            Notes = notes,
            RefranationReasons = refranationReasons,
            ProhibitionReasons = prohibitionReasons,
            ProhibitionEvents = prohibitionEvents,
        };

        // The finder sets the perfection instant with every perfecting outcome; a
        // perfecting approach without one is an invariant breach, not a case to skip.
        double PerfectionInstant() => approach.PerfectionJulianDay
            ?? throw new InvalidOperationException(
                $"The {pair} {aspectWord} is reported to perfect but carries no perfection instant.");
    }

    /// <summary>
    /// The instant the followed aspect would perfect if the chart's separation
    /// closed at the chart's relative speed — the linear perfection estimate
    /// (Julian Day, UT) — or null when the chart's speeds do not close it.
    /// </summary>
    /// <param name="l1Pos">L1's position and speed at the chart.</param>
    /// <param name="lxPos">Lx's position and speed at the chart.</param>
    /// <param name="target">The signed separation L1 − Lx the aspect perfects at (<see cref="AspectPerfectionFinder.SignedTarget"/>).</param>
    /// <param name="julianDayUt">The chart instant (Julian Day, UT).</param>
    private static double? LinearPerfectionEstimate(
        PlanetPosition l1Pos, PlanetPosition lxPos, double target, double julianDayUt)
    {
        double offset = Norm180(l1Pos.Longitude - lxPos.Longitude - target);
        double closing = -Math.Sign(offset) * (l1Pos.SpeedInLongitude - lxPos.SpeedInLongitude);
        return closing > 0.0 ? julianDayUt + Math.Abs(offset) / closing : null;
    }

    /// <summary>"retrograde" or "direct", for messages.</summary>
    private static string StationWord(HorarySignificatorStation station) =>
        station.IsRetrogradeStation ? "retrograde" : "direct";

    /// <summary>Everything derived from following the direct application.</summary>
    private sealed class DirectApplication
    {
        public required string Pair { get; init; }
        public required string AspectName { get; init; }
        public required double Orb { get; init; }
        public required string Applier { get; init; }
        public required AspectApproach Approach { get; init; }
        public required bool PerfectsOutOfSign { get; init; }
        public required IReadOnlyList<HorarySignificatorStation> Stations { get; init; }
        public required IReadOnlyList<string> Notes { get; init; }
        public required IReadOnlyList<string> RefranationReasons { get; init; }
        public required IReadOnlyList<string> ProhibitionReasons { get; init; }
        public required IReadOnlyList<HoraryProhibitionEvent> ProhibitionEvents { get; init; }
    }

    #endregion

    #region Reception Assessment

    /// <summary>
    /// Assesses the reception quality between L1 and Lx based on essential dignities.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When L1 and Lx are in a mutual reception that can perfect
    /// (<paramref name="mutualReception"/>), whatever the perfection, the line names its
    /// type — "Mutual by domicile", "Mutual by exaltation" or "Mutual by domicile and
    /// exaltation" — and the detail each significator's dignity in the other's sign, the
    /// kinds taken from the type: both domicile, both exaltation, or, for a mixed one, the
    /// side in the other's domicile "domicile" and the other "exaltation" (QI-2). Until
    /// 1.3.3 only the reception that was the mechanism was named: under a direct aspect an
    /// exaltation or mixed one printed "Neutral" with no detail and a domicile one
    /// "Mutual" (E1-1), and the detail took each side's kind domicile-first, so Venus in
    /// Virgo with Mercury in Pisces, a reception by exaltation, read "Venus in Mercury's
    /// domicile" (E1-5).
    /// </para>
    /// <para>
    /// Otherwise each direction is judged on its own: L1 in Lx's domicile or
    /// exaltation favours the querent, Lx in L1's favours the quesited, and
    /// either one in the other's detriment or fall is hostile, worded from its
    /// own side. One direction favoured and the other hostile is "Mixed", and
    /// the detail gives both; neither outranks the other. Hostility in both
    /// directions names both. Each in the other's domicile, which this path meets only
    /// without a chart context, is "Mutual by domicile".
    /// </para>
    /// </remarks>
    /// <param name="l1Name">The querent's significator.</param>
    /// <param name="lxName">The quesited's significator.</param>
    /// <param name="l1Pos">L1's position, or null when the chart lacks it.</param>
    /// <param name="lxPos">Lx's position, or null when the chart lacks it.</param>
    /// <param name="mutualReception">
    /// The L1–Lx mutual reception by domicile, exaltation, or mixed domicile–exaltation,
    /// from the chart context, or null when there is none.
    /// </param>
    /// <returns>The reception quality, and its detail or null.</returns>
    private static (string Quality, string? Detail) AssessReception(
        string l1Name, string lxName, PlanetPosition? l1Pos, PlanetPosition? lxPos,
        MutualReception? mutualReception)
    {
        if (l1Pos is null || lxPos is null)
            return ("Neutral", null);

        CelestialBodyId? l1Body = ParseBody(l1Name);
        CelestialBodyId? lxBody = ParseBody(lxName);

        if (l1Body is null || lxBody is null)
            return ("Neutral", null);

        ZodiacSign l1Sign = l1Pos.Sign;
        ZodiacSign lxSign = lxPos.Sign;

        // Check domicile relationships
        CelestialBodyId rulerOfL1Sign = ElementHelper.GetTraditionalRuler(l1Sign);
        CelestialBodyId rulerOfLxSign = ElementHelper.GetTraditionalRuler(lxSign);

        bool l1InLxDomicile = rulerOfL1Sign == lxBody.Value;
        bool lxInL1Domicile = rulerOfLxSign == l1Body.Value;

        // Check exaltation relationships
        bool l1InLxExaltation = s_exaltations.TryGetValue(lxBody.Value, out ZodiacSign lxExalt)
            && l1Sign == lxExalt;
        bool lxInL1Exaltation = s_exaltations.TryGetValue(l1Body.Value, out ZodiacSign l1Exalt)
            && lxSign == l1Exalt;

        // Check detriment/fall, in both directions
        bool l1InLxDetriOrFall = IsInDetriOrFall(l1Sign, lxBody.Value);
        bool lxInL1DetriOrFall = IsInDetriOrFall(lxSign, l1Body.Value);

        // A mutual reception that can perfect names its type whatever the mechanism, the kinds
        // taken from the type: for a mixed one exactly one side is in the other's domicile
        if (mutualReception is not null)
        {
            (string type, string l1Kind, string lxKind) = mutualReception.ReceptionType switch
            {
                "Domicile" => ("domicile", "domicile", "domicile"),
                "Exaltation" => ("exaltation", "exaltation", "exaltation"),
                _ => l1InLxDomicile
                    ? ("domicile and exaltation", "domicile", "exaltation")
                    : ("domicile and exaltation", "exaltation", "domicile"),
            };
            return ($"Mutual by {type}",
                $"{l1Name} in {lxName}'s {l1Kind} ({l1Sign}); {lxName} in {l1Name}'s {lxKind} ({lxSign}).");
        }

        // Mutual reception: both in each other's domicile
        if (l1InLxDomicile && lxInL1Domicile)
            return ("Mutual by domicile", $"{l1Name} in {lxName}'s domicile ({l1Sign}); {lxName} in {l1Name}'s domicile ({lxSign}).");

        // Each direction is favoured (in the other's domicile or exaltation), hostile (in the
        // other's detriment or fall), or neither, and is worded from its own side
        bool l1HasLxDignity = l1InLxDomicile || l1InLxExaltation;
        bool lxHasL1Dignity = lxInL1Domicile || lxInL1Exaltation;
        string l1Favoured =
            $"{l1Name} is in {lxName}'s {(l1InLxDomicile ? "domicile" : "exaltation")} ({l1Sign}) \u2014 querent is in quesited's power";
        string lxFavoured =
            $"{lxName} is in {l1Name}'s {(lxInL1Domicile ? "domicile" : "exaltation")} ({lxSign}) \u2014 quesited has the advantage";
        string l1Hostile =
            $"{l1Name} is in a sign of {lxName}'s detriment or fall ({l1Sign}) \u2014 resistance to the quesited";
        string lxHostile =
            $"{lxName} is in a sign of {l1Name}'s detriment or fall ({lxSign}) \u2014 the quesited is averse to the querent";

        // One direction favoured and the other hostile: mixed testimony, neither outranking the other
        if (l1HasLxDignity && lxInL1DetriOrFall)
            return ("Mixed", $"{l1Favoured}; {lxHostile}.");

        if (l1InLxDetriOrFall && lxHasL1Dignity)
            return ("Mixed", $"{l1Hostile}; {lxFavoured}.");

        if (l1HasLxDignity && !lxHasL1Dignity)
            return ("Querent favoured", $"{l1Favoured}.");

        if (lxHasL1Dignity && !l1HasLxDignity)
            return ("Quesited favoured", $"{lxFavoured}.");

        if (l1InLxDetriOrFall && lxInL1DetriOrFall)
            return ("Hostile", $"{l1Hostile}; {lxHostile}.");

        if (l1InLxDetriOrFall)
            return ("Hostile", $"{l1Hostile}.");

        if (lxInL1DetriOrFall)
            return ("Hostile", $"{lxHostile}.");

        return ("Neutral", null);
    }

    /// <summary>
    /// Checks whether a sign is the detriment or fall of a given planet.
    /// Detriment = opposite of domicile; Fall = opposite of exaltation.
    /// </summary>
    private static bool IsInDetriOrFall(ZodiacSign sign, CelestialBodyId planet)
    {
        // Detriment: opposite of the sign the planet rules
        // We need to check all signs the planet rules and see if 'sign' is opposite any of them.
        foreach (ZodiacSign s in Enum.GetValues<ZodiacSign>())
        {
            if (ElementHelper.GetTraditionalRuler(s) == planet)
            {
                ZodiacSign opposite = GetOppositeSign(s);
                if (sign == opposite)
                    return true;
            }
        }

        // Fall: opposite of exaltation sign
        if (s_exaltations.TryGetValue(planet, out ZodiacSign exaltSign))
        {
            if (sign == GetOppositeSign(exaltSign))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the opposite sign (180° away).
    /// </summary>
    private static ZodiacSign GetOppositeSign(ZodiacSign sign)
    {
        int val = (int)sign;
        int opposite = ((val - 1 + 6) % 12) + 1;
        return (ZodiacSign)opposite;
    }

    #endregion

    #region Moon Testimony

    /// <summary>
    /// Builds the Moon's testimony for the horary question.
    /// </summary>
    private static (string? FirstApplying, bool VoC, string Testimony) AssessMoonTestimony(
        ChartResult chart, string l1Name, string lxName)
    {
        MoonConditionInfo? moon = chart.MoonCondition;
        bool voc = moon?.IsVoidOfCourse == true;

        if (voc)
        {
            return (null, true,
                "Moon is Void of Course \u2014 the querent's situation is at a standstill.");
        }

        string? firstApplying = null;
        string testimony;

        if (moon?.FirstApplyingPlanet is not null && moon.FirstApplyingAspectType.HasValue)
        {
            string aspectName = moon.FirstApplyingAspectType.Value.ToString();
            firstApplying = $"{moon.FirstApplyingPlanet} by {aspectName}";

            if (moon.FirstApplyingPlanet.Equals(lxName, StringComparison.OrdinalIgnoreCase))
            {
                testimony = $"Moon applies to {lxName} by {aspectName} \u2014 the querent's instincts align with the outcome.";
            }
            else if (moon.FirstApplyingPlanet.Equals(l1Name, StringComparison.OrdinalIgnoreCase)
                     && !l1Name.Equals("Moon", StringComparison.OrdinalIgnoreCase))
            {
                testimony = $"Moon applies to {l1Name} \u2014 querent is focused on themselves.";
            }
            else if (IsBenefic(moon.FirstApplyingPlanet))
            {
                testimony = $"Moon first applies to {moon.FirstApplyingPlanet} ({aspectName}) \u2014 supportive conditions ahead.";
            }
            else if (IsMalefic(moon.FirstApplyingPlanet))
            {
                testimony = $"Moon first applies to {moon.FirstApplyingPlanet} ({aspectName}) \u2014 obstacles ahead, even if perfection occurs.";
            }
            else
            {
                testimony = $"Moon first applies to {moon.FirstApplyingPlanet} by {aspectName}.";
            }
        }
        else
        {
            testimony = "Moon has no applying aspect \u2014 limited additional testimony.";
        }

        return (firstApplying, voc, testimony);
    }

    /// <summary>
    /// Returns true if the planet is a traditional benefic (Venus, Jupiter).
    /// </summary>
    private static bool IsBenefic(string planetName)
    {
        return planetName.Equals("Venus", StringComparison.OrdinalIgnoreCase) ||
               planetName.Equals("Jupiter", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns true if the planet is a traditional malefic (Mars, Saturn).
    /// </summary>
    private static bool IsMalefic(string planetName)
    {
        return planetName.Equals("Mars", StringComparison.OrdinalIgnoreCase) ||
               planetName.Equals("Saturn", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Judgment

    /// <summary>
    /// Determines the overall horary judgment based on perfection and denial analysis.
    /// </summary>
    /// <remarks>
    /// When the chart is neither perfected nor denied, the note names the
    /// followed direct application if there is one: it then perfected only out
    /// of sign, failed — turned around without completing — or was still
    /// applying at the finder's cap, and saying there was no applying aspect
    /// would contradict the note or the "Application fails" or "Application
    /// unresolved" line above it. The judgment itself is the same either way.
    /// </remarks>
    private static (string Judgment, string Note) DetermineJudgment(
        bool isPerfected, bool isDenied, string? mechanism, List<string> denialReasons,
        DirectApplication? application)
    {
        if (isPerfected && !isDenied)
        {
            return ($"Yes", $"Perfection by {mechanism} \u2014 the matter is likely to come to completion.");
        }

        if (isPerfected && isDenied)
        {
            string denialSummary = string.Join("; ", denialReasons.Select(r => r.Split('\u2014')[0].Trim()));
            string verb = denialReasons.Count == 1 ? "complicates" : "complicate";
            return ("Uncertain",
                $"Perfection exists ({mechanism}) but {denialSummary} {verb} the outcome. The matter may succeed with difficulty or delay.");
        }

        if (!isPerfected && isDenied)
        {
            string primaryReason = denialReasons[0].Split('\u2014')[0].Trim();
            return ("No",
                $"{primaryReason} \u2014 the chart does not show this matter coming to completion.");
        }

        // Neither perfected nor denied
        return application switch
        {
            { PerfectsOutOfSign: true } outOfSign => ("Uncertain",
                $"The applying {outOfSign.Pair} {outOfSign.AspectName.ToLowerInvariant()} perfects only after a significator " +
                "leaves its sign, and there is no clear denial \u2014 the matter may not come to fruition, or the outcome is unclear."),
            { Approach.Outcome: AspectApproachOutcome.TurnsAround } failed => ("Uncertain",
                $"The applying {failed.Pair} {failed.AspectName.ToLowerInvariant()} turns around without completing, " +
                "and there is no clear denial \u2014 the matter may not come to fruition, or the outcome is unclear."),
            { Approach.Outcome: AspectApproachOutcome.Capped } unresolved => ("Uncertain",
                string.Create(CultureInfo.InvariantCulture, $"The {unresolved.Pair} {unresolved.AspectName.ToLowerInvariant()} is still applying " +
                $"{AspectPerfectionFinder.MaxFollowDays:F0} days after the chart, ") +
                "and there is no clear denial \u2014 the matter may not come to fruition, or the outcome is unclear."),
            _ => ("Uncertain",
                "No applying aspect between significators and no clear denial \u2014 the matter may not come to fruition, or the outcome is unclear."),
        };
    }

    #endregion
}
