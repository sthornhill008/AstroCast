using System.Globalization;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.MedicalAstrology;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="ChartResult"/> as a Markdown document for
/// quick viewing and fallback display.
/// </summary>
public static class ChartMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generate a complete chart report as a Markdown string.
    /// Paran data is read from <see cref="ChartResult.Parans"/> when available.
    /// </summary>
    /// <param name="result">The chart calculation result to format.</param>
    /// <param name="timeProvider">
    /// Clock used to resolve "today" for the date-keyed time-lord sections
    /// (Annual Profection, Firdaria, Zodiacal Releasing). Defaults to
    /// <see cref="TimeProvider.System"/> when <c>null</c>, so production output
    /// is byte-for-byte identical to the previous <see cref="DateTime.Today"/>
    /// behaviour. It is injectable purely so golden and unit tests can pin a
    /// <c>FakeTimeProvider</c> and keep those sections deterministic instead of
    /// drifting with the calendar (F-GR-2); the natal call site here is the
    /// only place "now" enters this formatter.
    /// </param>
    /// <param name="includeTemperament">
    /// When true (the default), the Medical Astrology Correspondences section opens with the
    /// <c>### Temperament</c> block. The relocation, composite and electional exports pass false: a
    /// derived chart prints no temperament (ruling S-D9, confirmed 2026-10-11), and the rest of its
    /// medical section prints as before. Every other export keeps it, the return and event charts
    /// included.
    /// </param>
    /// <param name="timeLordsChart">
    /// The chart the Annual Profection, Firdaria and Zodiacal Releasing sections are computed from, where
    /// it is not <paramref name="result"/>: the relocation export passes the natal chart, so a relocated
    /// chart's time lords are the birth's (S-D6, branch K2, 1.3.4), printed where they print in every
    /// export. <see langword="null"/>, the default, reads <paramref name="result"/>.
    /// </param>
    /// <param name="composite">
    /// When true, the chart is a composite, whose bodies are midpoints and not points in the sky (ruling
    /// S-D4, branch L2, 1.3.4): the Planetary Positions table prints no Dec and no OOB cell, the Equatorial
    /// Coordinates and Out-of-Bounds Planets sections are left out, the Moon Conditions print the phase,
    /// sign and via combusta only, the Aspects table has no Ap/Sp cell, and the Declination Aspects section
    /// and Declination Clusters table are left out. <see cref="CompositeMarkdownFormatter"/> passes true
    /// and says so once in its header. When false, the default, every chart prints as before.
    /// </param>
    /// <param name="minorPoints">
    /// When true, the default, a chart that carries Ceres, Pallas, Juno, Vesta and Lilith
    /// (<see cref="ChartResult.MinorPoints"/>, behind <see cref="ChartInput.IncludeMinorPoints"/>) prints them in
    /// the <c>## Asteroids and Lilith</c> section, directly after the Out-of-Bounds Planets and before the
    /// Essential Dignities (R6, scoping decision 3; branch AsteroidsExport, 1.6.0): the opening line that defines
    /// Lilith and the orb, their positions, then their aspects at 2°00′, in the composite form (no RA, Dec, OOB
    /// or Ap/Sp cell) when <paramref name="composite"/> is true. The reports that never print the five pass false
    /// (R8): the primary-direction, medical, electional, ingress, lunation and great-conjunction exports. A chart
    /// without the five prints no section either way, so every export with the switch off is as before (R9).
    /// </param>
    /// <param name="medicalCorrespondences">
    /// When true (the default), the chart prints its <c>## Medical Astrology Correspondences</c> section. The
    /// medical export passes false (branch ExportText, 1.7.2; finding N14): it prints its own, fuller sections
    /// after the chart it embeds, under <see cref="MedicalBasisLine"/>, and until 1.7.2 a reader met two
    /// versions of each table.
    /// </param>
    /// <returns>
    /// A Markdown-formatted chart report. Every orb prints as truncated <c>D°MM′</c> through
    /// <see cref="PositionFormatter.FormatOrb"/> (branch O-D3, 1.3.4): the angular, aspect, declination,
    /// midpoint, fixed-star and antiscia tables, the Moon's first applying aspect and the Fixed Stars
    /// heading. Until 1.3.4 an electional candidate's embedded chart took the unified form and every
    /// other export a legacy one ("129'", "0°03'", "(orb 1.49°)").
    /// </returns>
    public static string ToMarkdown(
        ChartResult result,
        TimeProvider? timeProvider = null,
        bool includeTemperament = true,
        ChartResult? timeLordsChart = null,
        bool composite = false,
        bool minorPoints = true,
        bool medicalCorrespondences = true)
    {
        ArgumentNullException.ThrowIfNull(result);

        // Resolve "today" once, here at the formatter's composition boundary,
        // from the injected clock. TimeProvider.System reproduces
        // DateTime.Today exactly in production; a test-supplied FakeTimeProvider
        // pins it. Threaded below into the profection / firdaria / zodiacal
        // releasing appenders as their reference date.
        DateTime asOfDate = (timeProvider ?? TimeProvider.System).GetLocalNow().Date;

        StringBuilder sb = new();

        // Header — horary charts use the question as the title
        bool isHorary = string.Equals(result.Input.EventType, "Horary", StringComparison.OrdinalIgnoreCase);
        string? horaryQuestion = result.Input.HoraryQuestion;

        if (isHorary && !string.IsNullOrWhiteSpace(horaryQuestion))
        {
            // Use the first line of the question as H1 title
            string firstLine = horaryQuestion.Split('\n', 2)[0].Trim();
            sb.AppendLine($"# {firstLine}");
        }
        else
        {
            sb.AppendLine($"# {result.Input.Name}");
        }

        sb.AppendLine();
        // An explicit offset overrides the zone's rules — the return, ingress, lunation and
        // great-conjunction charts are cast at a UTC wall clock with an explicit 0 — so the
        // wall clock is labelled with that offset, as ChartTimeFormatter.FormatLocalTime
        // labels the Stationing rows, rather than with a zone it was not read on. Such a
        // chart's clock is its instant plus the offset, on the calendar in force (R4, branch
        // R2, 1.3.4): for a reading that is the reading itself, byte for byte; for an event
        // chart handed the proleptic instant as its reading it is the Julian date before
        // 1582-10-15, as the Return Date/Time and Calendar lines print it, and a return on
        // Julian 29 February prints the date no reading could hold. A zone chart prints its
        // reading as typed.
        bool explicitOffset = result.Input.UseExplicitOffset && result.Input.UtcOffsetHours.HasValue;
        string clock = explicitOffset
            ? CalendarDateFormatter.DateTime(ExplicitOffsetClock(result))
            : result.Input.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", Inv);
        string clockLabel = explicitOffset
            ? result.ResolvedOffsetLabel
            : $"({result.Input.TimeZone})";
        sb.AppendLine($"**Date/Time:** {clock} {clockLabel}  ");
        if (CalendarDateFormatter.IsJulian(result.UtcDateTime))
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine + "  ");
        sb.AppendLine(Inv, $"**Location:** {result.Input.LocationName} ({result.Input.Latitude:F4}, {result.Input.Longitude:F4})  ");
        sb.AppendLine($"**Type:** {result.Input.EventType}  ");

        if (result.Input.Gender != Gender.Unspecified)
            sb.AppendLine($"**Gender:** {result.Input.Gender}  ");

        if (!string.IsNullOrEmpty(result.Input.SourceNotes))
            sb.AppendLine($"**Source:** {result.Input.SourceNotes}  ");

        if (!string.IsNullOrEmpty(result.Input.EventDescription))
            sb.AppendLine($"**Description:** {result.Input.EventDescription}  ");

        // For horary charts, render the full question text in the Notes field
        if (isHorary && !string.IsNullOrWhiteSpace(horaryQuestion))
        {
            sb.AppendLine($"**Notes:** {horaryQuestion.Trim()}  ");
        }
        else if (isHorary && !string.IsNullOrEmpty(result.Input.Notes))
        {
            // Backward compatibility: use Notes as the question for older horary charts
            sb.AppendLine($"**Notes:** {result.Input.Notes}  ");
        }
        else if (!string.IsNullOrEmpty(result.Input.Notes))
        {
            sb.AppendLine($"**Notes:** {result.Input.Notes}  ");
        }

        sb.AppendLine($"**UTC Offset:** {result.ResolvedOffsetLabel}  ");

        // A wall clock the zone repeated or skipped, or read on local mean time, is said so
        // under its own label, never in Notes, which holds the user's text (D17, Q5).
        if (result.TimeNote is not null)
            sb.AppendLine($"**Time Note:** {result.TimeNote}  ");

        sb.AppendLine(Inv, $"**Julian Day:** {result.JulianDay:F6}");
        sb.AppendLine();

        // Planetary Positions. A composite body has no declination, so its table has no Dec and no
        // OOB cell and the two sections read from declinations are left out (S-D4).
        sb.AppendLine("## Planetary Positions");
        sb.AppendLine();
        if (composite)
        {
            sb.AppendLine("| Body | Position | Lat | House | Speed | Dir |");
            sb.AppendLine("|------|----------|-----|-------|-------|-----|");
        }
        else
        {
            sb.AppendLine("| Body | Position | Lat | Dec | House | Speed | Dir | OOB |");
            sb.AppendLine("|------|----------|-----|-----|-------|-------|-----|-----|");
        }

        foreach (PlanetPosition p in result.Positions)
        {
            string houseStr = p.House.HasValue ? string.Create(Inv, $"{p.House.Value}") : "-";
            string latStr = PositionFormatter.FormatLatitude(p.Latitude);
            if (composite)
            {
                sb.AppendLine(
                    Inv, $"| {p.Name} | {PositionFormatter.FormatPosition(p)} | {latStr} | {houseStr} | {p.SpeedInLongitude:F3}/d | {p.DirectionCode} |");
                continue;
            }

            string decStr = p.Declination.HasValue
                ? PositionFormatter.FormatDeclination(p.Declination.Value) : "-";
            string oobStr = PositionFormatter.IsOutOfBounds(p, result.Obliquity) ? "OOB" : "";
            sb.AppendLine(
                Inv, $"| {p.Name} | {PositionFormatter.FormatPosition(p)} | {latStr} | {decStr} | {houseStr} | {p.SpeedInLongitude:F3}/d | {p.DirectionCode} | {oobStr} |");
        }

        sb.AppendLine();

        if (!composite)
        {
            // Equatorial Coordinates
            AppendEquatorialCoordinates(sb, result.Positions);

            // Out-of-Bounds Planets
            AppendOutOfBoundsPlanets(sb, result.Positions, result.Obliquity);
        }

        // Asteroids and Lilith: the five minor points in a section of their own, where the chart carries them
        // and the embedder prints them (R6, R8; branch AsteroidsExport, 1.6.0).
        if (minorPoints && result.MinorPoints.Count > 0)
            AppendMinorPoints(sb, result, composite);

        // Essential Dignities
        sb.AppendLine("## Essential Dignities");
        sb.AppendLine();
        sb.AppendLine("| Planet | Sign | Dignity | Score | Dispositor |");
        sb.AppendLine("|--------|------|---------|-------|------------|");

        foreach (DignityInfo d in result.Dignities)
        {
            string scoreStr = d.Score > 0 ? string.Create(Inv, $"+{d.Score}") : d.Score.ToString(Inv);
            sb.AppendLine(
                $"| {d.PlanetName} | {d.Sign} | {d.Label} | {scoreStr} | {d.Dispositor} |");
        }

        sb.AppendLine();

        // Planet Conditions (Bonification/Maltreatment) — Stage 2
        AppendPlanetConditions(sb, result);

        // Stations — Stage 6
        AppendStations(sb, result);

        // Moon Conditions
        if (result.MoonCondition is not null)
        {
            MoonConditionInfo mc = result.MoonCondition;

            sb.AppendLine("## Moon Conditions");
            sb.AppendLine();
            sb.AppendLine(Inv, $"**Phase:** {mc.PhaseLabel} ({mc.PhaseAngle:F1}\u00b0, {(mc.IsWaxing ? "waxing" : "waning")}, {mc.PhasePosition}-phase)  ");
            sb.AppendLine($"**Moon Sign:** {mc.MoonSign}  ");

            // A composite's Moon, a midpoint, has no next aspect and no void of course to print (S-D4).
            if (!composite)
            {
                sb.AppendLine($"**Void of Course:** {(mc.IsVoidOfCourse ? "Yes" : "No")}  ");

                if (!mc.IsVoidOfCourse && mc.FirstApplyingPlanet is not null)
                {
                    // The canonical orb: the aspect table's value where the aspect appears there, else
                    // the same quantity from the positions, else the evaluator's own (O-D3, 1.3.4).
                    double orb = ResolveCanonicalMoonOrb(result, mc);
                    sb.AppendLine($"**First Applying Aspect:** Moon {mc.FirstApplyingAspectType} {mc.FirstApplyingPlanet} (orb {PositionFormatter.FormatOrb(orb)})  ");
                }
            }

            sb.AppendLine($"**Via Combusta:** {(mc.IsViaCombusta ? "Yes" : "No")}");
            sb.AppendLine();
        }

        // Moon Mansion — Stage 3
        AppendMoonMansion(sb, result);

        // Chart Context
        if (result.ChartContext is not null)
        {
            ChartContextInfo ctx = result.ChartContext;

            sb.AppendLine("## Chart Context");
            sb.AppendLine();
            sb.AppendLine("| Element | Value |");
            sb.AppendLine("|---------|-------|");
            sb.AppendLine($"| Chart Ruler | {ctx.ChartRuler} (Ascendant in {ctx.AscendantSign}) |");

            string dignityQualifier = FormatDignityQualifier(ctx.ChartRulerDignity.Score);
            string scoreStr = ctx.ChartRulerDignity.Score > 0
                ? string.Create(Inv, $"+{ctx.ChartRulerDignity.Score}")
                : ctx.ChartRulerDignity.Score.ToString(Inv);
            sb.AppendLine($"| Ruler Dignity | {ctx.ChartRulerDignity.Label} ({scoreStr}) \u2014 {dignityQualifier} |");
            sb.AppendLine($"| Ruler House | {OrdinalHouse(ctx.ChartRulerHouse)} |");
            sb.AppendLine($"| Ruler Retrograde | {(ctx.IsChartRulerRetrograde ? "Yes" : "No")} |");
            sb.AppendLine($"| Sect | {ctx.SectLabel} chart |");

            if (ctx.EventSignificator is not null)
            {
                string sigDignity = ctx.EventSignificatorDignity is not null
                    ? string.Create(Inv, $" \u2014 {ctx.EventSignificatorDignity.Label} ({(ctx.EventSignificatorDignity.Score > 0 ? "+" : "")}{ctx.EventSignificatorDignity.Score})")
                    : "";
                sb.AppendLine($"| Event Significator | {ctx.EventSignificator}{sigDignity} |");
                sb.AppendLine($"| Key Houses | {string.Join(", ", ctx.KeyHouses.Select(h => h.ToString(Inv)))} |");
            }

            sb.AppendLine();

            // Angular Planets
            sb.AppendLine("### Angular Planets");
            sb.AppendLine();

            if (ctx.AngularPlanets.Count > 0)
            {
                sb.AppendLine("| Planet | Angle | Orb | Nature |");
                sb.AppendLine("|--------|-------|-----|--------|");

                foreach (AngularPlanet ap in ctx.AngularPlanets)
                {
                    string orbStr = PositionFormatter.FormatOrb(ap.Orb);
                    string nature = ap.IsBenefic ? "Benefic" : ap.IsMalefic ? "Malefic" : "Neutral";
                    sb.AppendLine($"| {ap.PlanetName} | {ap.Angle} | {orbStr} | {nature} |");
                }
            }
            else
            {
                sb.AppendLine("None within 5\u00b0 of angles.");
            }

            sb.AppendLine();

            // Mutual Receptions
            sb.AppendLine("### Mutual Receptions");
            sb.AppendLine();

            if (ctx.MutualReceptions.Count > 0)
            {
                foreach (MutualReception mr in ctx.MutualReceptions)
                {
                    sb.AppendLine($"- {mr.Notation}");
                }
            }
            else
            {
                sb.AppendLine("None.");
            }

            sb.AppendLine();

            // Planetary Sect — Stage 1
            AppendPlanetarySect(sb, ctx);

            // Ascendant Triplicity Rulers — Stage 1
            AppendTriplicityRulers(sb, ctx);
        }

        // Planetary Hour — Stage 3
        AppendPlanetaryHour(sb, result);

        // Horary Perfection Analysis — between Planetary Hour and Arabic Parts
        AppendHoraryPerfection(sb, result);

        // The time lords — only for Birth charts; a relocation's are the natal chart's (S-D6)
        ChartResult timeLords = timeLordsChart ?? result;

        // Annual Profection
        AppendProfectionYear(sb, timeLords, asOfDate);

        // Firdaria
        AppendFirdaria(sb, timeLords, asOfDate);

        // Zodiacal Releasing
        AppendZodiacalReleasing(sb, timeLords, asOfDate);

        // Arabic Parts — Stage 4
        AppendArabicParts(sb, result);

        // Antiscia Connections — Stage 5
        AppendAntisciaConnections(sb, result);

        // Houses
        sb.AppendLine($"## House Cusps ({result.HouseSystemName})");
        sb.AppendLine();
        sb.AppendLine("| House | Cusp |");
        sb.AppendLine("|-------|------|");

        for (int i = 0; i < result.Houses.Cusps.Count; i++)
        {
            sb.AppendLine(Inv, $"| {i + 1} | {PositionFormatter.FormatLongitude(result.Houses.Cusps[i])} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**ASC:** {PositionFormatter.FormatLongitude(result.Houses.Ascendant)}  ");
        sb.AppendLine($"**MC:** {PositionFormatter.FormatLongitude(result.Houses.Midheaven)}  ");
        sb.AppendLine($"**DSC:** {PositionFormatter.FormatLongitude(result.Houses.Descendant)}  ");
        sb.AppendLine($"**IC:** {PositionFormatter.FormatLongitude(result.Houses.ImumCoeli)}");
        sb.AppendLine();

        // Aspects. A composite's carry no Ap/Sp mark: a midpoint is motionless (S-D4).
        sb.AppendLine("## Aspects");
        sb.AppendLine();
        if (composite)
        {
            sb.AppendLine("| Body 1 | Aspect | Body 2 | Orb |");
            sb.AppendLine("|--------|--------|--------|-----|");
        }
        else
        {
            sb.AppendLine("| Body 1 | Aspect | Body 2 | Orb | Ap/Sp |");
            sb.AppendLine("|--------|--------|--------|-----|-------|");
        }

        foreach (AspectResult a in result.Aspects)
        {
            string orbStr = PositionFormatter.FormatOrb(a.Orb);
            if (composite)
            {
                sb.AppendLine($"| {a.Body1Name} | {a.Aspect.Abbreviation} | {a.Body2Name} | {orbStr} |");
                continue;
            }

            string appSep = a.IsApplying ? "Ap" : "Sp";
            sb.AppendLine(
                $"| {a.Body1Name} | {a.Aspect.Abbreviation} | {a.Body2Name} | {orbStr} | {appSep} |");
        }

        sb.AppendLine();

        if (!composite)
        {
            // Declination Aspects
            AppendDeclinationAspects(sb, result.DeclinationAspects);

            // Declination Clusters
            AppendDeclinationClusters(sb, result.Positions, result.DeclinationAspects);
        }

        // Midpoint Activations
        sb.AppendLine("## Midpoint Activations (45 Dial)");
        sb.AppendLine();
        sb.AppendLine("| Midpoint | Activator | Harmonic | Orb |");
        sb.AppendLine("|----------|-----------|----------|-----|");

        foreach (MidpointActivation ma in result.MidpointActivations)
        {
            string harmonic = ma.HarmonicAngle == 0 ? "direct" : string.Create(Inv, $"{ma.HarmonicAngle}");
            sb.AppendLine(
                Inv, $"| {ma.Midpoint.Notation} | {ma.ActivatorName} | {harmonic} | {PositionFormatter.FormatOrb(ma.Orb)} |");
        }

        sb.AppendLine();

        // Fixed Star Conjunctions
        sb.AppendLine("## Fixed Stars (1\u00b000\u2032 orb, conjunction only)");
        sb.AppendLine();

        if (result.FixedStarConjunctions.Count > 0)
        {
            sb.AppendLine("| Star | Mag | Position | Conjunct | Orb | Nature | Keywords |");
            sb.AppendLine("|------|-----|----------|----------|-----|--------|----------|");

            foreach (FixedStarConjunction fsc in result.FixedStarConjunctions)
            {
                string orbStr = PositionFormatter.FormatOrb(fsc.Orb);
                string starFlags = FormatStarFlags(fsc);
                sb.AppendLine(
                    Inv, $"| {fsc.StarName}{starFlags} | {fsc.Magnitude:F2} | {PositionFormatter.FormatLongitude(fsc.StarLongitude)} | {fsc.ChartPointName} | {orbStr} | {fsc.Nature} | {fsc.Keywords} |");
            }
        }
        else
        {
            sb.AppendLine("No fixed star conjunctions found within 1\u00b000\u2032 orb.");
        }

        sb.AppendLine();

        // Parans (Paranatellonta)
        if (result.Parans is not null)
        {
            AppendParanSection(sb, result.Parans);
        }

        // Medical Astrology Correspondences (left out of the chart the medical export embeds)
        if (medicalCorrespondences)
        {
            AppendMedicalSection(sb, result, includeTemperament);
        }

        // Footer
        sb.AppendLine("---");
        sb.AppendLine(
            Inv, $"*Generated by {result.EngineVersion} at {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// The clock of a chart cast at an explicit offset: its instant plus the offset in whole
    /// seconds, the seconds <c>ChartEngine.ResolveInstant</c> subtracted from the reading, so a
    /// reading comes back as itself (R4, branch R2, 1.3.4).
    /// </summary>
    /// <param name="result">A chart cast at an explicit offset.</param>
    /// <returns>The clock, as proleptic ticks of <see cref="DateTimeKind.Unspecified"/> kind.</returns>
    private static DateTime ExplicitOffsetClock(ChartResult result) =>
        DateTime.SpecifyKind(
            result.UtcDateTime.AddSeconds(Math.Round(result.ResolvedUtcOffsetHours * 3600.0, MidpointRounding.AwayFromZero)),
            DateTimeKind.Unspecified);

    /// <summary>
    /// Formats a horary orb that may be absent as truncated D°MM′ through
    /// <see cref="PositionFormatter.FormatOrb"/>, or as nothing when it is absent, as the
    /// decimal format it replaces printed nothing for null (branch O-D3, 1.3.4).
    /// </summary>
    /// <param name="orb">The orb in decimal degrees, or null.</param>
    /// <returns>The orb as D°MM′, or an empty string.</returns>
    private static string FormatOrbOrEmpty(double? orb) =>
        orb is double value ? PositionFormatter.FormatOrb(value) : "";

    /// <summary>
    /// Resolves the orb to display for the Moon's first applying aspect.
    /// Prefers the canonical value from the chart's aspect table
    /// (<see cref="AspectService.FindAspectBetween"/>). When the aspect is
    /// wider than the table's per-aspect orb limits (no table row), computes
    /// the same quantity — current angular distance from exact — from the
    /// chart's own positions via <see cref="ComputeMoonAspectOrb"/>, so every
    /// export site still shows one consistent value. The Moon-condition
    /// forward-travel distance is the last-resort fallback. The Markdown and the
    /// PDF both print it, through <see cref="PositionFormatter.FormatOrb"/>
    /// (branch O-D3, 1.3.4); until 1.3.4 only an electional candidate's embedded
    /// chart did, and every other export printed the evaluator's orb to two decimals.
    /// </summary>
    /// <param name="result">The chart whose aspect table is canonical.</param>
    /// <param name="mc">The Moon condition carrying the first applying aspect.</param>
    /// <returns>The orb in decimal degrees.</returns>
    internal static double ResolveCanonicalMoonOrb(ChartResult result, MoonConditionInfo mc)
    {
        if (mc.FirstApplyingAspectType is not null && mc.FirstApplyingPlanet is not null)
        {
            AspectResult? canonical = AspectService.FindAspectBetween(
                result.Aspects, "Moon", mc.FirstApplyingPlanet, mc.FirstApplyingAspectType.Value);

            if (canonical is not null)
            {
                return canonical.Orb;
            }

            double? computed = ComputeMoonAspectOrb(
                result.Positions, mc.FirstApplyingPlanet, mc.FirstApplyingAspectType.Value);

            if (computed is not null)
            {
                return computed.Value;
            }
        }

        return mc.FirstApplyingOrb ?? 0.0;
    }

    /// <summary>
    /// Computes the current angular orb of a Moon aspect directly from chart
    /// positions: the absolute difference between the Moon-target separation
    /// and the exact aspect angle. This is the same quantity the aspect table
    /// carries, available even when the aspect is outside table orb limits.
    /// </summary>
    /// <param name="positions">The chart's planetary positions.</param>
    /// <param name="targetPlanet">Name of the aspected planet.</param>
    /// <param name="aspectType">The aspect type whose exact angle is used.</param>
    /// <returns>The orb in decimal degrees, or null when either body or the aspect angle is unavailable.</returns>
    internal static double? ComputeMoonAspectOrb(
        IReadOnlyList<PlanetPosition> positions,
        string targetPlanet,
        AspectType aspectType)
    {
        PlanetPosition? moon = positions.FirstOrDefault(p =>
            string.Equals(p.Name, "Moon", StringComparison.OrdinalIgnoreCase));
        PlanetPosition? target = positions.FirstOrDefault(p =>
            string.Equals(p.Name, targetPlanet, StringComparison.OrdinalIgnoreCase));

        if (moon is null || target is null)
        {
            return null;
        }

        AspectDefinition? definition = AspectDefinition.All
            .FirstOrDefault(d => d.Type == aspectType);

        if (definition is null)
        {
            return null;
        }

        double separation = AspectService.AngularSeparation(moon.Longitude, target.Longitude);
        return Math.Abs(separation - definition.Angle);
    }

    /// <summary>
    /// Formats Royal Star and Behenian Star flags as a parenthetical suffix.
    /// </summary>
    /// <param name="fsc">The fixed star conjunction to format flags for.</param>
    /// <returns>A suffix string, e.g., " (Royal)", " (Behenian)", or empty.</returns>
    private static string FormatStarFlags(FixedStarConjunction fsc)
    {
        if (fsc.IsRoyalStar) return " (Royal)";
        if (fsc.IsBehenianStar) return " (Behenian)";
        return "";
    }

    /// <summary>
    /// Formats a dignity score into a human-readable qualifier.
    /// </summary>
    private static string FormatDignityQualifier(int score) => score switch
    {
        >= 4 => "strong",
        > 0 => "moderately strong",
        0 => "peregrine (neutral)",
        > -4 => "weakened",
        _ => "debilitated",
    };

    /// <summary>
    /// Formats a house number with ordinal suffix (e.g., "1st", "2nd", "10th").
    /// </summary>
    private static string OrdinalHouse(int house) => house switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => string.Create(Inv, $"{house}th"),
    };

    /// <summary>
    /// Appends the equatorial coordinates subsection showing right ascension
    /// and declination for all bodies that have equatorial data.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="positions">All planetary positions.</param>
    private static void AppendEquatorialCoordinates(
        StringBuilder sb, IReadOnlyList<PlanetPosition> positions)
    {
        // Only show if any body has equatorial data
        bool hasEquatorial = false;
        foreach (PlanetPosition p in positions)
        {
            if (p.RightAscension.HasValue || p.Declination.HasValue)
            {
                hasEquatorial = true;
                break;
            }
        }

        if (!hasEquatorial) return;

        sb.AppendLine("### Equatorial Coordinates");
        sb.AppendLine();
        sb.AppendLine("| Body | Right Ascension | Declination |");
        sb.AppendLine("|------|-----------------|-------------|");

        foreach (PlanetPosition p in positions)
        {
            string raStr = p.RightAscension.HasValue
                ? PositionFormatter.FormatRightAscension(p.RightAscension.Value) : "-";
            string decStr = p.Declination.HasValue
                ? PositionFormatter.FormatDeclination(p.Declination.Value) : "-";
            sb.AppendLine($"| {p.Name} | {raStr} | {decStr} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Appends the out-of-bounds planets subsection listing any planets
    /// whose declination exceeds the chart's true obliquity of the ecliptic,
    /// with the excess over it. The Sun is never listed.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="positions">All planetary positions.</param>
    /// <param name="obliquity">The chart's true obliquity of the ecliptic, in degrees (<see cref="ChartResult.Obliquity"/>).</param>
    private static void AppendOutOfBoundsPlanets(
        StringBuilder sb, IReadOnlyList<PlanetPosition> positions, double obliquity)
    {
        List<(string Name, double Declination)> oobPlanets = [];
        foreach (PlanetPosition p in positions)
        {
            if (p.Declination is double declination && PositionFormatter.IsOutOfBounds(p, obliquity))
                oobPlanets.Add((p.Name, declination));
        }

        sb.AppendLine("### Out-of-Bounds Planets");
        sb.AppendLine();

        if (oobPlanets.Count > 0)
        {
            sb.AppendLine("| Planet | Declination | Excess |");
            sb.AppendLine("|--------|-------------|--------|");

            foreach ((string name, double declination) in oobPlanets)
            {
                double excess = Math.Abs(declination) - obliquity;
                sb.AppendLine(
                    Inv, $"| {name} | {PositionFormatter.FormatDeclination(declination)} | {excess:F2}\u00b0 |");
            }
        }
        else
        {
            sb.AppendLine("No planets are out-of-bounds.");
        }

        sb.AppendLine();
    }

    /// <summary>The heading of the minor points' section (R6, scoping decision 3; branch AsteroidsExport, 1.6.0).</summary>
    internal const string MinorPointsHeading = "## Asteroids and Lilith";

    /// <summary>
    /// The section's opening line: what the five are, Lilith's definition and the orb, as the plan's R6 asks. The
    /// PDF prints the same line in plain text.
    /// </summary>
    internal const string MinorPointsIntroduction =
        "Ceres, Pallas, Juno, Vesta and Lilith (Black Moon Lilith, the mean lunar apogee): minor modern points, " +
        "read as Chiron is. Their aspects are the five Ptolemaic aspects at a 2\u00b000\u2032 orb, to the Sun to " +
        "Pluto, the Ascendant and the Midheaven only.";

    /// <summary>The section's positions sub-heading, without its <c>### </c>.</summary>
    internal const string MinorPointPositionsTitle = "Positions";

    /// <summary>The section's aspects sub-heading, without its <c>### </c>, naming the orb.</summary>
    internal const string MinorPointAspectsTitle = "Aspects (2\u00b000\u2032 orb)";

    /// <summary>
    /// The line printed in place of an empty aspects table: the chart export, the synastry export's inter-chart
    /// section and the PDF print it.
    /// </summary>
    internal const string NoMinorPointAspectsLine = "No aspects within 2\u00b000\u2032.";

    /// <summary>
    /// Appends the Asteroids and Lilith section (R6; branch AsteroidsExport, 1.6.0): the heading, the opening line,
    /// then <c>### Positions</c>, one row per point in R1's order with the positions table's cells and the right
    /// ascension (nine columns: Body, Position, Lat, RA, Dec, House, Speed, Dir, OOB), then <c>### Aspects (2°00′
    /// orb)</c>, one row per aspect in the chart's order (by orb) with the aspect table's cells, or the none line.
    /// Every orb is the row's own (<see cref="AspectResult.Orb"/>), truncated by <see cref="PositionFormatter.FormatOrb"/>,
    /// never the definition's <see cref="AspectDefinition.Orb"/>, which is the chart's 8°, 7° or 5°; the 2° limit
    /// is the aspect service's. A composite prints the composite form: no RA, Dec or OOB cell and no Ap/Sp cell
    /// (S-D4).
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="result">The chart, carrying the five.</param>
    /// <param name="composite">Whether the chart is a composite.</param>
    private static void AppendMinorPoints(StringBuilder sb, ChartResult result, bool composite)
    {
        sb.AppendLine(MinorPointsHeading);
        sb.AppendLine();
        sb.AppendLine(MinorPointsIntroduction);
        sb.AppendLine();

        sb.AppendLine("### " + MinorPointPositionsTitle);
        sb.AppendLine();
        if (composite)
        {
            sb.AppendLine("| Body | Position | Lat | House | Speed | Dir |");
            sb.AppendLine("|------|----------|-----|-------|-------|-----|");
        }
        else
        {
            sb.AppendLine("| Body | Position | Lat | RA | Dec | House | Speed | Dir | OOB |");
            sb.AppendLine("|------|----------|-----|----|-----|-------|-------|-----|-----|");
        }

        foreach (PlanetPosition p in result.MinorPoints)
        {
            string houseStr = p.House.HasValue ? string.Create(Inv, $"{p.House.Value}") : "-";
            string latStr = PositionFormatter.FormatLatitude(p.Latitude);
            if (composite)
            {
                sb.AppendLine(
                    Inv, $"| {p.Name} | {PositionFormatter.FormatPosition(p)} | {latStr} | {houseStr} | {p.SpeedInLongitude:F3}/d | {p.DirectionCode} |");
                continue;
            }

            string raStr = p.RightAscension.HasValue
                ? PositionFormatter.FormatRightAscension(p.RightAscension.Value) : "-";
            string decStr = p.Declination.HasValue
                ? PositionFormatter.FormatDeclination(p.Declination.Value) : "-";
            string oobStr = PositionFormatter.IsOutOfBounds(p, result.Obliquity) ? "OOB" : "";
            sb.AppendLine(
                Inv, $"| {p.Name} | {PositionFormatter.FormatPosition(p)} | {latStr} | {raStr} | {decStr} | {houseStr} | {p.SpeedInLongitude:F3}/d | {p.DirectionCode} | {oobStr} |");
        }

        sb.AppendLine();

        sb.AppendLine("### " + MinorPointAspectsTitle);
        sb.AppendLine();
        if (result.MinorPointAspects.Count == 0)
        {
            sb.AppendLine(NoMinorPointAspectsLine);
        }
        else
        {
            if (composite)
            {
                sb.AppendLine("| Body 1 | Aspect | Body 2 | Orb |");
                sb.AppendLine("|--------|--------|--------|-----|");
            }
            else
            {
                sb.AppendLine("| Body 1 | Aspect | Body 2 | Orb | Ap/Sp |");
                sb.AppendLine("|--------|--------|--------|-----|-------|");
            }

            foreach (AspectResult a in result.MinorPointAspects)
            {
                // The row's own orb at the 2° limit, never the definition's 8°, 7° or 5°.
                string orbStr = PositionFormatter.FormatOrb(a.Orb);
                if (composite)
                {
                    sb.AppendLine($"| {a.Body1Name} | {a.Aspect.Abbreviation} | {a.Body2Name} | {orbStr} |");
                    continue;
                }

                sb.AppendLine(
                    $"| {a.Body1Name} | {a.Aspect.Abbreviation} | {a.Body2Name} | {orbStr} | {a.ApplyingSeparating} |");
            }
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Appends the declination aspects section showing parallels and
    /// contraparallels between planetary positions.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="declinationAspects">Declination aspects to format.</param>
    private static void AppendDeclinationAspects(
        StringBuilder sb, IReadOnlyList<DeclinationAspect> declinationAspects)
    {
        sb.AppendLine("## Declination Aspects (\u00b11\u00b0 orb)");
        sb.AppendLine();

        if (declinationAspects.Count > 0)
        {
            sb.AppendLine("| Body 1 | Dec 1 | Aspect | Body 2 | Dec 2 | Orb |");
            sb.AppendLine("|--------|-------|--------|--------|-------|-----|");

            foreach (DeclinationAspect da in declinationAspects)
            {
                string orbStr = PositionFormatter.FormatOrb(da.Orb);
                sb.AppendLine(
                    $"| {da.Body1Name} | {PositionFormatter.FormatDeclination(da.Body1Declination)} | {da.Abbreviation} | {da.Body2Name} | {PositionFormatter.FormatDeclination(da.Body2Declination)} | {orbStr} |");
            }
        }
        else
        {
            sb.AppendLine("No declination aspects found within \u00b11\u00b0 orb.");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Appends the declination clusters subsection showing groups of 3+
    /// chart points whose absolute declinations fall within a span of 2° or less.
    /// Only emitted when at least one cluster is found.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="positions">All planetary positions.</param>
    /// <param name="declinationAspects">Declination aspects for intra-cluster counting.</param>
    private static void AppendDeclinationClusters(
        StringBuilder sb,
        IReadOnlyList<PlanetPosition> positions,
        IReadOnlyList<DeclinationAspect> declinationAspects)
    {
        IReadOnlyList<DeclinationCluster> clusters =
            DeclinationAspectService.IdentifyClusters(positions, declinationAspects);

        if (clusters.Count == 0)
            return;

        sb.AppendLine("### Declination Clusters");
        sb.AppendLine();
        sb.AppendLine("| Cluster Zone | Points | Count | Aspects |");
        sb.AppendLine("|-------------|--------|-------|---------|");

        foreach (DeclinationCluster cluster in clusters)
        {
            string zone = FormatClusterZone(cluster.ZoneMinAbsDeclination, cluster.ZoneMaxAbsDeclination);
            string points = FormatClusterMembers(cluster.Members);
            sb.AppendLine(
                Inv, $"| {zone} | {points} | {cluster.Members.Count} | {cluster.IntraClusterAspectCount} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Formats a cluster zone range as ±{min}–{max} using degree/minute notation.
    /// </summary>
    /// <param name="minAbs">Minimum absolute declination in degrees.</param>
    /// <param name="maxAbs">Maximum absolute declination in degrees.</param>
    /// <returns>Formatted zone string, e.g., "±22°49'–23°51'".</returns>
    private static string FormatClusterZone(double minAbs, double maxAbs)
    {
        int minDeg = (int)minAbs;
        int minMin = (int)((minAbs - minDeg) * 60.0);
        int maxDeg = (int)maxAbs;
        int maxMin = (int)((maxAbs - maxDeg) * 60.0);
        return string.Create(Inv, $"\u00b1{minDeg}\u00b0{minMin:D2}'\u2013{maxDeg}\u00b0{maxMin:D2}'");
    }

    /// <summary>
    /// Formats cluster members as a comma-separated list with signed declinations.
    /// </summary>
    /// <param name="members">The cluster members to format.</param>
    /// <returns>Formatted string, e.g., "Moon (+22°08'), Jupiter (+22°53')".</returns>
    private static string FormatClusterMembers(IReadOnlyList<DeclinationClusterMember> members)
    {
        List<string> parts = new(members.Count);
        foreach (DeclinationClusterMember m in members)
        {
            string dec = PositionFormatter.FormatDeclination(m.Declination);
            parts.Add($"{m.Name} ({dec})");
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Save a chart result as Markdown to a file.
    /// Paran data is read from <see cref="ChartResult.Parans"/> when available.
    /// </summary>
    /// <param name="result">The chart calculation result to save.</param>
    /// <param name="filePath">Destination file path for the Markdown output.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        ChartResult result, string filePath, CancellationToken ct = default)
    {
        string md = ToMarkdown(result);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Appends the paran (paranatellonta) section showing angular crossing
    /// simultaneities detected within the configured time orb. Renders the
    /// paran events table (primary interpretive data).
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="paranResult">The paran calculation result to format.</param>
    private static void AppendParanSection(StringBuilder sb, ParanResult paranResult)
    {
        sb.AppendLine(Inv, $"## Parans (Paranatellonta) \u2014 {paranResult.OrbMinutes:F1} min orb");
        sb.AppendLine();

        if (paranResult.Parans.Count > 0)
        {
            sb.AppendLine("| Body A | Angle A | Position A | Body B | Angle B | Position B | Orb (min) |");
            sb.AppendLine("|--------|---------|------------|--------|---------|------------|-----------|");

            foreach (ParanEvent paran in paranResult.Parans)
            {
                string posA = PositionFormatter.FormatLongitude(paran.LongitudeA);
                string posB = PositionFormatter.FormatLongitude(paran.LongitudeB);

                sb.AppendLine(
                    Inv, $"| {paran.BodyAName} " +
                    $"| {FormatAngleType(paran.AngleA)} " +
                    $"| {posA} " +
                    $"| {paran.BodyBName} " +
                    $"| {FormatAngleType(paran.AngleB)} " +
                    $"| {posB} " +
                    $"| {paran.OrbMinutes:F2} |");
            }
        }
        else
        {
            sb.AppendLine("No paran events detected within the configured orb.");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Formats an <see cref="AngleType"/> as a human-readable string for
    /// the paran table display.
    /// </summary>
    /// <param name="angle">The angle type to format.</param>
    /// <returns>A string: "Rising", "Culminating", "Setting", or "Anti-Culm.".</returns>
    internal static string FormatAngleType(AngleType angle) => angle switch
    {
        AngleType.Rising => "Rising",
        AngleType.Culminating => "Culminating",
        AngleType.Setting => "Setting",
        AngleType.AntiCulminating => "Anti-Culm.",
        _ => angle.ToString(),
    };

    #region Stage 1-6 Sections

    /// <summary>
    /// Appends the planet conditions section (combustion, besiegement, benefic/malefic influence).
    /// Only rendered if any planet has active conditions.
    /// </summary>
    private static void AppendPlanetConditions(StringBuilder sb, ChartResult result)
    {
        if (result.PlanetConditions.Count == 0)
            return;

        List<PlanetCondition> active = result.PlanetConditions
            .Where(pc => pc.Conditions.Count > 0)
            .ToList();

        if (active.Count == 0)
            return;

        sb.AppendLine("## Planet Conditions (Bonification / Maltreatment)");
        sb.AppendLine();
        sb.AppendLine("| Planet | Score | Conditions |");
        sb.AppendLine("|--------|-------|------------|");

        foreach (PlanetCondition pc in active)
        {
            string scoreStr = pc.AccidentalScore > 0
                ? string.Create(Inv, $"+{pc.AccidentalScore}") : pc.AccidentalScore.ToString(Inv);
            string conditions = string.Join("; ", pc.Conditions);
            sb.AppendLine($"| {pc.PlanetName} | {scoreStr} | {conditions} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Appends the stations section. Only rendered if any planet is stationing.
    /// </summary>
    /// <remarks>
    /// The station type names the nearest station <i>event</i>; the Phase column
    /// says whether it is ahead or behind, and the time and position place it.
    /// The time is on the chart's own clock, as the Date/Time and Hour Period
    /// lines are: the zone's local time, or the explicit offset's for a chart
    /// cast at one (<see cref="ChartTimeFormatter.FormatLocalTime"/>). The speed
    /// keeps its sign so the current direction is visible beside the station
    /// type, and the proximity is rounded to the nearest percent rather than
    /// truncated (0.5874 is 59%, not 58%).
    /// </remarks>
    private static void AppendStations(StringBuilder sb, ChartResult result)
    {
        if (result.Stations.Count == 0)
            return;

        sb.AppendLine("## Stationing Planets");
        sb.AppendLine();
        sb.AppendLine("| Planet | Station | Phase | Station Time | Station Position | Speed | Proximity |");
        sb.AppendLine("|--------|---------|-------|--------------|------------------|-------|-----------|");

        foreach (StationInfo s in result.Stations)
        {
            int pct = s.StationProximity.HasValue
                ? (int)Math.Round(s.StationProximity.Value * 100.0, MidpointRounding.AwayFromZero)
                : 0;
            string phase = FormatStationPhase(s);
            string time = s.StationTimeUtc.HasValue
                ? ChartTimeFormatter.FormatLocalTime(s.StationTimeUtc.Value, result)
                : "-";
            string position = s.StationLongitude.HasValue
                ? PositionFormatter.FormatLongitude(s.StationLongitude.Value)
                : "-";

            sb.AppendLine(
                Inv, $"| {s.PlanetName} | {s.StationType} | {phase} | {time} | {position} | {s.DailySpeed:F4}\u00b0/day | {pct}% of threshold |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Formats the phase cell: "Approaching (in 1.85 d)" or "Separating (1.50 d ago)".
    /// </summary>
    private static string FormatStationPhase(StationInfo s)
    {
        if (!s.Phase.HasValue || !s.DaysToStation.HasValue)
            return "-";

        double days = Math.Abs(s.DaysToStation.Value);
        return s.Phase.Value == StationPhase.Approaching
            ? string.Create(Inv, $"Approaching (in {days:F2} d)")
            : string.Create(Inv, $"Separating ({days:F2} d ago)");
    }

    /// <summary>
    /// Appends the Moon mansion information.
    /// </summary>
    private static void AppendMoonMansion(StringBuilder sb, ChartResult result)
    {
        if (result.MoonMansion is null)
            return;

        MoonMansionInfo m = result.MoonMansion;
        sb.AppendLine(Inv, $"**Moon Mansion:** #{m.Number} {m.Name} ({m.Meaning}) \u2014 {m.Nature}  ");
        sb.AppendLine($"**Mansion Keywords:** {m.Keywords}");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends the planetary sect analysis subsection within Chart Context.
    /// </summary>
    private static void AppendPlanetarySect(StringBuilder sb, ChartContextInfo ctx)
    {
        if (ctx.PlanetarySect.Count == 0)
            return;

        sb.AppendLine("### Planetary Sect");
        sb.AppendLine();
        sb.AppendLine("| Planet | Sect | In Sect | Role | Hayz |");
        sb.AppendLine("|--------|------|---------|------|------|");

        foreach (SectInfo s in ctx.PlanetarySect)
        {
            string role = "";
            if (s.IsInSectBenefic) role = "In-Sect Benefic";
            else if (s.IsOutOfSectMalefic) role = "Out-of-Sect Malefic";
            else if (s.IsInSectMalefic) role = "In-Sect Malefic";
            else if (s.IsBenefic) role = "Benefic";
            else if (s.IsMalefic) role = "Malefic";

            sb.AppendLine(
                $"| {s.PlanetName} | {s.NaturalSect} | {(s.IsInSect ? "Yes" : "No")} | {role} | {(s.IsHayz ? "Yes" : "")} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Appends the Ascendant triplicity rulers subsection within Chart Context.
    /// </summary>
    private static void AppendTriplicityRulers(StringBuilder sb, ChartContextInfo ctx)
    {
        if (ctx.AscendantTriplicityRulers is null)
            return;

        TriplicityRulerInfo t = ctx.AscendantTriplicityRulers;
        sb.AppendLine("### Ascendant Triplicity Rulers");
        sb.AppendLine();
        sb.AppendLine($"**Element:** {t.Element} | **Day Ruler:** {t.DayRuler} | **Night Ruler:** {t.NightRuler} | **Participating:** {t.ParticipatingRuler}  ");
        sb.AppendLine($"**Active Ruler ({ctx.SectLabel} chart):** {t.ActiveRuler}");
        sb.AppendLine();
    }

    /// <summary>
    /// The Planetary Hour section exactly as <see cref="ToMarkdown"/> prints it, so the chart PDF prints
    /// the same text (O-D1 option b, branch O-6, 1.3.4).
    /// </summary>
    /// <param name="result">The chart.</param>
    /// <returns>The section's Markdown, or <see cref="string.Empty"/> when the chart has no planetary hour.</returns>
    internal static string PlanetaryHourSection(ChartResult result)
    {
        StringBuilder sb = new();
        AppendPlanetaryHour(sb, result);
        return sb.ToString();
    }

    /// <summary>
    /// The Horary Perfection Analysis section exactly as <see cref="ToMarkdown"/> prints it, so the chart
    /// PDF prints the same text (O-D1 option b, branch O-6, 1.3.4).
    /// </summary>
    /// <param name="result">The chart.</param>
    /// <returns>
    /// The section's Markdown, or <see cref="string.Empty"/> when the chart carries no horary analysis.
    /// </returns>
    internal static string HoraryPerfectionSection(ChartResult result)
    {
        StringBuilder sb = new();
        AppendHoraryPerfection(sb, result);
        return sb.ToString();
    }

    /// <summary>
    /// Appends the planetary hour section with radicality for horary charts.
    /// </summary>
    private static void AppendPlanetaryHour(StringBuilder sb, ChartResult result)
    {
        if (result.PlanetaryHour is null)
            return;

        PlanetaryHourInfo ph = result.PlanetaryHour;
        sb.AppendLine("## Planetary Hour");
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Hour Ruler:** {ph.RulingPlanet} ({(ph.Hour.IsDaytime ? "Day" : "Night")} hour #{ph.Hour.HourNumber})  ");
        // The period is on the chart's own clock, so it is labelled as the Date/Time line
        // labels that clock: the explicit offset, or the zone.
        string clockLabel = result.Input.UseExplicitOffset && result.Input.UtcOffsetHours.HasValue
            ? result.ResolvedOffsetLabel
            : $"({result.Input.TimeZone})";
        sb.AppendLine(Inv, $"**Hour Period:** {FormatHourBounds(ph.Hour, result)} {clockLabel}");

        if (ph.IsRadical.HasValue)
        {
            sb.AppendLine($"  ");
            sb.AppendLine($"**Radicality:** {ph.RadicalityLevel}  ");
            sb.AppendLine($"**Note:** {ph.RadicalityNote}");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// The Hour Period's start and end on the chart's own clock. When they fall on different
    /// offsets of the chart's zone — the clocks changed during the hour — each bound carries
    /// the zone record's name for its offset ("01:47:57 EDT – 01:55:57 EST"), or, when the
    /// two names are the same, its full offset label ("01:14:14 MSK (UTC+4) – 01:25:58 MSK
    /// (UTC+3)"), so the period cannot read backwards or shorter than it is. Otherwise the
    /// bounds are bare, as they always were.
    /// </summary>
    /// <param name="hour">The planetary hour.</param>
    /// <param name="result">The chart whose clock the hour is on.</param>
    /// <returns>The two bounds, <c>HH:mm:ss</c>, each with its label when they straddle a change.</returns>
    /// <remarks>
    /// A chart cast at an explicit offset reads its hour on that fixed clock, which never
    /// changes. Each bound's offset and name are read through the chart's time-zone rules from
    /// the bound's instant, never from its wall clock, which in a repeated hour names two
    /// instants. Culture-independent.
    /// </remarks>
    private static string FormatHourBounds(PlanetaryHour hour, ChartResult result)
    {
        string bare = string.Create(Inv, $"{hour.HourStart:HH:mm:ss} \u2013 {hour.HourEnd:HH:mm:ss}");

        if ((result.Input.UseExplicitOffset && result.Input.UtcOffsetHours.HasValue)
            || hour.HourStartUtc is not DateTime startUtc
            || hour.HourEndUtc is not DateTime endUtc)
        {
            return bare;
        }

        ITimeZoneRules rules = result.TimeZoneRules ?? new TzdbTimeZoneRules();
        ZoneReading start = rules.ToLocal(startUtc, result.Input.TimeZone);
        ZoneReading end = rules.ToLocal(endUtc, result.Input.TimeZone);

        if (start.Offset == end.Offset)
            return bare;

        // Where the change kept the zone's name (Moscow's MSK in 2014), the names cannot tell
        // the bounds apart, so each takes its full label from the one label builder.
        bool sameName = string.Equals(start.Abbreviation, end.Abbreviation, StringComparison.Ordinal);
        string startLabel = sameName
            ? OffsetLabelFormatter.Format(start.Offset, start.Abbreviation, isExplicit: false)
            : start.Abbreviation;
        string endLabel = sameName
            ? OffsetLabelFormatter.Format(end.Offset, end.Abbreviation, isExplicit: false)
            : end.Abbreviation;

        return string.Create(Inv, $"{hour.HourStart:HH:mm:ss} {startLabel} \u2013 {hour.HourEnd:HH:mm:ss} {endLabel}");
    }

    /// <summary>
    /// Appends the horary perfection analysis section. Only rendered when the
    /// engine stored an analysis, which it does for horary charts that have a
    /// question category set.
    /// </summary>
    private static void AppendHoraryPerfection(StringBuilder sb, ChartResult result)
    {
        if (result.HoraryPerfection is not HoraryPerfectionResult perfection)
            return;

        sb.AppendLine("## Horary Perfection Analysis");
        sb.AppendLine();
        sb.AppendLine($"**Question:** {perfection.Question}  ");
        sb.AppendLine($"**Category:** {perfection.QuestionCategory} \u2192 {OrdinalHouse(perfection.QuesitedHouseNumber)} house  ");
        sb.AppendLine($"**Querent (L1):** {perfection.QuerentSignificator} in {perfection.QuerentSign}, {OrdinalHouse(perfection.QuerentHouse)} \u2014 {perfection.QuerentDignity}  ");
        sb.AppendLine($"**Quesited (Lx):** {perfection.QuesitedSignificator} in {perfection.QuesitedSign}, {OrdinalHouse(perfection.QuesitedHouse)} \u2014 {perfection.QuesitedDignity}  ");
        sb.AppendLine();

        // Perfection
        sb.AppendLine("### Perfection");
        sb.AppendLine();
        if (perfection.IsPerfected)
        {
            sb.AppendLine("**Status:** Perfected  ");
            sb.Append($"**Mechanism:** {perfection.PerfectionMechanism}");
            if (perfection.PerfectionAspect is not null)
                sb.Append($" ({perfection.PerfectionAspect}, orb {FormatOrbOrEmpty(perfection.PerfectionOrb)})");
            if (perfection.Translator is not null)
                sb.Append($" via {perfection.Translator}");
            if (perfection.Collector is not null)
                sb.Append($" collected by {perfection.Collector}");
            sb.AppendLine("  ");
            if (perfection.PerfectionTimeUtc is DateTime perfectsAt)
                sb.AppendLine($"**Perfects:** {ChartTimeFormatter.FormatLocalTime(perfectsAt, result)}  ");
        }
        else
        {
            sb.AppendLine("**Status:** Not perfected  ");
        }

        // The followed application's fate, whichever way the status went: a direct
        // aspect that turns around is reported even when another mechanism perfects.
        if (perfection.ApplicationFailsTimeUtc is DateTime failsAt)
        {
            sb.AppendLine(
                $"**Application fails:** {perfection.ApplyingPair} {perfection.ApplyingAspect?.ToLowerInvariant()}, " +
                $"closest {FormatOrbOrEmpty(perfection.ApplicationClosestOrb)} on {ChartTimeFormatter.FormatLocalTime(failsAt, result)}  ");
        }
        else if (perfection.ApplicationCapped)
        {
            sb.AppendLine(
                Inv, $"**Application unresolved:** {perfection.ApplyingPair} {perfection.ApplyingAspect?.ToLowerInvariant()} " +
                $"still applying {AspectPerfectionFinder.MaxFollowDays:F0} days after the chart  ");
        }

        sb.AppendLine();

        // Denial
        sb.AppendLine("### Denial Factors");
        sb.AppendLine();
        if (perfection.DenialReasons.Count > 0)
        {
            foreach (string reason in perfection.DenialReasons)
                sb.AppendLine($"- {reason}  ");
        }
        else
        {
            sb.AppendLine("None detected.  ");
        }

        sb.AppendLine();

        // Notes — what qualifies the judgment without denying it (stations that
        // did not prevent, failed applications with no station, unresolved
        // applications, and the rest listed on HoraryPerfectionResult.Notes).
        // Only rendered when there are any.
        if (perfection.Notes.Count > 0)
        {
            sb.AppendLine("### Notes");
            sb.AppendLine();
            foreach (string note in perfection.Notes)
                sb.AppendLine($"- {note}  ");
            sb.AppendLine();
        }

        // Reception
        sb.AppendLine("### Reception");
        sb.AppendLine();
        sb.AppendLine($"**Quality:** {perfection.ReceptionQuality}  ");
        if (perfection.ReceptionDetail is not null)
            sb.AppendLine($"**Detail:** {perfection.ReceptionDetail}  ");
        sb.AppendLine();

        // Moon
        sb.AppendLine("### Moon's Testimony");
        sb.AppendLine();
        if (perfection.MoonFirstApplying is not null)
            sb.AppendLine($"**First Applying:** {perfection.MoonFirstApplying}  ");
        sb.AppendLine($"**Void of Course:** {(perfection.MoonVoC ? "Yes" : "No")}  ");
        sb.AppendLine($"**Testimony:** {perfection.MoonTestimony}  ");
        sb.AppendLine();

        // Judgment
        sb.AppendLine("### Judgment");
        sb.AppendLine();
        sb.AppendLine($"**{perfection.Judgment}**  ");
        sb.AppendLine(perfection.JudgmentNote);
        sb.AppendLine();
    }

    /// <summary>
    /// Appends the annual profection analysis section. Only rendered for
    /// Birth charts. Uses <paramref name="referenceDate"/> if supplied, else
    /// today's date.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="result">The natal chart result.</param>
    /// <param name="referenceDate">
    /// The date the profection is evaluated "as of". Always supplied by the
    /// caller — <see cref="ToMarkdown"/> passes the date resolved from its
    /// injected <see cref="TimeProvider"/> (the real current day in
    /// production), and the solar/lunar/planetary return formatters pass the
    /// return-ingress instant to evaluate the natal-keyed profection at that
    /// moment (a solar return's for the year it opens: <paramref name="isReturnContext"/>).
    /// Made non-optional in F-GR-2 so no ambient
    /// <see cref="DateTime.Today"/> can re-enter this formatter and drift the
    /// golden snapshots.
    /// </param>
    /// <param name="headingOverride">
    /// Custom H2 heading text. Defaults to <c>## Annual Profection</c>.
    /// Return formatters override this to <c>## Annual Profection (at Return Ingress)</c>
    /// to disambiguate from natal exports.
    /// </param>
    /// <param name="isReturnContext">
    /// When <c>true</c>, dispatches to <see cref="ProfectionAnalyzer.AnalyzeForReturn"/>:
    /// the year the solar return opens, its age rounded from the birth instant
    /// whichever side of the birthday the return falls on, dated the day that
    /// year is read on, the birthday it begins on when the return falls before
    /// it (branch ReturnAges, 1.7.2). Defaults to <c>false</c>, which preserves the natal
    /// call-site semantic via <see cref="ProfectionAnalyzer.Analyze"/>, the year
    /// running at the reference date by completed anniversaries. Only the solar
    /// return formatter passes <c>true</c>, to this section and to the two that
    /// follow it; the lunar and planetary return
    /// formatters, and the natal call site at <see cref="ToMarkdown"/>, pass
    /// nothing and fall through to <c>false</c> (branch H, 1.3.4).
    /// </param>
    internal static void AppendProfectionYear(
        StringBuilder sb,
        ChartResult result,
        DateTime referenceDate,
        string headingOverride = "## Annual Profection",
        bool isReturnContext = false)
    {
        if (!string.Equals(result.Input.EventType, "Birth", StringComparison.OrdinalIgnoreCase))
            return;

        ProfectionYearResult profection = isReturnContext
            ? ProfectionAnalyzer.AnalyzeForReturn(result, referenceDate)
            : ProfectionAnalyzer.Analyze(result, referenceDate);

        sb.AppendLine(headingOverride);
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Age:** {profection.Age} (as of {profection.ReferenceDate:yyyy-MM-dd})  ");
        sb.AppendLine($"**Profected House:** {OrdinalHouse(profection.ProfectedHouse)} ({profection.ProfectedSign})  ");
        sb.AppendLine($"**Lord of the Year:** {profection.LordOfTheYear}  ");
        sb.AppendLine($"**Lord Natal Position:** {profection.LordNatalSign}, {OrdinalHouse(profection.LordNatalHouse)}  ");
        sb.AppendLine($"**Lord Dignity:** {profection.LordDignity}  ");
        if (profection.LordRetrograde)
            sb.AppendLine("**Lord Retrograde:** Yes  ");
        if (profection.LordConditions.Count > 0)
            sb.AppendLine($"**Lord Conditions:** {string.Join(", ", profection.LordConditions)}  ");
        sb.AppendLine($"**Lord In Sect:** {(profection.LordInSect ? "Yes" : "No")}  ");
        sb.AppendLine($"**Activated Themes:** {profection.ActivatedThemes}  ");
        sb.AppendLine();

        // Lord's natal aspects
        if (profection.LordNatalAspects.Count > 0)
        {
            sb.AppendLine("### Lord's Natal Aspects");
            sb.AppendLine();
            foreach (string aspect in profection.LordNatalAspects)
                sb.AppendLine($"- {aspect}  ");
            sb.AppendLine();
        }

        // 12-year profection table
        sb.AppendLine("### Profection Table");
        sb.AppendLine();
        sb.AppendLine("| House | Sign | Lord | Ages | Themes |");
        sb.AppendLine("|-------|------|------|------|--------|");
        foreach (ProfectionTableEntry entry in profection.ProfectionTable)
        {
            string marker = entry.IsCurrent ? " **\u2190 current**" : string.Empty;
            sb.AppendLine(
                $"| {OrdinalHouse(entry.House)} | {entry.Sign} | {entry.Lord} | {entry.Ages} | {entry.Themes}{marker} |");
        }

        sb.AppendLine();

        // Summary
        sb.AppendLine("### Summary");
        sb.AppendLine();
        sb.AppendLine(profection.Summary);
        sb.AppendLine();
    }

    /// <summary>
    /// The day a solar return's Firdaria and Zodiacal Releasing are read on, as a plain date the analyzers
    /// take as given: the day <see cref="ProfectionAnalyzer.YearOpenedBySolarReturn"/> decides, the one place
    /// the profection's age and date come from too, so the three sections are one year's (branch ReturnAges,
    /// 1.7.2). It is the birthday the year begins on when the return falls before it, and the return's own
    /// day on the natal chart's clock otherwise.
    /// </summary>
    /// <param name="natal">The natal chart, a Birth chart.</param>
    /// <param name="returnIngressUtc">The solar return's instant, UTC.</param>
    /// <returns>The reading day at midnight, of <see cref="DateTimeKind.Unspecified"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the return's day on the chart's clock is before the birth day (T6-6).
    /// </exception>
    private static DateTime SolarReturnReadingDay(ChartResult natal, DateTime returnIngressUtc) =>
        ProfectionAnalyzer.YearOpenedBySolarReturn(natal, returnIngressUtc).ReadingDay.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Appends the firdaria section. Only rendered for Birth charts.
    /// Uses <paramref name="referenceDate"/> if supplied, else today's date. Every boundary prints on the
    /// calendar in force through <see cref="CalendarDateFormatter"/> (R-D6); a node's period, undivided,
    /// prints no sub-period and no Sub-Periods table (T-D8).
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="result">The natal chart result.</param>
    /// <param name="referenceDate">
    /// The date the firdaria periods are evaluated "as of". Always supplied by
    /// the caller — <see cref="ToMarkdown"/> passes the date resolved from its
    /// injected <see cref="TimeProvider"/> (the real current day in
    /// production), and the solar/lunar/planetary return formatters pass the
    /// return-ingress instant to evaluate the natal-keyed firdaria periods at
    /// that moment (a solar return's for the year it opens: <paramref name="isReturnContext"/>).
    /// Made non-optional in F-GR-2 so no ambient
    /// <see cref="DateTime.Today"/> can re-enter this formatter and drift the
    /// golden snapshots.
    /// </param>
    /// <param name="headingOverride">
    /// Custom H2 heading text. Defaults to <c>## Firdaria</c>.
    /// Return formatters override this to <c>## Firdaria (at Return Ingress)</c>
    /// to disambiguate from natal exports.
    /// </param>
    /// <param name="isReturnContext">
    /// When <c>true</c>, <paramref name="referenceDate"/> is a solar return's instant and the periods are
    /// those of the year the return opens, read on the day <see cref="SolarReturnReadingDay"/> gives, as the
    /// profection beside them is (branch ReturnAges, 1.7.2). Defaults to <c>false</c>, the periods at the
    /// reference date. Only the solar return formatter passes <c>true</c>; a lunar or planetary return keeps
    /// the year running at the return.
    /// </param>
    internal static void AppendFirdaria(
        StringBuilder sb,
        ChartResult result,
        DateTime referenceDate,
        string headingOverride = "## Firdaria",
        bool isReturnContext = false)
    {
        if (!string.Equals(result.Input.EventType, "Birth", StringComparison.OrdinalIgnoreCase))
            return;

        if (isReturnContext)
            referenceDate = SolarReturnReadingDay(result, referenceDate);

        FirdariaResult firdaria = FirdariaAnalyzer.Analyze(result, referenceDate);

        sb.AppendLine(headingOverride);
        sb.AppendLine();
        sb.AppendLine($"**Chart Sect:** {(firdaria.IsDayChart ? "Day" : "Night")}  ");
        sb.AppendLine(Inv, $"**Current Major Period:** {firdaria.CurrentMajorRuler} " +
                      $"({CalendarDateFormatter.Format(firdaria.CurrentMajorStart, "yyyy")}\u2013{CalendarDateFormatter.Format(firdaria.CurrentMajorEnd, "yyyy")}, " +
                      $"{firdaria.CurrentMajorYears}y)  ");
        sb.AppendLine(firdaria.CurrentSubRuler.Length == 0
            ? "**Current Sub-Period:** none (a node's period is not divided)  "
            : $"**Current Sub-Period:** {firdaria.CurrentSubRuler} " +
              $"({CalendarDateFormatter.Date(firdaria.CurrentSubStart)}\u2013{CalendarDateFormatter.Date(firdaria.CurrentSubEnd)})  ");
        sb.AppendLine();

        // Full major period timeline
        sb.AppendLine("### Major Period Timeline");
        sb.AppendLine();
        sb.AppendLine("| Ruler | Years | Start | End | Status |");
        sb.AppendLine("|-------|-------|-------|-----|--------|");

        foreach (FirdariaPeriod period in firdaria.Periods)
        {
            string status = period.IsCurrent ? "**\u2190 current**" : string.Empty;
            sb.AppendLine(
                Inv, $"| {period.Ruler} | {period.Years} | {CalendarDateFormatter.Date(period.StartDate)} | " +
                $"{CalendarDateFormatter.Date(period.EndDate)} | {status} |");
        }

        sb.AppendLine();

        // Sub-periods for current major period
        FirdariaPeriod? currentPeriod = firdaria.Periods.FirstOrDefault(p => p.IsCurrent);
        if (currentPeriod is not null && currentPeriod.SubPeriods.Count > 0)
        {
            sb.AppendLine($"### Sub-Periods ({currentPeriod.Ruler} Period)");
            sb.AppendLine();
            sb.AppendLine("| Ruler | Start | End | Status |");
            sb.AppendLine("|-------|-------|-----|--------|");

            foreach (FirdariaSubPeriod sub in currentPeriod.SubPeriods)
            {
                string status = sub.IsCurrent ? "**\u2190 current**" : string.Empty;
                sb.AppendLine(
                    $"| {sub.Ruler} | {CalendarDateFormatter.Date(sub.StartDate)} | " +
                    $"{CalendarDateFormatter.Date(sub.EndDate)} | {status} |");
            }

            sb.AppendLine();
        }

        // Summary
        sb.AppendLine("### Summary");
        sb.AppendLine();
        sb.AppendLine(firdaria.Summary);
        sb.AppendLine();
    }

    /// <summary>
    /// Appends the zodiacal releasing section. Only rendered for Birth charts.
    /// Uses <paramref name="referenceDate"/> if supplied, else today's date. Every boundary prints on the
    /// calendar in force through <see cref="CalendarDateFormatter"/> (R-D6), and a Level 2 row's Duration
    /// is its whole months of 30 days, "27m" (T-D10).
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="result">The natal chart result.</param>
    /// <param name="referenceDate">
    /// The date the zodiacal releasing periods are evaluated "as of". Always
    /// supplied by the caller — <see cref="ToMarkdown"/> passes the date
    /// resolved from its injected <see cref="TimeProvider"/> (the real current
    /// day in production), and the solar/lunar/planetary return formatters pass
    /// the return-ingress instant to evaluate the natal-keyed releasing periods
    /// at that moment (a solar return's for the year it opens: <paramref name="isReturnContext"/>).
    /// Made non-optional in F-GR-2 so no ambient
    /// <see cref="DateTime.Today"/> can re-enter this formatter and drift the
    /// golden snapshots.
    /// </param>
    /// <param name="headingOverride">
    /// Custom H2 heading text. Defaults to <c>## Zodiacal Releasing (Spirit)</c>.
    /// Return formatters override this to
    /// <c>## Zodiacal Releasing (Spirit) (at Return Ingress)</c> to disambiguate
    /// from natal exports. The override is also applied to the Lot-of-Spirit
    /// unavailable placeholder branch.
    /// </param>
    /// <param name="isReturnContext">
    /// When <c>true</c>, <paramref name="referenceDate"/> is a solar return's instant and the periods are
    /// those of the year the return opens, read on the day <see cref="SolarReturnReadingDay"/> gives, as the
    /// profection and the Firdaria beside them are (branch ReturnAges, 1.7.2). Defaults to <c>false</c>, the
    /// periods at the reference date. Only the solar return formatter passes <c>true</c>; a lunar or planetary
    /// return keeps the year running at the return.
    /// </param>
    internal static void AppendZodiacalReleasing(
        StringBuilder sb,
        ChartResult result,
        DateTime referenceDate,
        string headingOverride = "## Zodiacal Releasing (Spirit)",
        bool isReturnContext = false)
    {
        if (!string.Equals(result.Input.EventType, "Birth", StringComparison.OrdinalIgnoreCase))
            return;

        if (isReturnContext)
            referenceDate = SolarReturnReadingDay(result, referenceDate);

        ZodiacalReleasingResult zr = ZodiacalReleasingAnalyzer.Analyze(result, referenceDate);

        // Skip if Lot of Spirit was not available
        if (zr.Level1Periods.Count == 0)
        {
            sb.AppendLine(headingOverride);
            sb.AppendLine();
            sb.AppendLine("*Lot of Spirit not available in this chart.*");
            sb.AppendLine();
            return;
        }

        sb.AppendLine(headingOverride);
        sb.AppendLine();
        sb.AppendLine($"**Lot of Spirit:** {zr.LotOfSpiritSign}  ");
        sb.AppendLine($"**Lot of Fortune:** {zr.LotOfFortuneSign}  ");
        sb.AppendLine(Inv, $"**Level 1:** {zr.CurrentL1Sign} ({zr.CurrentL1Ruler}), " +
                      $"{CalendarDateFormatter.Format(zr.CurrentL1Start, "yyyy")}\u2013{CalendarDateFormatter.Format(zr.CurrentL1End, "yyyy")}, " +
                      $"{zr.CurrentL1Years}y — {zr.CurrentL1Angularity}" +
                      $"{(zr.CurrentL1IsPeak ? " (Peak)" : string.Empty)}  ");
        sb.AppendLine($"**Level 2:** {zr.CurrentL2Sign} ({zr.CurrentL2Ruler}), " +
                      $"{CalendarDateFormatter.Date(zr.CurrentL2Start)}\u2013{CalendarDateFormatter.Date(zr.CurrentL2End)} — " +
                      $"{zr.CurrentL2Angularity}" +
                      $"{(zr.CurrentL2IsPeak ? " (Peak)" : string.Empty)}  ");
        sb.AppendLine();

        // Level 1 timeline
        sb.AppendLine("### Level 1 Timeline");
        sb.AppendLine();
        sb.AppendLine("| Sign | Ruler | Years | Start | End | Angularity | Status |");
        sb.AppendLine("|------|-------|-------|-------|-----|------------|--------|");

        foreach (ZRPeriod period in zr.Level1Periods)
        {
            string peakMark = period.IsPeak ? "\u2605" : string.Empty;
            string status = period.IsCurrent ? "**\u2190 current**" : string.Empty;
            sb.AppendLine(
                Inv, $"| {period.Sign} | {period.Ruler} | {(int)period.Years} | " +
                $"{CalendarDateFormatter.Date(period.StartDate)} | {CalendarDateFormatter.Date(period.EndDate)} | " +
                $"{period.Angularity} {peakMark} | {status} |");
        }

        sb.AppendLine();

        // Level 2 sub-periods for current Level 1
        if (zr.CurrentL2Periods.Count > 0)
        {
            sb.AppendLine($"### Level 2 Sub-Periods ({zr.CurrentL1Sign})");
            sb.AppendLine();
            sb.AppendLine("| Sign | Ruler | Duration | Start | End | Angularity | Status |");
            sb.AppendLine("|------|-------|----------|-------|-----|------------|--------|");

            foreach (ZRPeriod sub in zr.CurrentL2Periods)
            {
                string peakMark = sub.IsPeak ? "\u2605" : string.Empty;
                string status = sub.IsCurrent ? "**\u2190 current**" : string.Empty;
                // The whole months of 30 days, the clamped last row its own (T-D10).
                int months = (int)Math.Round((sub.EndDate - sub.StartDate).TotalDays / 30.0);
                sb.AppendLine(
                    Inv, $"| {sub.Sign} | {sub.Ruler} | {months}m | " +
                    $"{CalendarDateFormatter.Date(sub.StartDate)} | {CalendarDateFormatter.Date(sub.EndDate)} | " +
                    $"{sub.Angularity} {peakMark} | {status} |");
            }

            sb.AppendLine();
        }

        // Summary
        sb.AppendLine("### Summary");
        sb.AppendLine();
        sb.AppendLine(zr.Summary);
        sb.AppendLine();
    }

    /// <summary>
    /// Appends the Arabic Parts (Lots) section.
    /// </summary>
    private static void AppendArabicParts(StringBuilder sb, ChartResult result)
    {
        if (result.ArabicParts.Count == 0)
            return;

        sb.AppendLine("## Arabic Parts / Lots");
        sb.AppendLine();
        sb.AppendLine("| Name | Position | House | Ruler | Formula | Domain |");
        sb.AppendLine("|------|----------|-------|-------|---------|--------|");

        foreach (ArabicPart ap in result.ArabicParts)
        {
            string position = PositionFormatter.FormatLongitude(ap.Longitude);
            string house = ap.House.HasValue ? ap.House.Value.ToString(Inv) : "-";
            sb.AppendLine(
                $"| {ap.Name} | {position} | {house} | {ap.Ruler} | {ap.Formula} | {ap.Domain} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Appends the antiscia connections section.
    /// Only rendered if connections exist.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="result">The chart calculation result.</param>
    private static void AppendAntisciaConnections(StringBuilder sb, ChartResult result)
    {
        if (result.AntisciaConnections.Count == 0)
            return;

        sb.AppendLine("## Antiscia Connections");
        sb.AppendLine();
        sb.AppendLine("| Planet 1 | Planet 2 | Type | Orb |");
        sb.AppendLine("|----------|----------|------|-----|");

        foreach (AntisciaConnection ac in result.AntisciaConnections)
        {
            string orbStr = PositionFormatter.FormatOrb(ac.Orb);
            string typeLabel = ac.Type == "Antiscia"
                ? "Antiscia (hidden \u260c)" : "Contra-antiscia (hidden \u260d)";
            sb.AppendLine(
                $"| {ac.Planet1} | {ac.Planet2} | {typeLabel} | {orbStr} |");
        }

        sb.AppendLine();
    }

    #endregion

    #region Medical Astrology

    /// <summary>
    /// The line that heads the medical correspondences, in the chart export's section and, since branch
    /// ExportText (1.7.2; finding N14, Steve's ruling), once at the head of the medical export's own sections.
    /// </summary>
    internal const string MedicalBasisLine =
        "*Traditional correspondences for educational and historical study. Not medical advice.*";

    /// <summary>
    /// Appends medical astrology correspondence tables after the last chart
    /// section and before the footer. Contains temperament, planetary body
    /// correspondences, zodiacal anatomy, and health houses.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="result">The chart calculation result.</param>
    /// <param name="includeTemperament">
    /// When false, the temperament block is left out and the other tables print as before
    /// (a derived chart, ruling S-D9).
    /// </param>
    private static void AppendMedicalSection(StringBuilder sb, ChartResult result, bool includeTemperament)
    {
        sb.AppendLine("## Medical Astrology Correspondences");
        sb.AppendLine();
        sb.AppendLine(MedicalBasisLine);
        sb.AppendLine();

        // Temperament
        if (includeTemperament)
            AppendTemperamentTable(sb, result);

        // Planetary Body Correspondences
        AppendPlanetaryCorrespondences(sb, result);

        // Zodiacal Anatomy (Emphasised Signs)
        AppendZodiacalAnatomy(sb, result);

        // Health Houses (6th, 8th, 12th)
        AppendHealthHouses(sb, result);
    }

    /// <summary>
    /// Appends the temperament assessment table with elemental scores.
    /// </summary>
    private static void AppendTemperamentTable(StringBuilder sb, ChartResult result)
    {
        TemperamentProfile temperament = TemperamentCalculator.Calculate(result);

        sb.AppendLine("### Temperament");
        sb.AppendLine();
        sb.AppendLine($"**Dominant:** {temperament.DominantText} | **Secondary:** {temperament.SecondaryText}");
        sb.AppendLine();
        sb.AppendLine("| Element | Temperament | Qualities | Score |");
        sb.AppendLine("|---------|-------------|-----------|-------|");
        sb.AppendLine(Inv, $"| Fire | Choleric | Hot & Dry | {temperament.FireScore} |");
        sb.AppendLine(Inv, $"| Earth | Melancholic | Cold & Dry | {temperament.EarthScore} |");
        sb.AppendLine(Inv, $"| Air | Sanguine | Hot & Moist | {temperament.AirScore} |");
        sb.AppendLine(Inv, $"| Water | Phlegmatic | Cold & Moist | {temperament.WaterScore} |");
        sb.AppendLine();
        sb.AppendLine("Scoring (AstroCast's own scheme): ASC sign 3pts, ASC ruler sign 2pts, Moon sign 3pts, Moon dispositor sign 2pts, Sun sign 1pt, each body in houses 1, 4, 7 or 10 1pt.");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends the planetary body correspondences table.
    /// </summary>
    private static void AppendPlanetaryCorrespondences(StringBuilder sb, ChartResult result)
    {
        sb.AppendLine("### Planetary Body Correspondences");
        sb.AppendLine();
        sb.AppendLine("| Planet | Sign | House | Body Regions | Health Role |");
        sb.AppendLine("|--------|------|-------|-------------|-------------|");

        foreach (PlanetPosition pos in result.Positions)
        {
            IReadOnlyList<BodyRegion> regions = PlanetaryHealthMap.GetBodyRegions(pos.Body);
            if (regions.Count == 0) continue; // Skip TrueNode, Chiron

            string healthRole = PlanetaryHealthMap.GetHealthRole(pos.Body);
            string houseStr = pos.House.HasValue ? pos.House.Value.ToString(Inv) : "-";
            string regionNames = string.Join("; ", regions.Select(r => r.Name));
            string roleShort = TruncateAtSentence(healthRole);

            sb.AppendLine($"| {pos.Name} | {pos.Sign} | {houseStr} | {regionNames} | {roleShort} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Appends the zodiacal anatomy table for emphasised signs (ASC, Sun, Moon,
    /// and stellium signs with 2+ planets).
    /// </summary>
    private static void AppendZodiacalAnatomy(StringBuilder sb, ChartResult result)
    {
        // Collect emphasised signs with reasons
        Dictionary<ZodiacSign, List<string>> emphases = new();

        void AddEmphasis(ZodiacSign sign, string reason)
        {
            if (!emphases.ContainsKey(sign))
                emphases[sign] = [];
            emphases[sign].Add(reason);
        }

        // Ascendant sign
        ZodiacSign ascSign = GetSignFromLongitude(result.Houses.Ascendant);
        AddEmphasis(ascSign, "Ascendant");

        // Sun sign
        PlanetPosition? sunPos = result.Positions.FirstOrDefault(p => p.Body == CelestialBodyId.Sun);
        if (sunPos is not null)
            AddEmphasis(sunPos.Sign, "Sun sign");

        // Moon sign
        PlanetPosition? moonPos = result.Positions.FirstOrDefault(p => p.Body == CelestialBodyId.Moon);
        if (moonPos is not null)
            AddEmphasis(moonPos.Sign, "Moon sign");

        // Stellium signs (2+ planets)
        Dictionary<ZodiacSign, int> signCounts = new();
        foreach (PlanetPosition pos in result.Positions)
        {
            if (!signCounts.ContainsKey(pos.Sign))
                signCounts[pos.Sign] = 0;
            signCounts[pos.Sign]++;
        }

        foreach (KeyValuePair<ZodiacSign, int> kv in signCounts)
        {
            if (kv.Value >= 2 && !emphases.ContainsKey(kv.Key))
                AddEmphasis(kv.Key, string.Create(Inv, $"Stellium ({kv.Value} planets)"));
        }

        sb.AppendLine("### Zodiacal Anatomy");
        sb.AppendLine();
        sb.AppendLine("| Sign | Reason | Body Regions | Focus Areas |");
        sb.AppendLine("|------|--------|-------------|-------------|");

        foreach (KeyValuePair<ZodiacSign, List<string>> kv in emphases)
        {
            IReadOnlyList<BodyRegion> regions = ZodiacBodyMap.GetBodyRegions(kv.Key);
            string regionNames = string.Join("; ", regions.Select(r => r.Name));
            string summary = ZodiacBodyMap.GetHealthSummary(kv.Key);
            string reasons = string.Join(", ", kv.Value);

            sb.AppendLine($"| {kv.Key} | {reasons} | {regionNames} | {summary} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Appends the health houses table (6th, 8th, 12th) with cusp signs,
    /// planets, and body region correspondences.
    /// </summary>
    private static void AppendHealthHouses(StringBuilder sb, ChartResult result)
    {
        sb.AppendLine("### Health Houses");
        sb.AppendLine();
        sb.AppendLine("| House | Meaning | Cusp Sign | Planets | Sign Body Regions |");
        sb.AppendLine("|-------|---------|-----------|---------|-------------------|");

        AppendHealthHouseRow(sb, result, 6, "Daily health, acute illness",
            result.Houses.Cusps[5]);
        AppendHealthHouseRow(sb, result, 8, "Chronic conditions, surgery",
            result.Houses.Cusps[7]);
        AppendHealthHouseRow(sb, result, 12, "Hidden illness, immunity",
            result.Houses.Cusps[11]);

        sb.AppendLine();
    }

    /// <summary>
    /// Appends a single health house row to the table.
    /// </summary>
    private static void AppendHealthHouseRow(
        StringBuilder sb, ChartResult result, int houseNumber, string meaning, double cuspLongitude)
    {
        ZodiacSign cuspSign = GetSignFromLongitude(cuspLongitude);
        List<string> planets = result.Positions
            .Where(p => p.House == houseNumber)
            .Select(p => p.Name)
            .ToList();
        string planetsStr = planets.Count > 0 ? string.Join(", ", planets) : "None";

        IReadOnlyList<BodyRegion> regions = ZodiacBodyMap.GetBodyRegions(cuspSign);
        string regionNames = string.Join("; ", regions.Select(r => r.Name));

        sb.AppendLine($"| {OrdinalHouse(houseNumber)} | {meaning} | {cuspSign} | {planetsStr} | {regionNames} |");
    }

    /// <summary>
    /// Truncate a string at the first sentence boundary for table brevity.
    /// </summary>
    private static string TruncateAtSentence(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        int periodIndex = text.IndexOf(". ", StringComparison.Ordinal);
        return periodIndex > 0 ? text[..(periodIndex + 1)] : text;
    }

    /// <summary>
    /// Derive the zodiac sign from an ecliptic longitude (0–360 degrees).
    /// </summary>
    private static ZodiacSign GetSignFromLongitude(double longitude) =>
        (ZodiacSign)((int)(longitude % 360.0 / 30.0) + 1);

    #endregion
}
