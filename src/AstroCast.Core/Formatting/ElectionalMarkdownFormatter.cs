using System.Globalization;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Generates a structured markdown export of an electional calculation result.
/// The output is designed for both human reading and AI interpretation prompt
/// consumption. Follows the same static pattern as <see cref="TransitMarkdownFormatter"/>.
/// </summary>
public static class ElectionalMarkdownFormatter
{
    /// <summary>
    /// The invariant number format with one change: a percentage prints with no
    /// space before its sign, so a score weight reads "25%" as it always has.
    /// The invariant culture's own percent pattern is "25 %".
    /// </summary>
    /// <remarks>
    /// Through this format "P0" is en-US's "P0" exactly, rounding included
    /// (0.125 prints "12%"); a custom "0%" pattern would round that tie up.
    /// </remarks>
    private static readonly NumberFormatInfo InvariantWeightFormat = new()
    {
        PercentPositivePattern = 1,
        PercentNegativePattern = 1,
    };

    /// <summary>
    /// Traditional sign rulers: sign → ruling planet name.
    /// Used for the Ascendant ruler lookup in candidate sections.
    /// </summary>
    private static readonly Dictionary<ZodiacSign, string> SignRulers = new()
    {
        [ZodiacSign.Aries] = "Mars",
        [ZodiacSign.Taurus] = "Venus",
        [ZodiacSign.Gemini] = "Mercury",
        [ZodiacSign.Cancer] = "Moon",
        [ZodiacSign.Leo] = "Sun",
        [ZodiacSign.Virgo] = "Mercury",
        [ZodiacSign.Libra] = "Venus",
        [ZodiacSign.Scorpio] = "Mars",
        [ZodiacSign.Sagittarius] = "Jupiter",
        [ZodiacSign.Capricorn] = "Saturn",
        [ZodiacSign.Aquarius] = "Saturn",
        [ZodiacSign.Pisces] = "Jupiter",
    };

    /// <summary>
    /// Converts an electional result to a structured markdown string.
    /// </summary>
    /// <param name="result">The complete electional result.</param>
    /// <returns>Formatted markdown string.</returns>
    public static string ToMarkdown(ElectionalResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder sb = new();

        AppendHeader(sb, result);
        AppendNatalChartSummary(sb, result);

        if (result.Candidates.Count == 0)
        {
            AppendNoCandidatesNotice(sb, result);
        }
        else
        {
            foreach (CandidateWindow candidate in result.Candidates)
            {
                AppendCandidateSection(sb, candidate, result);
            }
        }

        AppendDisqualifiedPeriods(sb, result);
        AppendVocCalendar(sb, result);
        AppendFooter(sb, result);

        return sb.ToString();
    }

    /// <summary>
    /// Saves the electional result as a markdown file.
    /// </summary>
    /// <param name="result">The complete electional result.</param>
    /// <param name="filePath">Path to save the markdown file.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        ElectionalResult result, string filePath, CancellationToken ct = default)
    {
        string md = ToMarkdown(result);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }

    #region Section 1: Report Header

    /// <summary>
    /// Appends the report header with election metadata.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="result">The electional result being rendered.</param>
    /// <remarks>
    /// <para>
    /// When the search did not score some eligible samples because they have no planetary hour,
    /// a line directly after the Separation line says how many (branch O3, 1.3.3). Where it scored
    /// every one, the header is as it was before. A count of one reads in the singular, "on that
    /// day" (branch Q, 1.3.3).
    /// </para>
    /// <para>
    /// Two lines since branch Q, 1.3.3. Directly after the Election Type line, the type's score
    /// ceiling, <see cref="ElectionalScorer.ScoreCeiling"/>, to two decimals at most: a score is read
    /// against it, not against 100. Directly after the Event Location line, the event's time zone as
    /// entered: every candidate instant and every period table is on that clock, and until 1.3.3
    /// only the natal chart's zone was named.
    /// </para>
    /// </remarks>
    private static void AppendHeader(StringBuilder sb, ElectionalResult result)
    {
        ElectionalInput input = result.Input;
        ChartInput natalInput = result.NatalChart.Input;

        sb.AppendLine("# Electional Report");
        sb.AppendLine();
        sb.AppendLine($"**Name:** {natalInput.Name}");
        // An explicit offset overrides the zone's rules, so the birth wall clock is labelled
        // with that offset, as the chart export's Date/Time line labels it, never with the zone.
        string natalClock = natalInput.UseExplicitOffset && natalInput.UtcOffsetHours.HasValue
            ? result.NatalChart.ResolvedOffsetLabel
            : natalInput.TimeZone;
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Natal Date:** {natalInput.LocalDateTime:yyyy-MM-dd}, {natalInput.LocalDateTime:HH:mm:ss} {natalClock}, {natalInput.LocationName}");
        sb.AppendLine($"**Election Type:** {input.ElectionType}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Score ceiling:** {ElectionalScorer.ScoreCeiling(input.ElectionType):0.##} (each of the seven dimensions at its highest)");
        sb.AppendLine($"**Election Goal:** {input.ElectionGoal}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Date Range:** {input.RangeStart:yyyy-MM-dd} to {input.RangeEnd:yyyy-MM-dd}");
        if (CalendarDateFormatter.IsJulian(result.NatalChart.UtcDateTime) || input.RangeStart < CalendarDateFormatter.GregorianCalendarStartDate)
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine);
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Event Location:** {input.LocationName} ({input.Latitude:F4}\u00b0, {input.Longitude:F4}\u00b0)");
        sb.AppendLine($"**Event Time Zone:** {input.TimeZone} (candidate instants and the period tables are on this clock)");
        sb.AppendLine($"**Constraints:** {BuildConstraintsDisplay(input)}");
        sb.AppendLine($"**Candidate instants:** {BuildCandidateCountDisplay(result)}");
        sb.AppendLine($"**Separation:** {BuildSeparationDisplay(input)}");
        if (result.SamplesWithoutPlanetaryHour == 1)
        {
            sb.AppendLine("**Samples without a planetary hour:** 1, not scored (the Sun does not rise or set there on that day)");
        }
        else if (result.SamplesWithoutPlanetaryHour > 0)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"**Samples without a planetary hour:** {result.SamplesWithoutPlanetaryHour}, not scored (the Sun does not rise or set there on "
                + $"those days)");
        }

        sb.AppendLine(CultureInfo.InvariantCulture, $"**Generated:** {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Software:** AstroCast \u2014 Swiss Ephemeris, Tropical, {result.NatalChart.HouseSystemName}");
        sb.AppendLine();
    }

    /// <summary>
    /// Composes the header Candidate instants field ("Candidates" until 1.3.3): the
    /// delivered count on its own when the request was met, or the delivered count
    /// against the requested count plus the reason when it was not.
    /// </summary>
    /// <param name="result">The electional result being rendered.</param>
    /// <returns>The Candidate instants field value.</returns>
    /// <remarks>
    /// <para>
    /// A search can deliver fewer candidate windows than were asked for when the
    /// date range, the time-of-day and day-of-week constraints, and the minimum
    /// separation together leave the window unable to supply that many distinct
    /// moments. Before this field distinguished the two numbers the export said
    /// only <c>Candidates: 3</c>, which reads identically whether three is all
    /// the window had or three is all the engine managed to find -- the reader
    /// had no way to tell that asking for five had failed.
    /// </para>
    /// <para>
    /// The shortfall clause is emitted only when the counts differ. A met request
    /// renders the bare count, so the common case gains no noise. Since 1.3.3 the
    /// field is labelled "Candidate instants" (ruling 7, branch O2): an interpretation
    /// prompt that parses the header reads that label.
    /// </para>
    /// <para>
    /// When nothing was delivered the clause says that no moment in the date range qualified
    /// (branch Q, 1.3.3): with nothing delivered the separation cannot be the cause, whatever
    /// emptied the range. The "No Candidates Found" notice and, where samples had no planetary
    /// hour, the header line say why. Until 1.3.3 the clause named the separation here too.
    /// </para>
    /// </remarks>
    internal static string BuildCandidateCountDisplay(ElectionalResult result)
    {
        int delivered = result.Candidates.Count;

        if (!result.IsRequestUnmet)
        {
            return delivered.ToString(CultureInfo.InvariantCulture);
        }

        int requested = result.RequestedCandidateCount;

        if (delivered == 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{delivered} of {requested} requested \u2014 no moment in the date range qualified");
        }

        double separationHours = result.Input.MinimumCandidateSeparationHours;

        string reason = separationHours > 0.0
            ? string.Create(CultureInfo.InvariantCulture, $"the date range could not supply {requested} qualifying moments at least "
              + $"{separationHours.ToString("0.##", CultureInfo.InvariantCulture)} {(separationHours == 1.0 ? "hour" : "hours")} apart")
            : string.Create(CultureInfo.InvariantCulture, $"the date range could not supply {requested} qualifying moments");

        return string.Create(CultureInfo.InvariantCulture, $"{delivered} of {requested} requested \u2014 {reason}");
    }

    /// <summary>
    /// Composes the header Separation field: the minimum time the search kept between
    /// candidate instants, or "none" when it kept none.
    /// </summary>
    /// <param name="input">The electional input whose separation is rendered.</param>
    /// <returns>
    /// The Separation field value: "at least 6 hours between candidate instants", "at least
    /// 1 hour between candidate instants" when the separation is exactly one hour, or "none"
    /// when it is zero or less.
    /// </returns>
    /// <remarks>
    /// The search has always kept six hours between the instants it reports unless told
    /// otherwise, and never said so (ruling 4, branch O2, 1.3.3). The hours print to two
    /// decimals at most, culture-independently: 4.5, never 4,5.
    /// </remarks>
    private static string BuildSeparationDisplay(ElectionalInput input)
    {
        double separationHours = input.MinimumCandidateSeparationHours;

        if (separationHours <= 0.0)
        {
            return "none";
        }

        string unit = separationHours == 1.0 ? "hour" : "hours";

        return string.Create(CultureInfo.InvariantCulture,
            $"at least {separationHours.ToString("0.##", CultureInfo.InvariantCulture)} {unit} between candidate instants");
    }

    /// <summary>
    /// Day-of-week abbreviations for the Constraints header field, Monday first.
    /// Hardcoded (not culture-derived) so the export wording is stable.
    /// </summary>
    private static readonly Dictionary<DayOfWeek, string> DayAbbreviations = new()
    {
        [DayOfWeek.Monday] = "Mon",
        [DayOfWeek.Tuesday] = "Tue",
        [DayOfWeek.Wednesday] = "Wed",
        [DayOfWeek.Thursday] = "Thu",
        [DayOfWeek.Friday] = "Fri",
        [DayOfWeek.Saturday] = "Sat",
        [DayOfWeek.Sunday] = "Sun",
    };

    /// <summary>
    /// Composes the header Constraints field: the user's free-text constraint
    /// notes followed by the active search constraints, semicolon-separated.
    /// Active time/day constraints render in the fixed form
    /// <c>Candidates limited to 08:00\u201317:00 local; days: Mon, Tue, Wed, Thu, Fri</c>
    /// so interpretation prompts can read them (requirement B4). When nothing
    /// is set, returns "None" (preserving the legacy header).
    /// </summary>
    /// <param name="input">The electional input whose constraints are rendered.</param>
    /// <returns>The Constraints field value.</returns>
    internal static string BuildConstraintsDisplay(ElectionalInput input)
    {
        List<string> parts = [];

        if (!string.IsNullOrWhiteSpace(input.ConstraintNotes))
        {
            parts.Add(input.ConstraintNotes.Trim());
        }

        if (input.TimeConstraint is not null)
        {
            string earliest = input.TimeConstraint.Earliest.ToString("HH':'mm", CultureInfo.InvariantCulture);
            string latest = input.TimeConstraint.Latest.ToString("HH':'mm", CultureInfo.InvariantCulture);
            parts.Add($"Candidates limited to {earliest}\u2013{latest} local");
        }

        if (input.AllowedDays is { Count: > 0 } allowedDays)
        {
            IEnumerable<string> ordered = allowedDays
                .Distinct()
                .OrderBy(d => ((int)d + 6) % 7) // Monday first
                .Select(d => DayAbbreviations[d]);
            parts.Add($"days: {string.Join(", ", ordered)}");
        }

        return parts.Count > 0 ? string.Join("; ", parts) : "None";
    }

    /// <summary>
    /// Appends the notice emitted when the search produced no candidates \u2014
    /// typically because the configured constraints eliminated every viable
    /// moment (requirement B5). The export remains valid and complete; the
    /// disqualified-periods and void-of-course sections still follow.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="result">The electional result with an empty candidate list.</param>
    /// <remarks>
    /// When some eligible samples had no planetary hour, a sentence after the constraints
    /// sentence, in the same paragraph, says how many and why (branch O3, 1.3.3). A count of one
    /// reads in the singular (branch Q, 1.3.3).
    /// </remarks>
    private static void AppendNoCandidatesNotice(StringBuilder sb, ElectionalResult result)
    {
        ElectionalInput input = result.Input;
        string withoutHour = result.SamplesWithoutPlanetaryHour == 1
            ? " 1 eligible sample had no planetary hour, because the Sun does not rise or set there on that day, and "
              + "was not scored."
            : result.SamplesWithoutPlanetaryHour > 0
            ? string.Create(CultureInfo.InvariantCulture, $" {result.SamplesWithoutPlanetaryHour} eligible samples had no planetary hour, because the Sun does not rise or set there on those "
              + $"days, and were not scored.")
            : string.Empty;

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## No Candidates Found");
        sb.AppendLine();
        sb.AppendLine(
            CultureInfo.InvariantCulture, $"No viable election moments satisfied the configured constraints within "
            + $"{input.RangeStart:yyyy-MM-dd} to {input.RangeEnd:yyyy-MM-dd}. "
            + $"Constraints applied: {BuildConstraintsDisplay(input)}.{withoutHour}");
        sb.AppendLine();
        sb.AppendLine(
            "Consider widening the date range, relaxing the time-of-day or day-of-week "
            + "limits, or reviewing the disqualified periods below.");
        sb.AppendLine();
    }

    #endregion

    #region Section 2: Natal Chart Summary

    /// <summary>
    /// Appends an abbreviated natal chart summary (positions, houses, major aspects).
    /// </summary>
    private static void AppendNatalChartSummary(StringBuilder sb, ElectionalResult result)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Natal Chart Summary");
        sb.AppendLine();

        ChartResult natal = result.NatalChart;

        // Positions table
        sb.AppendLine("| Body | Position | Lat | Dec | House | Speed | Dir | OOB |");
        sb.AppendLine("|------|----------|-----|-----|-------|-------|-----|-----|");

        foreach (PlanetPosition p in natal.Positions)
        {
            string houseStr = p.House.HasValue ? string.Create(CultureInfo.InvariantCulture, $"{p.House.Value}") : "-";
            string latStr = PositionFormatter.FormatLatitude(p.Latitude);
            string decStr = p.Declination.HasValue
                ? PositionFormatter.FormatDeclination(p.Declination.Value) : "-";
            string oobStr = PositionFormatter.IsOutOfBounds(p, natal.Obliquity) ? "OOB" : "";
            sb.AppendLine(
                CultureInfo.InvariantCulture, $"| {p.Name} | {PositionFormatter.FormatPosition(p)} | {latStr} | {decStr} | {houseStr} | {p.SpeedInLongitude:F3}/d | {p.DirectionCode} | {oobStr} |");
        }

        sb.AppendLine();

        // House cusps
        sb.AppendLine($"### House Cusps ({natal.HouseSystemName})");
        sb.AppendLine();
        sb.AppendLine("| House | Cusp |");
        sb.AppendLine("|-------|------|");

        for (int i = 0; i < natal.Houses.Cusps.Count; i++)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {i + 1} | {PositionFormatter.FormatLongitude(natal.Houses.Cusps[i])} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**ASC:** {PositionFormatter.FormatLongitude(natal.Houses.Ascendant)}  ");
        sb.AppendLine($"**MC:** {PositionFormatter.FormatLongitude(natal.Houses.Midheaven)}  ");
        sb.AppendLine($"**DSC:** {PositionFormatter.FormatLongitude(natal.Houses.Descendant)}  ");
        sb.AppendLine($"**IC:** {PositionFormatter.FormatLongitude(natal.Houses.ImumCoeli)}");
        sb.AppendLine();

        // Essential Dignities
        sb.AppendLine("### Essential Dignities");
        sb.AppendLine();
        sb.AppendLine("| Planet | Sign | Dignity | Score | Dispositor |");
        sb.AppendLine("|--------|------|---------|-------|------------|");

        foreach (DignityInfo d in natal.Dignities)
        {
            string scoreStr = d.Score > 0 ? string.Create(CultureInfo.InvariantCulture, $"+{d.Score}") : d.Score.ToString(CultureInfo.InvariantCulture);
            sb.AppendLine(
                $"| {d.PlanetName} | {d.Sign} | {d.Label} | {scoreStr} | {d.Dispositor} |");
        }

        sb.AppendLine();

        // Major aspects only (conjunction, opposition, trine, square, sextile)
        sb.AppendLine("### Key Aspects");
        sb.AppendLine();

        List<AspectResult> majorAspects = natal.Aspects
            .Where(a => a.Aspect.Type is AspectType.Conjunction or AspectType.Opposition
                or AspectType.Trine or AspectType.Square or AspectType.Sextile)
            .ToList();

        if (majorAspects.Count > 0)
        {
            sb.AppendLine("| Body 1 | Aspect | Body 2 | Orb | Ap/Sp |");
            sb.AppendLine("|--------|--------|--------|-----|-------|");

            foreach (AspectResult a in majorAspects)
            {
                string appSep = a.IsApplying ? "Ap" : "Sp";
                sb.AppendLine(
                    $"| {a.Body1Name} | {a.Aspect.Abbreviation} | {a.Body2Name} | {PositionFormatter.FormatOrb(a.Orb)} | {appSep} |");
            }
        }
        else
        {
            sb.AppendLine("No major aspects found.");
        }

        sb.AppendLine();
    }

    #endregion

    #region Section 3: Candidate Sections

    /// <summary>
    /// Appends a single candidate section with all subsections.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="candidate">The candidate window being rendered.</param>
    /// <param name="result">The electional result the candidate belongs to.</param>
    /// <remarks>
    /// The Score line prints the candidate's score to two decimals at most (82.25, 88.5, 91), as the breakdown's
    /// Overall row does, so the two always agree (branch Q, 1.3.3). Until 1.3.3 it printed a whole number with
    /// "/100", rounding a midpoint to even (88.5 as "88/100"); the score's ceiling is below 100, from 90.5 to 95.5 by
    /// type.
    /// </remarks>
    private static void AppendCandidateSection(
        StringBuilder sb, CandidateWindow candidate, ElectionalResult result)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture, $"## Candidate instant {candidate.Rank} \u2014 {CalendarDateFormatter.Format(candidate.LocalDateTime, "ddd, dd MMM yyyy h:mm tt")}{RepeatedReadingOffset(candidate)}");
        sb.AppendLine();

        // Summary line
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Score:** {candidate.Score:0.##}");

        // Moon line
        string voidStatus = candidate.Moon.IsVoidOfCourse
            ? string.Create(CultureInfo.InvariantCulture, $"void-of-course (since {candidate.Moon.VoidStartLocal:HH:mm}, ends {candidate.Moon.VoidEndLocal:HH:mm})")
            : "not void";
        string applyingAspect = BuildMoonApplyingAspectDisplay(candidate);
        sb.AppendLine($"**Moon:** {candidate.Moon.Sign}, {candidate.Moon.Phase} ({candidate.Moon.PhasePosition}-phase), {voidStatus}, {applyingAspect}");

        // Ascendant line
        ZodiacSign ascSign = (ZodiacSign)((int)(candidate.Chart.Houses.Ascendant / 30.0) + 1);
        string rulerPlanet = SignRulers.TryGetValue(ascSign, out string? ruler) ? ruler : "Unknown";
        PlanetPosition? rulerPosition = candidate.Chart.Positions
            .FirstOrDefault(p => string.Equals(p.Name, rulerPlanet, StringComparison.OrdinalIgnoreCase));
        string rulerHouse = rulerPosition?.House.HasValue == true ? string.Create(CultureInfo.InvariantCulture, $"House {rulerPosition.House.Value}") : "unknown";
        sb.AppendLine($"**Ascendant:** {ascSign}, ruler {rulerPlanet} in {rulerHouse}");

        // Planetary hour
        string dayNight = candidate.PlanetaryHour.IsDaytime ? "Day" : "Night";
        sb.AppendLine(CultureInfo.InvariantCulture, $"**Planetary Hour:** {candidate.PlanetaryHour.RulingPlanet} ({dayNight} hour {candidate.PlanetaryHour.HourNumber})");
        sb.AppendLine();

        // Score Breakdown
        AppendScoreBreakdown(sb, candidate);

        // Strengths
        sb.AppendLine("### Strengths");
        sb.AppendLine();
        foreach (string strength in candidate.Strengths)
        {
            sb.AppendLine($"- {strength}");
        }
        sb.AppendLine();

        // Compromises
        sb.AppendLine("### Compromises");
        sb.AppendLine();
        if (candidate.Compromises.Count > 0)
        {
            foreach (string compromise in candidate.Compromises)
            {
                sb.AppendLine($"- {compromise}");
            }
        }
        else
        {
            sb.AppendLine("- None identified");
        }
        sb.AppendLine();

        // Natal Harmony
        AppendNatalHarmony(sb, candidate);

        // Candidate-to-natal declination aspects
        IReadOnlyList<DeclinationAspect> crossDecAspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(
                candidate.Chart.Positions, result.NatalChart.Positions);

        PositionFormatter.AppendCrossChartDeclinationTable(sb, crossDecAspects,
            "### Candidate-to-Natal Declination Aspects (\u00b11\u00b0 orb)",
            "CA", "Candidate", "CA Dec",
            "NA", "Natal", "Na Dec");

        // Chart Data
        AppendChartData(sb, candidate);
    }

    /// <summary>
    /// Builds the Moon applying-aspect phrase for the candidate summary line,
    /// e.g. "Moon sextile Jupiter (applying, 2°19′)". The orb is the canonical
    /// value from the candidate chart's aspect table when the aspect appears
    /// there (requirements C1/C3). For aspects wider than the table's
    /// per-aspect orb limits, the same quantity is computed from the chart's
    /// own positions so this line and the Moon Conditions block stay
    /// consistent; the Moon-condition evaluator's orb is the last resort.
    /// </summary>
    /// <param name="candidate">The candidate window being rendered.</param>
    /// <returns>The applying-aspect phrase, or "no applying aspects".</returns>
    private static string BuildMoonApplyingAspectDisplay(CandidateWindow candidate)
    {
        MoonCondition moon = candidate.Moon;

        if (moon.NextApplyingPlanet is null || moon.NextApplyingAspectType is null)
        {
            return moon.NextApplyingAspectDescription ?? "no applying aspects";
        }

        AspectResult? canonical = AspectService.FindAspectBetween(
            candidate.Chart.Aspects, "Moon", moon.NextApplyingPlanet,
            moon.NextApplyingAspectType.Value);

        double orb = canonical?.Orb
            ?? ChartMarkdownFormatter.ComputeMoonAspectOrb(
                candidate.Chart.Positions, moon.NextApplyingPlanet, moon.NextApplyingAspectType.Value)
            ?? moon.NextApplyingOrb
            ?? 0.0;
        string aspectName = moon.NextApplyingAspectType.Value.ToString().ToLowerInvariant();

        return $"Moon {aspectName} {moon.NextApplyingPlanet} (applying, {PositionFormatter.FormatOrb(orb)})";
    }

    /// <summary>
    /// Appends the score breakdown table for a candidate.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="candidate">The candidate window whose breakdown is rendered.</param>
    /// <remarks>
    /// Every dimension score is a whole number and every weight a whole percent, so each weighted cell and the
    /// Overall print exactly to two decimals at most (12.75, 22.5, 5; 82.25), and the cells add up to the Overall
    /// (branch Q, 1.3.3). Dimension scores print as whole numbers and weights as whole percents. Until 1.3.3 the cells
    /// and the Overall printed one decimal, rounding a midpoint to even: 82.25 printed "82.2" beside cells adding up to
    /// 82.3.
    /// </remarks>
    private static void AppendScoreBreakdown(StringBuilder sb, CandidateWindow candidate)
    {
        ScoreBreakdown b = candidate.ScoreBreakdown;
        ScoreWeights w = b.Weights;

        sb.AppendLine("### Score Breakdown");
        sb.AppendLine();
        sb.AppendLine("| Dimension | Score | Weight | Weighted |");
        sb.AppendLine("|-----------|-------|--------|----------|");
        sb.AppendLine(InvariantWeightFormat, $"| Moon | {b.MoonScore:F0} | {w.Moon:P0} | {b.MoonScore * w.Moon:0.##} |");
        sb.AppendLine(InvariantWeightFormat, $"| Ascendant | {b.AscendantScore:F0} | {w.Ascendant:P0} | {b.AscendantScore * w.Ascendant:0.##} |");
        sb.AppendLine(InvariantWeightFormat, $"| Significator | {b.SignificatorScore:F0} | {w.Significator:P0} | {b.SignificatorScore * w.Significator:0.##} |");
        sb.AppendLine(InvariantWeightFormat, $"| Natal Harmony | {b.NatalHarmonyScore:F0} | {w.NatalHarmony:P0} | {b.NatalHarmonyScore * w.NatalHarmony:0.##} |");
        sb.AppendLine(InvariantWeightFormat, $"| Malefic Avoidance | {b.MaleficAvoidanceScore:F0} | {w.MaleficAvoidance:P0} | {b.MaleficAvoidanceScore * w.MaleficAvoidance:0.##} |");
        sb.AppendLine(InvariantWeightFormat, $"| Mercury | {b.MercuryScore:F0} | {w.Mercury:P0} | {b.MercuryScore * w.Mercury:0.##} |");
        sb.AppendLine(InvariantWeightFormat, $"| Planetary Hour | {b.PlanetaryHourScore:F0} | {w.PlanetaryHour:P0} | {b.PlanetaryHourScore * w.PlanetaryHour:0.##} |");
        sb.AppendLine(CultureInfo.InvariantCulture, $"| **Overall** | | | **{b.OverallScore:0.##}** |");
        sb.AppendLine();
    }

    /// <summary>
    /// Maximum number of natal harmony aspects to display after major-aspect filtering.
    /// Keeps the output concise even for charts with many major inter-chart aspects.
    /// </summary>
    private const int MaxNatalHarmonyAspects = 15;

    /// <summary>
    /// Appends the natal harmony inter-chart aspects table.
    /// Filters to major aspects only (conjunction, sextile, square, trine, opposition)
    /// and caps at the tightest <see cref="MaxNatalHarmonyAspects"/>.
    /// </summary>
    private static void AppendNatalHarmony(StringBuilder sb, CandidateWindow candidate)
    {
        sb.AppendLine("### Natal Harmony (major aspects, tightest 15)");
        sb.AppendLine();

        List<InterChartAspect> filtered = FilterNatalHarmonyAspects(
            candidate.NatalHarmonyAspects);

        if (filtered.Count > 0)
        {
            sb.AppendLine("| Election Planet | Aspect | Natal Planet | Orb |");
            sb.AppendLine("|----------------|--------|--------------|-----|");

            foreach (InterChartAspect aspect in filtered)
            {
                sb.AppendLine(
                    $"| {aspect.Body1Name} | {aspect.Aspect.Abbreviation} | {aspect.Body2Name} | {PositionFormatter.FormatOrb(aspect.Orb)} |");
            }
        }
        else
        {
            sb.AppendLine("No significant inter-chart aspects found.");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Filters inter-chart aspects to major Ptolemaic aspects only
    /// (conjunction, opposition, trine, square, sextile), then caps
    /// to the tightest <see cref="MaxNatalHarmonyAspects"/> by orb.
    /// </summary>
    /// <param name="aspects">Unfiltered inter-chart aspects.</param>
    /// <returns>Filtered and capped list sorted by orb ascending.</returns>
    internal static List<InterChartAspect> FilterNatalHarmonyAspects(
        IReadOnlyList<InterChartAspect> aspects)
    {
        ArgumentNullException.ThrowIfNull(aspects);

        return aspects
            .Where(a => a.Aspect.Type.IsMajor())
            .OrderBy(a => a.Orb)
            .Take(MaxNatalHarmonyAspects)
            .ToList();
    }

    /// <summary>
    /// Appends the full chart data for a candidate by reusing
    /// <see cref="ChartMarkdownFormatter"/>.
    /// </summary>
    private static void AppendChartData(StringBuilder sb, CandidateWindow candidate)
    {
        sb.AppendLine("### Chart Data");
        sb.AppendLine();

        // Every orb in the embedded chart body is truncated D°MM′ from the shared formatter, as in
        // every chart export since branch O-D3 (1.3.4). Never the five minor points (R8): a candidate is cast
        // without them, and the rule is stated here too.
        string chartMarkdown = ChartMarkdownFormatter.ToMarkdown(candidate.Chart, includeTemperament: false, minorPoints: false);
        sb.Append(FormatChartBody(chartMarkdown));
        sb.AppendLine();
        sb.AppendLine();
    }

    /// <summary>
    /// Strips the H1 header and footer from the chart markdown,
    /// following the same pattern as TransitMarkdownFormatter.FormatNatalBody().
    /// </summary>
    private static string FormatChartBody(string fullMarkdown)
    {
        string[] lines = MarkdownLineSplitter.SplitLines(fullMarkdown);

        int startIndex = 0;
        if (lines.Length > 0 && lines[0].TrimEnd().StartsWith('#'))
            startIndex = 1;

        if (startIndex < lines.Length && string.IsNullOrWhiteSpace(lines[startIndex]))
            startIndex++;

        int endIndex = lines.Length - 1;
        while (endIndex > startIndex && string.IsNullOrWhiteSpace(lines[endIndex]))
            endIndex--;

        if (endIndex > startIndex && lines[endIndex].TrimEnd().StartsWith("*Generated by"))
            endIndex--;

        if (endIndex > startIndex && lines[endIndex].TrimEnd() == "---")
            endIndex--;

        StringBuilder result = new();
        for (int i = startIndex; i <= endIndex; i++)
        {
            result.Append(lines[i]);
            if (i < endIndex)
                result.Append('\n');
        }

        return result.ToString();
    }

    #endregion

    #region Offsets of repeated readings

    /// <summary>
    /// The offset that follows a candidate's printed time when its reading is one the clocks
    /// repeat: a space and the zone's abbreviation from the candidate chart's own label
    /// ("EDT"), or the offset where the zone's name is not a word ("UTC−3"); otherwise empty.
    /// </summary>
    /// <remarks>
    /// In an hour the clocks repeat, two candidates can print the same "1:00 AM", and until 1.3.3
    /// nothing in the heading said which; "1:00 AM" (EST) was listed above "1:30 AM" (EDT), which
    /// is half an hour before it.
    /// </remarks>
    /// <param name="candidate">The candidate whose heading is printed.</param>
    /// <returns>" EDT", " UTC−3" or <see cref="string.Empty"/>.</returns>
    private static string RepeatedReadingOffset(CandidateWindow candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return candidate.Chart.LocalTimeMapping == LocalTimeMapping.Repeated
            ? " " + ChartTimeFormatter.ZoneWord(candidate.Chart.ResolvedOffsetLabel)
            : string.Empty;
    }

    /// <summary>
    /// The time-zone rules a result's times are read on: those the natal chart was cast with, or the
    /// IANA data the app carries.
    /// </summary>
    /// <param name="result">The electional result.</param>
    /// <returns>The rules.</returns>
    private static ITimeZoneRules ZoneRules(ElectionalResult result) =>
        result.NatalChart.TimeZoneRules ?? new TzdbTimeZoneRules();

    #endregion

    #region Section 4: Disqualified Periods

    /// <summary>
    /// Appends the disqualified periods table, after a line naming the rules that disqualify: the
    /// Moon void of course always, and Mercury retrograde when the search avoids it (E2-3).
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="result">The electional result.</param>
    private static void AppendDisqualifiedPeriods(StringBuilder sb, ElectionalResult result)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Disqualified Periods");
        sb.AppendLine();
        sb.AppendLine(result.Input.AvoidMercuryRetrograde
            ? "Disqualifying rules: the Moon void of course and Mercury retrograde."
            : "Disqualifying rules: the Moon void of course (Mercury retrograde not avoided).");
        sb.AppendLine();

        if (result.DisqualifiedPeriods.Count > 0)
        {
            sb.AppendLine("| Start | End | Reason |");
            sb.AppendLine("|-------|-----|--------|");

            ITimeZoneRules rules = ZoneRules(result);
            string zone = result.Input.TimeZone;
            foreach (DisqualifiedPeriod period in result.DisqualifiedPeriods)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture, $"| {CalendarDateFormatter.Format(period.Start, "ddd, dd MMM yyyy h:mm tt")}{ChartTimeFormatter.RepeatedReadingOffset(period.Start, period.StartUtc, rules, zone)} | {CalendarDateFormatter.Format(period.End, "ddd, dd MMM yyyy h:mm tt")}{ChartTimeFormatter.RepeatedReadingOffset(period.End, period.EndUtc, rules, zone)} | {period.Reason} |");
            }
        }
        else
        {
            sb.AppendLine("No periods were disqualified.");
        }

        sb.AppendLine();
    }

    #endregion

    #region Section 5: Moon Void-of-Course Calendar

    /// <summary>
    /// Appends the Moon void-of-course calendar table.
    /// </summary>
    private static void AppendVocCalendar(StringBuilder sb, ElectionalResult result)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Moon Void-of-Course Calendar");
        sb.AppendLine();

        if (result.VoidOfCoursePeriods.Count > 0)
        {
            sb.AppendLine("| Start (Local) | End (Local) | Moon Sign |");
            sb.AppendLine("|---------------|-------------|-----------|");

            ITimeZoneRules rules = ZoneRules(result);
            string zone = result.Input.TimeZone;
            foreach (VoidOfCoursePeriod voc in result.VoidOfCoursePeriods)
            {
                sb.AppendLine(
                    CultureInfo.InvariantCulture, $"| {CalendarDateFormatter.Format(voc.StartLocal, "ddd, dd MMM yyyy h:mm tt")}{ChartTimeFormatter.RepeatedReadingOffset(voc.StartLocal, voc.StartUtc, rules, zone)} | {CalendarDateFormatter.Format(voc.EndLocal, "ddd, dd MMM yyyy h:mm tt")}{ChartTimeFormatter.RepeatedReadingOffset(voc.EndLocal, voc.EndUtc, rules, zone)} | {voc.MoonSign} |");
            }
        }
        else
        {
            sb.AppendLine("No void-of-course periods detected.");
        }

        sb.AppendLine();
    }

    #endregion

    #region Footer

    /// <summary>
    /// Appends the footer: the engine version, as the chart export prints it ("AstroCast
    /// 1.3.3 (SwissEph 2.10.03, tzdb 2026e)"), so the file can be dated to the version that
    /// made it (E2-9). The version is the natal chart's: the candidates are scored against it,
    /// by the same engine, in the same run. The header's Generated and Software lines are
    /// unchanged.
    /// </summary>
    private static void AppendFooter(StringBuilder sb, ElectionalResult result)
    {
        sb.AppendLine("---");
        sb.AppendLine($"*Engine: {result.NatalChart.EngineVersion}*");
    }

    #endregion
}
