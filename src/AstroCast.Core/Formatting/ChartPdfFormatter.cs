using System.Globalization;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Generates a professional PDF chart sheet from a <see cref="ChartResult"/>.
/// Uses QuestPDF with a clean, black-and-white layout suitable for printing
/// and verification against reference charts.
/// </summary>
/// <remarks>
/// <para>
/// The PDF contains: a page header with the chart's name, date, calendar, zone, place, type, gender,
/// offset and Julian Day; then, once, the chart's source, description, notes and Time Note; the
/// planetary positions table with the Markdown's columns (body, position, latitude, declination, house,
/// speed, direction, out-of-bounds); the Asteroids and Lilith section when the chart carries the five
/// (R6; branch AsteroidsExport, 1.6.0); essential dignities; Moon conditions; chart context; the planetary
/// hour; a horary chart's question and perfection analysis; house cusps (two-column layout); aspects;
/// midpoint activations; fixed stars; parans; and a footer with engine version and generation timestamp.
/// </para>
/// <para>
/// The planetary hour and the horary analysis are the Markdown's own sections
/// (<see cref="ChartMarkdownFormatter.PlanetaryHourSection"/>,
/// <see cref="ChartMarkdownFormatter.HoraryPerfectionSection"/>), drawn line for line, so the two exports
/// say the same (O-D1 option b, branch O-6, 1.3.4). Until 1.3.4 the PDF had neither, its positions had no
/// latitude, declination or out-of-bounds column, and the source, description, notes and Time Note were in
/// the page header, so they printed on every page and a note taller than a page could not be laid out
/// (E1-6).
/// </para>
/// </remarks>
public static class ChartPdfFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generate and save a chart PDF to a file.
    /// </summary>
    /// <param name="result">The chart calculation result to render.</param>
    /// <param name="filePath">Destination file path for the PDF.</param>
    /// <param name="cancellationToken">
    /// Cooperative cancellation token. Checked before QuestPDF layout begins
    /// and again after the file has been written (at which point the caller
    /// may delete the partial file if desired). QuestPDF itself cannot be
    /// interrupted mid-render, so mid-generation cancellation is not supported.
    /// </param>
    public static void Save(ChartResult result, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        cancellationToken.ThrowIfCancellationRequested();

        QuestPDF.Settings.License = LicenseType.Community;

        BuildDocument(result).GeneratePdf(filePath);

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Generate a chart PDF as a byte array.
    /// </summary>
    /// <param name="result">The chart calculation result to render.</param>
    /// <returns>PDF file contents as a byte array.</returns>
    public static byte[] Generate(ChartResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        QuestPDF.Settings.License = LicenseType.Community;

        return BuildDocument(result).GeneratePdf();
    }

    #region Document Builder

    /// <summary>
    /// Build the QuestPDF document model for a chart result, with its document information: the title and the
    /// creator, and no author or subject (branch ExportText, 1.7.2; finding A4). Until 1.7.2 the document
    /// carried no information of its own, so a PDF reader showed the file's name for its title.
    /// </summary>
    private static Document BuildDocument(ChartResult result)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(54); // 0.75 inches
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Element(c => ComposeHeader(c, result));
                page.Content().Element(c => ComposeContent(c, result));
                page.Footer().Element(c => ComposeFooter(c, result));
            });
        })
        .WithMetadata(new DocumentMetadata
        {
            Title = DocumentTitle(result),
            Creator = DocumentCreator(result),
        });
    }

    /// <summary>
    /// The PDF's title: the chart's name and its kind, as the page heading states them ("Eleanor Hartwell
    /// (Birth)"); the name alone for a chart with no kind.
    /// </summary>
    /// <param name="result">The chart.</param>
    /// <returns>The title.</returns>
    private static string DocumentTitle(ChartResult result) =>
        string.IsNullOrWhiteSpace(result.Input.EventType)
            ? result.Input.Name
            : $"{result.Input.Name} ({result.Input.EventType})";

    /// <summary>
    /// The PDF's creator: the program's name and version, "AstroCast 1.7.2", the head of the engine version
    /// the footer prints, without its Swiss Ephemeris and time-zone part.
    /// </summary>
    /// <param name="result">The chart, whose engine version names the program that cast it.</param>
    /// <returns>The creator.</returns>
    private static string DocumentCreator(ChartResult result)
    {
        int detail = result.EngineVersion.IndexOf(" (", StringComparison.Ordinal);
        return detail < 0 ? result.EngineVersion : result.EngineVersion[..detail];
    }

    #endregion

    #region Header

    /// <summary>
    /// Compose the chart header with name, date, location, and metadata.
    /// </summary>
    private static void ComposeHeader(IContainer container, ChartResult result)
    {
        container.Column(column =>
        {
            column.Item()
                .BorderBottom(2)
                .PaddingBottom(5)
                .Text(result.Input.Name)
                .Bold().FontSize(16);

            column.Item().PaddingTop(4).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    left.Item().Text(text =>
                    {
                        text.Span("Date: ").Bold();
                        text.Span(HeaderDate(result));
                    });

                    if (CalendarDateFormatter.IsJulian(result.UtcDateTime))
                    {
                        left.Item().Text(text =>
                        {
                            text.Span("Calendar: ").Bold();
                            text.Span("dates before 1582-10-15 are on the Julian calendar");
                        });
                    }

                    if (HeaderTimeZone(result) is string timeZone)
                    {
                        left.Item().Text(text =>
                        {
                            text.Span("Timezone: ").Bold();
                            text.Span(timeZone);
                        });
                    }

                    left.Item().Text(text =>
                    {
                        text.Span("Location: ").Bold();
                        text.Span(string.Create(Inv, $"{result.Input.LocationName} ({result.Input.Latitude:F4}, {result.Input.Longitude:F4})"));
                    });
                });

                row.RelativeItem().Column(right =>
                {
                    right.Item().Text(text =>
                    {
                        text.Span("Type: ").Bold();
                        text.Span(result.Input.EventType);
                    });

                    if (result.Input.Gender != Gender.Unspecified)
                    {
                        right.Item().Text(text =>
                        {
                            text.Span("Gender: ").Bold();
                            text.Span(result.Input.Gender.ToString());
                        });
                    }

                    right.Item().Text(text =>
                    {
                        text.Span("UTC Offset: ").Bold();
                        text.Span(result.ResolvedOffsetLabel);
                    });

                    right.Item().Text(text =>
                    {
                        text.Span("JD: ").Bold();
                        text.Span(string.Create(Inv, $"{result.JulianDay:F6}"));
                    });
                });
            });

            column.Item().PaddingTop(8).BorderBottom(1).Text("");
        });
    }

    /// <summary>
    /// The value of the header's Date line: the chart's wall clock, followed, for a chart cast
    /// at an explicit offset, by that offset's label, as the Markdown chart header's Date/Time
    /// line prints it ("1947-10-05 09:30:00 UTC−5 (explicit)"). An explicit-offset chart's clock
    /// is its instant plus the offset, on the calendar in force (R4, branch R2, 1.3.4): a
    /// reading comes back as itself, and an event chart handed the proleptic instant as its
    /// reading prints the Julian date before 1582-10-15, as the Markdown line does.
    /// </summary>
    /// <param name="result">The chart whose header is composed.</param>
    /// <returns>
    /// The wall clock, <c>yyyy-MM-dd HH:mm:ss</c>, and for an explicit-offset chart its
    /// resolved offset label. A zone chart's Date is the bare wall clock, its zone being on
    /// the Timezone line.
    /// </returns>
    /// <remarks>
    /// An explicit offset is the condition the chart engine casts with
    /// (<see cref="ChartInput.UseExplicitOffset"/> with <see cref="ChartInput.UtcOffsetHours"/>
    /// set); the label is the engine's <see cref="ChartResult.ResolvedOffsetLabel"/>, built by
    /// <see cref="OffsetLabelFormatter"/>. Culture-independent.
    /// </remarks>
    internal static string HeaderDate(ChartResult result) =>
        result.Input.UseExplicitOffset && result.Input.UtcOffsetHours.HasValue
            ? $"{CalendarDateFormatter.DateTime(ExplicitOffsetClock(result))} {result.ResolvedOffsetLabel}"
            : string.Create(Inv, $"{result.Input.LocalDateTime:yyyy-MM-dd HH:mm:ss}");

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
    /// The value of the header's Timezone line, or none for a chart cast at an explicit
    /// offset: that offset overrode the zone's rules, and the Markdown chart header names no
    /// zone for such a chart.
    /// </summary>
    /// <param name="result">The chart whose header is composed.</param>
    /// <returns>
    /// The chart's IANA zone, or <see langword="null"/> when the chart was cast at an explicit
    /// offset and the header prints no Timezone line.
    /// </returns>
    internal static string? HeaderTimeZone(ChartResult result) =>
        result.Input.UseExplicitOffset && result.Input.UtcOffsetHours.HasValue
            ? null
            : result.Input.TimeZone;

    #endregion

    #region Content

    /// <summary>
    /// Compose the main content: the chart's source, description, notes and Time Note, once; positions, the
    /// Asteroids and Lilith section when the chart carries the five, dignities, Moon conditions, chart context,
    /// the planetary hour, a horary chart's perfection analysis, houses, aspects, midpoints, fixed stars and
    /// parans.
    /// </summary>
    private static void ComposeContent(IContainer container, ChartResult result)
    {
        string planetaryHour = ChartMarkdownFormatter.PlanetaryHourSection(result);
        string horaryPerfection = ChartMarkdownFormatter.HoraryPerfectionSection(result);

        container.PaddingTop(8).Column(column =>
        {
            column.Spacing(8);

            // Source, Description, Notes and Time Note: once, not in the page header (E1-6)
            if (HasChartNotes(result))
                column.Item().Element(c => ComposeChartNotes(c, result));

            // Planetary Positions
            column.Item().Element(c => ComposePositionsTable(c, result));

            // Asteroids and Lilith, when the chart carries the five (R6; branch AsteroidsExport, 1.6.0)
            if (result.MinorPoints.Count > 0)
                column.Item().Element(c => ComposeMinorPointsSection(c, result));

            // Essential Dignities
            column.Item().Element(c => ComposeDignityTable(c, result));

            // Moon Conditions
            if (result.MoonCondition is not null)
                column.Item().Element(c => ComposeMoonConditionsSection(c, result));

            // Chart Context
            if (result.ChartContext is not null)
                column.Item().Element(c => ComposeChartContextSection(c, result));

            // Planetary Hour, as the Markdown prints it
            if (planetaryHour.Length > 0)
                column.Item().Element(c => ComposeMarkdownSection(c, "PLANETARY HOUR", planetaryHour));

            // Horary Perfection Analysis, as the Markdown prints it
            if (horaryPerfection.Length > 0)
                column.Item().Element(c => ComposeMarkdownSection(c, "HORARY PERFECTION ANALYSIS", horaryPerfection));

            // House Cusps
            column.Item().Element(c => ComposeHousesSection(c, result));

            // Aspects
            column.Item().Element(c => ComposeAspectsTable(c, result));

            // Midpoint Activations
            column.Item().Element(c => ComposeMidpointsTable(c, result));

            // Fixed Star Conjunctions
            column.Item().Element(c => ComposeFixedStarsTable(c, result));

            // Parans (Paranatellonta)
            if (result.Parans is not null && result.Parans.Parans.Count > 0)
                column.Item().Element(c => ComposeParansTable(c, result));
        });
    }

    /// <summary>
    /// Compose the chart's source, description, notes and Time Note, each "Label: value" with the label
    /// bold, only when present, in that order: once, as the content's first item, where a note taller than
    /// a page lays out across pages (E1-6, branch O-6, 1.3.4).
    /// </summary>
    private static void ComposeChartNotes(IContainer container, ChartResult result)
    {
        container.Column(column =>
        {
            if (!string.IsNullOrEmpty(result.Input.SourceNotes))
                column.Item().Text(text => LabelledLine(text, "Source: ", result.Input.SourceNotes));

            if (!string.IsNullOrEmpty(result.Input.EventDescription))
                column.Item().Text(text => LabelledLine(text, "Description: ", result.Input.EventDescription));

            if (!string.IsNullOrEmpty(result.Input.Notes))
                column.Item().Text(text => LabelledLine(text, "Notes: ", result.Input.Notes));

            if (result.TimeNote is not null)
                column.Item().Text(text => LabelledLine(text, "Time Note: ", result.TimeNote));
        });
    }

    /// <summary>Whether the chart has a source, description, notes or Time Note to print.</summary>
    private static bool HasChartNotes(ChartResult result) =>
        !string.IsNullOrEmpty(result.Input.SourceNotes)
        || !string.IsNullOrEmpty(result.Input.EventDescription)
        || !string.IsNullOrEmpty(result.Input.Notes)
        || result.TimeNote is not null;

    /// <summary>A line of a bold label and its plain value.</summary>
    private static void LabelledLine(TextDescriptor text, string label, string value)
    {
        text.Span(label).Bold();
        text.Span(value);
    }

    /// <summary>
    /// Compose a section of the Markdown export, line for line (O-D1 option b): its <c>## </c> heading is
    /// replaced by the PDF's own uppercase title, bold 10 pt as the other section titles; a <c>### </c> line
    /// is a bold 9 pt subheading; <c>**</c> pairs print their text bold (a <c>**Label:** rest</c> line its
    /// label, a wholly <c>**text**</c> line all of it) and the rest plain; a leading <c>- </c> becomes
    /// <c>• </c>; trailing spaces are dropped; a blank line is spacing.
    /// </summary>
    /// <param name="container">The container to draw in.</param>
    /// <param name="title">The PDF's title for the section.</param>
    /// <param name="markdown">The section's Markdown, from <see cref="ChartMarkdownFormatter"/>.</param>
    private static void ComposeMarkdownSection(IContainer container, string title, string markdown)
    {
        container.Column(column =>
        {
            foreach (string raw in markdown.Split('\n'))
            {
                string line = raw.TrimEnd();

                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    column.Item().Text(title).Bold().FontSize(10);
                    continue;
                }

                if (line.Length == 0)
                {
                    column.Item().Height(4);
                    continue;
                }

                if (line.StartsWith("### ", StringComparison.Ordinal))
                {
                    column.Item().PaddingTop(2).Text(line[4..]).Bold().FontSize(9);
                    continue;
                }

                bool bullet = line.StartsWith("- ", StringComparison.Ordinal);
                string body = bullet ? line[2..] : line;
                column.Item().Text(text =>
                {
                    if (bullet)
                        text.Span("\u2022 ");

                    // Between each pair of ** the text is bold.
                    string[] runs = body.Split("**");
                    for (int i = 0; i < runs.Length; i++)
                    {
                        if (runs[i].Length == 0)
                            continue;

                        if (i % 2 == 1)
                            text.Span(runs[i]).Bold();
                        else
                            text.Span(runs[i]);
                    }
                });
            }
        });
    }

    /// <summary>
    /// Compose the planetary positions table, with the Markdown's columns: body, position, latitude,
    /// declination, house, speed, direction and out-of-bounds.
    /// </summary>
    private static void ComposePositionsTable(IContainer container, ChartResult result)
    {
        container.Column(column =>
        {
            column.Item().Text("PLANETARY POSITIONS").Bold().FontSize(10);
            PositionsTable(column, result.Positions, result.Obliquity);
        });
    }

    /// <summary>
    /// Compose the Asteroids and Lilith section (R6; branch AsteroidsExport, 1.6.0), drawn only when the chart
    /// carries the five: the title as the other section titles, the Markdown's opening line in plain text, then
    /// the five's positions and aspects drawn as the chart's own tables are (the sub-headings bold 9 pt, as a
    /// Markdown <c>### </c> line is drawn), or the none line in place of an empty aspects table.
    /// </summary>
    private static void ComposeMinorPointsSection(IContainer container, ChartResult result)
    {
        container.Column(column =>
        {
            column.Item().Text("ASTEROIDS AND LILITH").Bold().FontSize(10);
            column.Item().PaddingTop(4).Text(ChartMarkdownFormatter.MinorPointsIntroduction);

            column.Item().PaddingTop(4).Text(ChartMarkdownFormatter.MinorPointPositionsTitle).Bold().FontSize(9);
            PositionsTable(column, result.MinorPoints, result.Obliquity);

            column.Item().PaddingTop(4).Text(ChartMarkdownFormatter.MinorPointAspectsTitle).Bold().FontSize(9);
            if (result.MinorPointAspects.Count == 0)
                column.Item().PaddingTop(4).Text(ChartMarkdownFormatter.NoMinorPointAspectsLine);
            else
                AspectsTable(column, result.MinorPointAspects);
        });
    }

    /// <summary>
    /// Draw a positions table, with the Markdown's columns, as an item of a column: the chart's twelve bodies, or
    /// the five minor points. Out of bounds is read against the chart's obliquity, as the Markdown reads it.
    /// </summary>
    /// <param name="column">The column to draw in.</param>
    /// <param name="positions">The positions, one row each.</param>
    /// <param name="obliquity">The chart's true obliquity, in degrees (<see cref="ChartResult.Obliquity"/>).</param>
    private static void PositionsTable(ColumnDescriptor column, IReadOnlyList<PlanetPosition> positions, double obliquity)
    {
        column.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);    // Body
                columns.RelativeColumn(3);    // Position
                columns.ConstantColumn(56);   // Lat
                columns.ConstantColumn(56);   // Dec
                columns.ConstantColumn(32);   // House
                columns.ConstantColumn(48);   // Speed
                columns.ConstantColumn(24);   // Dir
                columns.ConstantColumn(28);   // OOB
            });

            table.Header(header =>
            {
                header.Cell().BorderBottom(1).Padding(3).Text("Body").Bold();
                header.Cell().BorderBottom(1).Padding(3).Text("Position").Bold();
                header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Lat").Bold();
                header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Dec").Bold();
                header.Cell().BorderBottom(1).Padding(3).AlignCenter().Text("House").Bold();
                header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Speed").Bold();
                header.Cell().BorderBottom(1).Padding(3).AlignCenter().Text("Dir").Bold();
                header.Cell().BorderBottom(1).Padding(3).AlignCenter().Text("OOB").Bold();
            });

            foreach (PlanetPosition p in positions)
            {
                string houseStr = p.House.HasValue ? p.House.Value.ToString(Inv) : "-";
                string decStr = p.Declination.HasValue ? PositionFormatter.FormatDeclination(p.Declination.Value) : "-";
                string oobStr = PositionFormatter.IsOutOfBounds(p, obliquity) ? "OOB" : string.Empty;

                table.Cell().Padding(2).Text(p.Name);
                table.Cell().Padding(2).Text(PositionFormatter.FormatPosition(p));
                table.Cell().Padding(2).AlignRight().Text(PositionFormatter.FormatLatitude(p.Latitude));
                table.Cell().Padding(2).AlignRight().Text(decStr);
                table.Cell().Padding(2).AlignCenter().Text(houseStr);
                table.Cell().Padding(2).AlignRight().Text(string.Create(Inv, $"{p.SpeedInLongitude:F3}/d"));
                table.Cell().Padding(2).AlignCenter().Text(p.DirectionCode);
                table.Cell().Padding(2).AlignCenter().Text(oobStr);
            }
        });
    }

    /// <summary>
    /// Compose the essential dignities table.
    /// </summary>
    private static void ComposeDignityTable(IContainer container, ChartResult result)
    {
        container.Column(column =>
        {
            column.Item().Text("ESSENTIAL DIGNITIES").Bold().FontSize(10);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);    // Planet
                    columns.RelativeColumn(2);    // Sign
                    columns.RelativeColumn(2);    // Dignity
                    columns.ConstantColumn(40);   // Score
                    columns.RelativeColumn(2);    // Dispositor
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(1).Padding(3).Text("Planet").Bold();
                    header.Cell().BorderBottom(1).Padding(3).Text("Sign").Bold();
                    header.Cell().BorderBottom(1).Padding(3).Text("Dignity").Bold();
                    header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Score").Bold();
                    header.Cell().BorderBottom(1).Padding(3).Text("Dispositor").Bold();
                });

                foreach (DignityInfo d in result.Dignities)
                {
                    string scoreStr = d.Score > 0 ? string.Create(Inv, $"+{d.Score}") : d.Score.ToString(Inv);

                    table.Cell().Padding(2).Text(d.PlanetName);
                    table.Cell().Padding(2).Text(d.Sign.ToString());
                    table.Cell().Padding(2).Text(d.Label);
                    table.Cell().Padding(2).AlignRight().Text(scoreStr);
                    table.Cell().Padding(2).Text(d.Dispositor);
                }
            });
        });
    }

    /// <summary>
    /// Compose the Moon conditions section (phase, VoC, first applying aspect, Via Combusta).
    /// </summary>
    private static void ComposeMoonConditionsSection(IContainer container, ChartResult result)
    {
        MoonConditionInfo mc = result.MoonCondition!;

        container.Column(column =>
        {
            column.Item().Text("MOON CONDITIONS").Bold().FontSize(10);
            column.Item().PaddingTop(4).Column(inner =>
            {
                inner.Item().Text(text =>
                {
                    text.Span("Phase: ").Bold();
                    text.Span(string.Create(Inv, $"{mc.PhaseLabel} ({mc.PhaseAngle:F1}\u00b0, {(mc.IsWaxing ? "waxing" : "waning")}, {mc.PhasePosition}-phase)"));
                });

                inner.Item().Text(text =>
                {
                    text.Span("Moon Sign: ").Bold();
                    text.Span(mc.MoonSign.ToString());
                });

                inner.Item().Text(text =>
                {
                    text.Span("Void of Course: ").Bold();
                    text.Span(mc.IsVoidOfCourse ? "Yes" : "No");
                });

                if (!mc.IsVoidOfCourse && mc.FirstApplyingPlanet is not null)
                {
                    inner.Item().Text(text =>
                    {
                        text.Span("First Applying Aspect: ").Bold();
                        text.Span($"Moon {mc.FirstApplyingAspectType} {mc.FirstApplyingPlanet} (orb {PositionFormatter.FormatOrb(ChartMarkdownFormatter.ResolveCanonicalMoonOrb(result, mc))})");
                    });
                }

                inner.Item().Text(text =>
                {
                    text.Span("Via Combusta: ").Bold();
                    text.Span(mc.IsViaCombusta ? "Yes" : "No");
                });
            });
        });
    }

    /// <summary>
    /// Compose the chart context section (ruler, sect, angular planets, mutual receptions).
    /// </summary>
    private static void ComposeChartContextSection(IContainer container, ChartResult result)
    {
        ChartContextInfo ctx = result.ChartContext!;

        container.Column(column =>
        {
            column.Item().Text("CHART CONTEXT").Bold().FontSize(10);
            column.Item().PaddingTop(4).Column(inner =>
            {
                inner.Item().Text(text =>
                {
                    text.Span("Chart Ruler: ").Bold();
                    text.Span($"{ctx.ChartRuler} (Ascendant in {ctx.AscendantSign})");
                });

                string dignityQualifier = ctx.ChartRulerDignity.Score switch
                {
                    >= 4 => "strong",
                    > 0 => "moderately strong",
                    0 => "peregrine (neutral)",
                    > -4 => "weakened",
                    _ => "debilitated",
                };
                string scoreStr = ctx.ChartRulerDignity.Score > 0
                    ? string.Create(Inv, $"+{ctx.ChartRulerDignity.Score}")
                    : ctx.ChartRulerDignity.Score.ToString(Inv);

                inner.Item().Text(text =>
                {
                    text.Span("Ruler Dignity: ").Bold();
                    text.Span($"{ctx.ChartRulerDignity.Label} ({scoreStr}) \u2014 {dignityQualifier}");
                });

                inner.Item().Text(text =>
                {
                    text.Span("Ruler House: ").Bold();
                    text.Span(ctx.ChartRulerHouse.ToString(Inv));
                });

                inner.Item().Text(text =>
                {
                    text.Span("Ruler Retrograde: ").Bold();
                    text.Span(ctx.IsChartRulerRetrograde ? "Yes" : "No");
                });

                inner.Item().Text(text =>
                {
                    text.Span("Sect: ").Bold();
                    text.Span($"{ctx.SectLabel} chart");
                });

                if (ctx.EventSignificator is not null)
                {
                    inner.Item().Text(text =>
                    {
                        text.Span("Event Significator: ").Bold();
                        string sigText = ctx.EventSignificator;
                        if (ctx.EventSignificatorDignity is not null)
                        {
                            string sigScore = ctx.EventSignificatorDignity.Score > 0
                                ? string.Create(Inv, $"+{ctx.EventSignificatorDignity.Score}")
                                : ctx.EventSignificatorDignity.Score.ToString(Inv);
                            sigText += $" \u2014 {ctx.EventSignificatorDignity.Label} ({sigScore})";
                        }

                        text.Span(sigText);
                    });

                    inner.Item().Text(text =>
                    {
                        text.Span("Key Houses: ").Bold();
                        text.Span(string.Join(", ", ctx.KeyHouses.Select(h => h.ToString(Inv))));
                    });
                }
            });

            // Angular Planets
            column.Item().PaddingTop(4).Text("Angular Planets").Bold().FontSize(9);

            if (ctx.AngularPlanets.Count > 0)
            {
                column.Item().PaddingTop(2).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);    // Planet
                        columns.ConstantColumn(40);   // Angle
                        columns.ConstantColumn(50);   // Orb
                        columns.RelativeColumn(1);    // Nature
                    });

                    table.Header(header =>
                    {
                        header.Cell().BorderBottom(1).Padding(3).Text("Planet").Bold();
                        header.Cell().BorderBottom(1).Padding(3).AlignCenter().Text("Angle").Bold();
                        header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Orb").Bold();
                        header.Cell().BorderBottom(1).Padding(3).Text("Nature").Bold();
                    });

                    foreach (AngularPlanet ap in ctx.AngularPlanets)
                    {
                        string nature = ap.IsBenefic ? "Benefic" : ap.IsMalefic ? "Malefic" : "Neutral";

                        table.Cell().Padding(2).Text(ap.PlanetName);
                        table.Cell().Padding(2).AlignCenter().Text(ap.Angle);
                        table.Cell().Padding(2).AlignRight().Text(PositionFormatter.FormatOrb(ap.Orb));
                        table.Cell().Padding(2).Text(nature);
                    }
                });
            }
            else
            {
                column.Item().PaddingTop(2).Text("None within 5\u00b0 of angles.");
            }

            // Mutual Receptions
            column.Item().PaddingTop(4).Text("Mutual Receptions").Bold().FontSize(9);

            if (ctx.MutualReceptions.Count > 0)
            {
                column.Item().PaddingTop(2).Column(inner =>
                {
                    foreach (MutualReception mr in ctx.MutualReceptions)
                    {
                        inner.Item().Text($"\u2022 {mr.Notation}");
                    }
                });
            }
            else
            {
                column.Item().PaddingTop(2).Text("None.");
            }
        });
    }

    /// <summary>
    /// Compose the house cusps section with a two-column layout and angles.
    /// </summary>
    private static void ComposeHousesSection(IContainer container, ChartResult result)
    {
        container.Column(column =>
        {
            column.Item().Text($"HOUSE CUSPS ({result.HouseSystemName})").Bold().FontSize(10);
            column.Item().PaddingTop(4).Row(row =>
            {
                // Left column: Houses 1-6
                row.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(50);
                        columns.RelativeColumn();
                    });

                    for (int i = 0; i < 6; i++)
                    {
                        string label = (i + 1) == 1 ? "(ASC)" : "";
                        table.Cell().Padding(2).Text(string.Create(Inv, $"House {i + 1}:"));
                        table.Cell().Padding(2).Text($"{PositionFormatter.FormatLongitude(result.Houses.Cusps[i])} {label}");
                    }
                });

                // Right column: Houses 7-12
                row.RelativeItem().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(55);
                        columns.RelativeColumn();
                    });

                    for (int i = 6; i < 12; i++)
                    {
                        string label = (i + 1) == 10 ? "(MC)" : "";
                        table.Cell().Padding(2).Text(string.Create(Inv, $"House {i + 1}:"));
                        table.Cell().Padding(2).Text($"{PositionFormatter.FormatLongitude(result.Houses.Cusps[i])} {label}");
                    }
                });
            });

            column.Item().PaddingTop(4).Text(text =>
            {
                text.Span("ASC: ").Bold();
                text.Span($"{PositionFormatter.FormatLongitude(result.Houses.Ascendant)}    ");
                text.Span("MC: ").Bold();
                text.Span($"{PositionFormatter.FormatLongitude(result.Houses.Midheaven)}    ");
                text.Span("DSC: ").Bold();
                text.Span($"{PositionFormatter.FormatLongitude(result.Houses.Descendant)}    ");
                text.Span("IC: ").Bold();
                text.Span(PositionFormatter.FormatLongitude(result.Houses.ImumCoeli));
            });
        });
    }

    /// <summary>
    /// Compose the aspects table.
    /// </summary>
    private static void ComposeAspectsTable(IContainer container, ChartResult result)
    {
        container.Column(column =>
        {
            column.Item().Text(string.Create(Inv, $"ASPECTS ({result.Aspects.Count})")).Bold().FontSize(10);
            AspectsTable(column, result.Aspects);
        });
    }

    /// <summary>
    /// Draw an aspects table as an item of a column: the chart's own aspects, or the five minor points'. Every orb
    /// is the row's own, through <see cref="PositionFormatter.FormatOrb"/>.
    /// </summary>
    /// <param name="column">The column to draw in.</param>
    /// <param name="aspects">The aspects, one row each.</param>
    private static void AspectsTable(ColumnDescriptor column, IReadOnlyList<AspectResult> aspects)
    {
        column.Item().PaddingTop(4).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);    // Body 1
                columns.ConstantColumn(30);   // Aspect abbreviation
                columns.RelativeColumn(2);    // Body 2
                columns.ConstantColumn(50);   // Orb
                columns.ConstantColumn(25);   // Ap/Sp
                columns.ConstantColumn(50);   // Category
            });

            table.Header(header =>
            {
                header.Cell().BorderBottom(1).Padding(3).Text("Body 1").Bold();
                header.Cell().BorderBottom(1).Padding(3).AlignCenter().Text("Asp").Bold();
                header.Cell().BorderBottom(1).Padding(3).Text("Body 2").Bold();
                header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Orb").Bold();
                header.Cell().BorderBottom(1).Padding(3).AlignCenter().Text("A/S").Bold();
                header.Cell().BorderBottom(1).Padding(3).Text("Type").Bold();
            });

            foreach (AspectResult a in aspects)
            {
                table.Cell().Padding(2).Text(a.Body1Name);
                table.Cell().Padding(2).AlignCenter().Text(a.Aspect.Abbreviation);
                table.Cell().Padding(2).Text(a.Body2Name);
                table.Cell().Padding(2).AlignRight().Text(PositionFormatter.FormatOrb(a.Orb));
                table.Cell().Padding(2).AlignCenter().Text(a.ApplyingSeparating);
                table.Cell().Padding(2).Text(a.Aspect.Category.ToString());
            }
        });
    }

    /// <summary>
    /// Compose the midpoint activations table.
    /// </summary>
    private static void ComposeMidpointsTable(IContainer container, ChartResult result)
    {
        container.Column(column =>
        {
            column.Item().Text(string.Create(Inv, $"MIDPOINT ACTIVATIONS ({result.MidpointActivations.Count})")).Bold().FontSize(10);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);    // Midpoint
                    columns.RelativeColumn(2);    // Activator
                    columns.ConstantColumn(55);   // Harmonic
                    columns.ConstantColumn(50);   // Orb
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(1).Padding(3).Text("Midpoint").Bold();
                    header.Cell().BorderBottom(1).Padding(3).Text("Activator").Bold();
                    header.Cell().BorderBottom(1).Padding(3).AlignCenter().Text("Harmonic").Bold();
                    header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Orb").Bold();
                });

                foreach (MidpointActivation ma in result.MidpointActivations)
                {
                    string harmonic = ma.HarmonicAngle == 0 ? "direct" : string.Create(Inv, $"{ma.HarmonicAngle}");

                    table.Cell().Padding(2).Text(ma.Midpoint.Notation);
                    table.Cell().Padding(2).Text(ma.ActivatorName);
                    table.Cell().Padding(2).AlignCenter().Text(harmonic);
                    table.Cell().Padding(2).AlignRight().Text(PositionFormatter.FormatOrb(ma.Orb));
                }
            });
        });
    }

    /// <summary>
    /// Compose the fixed star conjunctions table.
    /// </summary>
    private static void ComposeFixedStarsTable(IContainer container, ChartResult result)
    {
        container.Column(column =>
        {
            column.Item().Text(string.Create(Inv, $"FIXED STAR CONJUNCTIONS ({result.FixedStarConjunctions.Count})")).Bold().FontSize(10);

            if (result.FixedStarConjunctions.Count > 0)
            {
                column.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);    // Star
                        columns.ConstantColumn(30);   // Mag
                        columns.RelativeColumn(3);    // Position
                        columns.RelativeColumn(2);    // Conjunct
                        columns.ConstantColumn(45);   // Orb
                        columns.RelativeColumn(2);    // Nature
                        columns.RelativeColumn(3);    // Keywords
                    });

                    table.Header(header =>
                    {
                        header.Cell().BorderBottom(1).Padding(3).Text("Star").Bold();
                        header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Mag").Bold();
                        header.Cell().BorderBottom(1).Padding(3).Text("Position").Bold();
                        header.Cell().BorderBottom(1).Padding(3).Text("Conjunct").Bold();
                        header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Orb").Bold();
                        header.Cell().BorderBottom(1).Padding(3).Text("Nature").Bold();
                        header.Cell().BorderBottom(1).Padding(3).Text("Keywords").Bold();
                    });

                    foreach (FixedStarConjunction fsc in result.FixedStarConjunctions)
                    {
                        string starLabel = fsc.StarName;
                        if (fsc.IsRoyalStar) starLabel += " *";
                        else if (fsc.IsBehenianStar) starLabel += " +";

                        table.Cell().Padding(2).Text(starLabel);
                        table.Cell().Padding(2).AlignRight().Text(string.Create(Inv, $"{fsc.Magnitude:F2}"));
                        table.Cell().Padding(2).Text(PositionFormatter.FormatLongitude(fsc.StarLongitude));
                        table.Cell().Padding(2).Text(fsc.ChartPointName);
                        table.Cell().Padding(2).AlignRight().Text(PositionFormatter.FormatOrb(fsc.Orb));
                        table.Cell().Padding(2).Text(fsc.Nature);
                        table.Cell().Padding(2).Text(fsc.Keywords);
                    }
                });

                column.Item().PaddingTop(2).Text("* Royal Star    + Behenian Star").FontSize(7);
            }
            else
            {
                column.Item().PaddingTop(4).Text("No fixed star conjunctions found within 1\u00b000\u2032 orb.");
            }
        });
    }

    /// <summary>
    /// Compose the parans (paranatellonta) table showing angular crossing
    /// simultaneities detected within the configured time orb.
    /// </summary>
    private static void ComposeParansTable(IContainer container, ChartResult result)
    {
        ParanResult parans = result.Parans!;

        container.Column(column =>
        {
            column.Item().Text(string.Create(Inv, $"PARANS ({parans.Parans.Count}) \u2014 {parans.OrbMinutes:F1} min orb")).Bold().FontSize(10);
            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);    // Body A
                    columns.RelativeColumn(2);    // Angle A
                    columns.RelativeColumn(2);    // Body B
                    columns.RelativeColumn(2);    // Angle B
                    columns.ConstantColumn(55);   // Orb (min)
                });

                table.Header(header =>
                {
                    header.Cell().BorderBottom(1).Padding(3).Text("Body A").Bold();
                    header.Cell().BorderBottom(1).Padding(3).Text("Angle A").Bold();
                    header.Cell().BorderBottom(1).Padding(3).Text("Body B").Bold();
                    header.Cell().BorderBottom(1).Padding(3).Text("Angle B").Bold();
                    header.Cell().BorderBottom(1).Padding(3).AlignRight().Text("Orb (min)").Bold();
                });

                foreach (ParanEvent paran in parans.Parans)
                {
                    string angleA = FormatAngleType(paran.AngleA);
                    string angleB = FormatAngleType(paran.AngleB);

                    table.Cell().Padding(2).Text(paran.BodyAName);
                    table.Cell().Padding(2).Text(angleA);
                    table.Cell().Padding(2).Text(paran.BodyBName);
                    table.Cell().Padding(2).Text(angleB);
                    table.Cell().Padding(2).AlignRight().Text(string.Create(Inv, $"{paran.OrbMinutes:F2}"));
                }
            });
        });
    }

    /// <summary>
    /// Formats an <see cref="AngleType"/> as a human-readable string for
    /// the paran table display.
    /// </summary>
    /// <param name="angle">The angle type to format.</param>
    /// <returns>A string: "Rising", "Culminating", "Setting", or "Anti-Culm.".</returns>
    private static string FormatAngleType(AngleType angle) => angle switch
    {
        AngleType.Rising => "Rising",
        AngleType.Culminating => "Culminating",
        AngleType.Setting => "Setting",
        AngleType.AntiCulminating => "Anti-Culm.",
        _ => angle.ToString(),
    };

    #endregion

    #region Footer

    /// <summary>
    /// Compose the page footer with engine version and timestamp.
    /// </summary>
    private static void ComposeFooter(IContainer container, ChartResult result)
    {
        container
            .BorderTop(1)
            .PaddingTop(5)
            .Row(row =>
            {
                row.RelativeItem().Text(text =>
                {
                    text.Span(result.EngineVersion).FontSize(7);
                });

                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.Span(string.Create(Inv, $"Generated {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC")).FontSize(7);
                });
            });
    }

    #endregion
}
