using System.Globalization;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.MedicalAstrology;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="TransitResult"/> into a complete Markdown document for
/// AI interpretation. The document contains up to 8 sections: header, natal chart,
/// transiting planet positions/stations/ingresses, active transits, house overlays,
/// midpoint activations (the transit sections, when the transit scan ran), secondary
/// progressions (optional), and solar arc directions (optional). Since branch Modes
/// (1.4.0 item 2) the two technique sections give the positions and aspects at the
/// report's start and the dated hits in the period, and print one way for every
/// combination of techniques; since branch PrimaryExport (1.5.0) a fourth technique,
/// the primary directions exact in the period, prints after the solar arcs. When the natal
/// chart carries Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming, 1.6.0; R7),
/// the transits to them print in a section of their own after the Active Transits, and each
/// technique's aspects and hits to them in sub-sections after their twins.
/// </summary>
public static class TransitMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Formats the transit result as a complete Markdown document.
    /// </summary>
    /// <param name="result">The transit calculation result.</param>
    /// <param name="birthTimeSource">Birth time source (e.g., "Birth certificate").</param>
    /// <param name="interpretationContext">
    /// Optional free-text context for AI interpretation (e.g., "Focus on career themes.
    /// Client is considering a job change."). Rendered in the document header when provided.
    /// </param>
    /// <param name="timeProvider">
    /// Clock forwarded to <see cref="ChartMarkdownFormatter.ToMarkdown"/> for the
    /// embedded natal chart so its date-keyed profection / firdaria / zodiacal
    /// releasing sections resolve deterministically. Defaults to
    /// <see cref="TimeProvider.System"/> when <c>null</c>, preserving production
    /// behaviour; golden tests pass a fixed <c>FakeTimeProvider</c> (F-GR-2).
    /// </param>
    /// <returns>Complete Markdown string.</returns>
    public static string ToMarkdown(
        TransitResult result,
        string? birthTimeSource = null,
        string? interpretationContext = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        string effectiveSource = string.IsNullOrEmpty(birthTimeSource) ? "Not specified" : birthTimeSource;
        bool unknownBirthTime = string.Equals(birthTimeSource, "Unknown", StringComparison.OrdinalIgnoreCase);

        StringBuilder sb = new();

        AppendHeader(sb, result, effectiveSource, interpretationContext);
        AppendNatalChartSection(sb, result, timeProvider);

        // The transit sections only when the transit scan ran (branch Modes, 1.4.0 item 2)
        if (result.IncludesTransits)
        {
            AppendTransitingPlanetsSection(sb, result);
            AppendActiveTransitsSection(sb, result, unknownBirthTime);
            if (CarriesMinorPoints(result))
                AppendMinorPointTransitsSection(sb, result, unknownBirthTime);
            AppendHouseOverlaysSection(sb, result, unknownBirthTime);
            AppendMidpointActivationsSection(sb, result);
            AppendTransitDeclinationAspectsSection(sb, result);
            AppendTransitToTransitAspectsSection(sb, result);
        }

        if (result.Progressions is not null)
            AppendProgressionsSection(sb, result);

        if (result.SolarArcs is not null)
            AppendSolarArcsSection(sb, result);

        if (result.PrimaryDirections is not null)
            AppendPrimaryDirectionsSection(sb, result);

        AppendFooter(sb, result);

        return sb.ToString();
    }

    /// <summary>
    /// Save a transit result as Markdown to a file.
    /// </summary>
    /// <param name="result">The transit calculation result to save.</param>
    /// <param name="filePath">Destination file path for the Markdown output.</param>
    /// <param name="birthTimeSource">Birth time source.</param>
    /// <param name="interpretationContext">Optional free-text context for AI interpretation.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        TransitResult result,
        string filePath,
        string? birthTimeSource = null,
        string? interpretationContext = null,
        CancellationToken ct = default)
    {
        string md = ToMarkdown(result, birthTimeSource, interpretationContext);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }

    #region Header

    /// <summary>
    /// Appends the document header with chart metadata and optional interpretation context.
    /// </summary>
    private static void AppendHeader(
        StringBuilder sb, TransitResult result, string birthTimeSource, string? interpretationContext)
    {
        Models.ChartInput input = result.NatalChart.Input;

        // The title names the techniques the result holds (branch Modes, 1.4.0 item 2): the transit
        // report whenever the scan ran, else the solar arc, progressions or both report
        sb.AppendLine($"# {TransitReportNaming.Title(result)}");
        sb.AppendLine();
        sb.AppendLine($"**Name:** {input.Name}");
        // An explicit offset overrides the zone's rules, so the birth wall clock is labelled
        // with that offset, as the chart export's Date/Time line labels it, never with the zone.
        string natalClock = input.UseExplicitOffset && input.UtcOffsetHours.HasValue
            ? result.NatalChart.ResolvedOffsetLabel
            : input.TimeZone;
        sb.AppendLine(Inv, $"**Natal Date:** {input.LocalDateTime:yyyy-MM-dd}, {input.LocalDateTime:HH:mm:ss} {natalClock}, {input.LocationName}");
        sb.AppendLine($"**Birth Time Source:** {birthTimeSource}");
        sb.AppendLine($"**Report Period:** {FormatDate(result.ReportStartDate)} to {FormatDate(result.ReportEndDate)}");
        if (CalendarDateFormatter.IsJulian(result.NatalChart.UtcDateTime) || result.ReportStartDate < CalendarDateFormatter.GregorianCalendarStartDate)
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine);
        sb.AppendLine($"**Aspects:** {FormatAspectSets(result)}");

        if (!string.IsNullOrWhiteSpace(interpretationContext))
        {
            sb.AppendLine($"**Interpretation Context:** {interpretationContext.Trim()}");
        }

        // The techniques the result holds, in the order Transits, Solar Arcs, Secondary Progressions
        // (branch Modes, 1.4.0 item 2; until 1.4.0 a "Progressions: Included" line)
        List<string> techniques = [];
        if (result.IncludesTransits) techniques.Add("Transits");
        if (result.SolarArcs is not null) techniques.Add("Solar Arcs");
        if (result.Progressions is not null) techniques.Add("Secondary Progressions");
        if (result.PrimaryDirections is not null) techniques.Add("Primary Directions");
        sb.AppendLine($"**Techniques:** {string.Join(", ", techniques)}");

        sb.AppendLine(Inv, $"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"**Software:** AstroCast \u2014 Swiss Ephemeris, Tropical, {result.NatalChart.HouseSystemName}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    /// <summary>
    /// The header's Aspects line after its label. With the transits it is the transits' set, "Major"
    /// or "Major + Minor"; without them it names each technique's set (Steve, 2026-10-17, on
    /// ModesPanel's Finding 1), the solar arcs' with the semisquare and sesquiquadrate, the
    /// progressions' the five majors, and since branch PrimaryExport (1.5.0) the primary directions'
    /// majors, joined by "; " with the first set capitalised.
    /// </summary>
    /// <param name="result">The transit calculation result.</param>
    /// <returns>The aspect sets the report's techniques use.</returns>
    private static string FormatAspectSets(TransitResult result)
    {
        if (result.IncludesTransits)
            return result.IncludeMinorAspects ? "Major + Minor" : "Major";

        List<string> sets = [];
        if (result.SolarArcs is not null) sets.Add("solar arcs: majors, semisquare, sesquiquadrate");
        if (result.Progressions is not null) sets.Add("progressions: majors");
        if (result.PrimaryDirections is not null) sets.Add("primary directions: majors");

        string joined = string.Join("; ", sets);
        return joined.Length == 0 ? joined : char.ToUpperInvariant(joined[0]) + joined[1..];
    }

    #endregion

    #region Section 1: Natal Chart

    /// <summary>
    /// Appends the full natal chart by delegating to ChartMarkdownFormatter.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="result">The transit result containing the natal chart.</param>
    /// <param name="timeProvider">
    /// Clock forwarded to <see cref="ChartMarkdownFormatter.ToMarkdown"/> so the
    /// natal time-lord sections are date-deterministic under test.
    /// </param>
    private static void AppendNatalChartSection(
        StringBuilder sb, TransitResult result, TimeProvider? timeProvider)
    {
        sb.AppendLine("## Natal Chart");
        sb.AppendLine();

        string natalMarkdown = ChartMarkdownFormatter.ToMarkdown(result.NatalChart, timeProvider: timeProvider);
        sb.Append(FormatNatalBody(natalMarkdown));
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    /// <summary>
    /// Strips the H1 header and footer from the natal chart markdown,
    /// following the same pattern as SynastryMarkdownFormatter.FormatNatalSection().
    /// Internal since branch PrimaryExport (1.5.0): <see cref="PrimaryDirectionMarkdownFormatter"/>
    /// embeds the natal chart the same way.
    /// </summary>
    /// <param name="fullMarkdown">The chart's whole Markdown export.</param>
    /// <returns>The export without its title and footer.</returns>
    internal static string FormatNatalBody(string fullMarkdown)
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

        StringBuilder sb = new();
        for (int i = startIndex; i <= endIndex; i++)
        {
            sb.Append(lines[i]);
            if (i < endIndex)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    #endregion

    #region Section 2: Transiting Planets + Stations + Ingresses

    /// <summary>
    /// Appends transiting planet positions at report start, stations, and ingresses.
    /// </summary>
    private static void AppendTransitingPlanetsSection(StringBuilder sb, TransitResult result)
    {
        sb.AppendLine("## Transiting Planets");
        sb.AppendLine();

        // Positions at report start
        sb.AppendLine($"### At Report Start ({FormatDate(result.ReportStartDate)})");
        sb.AppendLine();
        sb.AppendLine("| Planet | Sign | Degree | Retrograde | Daily Motion |");
        sb.AppendLine("|--------|------|--------|------------|-------------|");

        foreach (TransitBody body in GetSortedBodies(result))
        {
            if (result.TransitPositions.TryGetValue(body, out TransitPlanetPosition? pos))
            {
                sb.AppendLine(
                    Inv, $"| {FormatPlanetName(body)} | {pos.Sign} | {pos.Degrees}\u00b0{pos.Minutes:D2}' | {(pos.IsRetrograde ? "Yes" : "No")} | {FormatDailyMotion(pos.DailyMotion)} |");
            }
        }

        sb.AppendLine();

        // Transit Dignities
        AppendTransitDignities(sb, result);

        // Stations
        sb.AppendLine("### Stations During Report Period");
        sb.AppendLine();

        if (result.Stations.Count == 0)
        {
            sb.AppendLine("None during this period.");
        }
        else
        {
            sb.AppendLine("| Planet | Station Type | Date | Sign | Degree |");
            sb.AppendLine("|--------|-------------|------|------|--------|");

            foreach (TransitStation station in result.Stations)
            {
                sb.AppendLine(
                    Inv, $"| {FormatPlanetName(station.Planet)} | {station.StationType} | {CalendarDateFormatter.Format(station.Date, "yyyy-MM-dd")} | {station.Sign} | {station.Degrees}\u00b0{station.Minutes:D2}' |");
            }
        }

        sb.AppendLine();

        // Ingresses (only if present)
        if (result.Ingresses.Count > 0)
        {
            sb.AppendLine("### Ingresses During Report Period");
            sb.AppendLine();
            sb.AppendLine("| Planet | From | To | Date |");
            sb.AppendLine("|--------|------|----|------|");

            foreach (TransitIngress ingress in result.Ingresses)
            {
                sb.AppendLine(
                    $"| {FormatPlanetName(ingress.Planet)} | {ingress.FromSign} | {ingress.ToSign} | {CalendarDateFormatter.Format(ingress.Date, "yyyy-MM-dd")} |");
            }

            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends the essential dignities table for transiting planets.
    /// </summary>
    private static void AppendTransitDignities(StringBuilder sb, TransitResult result)
    {
        sb.AppendLine("### Transit Dignities");
        sb.AppendLine();
        sb.AppendLine("| Planet | Sign | Dignity | Score | Dispositor |");
        sb.AppendLine("|--------|------|---------|-------|------------|");

        DignityService dignityService = new();

        foreach (TransitBody body in GetSortedBodies(result))
        {
            if (result.TransitPositions.TryGetValue(body, out TransitPlanetPosition? pos))
            {
                string planetName = FormatPlanetName(body);
                if (Enum.TryParse<ZodiacSign>(pos.Sign, ignoreCase: true, out ZodiacSign sign))
                {
                    DignityInfo dignity = dignityService.GetDignity(planetName, sign);
                    string scoreStr = dignity.Score > 0 ? string.Create(Inv, $"+{dignity.Score}") : dignity.Score.ToString(Inv);
                    sb.AppendLine(
                        $"| {planetName} | {pos.Sign} | {dignity.Label} | {scoreStr} | {dignity.Dispositor} |");
                }
            }
        }

        sb.AppendLine();
    }

    #endregion

    #region Section 3: Active Transits

    /// <summary>
    /// Appends active transit-to-natal aspects with lifecycle events.
    /// </summary>
    private static void AppendActiveTransitsSection(
        StringBuilder sb, TransitResult result, bool unknownBirthTime)
    {
        sb.AppendLine("## Active Transits");
        sb.AppendLine();

        if (result.ActiveTransits.Count == 0)
        {
            sb.AppendLine("No major outer planet transits are active during this period.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            return;
        }

        foreach (ActiveTransit transit in result.ActiveTransits)
        {
            AppendTransitBlock(sb, transit, unknownBirthTime, includeMedical: true);
        }

        sb.AppendLine("*Medical correspondences are traditional associations for educational purposes. Not medical advice.*");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends one transit's block: the sub-heading (### TR SA cnj NA SU), the transit planet and natal target
    /// at report start, the orb and its direction, the lifecycle events, the transit house, the medical
    /// correspondences when asked, and a blank line. One writer for the Active Transits section and, without
    /// the medical lines (R8), the section of the transits to the five (branch AsteroidsTiming, 1.6.0).
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="transit">The transit.</param>
    /// <param name="unknownBirthTime">Whether the birth time is unknown, for the angle caveat.</param>
    /// <param name="includeMedical">Whether to print the two medical lines.</param>
    private static void AppendTransitBlock(
        StringBuilder sb, ActiveTransit transit, bool unknownBirthTime, bool includeMedical)
    {
        // Subsection heading: ### TR SA cnj NA SU
        sb.AppendLine(
            $"### TR {FormatPlanetSymbol(transit.TransitPlanet)} {transit.AspectAbbreviation} NA {transit.NatalTargetSymbol}");
        sb.AppendLine();

        // Transit planet position
        string transitDegMin = FormatDegMinInSign(transit.TransitLongitudeAtStart);
        sb.AppendLine(
            $"- **Transit planet:** {FormatPlanetName(transit.TransitPlanet)} at {transitDegMin} {transit.TransitSignAtStart} (at report start)");

        // Natal target position
        string natalDegMin = FormatDegMinInSign(transit.NatalLongitude);
        sb.AppendLine(
            $"- **Natal target:** {transit.NatalTarget} at {natalDegMin} {transit.NatalSign} (natal {FormatOrdinal(transit.NatalHouseNumber)} house)");

        // Birth time caveat for ASC/MC targets
        if (unknownBirthTime && (transit.NatalTargetSymbol == "ASC" || transit.NatalTargetSymbol == "MC"))
        {
            sb.AppendLine("  *(Angle position approximate due to uncertain birth time)*");
        }

        // Orb at report start
        string orbDegMin = PositionFormatter.FormatOrb(transit.OrbAtStartDegrees);
        sb.AppendLine($"- **Orb at report start:** {orbDegMin}, {transit.OrbDirection}");

        // Lifecycle events
        foreach (TransitLifecycleEvent evt in transit.Lifecycle)
        {
            AppendLifecycleEvent(sb, evt, transit.TransitPlanet);
        }

        // Transit house
        sb.AppendLine($"- **Transit house:** {FormatOrdinal(transit.TransitHouseNumber)} (natal)");

        // Medical correspondences
        if (includeMedical)
            AppendTransitMedicalData(sb, transit);

        sb.AppendLine();
    }

    /// <summary>
    /// Whether the natal chart carries Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming, 1.6.0):
    /// the condition for the section of the transits to them and the technique sub-sections to them.
    /// </summary>
    /// <param name="result">The transit calculation result.</param>
    /// <returns>Whether the natal chart carries the five.</returns>
    private static bool CarriesMinorPoints(TransitResult result) => result.NatalChart.MinorPoints.Count > 0;

    /// <summary>
    /// Appends the transits to the natal Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming, 1.6.0;
    /// R7): the opening line, then one block per transit as the Active Transits section prints them, without
    /// the medical lines (R8), or the none sentence. Printed only when the natal chart carries the five.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="result">The transit calculation result.</param>
    /// <param name="unknownBirthTime">Whether the birth time is unknown.</param>
    private static void AppendMinorPointTransitsSection(
        StringBuilder sb, TransitResult result, bool unknownBirthTime)
    {
        sb.AppendLine("## Transits to the Asteroids and Lilith");
        sb.AppendLine();
        sb.AppendLine(
            "Transits to the natal Ceres, Pallas, Juno, Vesta and Lilith (Black Moon Lilith, the mean lunar apogee): "
            + "the five Ptolemaic aspects at each transiting body's major orbs.");
        sb.AppendLine();

        if (result.MinorPointTransits.Count == 0)
        {
            sb.AppendLine("No transit to the asteroids or Lilith is active during this period.");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            return;
        }

        foreach (ActiveTransit transit in result.MinorPointTransits)
        {
            AppendTransitBlock(sb, transit, unknownBirthTime, includeMedical: false);
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends a single lifecycle event line.
    /// </summary>
    /// <remarks>
    /// Every lifecycle event is a refined instant: an exact pass prints its instant to the second,
    /// and an entry, exit or station prints its instant to the minute (<see cref="FormatEventDateTime"/>).
    /// </remarks>
    private static void AppendLifecycleEvent(
        StringBuilder sb, TransitLifecycleEvent evt, TransitBody planet)
    {
        string boundaryNote = "";
        if (evt.BeforeReportPeriod)
            boundaryNote = " (before report period)";
        else if (evt.BeyondReportPeriod)
            boundaryNote = " (beyond report period)";

        switch (evt.EventType)
        {
            case TransitLifecycleEventType.Entry:
                sb.AppendLine($"- **Entry:** {FormatEventDateTime(evt)}{boundaryNote}");
                break;

            case TransitLifecycleEventType.Exact:
                string orbNote = FormatResidualOrb(evt.ExactOrbDegrees);
                sb.AppendLine(Inv, $"- **Exact pass {evt.PassNumber}:** {FormatEventDateTime(evt)}{orbNote}{boundaryNote}");
                break;

            case TransitLifecycleEventType.Station:
                sb.AppendLine(
                    $"- **Station:** {FormatEventDateTime(evt)}, {FormatPlanetName(planet)} stations {evt.StationType}{boundaryNote}");
                break;

            case TransitLifecycleEventType.Exit:
                sb.AppendLine($"- **Exit:** {FormatEventDateTime(evt)}{boundaryNote}");
                break;
        }
    }

    /// <summary>
    /// Appends medical body region correspondences for a transit's transiting
    /// planet and natal target.
    /// </summary>
    private static void AppendTransitMedicalData(StringBuilder sb, ActiveTransit transit)
    {
        // Transit planet medical data
        CelestialBodyId? transitBodyId = MapTransitBodyToCelestialBody(transit.TransitPlanet);
        if (transitBodyId.HasValue)
        {
            IReadOnlyList<BodyRegion> regions = PlanetaryHealthMap.GetBodyRegions(transitBodyId.Value);
            if (regions.Count > 0)
            {
                string regionNames = string.Join("; ", regions.Select(r => r.Name));
                sb.AppendLine($"- **Medical (transit planet):** {FormatPlanetName(transit.TransitPlanet)} body regions: {regionNames}");
            }
            else
            {
                sb.AppendLine($"- **Medical (transit planet):** {FormatPlanetName(transit.TransitPlanet)} \u2014 no traditional medical correspondences");
            }
        }
        else
        {
            sb.AppendLine($"- **Medical (transit planet):** {FormatPlanetName(transit.TransitPlanet)} \u2014 no traditional medical correspondences");
        }

        // Natal target medical data
        CelestialBodyId? natalBodyId = MapNatalSymbolToCelestialBody(transit.NatalTargetSymbol);
        if (natalBodyId.HasValue)
        {
            IReadOnlyList<BodyRegion> regions = PlanetaryHealthMap.GetBodyRegions(natalBodyId.Value);
            if (regions.Count > 0)
            {
                string regionNames = string.Join("; ", regions.Select(r => r.Name));
                sb.AppendLine($"- **Medical (natal target):** {transit.NatalTarget} body regions: {regionNames}");
            }
            else
            {
                sb.AppendLine($"- **Medical (natal target):** {transit.NatalTarget} \u2014 no traditional medical correspondences");
            }
        }
        else
        {
            // For ASC/MC, use the sign's zodiac body regions instead
            ZodiacSign? natalSign = ParseZodiacSign(transit.NatalSign);
            if (natalSign.HasValue)
            {
                IReadOnlyList<BodyRegion> regions = ZodiacBodyMap.GetBodyRegions(natalSign.Value);
                string regionNames = string.Join("; ", regions.Select(r => r.Name));
                sb.AppendLine($"- **Medical (natal target):** {transit.NatalTarget} in {transit.NatalSign} \u2014 sign regions: {regionNames}");
            }
            else
            {
                sb.AppendLine($"- **Medical (natal target):** {transit.NatalTarget} \u2014 no traditional medical correspondences");
            }
        }
    }

    /// <summary>
    /// Maps a <see cref="TransitBody"/> enum to its corresponding
    /// <see cref="CelestialBodyId"/> for medical correspondence lookup.
    /// Returns null for bodies with no medical correspondences.
    /// </summary>
    private static CelestialBodyId? MapTransitBodyToCelestialBody(TransitBody body) => body switch
    {
        TransitBody.Mars => CelestialBodyId.Mars,
        TransitBody.Jupiter => CelestialBodyId.Jupiter,
        TransitBody.Saturn => CelestialBodyId.Saturn,
        TransitBody.Uranus => CelestialBodyId.Uranus,
        TransitBody.Neptune => CelestialBodyId.Neptune,
        TransitBody.Pluto => CelestialBodyId.Pluto,
        TransitBody.Chiron => CelestialBodyId.Chiron,
        TransitBody.NorthNode => CelestialBodyId.TrueNode,
        TransitBody.Mercury => CelestialBodyId.Mercury,
        TransitBody.Venus => CelestialBodyId.Venus,
        _ => null
    };

    /// <summary>
    /// Maps a natal target symbol (e.g., "SU", "MO") to its corresponding
    /// <see cref="CelestialBodyId"/>. Returns null for angles (ASC, MC) and
    /// bodies without direct planet correspondences.
    /// </summary>
    private static CelestialBodyId? MapNatalSymbolToCelestialBody(string symbol) => symbol switch
    {
        "SU" => CelestialBodyId.Sun,
        "MO" => CelestialBodyId.Moon,
        "ME" => CelestialBodyId.Mercury,
        "VE" => CelestialBodyId.Venus,
        "MA" => CelestialBodyId.Mars,
        "JU" => CelestialBodyId.Jupiter,
        "SA" => CelestialBodyId.Saturn,
        "UR" => CelestialBodyId.Uranus,
        "NE" => CelestialBodyId.Neptune,
        "PL" => CelestialBodyId.Pluto,
        "CH" => CelestialBodyId.Chiron,
        "NN" => CelestialBodyId.TrueNode,
        _ => null // ASC, MC, etc.
    };

    /// <summary>
    /// Parses a zodiac sign name string into a <see cref="ZodiacSign"/> enum.
    /// </summary>
    private static ZodiacSign? ParseZodiacSign(string signName) =>
        Enum.TryParse<ZodiacSign>(signName, ignoreCase: true, out ZodiacSign sign) ? sign : null;

    #endregion

    #region Section 4: House Overlays

    /// <summary>
    /// Appends the transit house overlays table (12 rows, one per natal house).
    /// </summary>
    private static void AppendHouseOverlaysSection(
        StringBuilder sb, TransitResult result, bool unknownBirthTime)
    {
        sb.AppendLine($"## Transit House Overlays (at {CalendarDateFormatter.Format(result.MidpointDate, "yyyy-MM-dd")})");
        sb.AppendLine();
        sb.AppendLine("| Natal House | Transiting Planets Present |");
        sb.AppendLine("|-------------|--------------------------|");

        // Build a lookup: house number → list of planet names
        Dictionary<int, List<string>> houseMap = [];
        for (int i = 1; i <= 12; i++)
            houseMap[i] = [];

        foreach (TransitHouseOverlay overlay in result.HouseOverlays)
        {
            string name = FormatPlanetName(overlay.Planet);
            if (overlay.IsIntercepted)
                name += " (intercepted)";

            if (houseMap.TryGetValue(overlay.HouseNumber, out List<string>? planets))
                planets.Add(name);
        }

        for (int house = 1; house <= 12; house++)
        {
            string planets = houseMap[house].Count > 0
                ? string.Join(", ", houseMap[house])
                : "\u2014";
            sb.AppendLine($"| {FormatOrdinal(house)} | {planets} |");
        }

        sb.AppendLine();

        if (unknownBirthTime)
        {
            sb.AppendLine("*House positions are approximate due to uncertain birth time.*");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    #endregion

    #region Section 5: Midpoint Activations

    /// <summary>
    /// Appends transit midpoint activations on the 45° dial.
    /// </summary>
    private static void AppendMidpointActivationsSection(StringBuilder sb, TransitResult result)
    {
        sb.AppendLine("## Transit Midpoint Activations (45\u00b0 Dial)");
        sb.AppendLine();

        if (result.MidpointActivations.Count == 0)
        {
            sb.AppendLine("No transit midpoint activations during this period.");
            sb.AppendLine();
            return;
        }

        foreach (TransitMidpointActivation activation in result.MidpointActivations)
        {
            string priorityTag = activation.IsPriority ? " (priority)" : "";
            sb.AppendLine(
                $"### TR {FormatPlanetName(activation.TransitPlanet)} = NA {activation.MidpointBody1}/{activation.MidpointBody2}{priorityTag}");
            sb.AppendLine();

            string transitDial = FormatDegMin(activation.TransitDial45Position);
            string natalDial = FormatDegMin(activation.NatalMidpointDial45Position);
            string orb = PositionFormatter.FormatOrb(activation.OrbAtStartDegrees);

            sb.AppendLine($"- **Transit planet:** {FormatPlanetName(activation.TransitPlanet)} at {transitDial} (45\u00b0 dial)");
            sb.AppendLine($"- **Natal midpoint:** {activation.MidpointBody1}/{activation.MidpointBody2} at {natalDial} (45\u00b0 dial)");
            sb.AppendLine($"- **Orb at report start:** {orb}");

            // Lifecycle events (Entry, Exact, Exit)
            foreach (TransitLifecycleEvent evt in activation.Lifecycle)
            {
                string boundaryNote = "";
                if (evt.BeforeReportPeriod)
                    boundaryNote = " (before report period)";
                else if (evt.BeyondReportPeriod)
                    boundaryNote = " (beyond report period)";

                switch (evt.EventType)
                {
                    case TransitLifecycleEventType.Entry:
                        sb.AppendLine($"- **Entry:** {FormatEventDateTime(evt)}{boundaryNote}");
                        break;
                    case TransitLifecycleEventType.Exact:
                        string midOrbNote = FormatResidualOrb(evt.ExactOrbDegrees);
                        sb.AppendLine($"- **Exact:** {FormatEventDateTime(evt)}{midOrbNote}{boundaryNote}");
                        break;
                    case TransitLifecycleEventType.Exit:
                        sb.AppendLine($"- **Exit:** {FormatEventDateTime(evt)}{boundaryNote}");
                        break;
                }
            }

            sb.AppendLine();
        }
    }

    #endregion

    #region Section 6: Transit-to-Natal Declination Aspects

    /// <summary>
    /// Appends the transit-to-natal declination aspects section showing parallels
    /// and contraparallels between transiting planets (at exact pass times) and
    /// natal planets. Only emitted when at least one aspect is found.
    /// </summary>
    private static void AppendTransitDeclinationAspectsSection(StringBuilder sb, TransitResult result)
    {
        if (result.TransitDeclinationAspects.Count == 0)
            return;

        sb.AppendLine("## Transit-to-Natal Declination Aspects (\u00b11\u00b0 orb)");
        sb.AppendLine();
        sb.AppendLine("| Transit | Tr Dec | Aspect | Natal | Na Dec | Orb | Date |");
        sb.AppendLine("|---------|--------|--------|-------|--------|-----|------|");

        foreach (TransitDeclinationAspect tda in result.TransitDeclinationAspects)
        {
            string trDec = PositionFormatter.FormatDeclination(tda.TransitDeclination);
            string naDec = PositionFormatter.FormatDeclination(tda.NatalDeclination);
            sb.AppendLine(
                Inv, $"| TR {tda.TransitPlanetName} | {trDec} | {tda.Abbreviation} | NA {tda.NatalPlanetName} | {naDec} | {PositionFormatter.FormatOrb(tda.Orb)} | {CalendarDateFormatter.Format(tda.Date, "yyyy-MM-dd")} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    #endregion

    #region Section 6b: Transit-to-Transit Aspects

    /// <summary>
    /// Appends the transit-to-transit aspects section showing major Ptolemaic
    /// aspects between transiting planets that reach exact during the report
    /// period. Only emitted when at least one aspect is found.
    /// </summary>
    private static void AppendTransitToTransitAspectsSection(StringBuilder sb, TransitResult result)
    {
        if (result.TransitToTransitAspects.Count == 0)
            return;

        sb.AppendLine("## Transit-to-Transit Aspects");
        sb.AppendLine();
        sb.AppendLine("Major aspects between transiting planets reaching exact during the report period.");
        sb.AppendLine();
        sb.AppendLine("| Date (UTC) | Planet 1 | Position | Aspect | Planet 2 | Position | Orb |");
        sb.AppendLine("|------------|----------|----------|--------|----------|----------|-----|");

        foreach (TransitToTransitAspect aspect in result.TransitToTransitAspects)
        {
            string orbDisplay = PositionFormatter.FormatOrb(aspect.ExactOrbDegrees);

            sb.AppendLine(
                Inv, $"| {CalendarDateFormatter.Minutes(aspect.ExactDateTimeUtc)} | " +
                $"{aspect.Planet1Name} | {aspect.Planet1Position} | " +
                $"{aspect.AspectAbbreviation} | " +
                $"{aspect.Planet2Name} | {aspect.Planet2Position} | " +
                $"{orbDisplay} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    #endregion

    #region Section 7: Secondary Progressions

    /// <summary>
    /// Sort order for progressed bodies in the aspects table.
    /// </summary>
    private static readonly Dictionary<string, int> s_progressedBodyOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Moon"] = 0, ["Sun"] = 1, ["Mercury"] = 2, ["Venus"] = 3,
        ["Mars"] = 4, ["ASC"] = 5, ["MC"] = 6,
    };

    /// <summary>
    /// Appends the secondary progressions section (Section 7): the positions, Moon and aspects at the
    /// report's start, the dated hits in the period, then the sign changes and stations about the start
    /// (branch Modes, 1.4.0 item 2; until 1.4.0 a snapshot at the report's midpoint, named in the heading).
    /// </summary>
    private static void AppendProgressionsSection(StringBuilder sb, TransitResult result)
    {
        if (result.Progressions is not ProgressionResult prog)
            return;

        string start = FormatDate(result.ReportStartDate);

        sb.AppendLine("## Secondary Progressions");
        sb.AppendLine();
        sb.AppendLine("**Method:** Day-for-a-year (secondary)");
        sb.AppendLine($"**Progressed Date:** {CalendarDateFormatter.Minutes(prog.ProgressedDate)} UT");
        sb.AppendLine($"**Solar Arc:** {FormatDegMin(prog.SolarArcValue)}");
        sb.AppendLine();

        // Progressed Positions table
        sb.AppendLine($"### Progressed Positions (at report start, {start})");
        sb.AppendLine();
        sb.AppendLine("| Body | Natal Position | Progressed Position | House | Movement | Dir |");
        sb.AppendLine("|------|---------------|---------------------|-------|----------|-----|");

        foreach (ProgressedPosition pos in prog.Positions)
        {
            string house = pos.NatalHouse.HasValue ? pos.NatalHouse.Value.ToString(Inv) : "\u2014";
            string movement = FormatDegMin(pos.MovementFromNatal);
            string dir = pos.DirectionCode ?? "\u2014";

            sb.AppendLine(
                $"| {pos.Name} | {pos.NatalFormattedPosition} | {pos.ProgressedFormattedPosition} | {house} | {movement} | {dir} |");
        }

        sb.AppendLine();

        // Progressed Moon Detail
        AppendProgressedMoonDetail(sb, prog.MoonInfo, start);

        // Progressed-to-Natal Aspects at the start, then those to the five (branch AsteroidsTiming, 1.6.0)
        AppendProgressedAspects(sb, "### Progressed-to-Natal Aspects (major, 1\u00b0 orb, at report start)", prog.Aspects);
        if (CarriesMinorPoints(result))
        {
            AppendProgressedAspects(
                sb, "### Progressed-to-Natal Aspects: Asteroids and Lilith (major, 1\u00b0 orb, at report start)",
                prog.MinorPointAspects);
        }

        // The dated hits in the period, then those to the five
        AppendHitsTable(
            sb,
            "### Progressed Hits in the Report Period",
            "No progressed hit becomes exact in the report period.",
            ProgressedHitRows(result.ProgressedHits));
        if (CarriesMinorPoints(result))
        {
            AppendHitsTable(
                sb,
                "### Progressed Hits to the Asteroids and Lilith",
                "No progressed hit becomes exact in the report period.",
                ProgressedHitRows(result.MinorPointProgressedHits));
        }

        // Sign Changes (only if non-empty)
        if (prog.SignChanges.Count > 0)
        {
            AppendProgressedSignChanges(sb, prog.SignChanges);
        }

        // Stations (only if non-empty)
        if (prog.Stations.Count > 0)
        {
            AppendProgressedStations(sb, prog.Stations);
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends the progressed Moon detail subsection. When no sign change falls within the
    /// progression service's scan, the Next Sign Change line says how far it looked (E2-3).
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="moon">The progressed Moon.</param>
    /// <param name="start">The report's start as printed, from which the next sign change is sought.</param>
    private static void AppendProgressedMoonDetail(StringBuilder sb, ProgressedMoonInfo moon, string start)
    {
        sb.AppendLine("### Progressed Moon");
        sb.AppendLine();
        sb.AppendLine(Inv, $"- **Position:** {moon.Degrees}\u00b0{moon.Minutes:D2}' {moon.Sign} (natal {FormatOrdinal(moon.NatalHouse)} house)");
        sb.AppendLine(Inv, $"- **Progressed Lunar Phase:** {moon.PhaseName} ({moon.PhaseAngle:F0}\u00b0)");

        if (moon.NextSignChangeDate.HasValue && moon.NextSign is not null)
        {
            sb.AppendLine($"- **Next Sign Change:** {CalendarDateFormatter.Format(moon.NextSignChangeDate.Value, "yyyy-MM-dd")} into {moon.NextSign}");
        }
        else
        {
            sb.AppendLine(Inv, $"- **Next Sign Change:** none within {ProgressionService.NextSignChangeScanDays} days after {start}");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// The rows of a progressed hits table (<see cref="AppendHitsTable"/>): the hits in time order, each as its
    /// instant, its notation (P Moon cnj NA Moon) and its aspect's name.
    /// </summary>
    /// <param name="hits">The hits.</param>
    /// <returns>The rows.</returns>
    private static IEnumerable<(DateTime InstantUtc, string Hit, string Aspect)> ProgressedHitRows(IReadOnlyList<ProgressedHit> hits) =>
        hits
            .OrderBy(h => h.JulianDayUt)
            .Select(h => (h.InstantUtc, $"P {h.ProgressedPoint} {h.AspectAbbreviation} NA {h.NatalPoint}", h.AspectName));

    /// <summary>
    /// Appends a progressed-to-natal aspects subsection: the aspects in orb at the report's start, under the
    /// heading given, or the none sentence. One writer for the chart's own aspects and, since branch
    /// AsteroidsTiming (1.6.0), the aspects to the five.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="heading">The sub-section's heading line.</param>
    /// <param name="aspects">The aspects.</param>
    private static void AppendProgressedAspects(
        StringBuilder sb, string heading, IReadOnlyList<ProgressedAspect> aspects)
    {
        sb.AppendLine(heading);
        sb.AppendLine();

        if (aspects.Count == 0)
        {
            sb.AppendLine("No major progressed-to-natal aspects within 1\u00b0 orb.");
            sb.AppendLine();
            return;
        }

        // Sort: Moon first, then Sun, Mercury, Venus, Mars, ASC, MC.
        // Within each body, sort by orb ascending.
        List<ProgressedAspect> sorted = [.. aspects];
        sorted.Sort((a, b) =>
        {
            int orderA = GetProgressedBodyOrder(a.ProgressedBody);
            int orderB = GetProgressedBodyOrder(b.ProgressedBody);
            if (orderA != orderB) return orderA.CompareTo(orderB);
            return a.OrbDegrees.CompareTo(b.OrbDegrees);
        });

        sb.AppendLine("| Progressed | Aspect | Natal | Orb | Direction |");
        sb.AppendLine("|------------|--------|-------|-----|-----------|");

        foreach (ProgressedAspect aspect in sorted)
        {
            string orb = PositionFormatter.FormatOrb(aspect.OrbDegrees);
            string direction = char.ToUpper(aspect.Direction[0]) + aspect.Direction[1..];

            sb.AppendLine(
                $"| {aspect.ProgressedBody} | {aspect.AspectAbbreviation} | {aspect.NatalTarget} | {orb} | {direction} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Gets the sort order for a progressed body name (strips "P " prefix).
    /// </summary>
    private static int GetProgressedBodyOrder(string progressedBody)
    {
        string name = progressedBody.StartsWith("P ", StringComparison.Ordinal)
            ? progressedBody[2..] : progressedBody;

        return s_progressedBodyOrder.TryGetValue(name, out int order) ? order : 99;
    }

    /// <summary>
    /// Appends the progressed sign changes subsection.
    /// </summary>
    private static void AppendProgressedSignChanges(
        StringBuilder sb, IReadOnlyList<ProgressedSignChange> changes)
    {
        sb.AppendLine("### Progressed Sign Changes (within 6 months)");
        sb.AppendLine();
        sb.AppendLine("| Body | From | To | Approximate Date |");
        sb.AppendLine("|------|------|----|-----------------|");

        foreach (ProgressedSignChange change in changes)
        {
            sb.AppendLine(
                $"| {change.BodyName} | {change.FromSign} | {change.ToSign} | {CalendarDateFormatter.Format(change.Date, "yyyy-MM-dd")} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Appends the progressed stations subsection.
    /// </summary>
    private static void AppendProgressedStations(
        StringBuilder sb, IReadOnlyList<ProgressedStation> stations)
    {
        sb.AppendLine("### Progressed Stations (within 1 year)");
        sb.AppendLine();
        sb.AppendLine("| Body | Station | Approximate Date |");
        sb.AppendLine("|------|---------|-----------------|");

        foreach (ProgressedStation station in stations)
        {
            sb.AppendLine(
                $"| {station.BodyName} | {station.StationType} | {CalendarDateFormatter.Format(station.Date, "yyyy-MM-dd")} |");
        }

        sb.AppendLine();
    }

    #endregion

    #region Section 8: Solar Arc Directions

    /// <summary>
    /// Appends the solar arc directions section (Section 8): the directed positions and aspects at the
    /// report's start, then the dated hits in the period (branch Modes, 1.4.0 item 2; until 1.4.0 a
    /// snapshot at the report's midpoint, named in the heading, of the five majors).
    /// </summary>
    private static void AppendSolarArcsSection(StringBuilder sb, TransitResult result)
    {
        if (result.SolarArcs is not SolarArcResult sa)
            return;

        string start = FormatDate(result.ReportStartDate);

        sb.AppendLine("## Solar Arc Directions");
        sb.AppendLine();
        sb.AppendLine($"**Solar Arc:** {sa.ArcFormatted} (at report start, {start})");
        sb.AppendLine();

        // Directed Positions table
        sb.AppendLine($"### Directed Positions (at report start, {start})");
        sb.AppendLine();
        sb.AppendLine("| Body | Natal Position | Directed Position |");
        sb.AppendLine("|------|---------------|------------------|");

        foreach (SolarArcPosition pos in sa.Positions)
        {
            sb.AppendLine(
                $"| {pos.Name} | {pos.NatalFormattedPosition} | {pos.DirectedFormattedPosition} |");
        }

        sb.AppendLine();

        // Solar-Arc-to-Natal Aspects at the start: the majors and the 45\u00b0 series, then those to the five
        // (branch AsteroidsTiming, 1.6.0)
        AppendSolarArcAspects(
            sb, "### Solar-Arc-to-Natal Aspects (majors and the 45\u00b0 series, 1\u00b0 orb, at report start)", sa.Aspects);
        if (CarriesMinorPoints(result))
        {
            AppendSolarArcAspects(
                sb, "### Solar-Arc-to-Natal Aspects: Asteroids and Lilith (majors and the 45\u00b0 series, 1\u00b0 orb, at report start)",
                sa.MinorPointAspects);
        }

        // The dated hits in the period: a natal point in the export's notation, a midpoint as SA X = NA A/B;
        // then the hits to the five
        AppendHitsTable(
            sb,
            "### Solar Arc Hits in the Report Period",
            "No solar arc hit becomes exact in the report period.",
            SolarArcHitRows(result.SolarArcHits));
        if (CarriesMinorPoints(result))
        {
            AppendHitsTable(
                sb,
                "### Solar Arc Hits to the Asteroids and Lilith",
                "No solar arc hit becomes exact in the report period.",
                SolarArcHitRows(result.MinorPointSolarArcHits));
        }
    }

    /// <summary>
    /// Appends a solar-arc-to-natal aspects subsection: the aspects in orb at the report's start, in the
    /// result's order, under the heading given, or the none sentence. One writer for the chart's own aspects
    /// and, since branch AsteroidsTiming (1.6.0), the aspects to the five.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="heading">The sub-section's heading line.</param>
    /// <param name="aspects">The aspects.</param>
    private static void AppendSolarArcAspects(
        StringBuilder sb, string heading, IReadOnlyList<SolarArcAspect> aspects)
    {
        sb.AppendLine(heading);
        sb.AppendLine();

        if (aspects.Count == 0)
        {
            sb.AppendLine("No solar-arc-to-natal aspects within 1\u00b0 orb at the report start.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Directed | Aspect | Natal | Orb | Direction | Flags |");
        sb.AppendLine("|----------|--------|-------|-----|-----------|-------|");

        foreach (SolarArcAspect aspect in aspects)
        {
            string orb = PositionFormatter.FormatOrb(aspect.OrbDegrees);
            string direction = char.ToUpper(aspect.Direction[0]) + aspect.Direction[1..];

            // Build flags column
            List<string> flags = [];
            if (aspect.InvolvesAngle) flags.Add("angle");
            if (aspect.IsHardAspect) flags.Add("hard");
            string flagsStr = flags.Count > 0 ? string.Join(", ", flags) : "";

            sb.AppendLine(
                $"| {aspect.DirectedBody} | {aspect.AspectAbbreviation} | {aspect.NatalTarget} | {orb} | {direction} | {flagsStr} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// The rows of a solar arc hits table (<see cref="AppendHitsTable"/>): the hits in time order, each as its
    /// instant, its notation (a natal point as SA Mercury sqr NA MC, a midpoint as SA Sun = NA Pluto/ASC) and its
    /// aspect's name, a midpoint's with its dial.
    /// </summary>
    /// <param name="hits">The hits.</param>
    /// <returns>The rows.</returns>
    private static IEnumerable<(DateTime InstantUtc, string Hit, string Aspect)> SolarArcHitRows(IReadOnlyList<SolarArcHit> hits) =>
        hits
            .OrderBy(h => h.JulianDayUt)
            .Select(h => h.IsMidpoint
                ? (h.InstantUtc, $"SA {h.DirectedPoint} = NA {h.Target}", $"{h.AspectName} (45\u00b0 dial)")
                : (h.InstantUtc, $"SA {h.DirectedPoint} {h.AspectAbbreviation} NA {h.Target}", h.AspectName));

    #endregion

    #region Section 9: Primary Directions

    /// <summary>
    /// Appends the primary directions section (branch PrimaryExport, 1.5.0): the one section
    /// <see cref="PrimaryDirectionSectionFormatter"/> writes, with the period's hits under the sub-heading
    /// "Primary Direction Hits in the Report Period", or its none sentence.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="result">The transit result holding the primary directions.</param>
    private static void AppendPrimaryDirectionsSection(StringBuilder sb, TransitResult result)
    {
        if (result.PrimaryDirections is not PrimaryDirectionResult directions)
            return;

        PrimaryDirectionSectionFormatter.AppendSection(
            sb,
            directions,
            result.PrimaryDirectionHits,
            "Primary Direction Hits in the Report Period",
            "No primary direction becomes exact in the report period.");
    }

    #endregion

    #region Dated hits

    /// <summary>
    /// Appends a sub-section of dated hits (branch Modes, 1.4.0 item 2): a table headed Date (UTC), one
    /// row per hit in the order given, its UT date printed on the calendar in force
    /// (<see cref="CalendarDateFormatter"/>), or a sentence when there is none.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="heading">The sub-section's heading line.</param>
    /// <param name="noneSentence">The sentence printed when there is no hit.</param>
    /// <param name="hits">The hits: the instant, the contact's notation and the aspect's name.</param>
    private static void AppendHitsTable(
        StringBuilder sb, string heading, string noneSentence, IEnumerable<(DateTime InstantUtc, string Hit, string Aspect)> hits)
    {
        sb.AppendLine(heading);
        sb.AppendLine();

        List<(DateTime InstantUtc, string Hit, string Aspect)> rows = [.. hits];
        if (rows.Count == 0)
        {
            sb.AppendLine(noneSentence);
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Date (UTC) | Hit | Aspect |");
        sb.AppendLine("|------------|-----|--------|");
        foreach ((DateTime instantUtc, string hit, string aspect) in rows)
        {
            sb.AppendLine(
                $"| {CalendarDateFormatter.Format(DateOnly.FromDateTime(instantUtc), "yyyy-MM-dd")} | {hit} | {aspect} |");
        }

        sb.AppendLine();
    }

    #endregion

    #region Formatting Helpers

    /// <summary>
    /// Formats a <see cref="TransitBody"/> enum to its display name.
    /// </summary>
    /// <param name="body">The transit body.</param>
    /// <returns>Human-readable planet name.</returns>
    private static string FormatPlanetName(TransitBody body) => body switch
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

    /// <summary>
    /// Formats a <see cref="TransitBody"/> enum to its 2–3 letter symbol.
    /// </summary>
    /// <param name="body">The transit body.</param>
    /// <returns>Symbol string (JU, SA, UR, NE, PL, CH, NN, MA).</returns>
    private static string FormatPlanetSymbol(TransitBody body) => body switch
    {
        TransitBody.Jupiter => "JU",
        TransitBody.Saturn => "SA",
        TransitBody.Uranus => "UR",
        TransitBody.Neptune => "NE",
        TransitBody.Pluto => "PL",
        TransitBody.Chiron => "CH",
        TransitBody.NorthNode => "NN",
        TransitBody.Mars => "MA",
        TransitBody.Mercury => "ME",
        TransitBody.Venus => "VE",
        _ => body.ToString()[..2].ToUpperInvariant(),
    };

    /// <summary>
    /// Appends the footer: the engine version, as the chart export prints it ("AstroCast
    /// 1.3.3 (SwissEph 2.10.03, tzdb 2026e)"), so the file can be dated to the version that
    /// made it (E2-9). The version is the natal chart's: the transits are computed against it,
    /// by the same engine, in the same run. Exactly one rule stands before it: every section but
    /// the Solar Arcs closes with its own, so a rule is added only when the export does not
    /// already end with one (a Solar Arcs section ends with its hits table or none sentence).
    /// The header's Generated and Software lines are unchanged.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="result">The transit result, whose natal chart carries the engine version.</param>
    private static void AppendFooter(StringBuilder sb, TransitResult result)
    {
        string text = sb.ToString().TrimEnd('\r', '\n');
        if (!text.EndsWith("\n---", StringComparison.Ordinal))
            sb.AppendLine("---");
        sb.AppendLine($"*Engine: {result.NatalChart.EngineVersion}*");
    }

    /// <summary>
    /// Formats degrees and minutes from a decimal degree value, for a position or an arc (the
    /// solar arc, a progressed movement, a dial position): an orb goes through
    /// <see cref="PositionFormatter.FormatOrb"/> instead (branch O-D3, 1.3.4).
    /// </summary>
    /// <param name="degrees">Decimal degrees.</param>
    /// <returns>Formatted string like "22°14'".</returns>
    private static string FormatDegMin(double degrees)
    {
        int deg = (int)degrees;
        int min = (int)((degrees - deg) * 60.0);
        return string.Create(Inv, $"{deg}\u00b0{min:D2}'");
    }

    /// <summary>
    /// Formats the degree of a longitude within its sign, as <see cref="FormatDegMin"/> does
    /// ("D°MM'", truncated), for printing beside the sign that
    /// <see cref="TransitEphemerisHelper.LongitudeToSign"/> names for the same longitude (E2-2):
    /// the longitude modulo 360, less 30° times the sign's index as that method takes it, so the
    /// degree always agrees with the sign.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in decimal degrees.</param>
    /// <returns>Formatted string like "5°22'" for a longitude of 305°22′ (Aquarius).</returns>
    /// <remarks>
    /// Until 1.3.3 the Transit planet and Natal target lines printed the absolute longitude beside
    /// the sign ("Pluto at 305°22' Aquarius").
    /// </remarks>
    private static string FormatDegMinInSign(double longitude)
    {
        double normalized = longitude % 360.0;
        int signIndex = (int)(normalized / 30.0);
        return FormatDegMin(normalized - 30.0 * signIndex);
    }

    /// <summary>
    /// Formats a residual orb as a parenthetical note for exact pass display, to the second,
    /// truncated, with the degree sign, the prime and the double prime as a position prints them
    /// (branch O-D3, 1.3.4; until 1.3.4 an apostrophe and a quotation mark). The one orb printed
    /// with seconds: a pass is bisected to the second, so its residual is below a minute.
    /// Returns an empty string if the orb is null.
    /// </summary>
    /// <param name="orbDegrees">Residual orb in decimal degrees, or null.</param>
    /// <returns>
    /// Formatted string like " (orb: 0°00′03″)" or empty string when null.
    /// </returns>
    private static string FormatResidualOrb(double? orbDegrees)
    {
        if (orbDegrees is null)
            return "";

        double orb = Math.Abs(orbDegrees.Value);
        int degrees = (int)orb;
        double remainder = (orb - degrees) * 60.0;
        int minutes = (int)remainder;
        int seconds = (int)((remainder - minutes) * 60.0);

        return string.Create(Inv, $" (orb: {degrees}\u00b0{minutes:D2}\u2032{seconds:D2}\u2033)");
    }

    /// <summary>
    /// Formats an ordinal house number (1st, 2nd, 3rd, 4th, etc.).
    /// </summary>
    /// <param name="number">House number (1–12).</param>
    /// <returns>Ordinal string (e.g., "1st", "2nd", "3rd", "4th").</returns>
    private static string FormatOrdinal(int number) => number switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => string.Create(Inv, $"{number}th"),
    };

    /// <summary>
    /// Formats a picked <see cref="DateOnly"/>, a reading (the report's typed bounds), as typed
    /// (yyyy-MM-dd). The midpoint, a proleptic day derived from the scan (branch R3, 1.3.4), and a date
    /// derived from an instant go through <see cref="CalendarDateFormatter.Format(DateOnly, string)"/>.
    /// </summary>
    /// <param name="date">The date to format.</param>
    /// <returns>ISO date string.</returns>
    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", Inv);

    /// <summary>
    /// Formats a lifecycle event's instant on the calendar in force (<see cref="CalendarDateFormatter"/>,
    /// branch R2, 1.3.4). All event types carry a sub-minute UTC time from the bisection. Exact passes
    /// show seconds; other events show hours and minutes.
    /// </summary>
    /// <param name="evt">The lifecycle event to format.</param>
    /// <returns>
    /// For exact events: "yyyy-MM-dd HH:mm:ss UTC".
    /// For other events: "yyyy-MM-dd HH:mm UTC".
    /// </returns>
    private static string FormatEventDateTime(TransitLifecycleEvent evt)
    {
        if (evt.EventType == TransitLifecycleEventType.Exact)
            return CalendarDateFormatter.DateTime(evt.Date) + " UTC";

        return CalendarDateFormatter.Minutes(evt.Date) + " UTC";
    }

    /// <summary>
    /// Formats daily motion with sign and 3 decimal places.
    /// </summary>
    /// <param name="motion">Daily motion in degrees per day.</param>
    /// <returns>Formatted string like "+0.083°/day" or "-0.031°/day".</returns>
    private static string FormatDailyMotion(double motion) =>
        string.Create(Inv, $"{(motion >= 0 ? "+" : "")}{motion:F3}\u00b0/day");

    /// <summary>
    /// Returns the transit bodies from the result sorted in standard display order
    /// (outermost first: Pluto, Neptune, Uranus, Saturn, Jupiter, Chiron, NorthNode, Mars).
    /// </summary>
    private static IEnumerable<TransitBody> GetSortedBodies(TransitResult result)
    {
        TransitBody[] sortOrder =
        [
            TransitBody.Jupiter, TransitBody.Saturn, TransitBody.Uranus,
            TransitBody.Neptune, TransitBody.Pluto, TransitBody.Chiron,
            TransitBody.NorthNode, TransitBody.Mars,
        ];

        foreach (TransitBody body in sortOrder)
        {
            if (result.TransitPositions.ContainsKey(body))
                yield return body;
        }
    }

    #endregion
}
