using System.Text;
using System.Text.RegularExpressions;

using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using AstroCast.Tests.Formatting.Golden;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="ChartPdfFormatter"/> verifying PDF generation
/// produces valid output with correct magic bytes and file creation.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartPdfFormatterTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine.
    /// </summary>
    public ChartPdfFormatterTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    /// <summary>
    /// Create a ChartResult using the J2000.0 test input.
    /// </summary>
    /// <param name="notes">The chart's notes, or none.</param>
    private ChartResult CreateTestResult(string? notes = null) => _engine.Calculate(new ChartInput
    {
        Name = "PDF Test",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        Notes = notes,
    });

    /// <summary>
    /// The horary chart: an invented horary at the Venus-station instant (2026-10-01 06:56:30
    /// America/New_York, Columbus, Ohio, category Relationship), which carries a horary perfection analysis.
    /// </summary>
    private ChartResult CreateHoraryResult() => _engine.Calculate(new ChartInput
    {
        Name = "Parcel enquiry 1001",
        LocalDateTime = new DateTime(2026, 10, 1, 6, 56, 30),
        TimeZone = "America/New_York",
        Latitude = 39.9612,
        Longitude = -82.9988,
        LocationName = "Columbus, Ohio",
        EventType = "Horary",
        HoraryCategory = "Relationship",
    });

    /// <summary>
    /// The text of a PDF as the tests compare it: every page's words joined by single spaces, the pages
    /// joined, runs of whitespace collapsed to one space, and the primes mapped back from the modifier letters
    /// the font's glyphs extract as (U+02B9 to U+2032, U+02BA to U+2033).
    /// </summary>
    /// <remarks>
    /// The page's words, not <see cref="Page.Text"/>: the page text runs separately drawn blocks together with
    /// no separator ("BodyPositionHouseDirSpeed" for a table's header cells, "PDF TestDate:" for the title and
    /// the next line), where PdfPig's default word extractor splits them as drawn.
    /// </remarks>
    /// <param name="pdf">The PDF's bytes.</param>
    /// <returns>The text and the number of pages.</returns>
    private static (string Text, int Pages) PdfText(byte[] pdf)
    {
        using PdfDocument document = PdfDocument.Open(pdf);
        StringBuilder text = new();
        foreach (Page page in document.GetPages())
        {
            text.AppendJoin(' ', page.GetWords().Select(w => w.Text)).Append(' ');
        }

        string mapped = text.ToString().Replace('\u02B9', '\u2032').Replace('\u02BA', '\u2033');
        return (Regex.Replace(mapped, @"\s+", " ").Trim(), document.NumberOfPages);
    }

    /// <summary>
    /// The Markdown's text as the PDF draws it: <c>**</c> removed, a leading <c>### </c> and a leading
    /// <c>- </c> removed, trailing spaces dropped, and whitespace runs collapsed as <see cref="PdfText"/>
    /// collapses them.
    /// </summary>
    /// <param name="line">A line of a Markdown section.</param>
    /// <returns>The line as the PDF's text holds it.</returns>
    private static string AsDrawn(string line)
    {
        string drawn = line.TrimEnd().Replace("**", string.Empty, StringComparison.Ordinal);
        if (drawn.StartsWith("### ", StringComparison.Ordinal))
            drawn = drawn[4..];
        if (drawn.StartsWith("- ", StringComparison.Ordinal))
            drawn = drawn[2..];
        return Regex.Replace(drawn, @"\s+", " ").Trim();
    }

    /// <summary>How many times <paramref name="needle"/> occurs in <paramref name="text"/>, without overlap.</summary>
    private static int Occurrences(string text, string needle)
    {
        int count = 0;
        for (int at = text.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    /// <summary>
    /// The PDF prints the Planetary Hour section as the Markdown does (O-D1 option b): J2000 NYC's hour is the
    /// Moon's, the twelfth of the night, 06:07:57 to 07:21:44 on the New York clock. Until 1.3.4 the PDF had no
    /// planetary hour.
    /// </summary>
    [Fact]
    public void Pdf_PrintsThePlanetaryHour_AsTheMarkdownDoes()
    {
        (string text, _) = PdfText(ChartPdfFormatter.Generate(CreateTestResult()));

        text.Should().Contain("PLANETARY HOUR");
        text.Should().Contain("Hour Ruler: Moon (Night hour #12)");
        text.Should().Contain("Hour Period: 06:07:57 \u2013 07:21:44 (America/New_York)");
    }

    /// <summary>
    /// A horary chart's PDF prints its question and perfection analysis, every line of the Markdown's section
    /// (O-D1 option b). Until 1.3.4 the PDF of a horary chart had neither.
    /// </summary>
    [Fact]
    public void Pdf_HoraryChart_PrintsTheQuestionAndThePerfectionAnalysis()
    {
        ChartResult result = CreateHoraryResult();
        string section = ChartMarkdownFormatter.HoraryPerfectionSection(result);
        section.Should().NotBeEmpty("the horary chart carries a horary analysis");

        (string text, _) = PdfText(ChartPdfFormatter.Generate(result));

        text.Should().Contain("HORARY PERFECTION ANALYSIS");
        IEnumerable<string> lines = section.Split('\n').Select(l => l.TrimEnd('\r'))
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("## ", StringComparison.Ordinal));
        foreach (string line in lines)
        {
            text.Should().Contain(AsDrawn(line));
        }
    }

    /// <summary>
    /// The PDF's document information carries a title, the chart's name and kind as the page heading states
    /// them, and names AstroCast with its version as the creator; it names no author and no subject (branch
    /// ExportText, 1.7.2; finding A4). Until 1.7.2 the PDF carried no title, so a reader showed the file's name.
    /// </summary>
    [Fact]
    public void Pdf_CarriesItsTitleAndCreator_AndNoAuthorOrSubject()
    {
        ChartResult result = CreateTestResult();

        using PdfDocument document = PdfDocument.Open(ChartPdfFormatter.Generate(result));

        document.Information.Title.Should().Be("PDF Test (Birth)");
        document.Information.Creator.Should().MatchRegex(@"^AstroCast \d+\.\d+\.\d+$")
            .And.Be(result.EngineVersion[..result.EngineVersion.IndexOf(" (", StringComparison.Ordinal)]);
        document.Information.Author.Should().BeNull();
        document.Information.Subject.Should().BeNull();
    }

    /// <summary>A horary chart's title names its kind as its heading does: "Parcel enquiry 1001 (Horary)".</summary>
    [Fact]
    public void Pdf_HoraryChart_TitleNamesItsKind()
    {
        using PdfDocument document = PdfDocument.Open(ChartPdfFormatter.Generate(CreateHoraryResult()));

        document.Information.Title.Should().Be("Parcel enquiry 1001 (Horary)");
    }

    /// <summary>A guard: a chart with no horary analysis prints no horary section.</summary>
    [Fact]
    public void Pdf_NonHoraryChart_HasNoHorarySection()
    {
        (string text, _) = PdfText(ChartPdfFormatter.Generate(CreateTestResult()));

        text.Should().NotContain("HORARY PERFECTION ANALYSIS");
    }

    /// <summary>
    /// The positions table has the Markdown's eight columns, Lat, Dec and OOB among them. Until 1.3.4 it had
    /// five: Body, Position, House, Dir, Speed.
    /// </summary>
    [Fact]
    public void Pdf_Positions_HaveLatitudeDeclinationAndOutOfBoundsColumns()
    {
        (string text, _) = PdfText(ChartPdfFormatter.Generate(CreateTestResult()));

        text.Should().Contain("Body Position Lat Dec House Speed Dir OOB");
    }

    /// <summary>
    /// The notes print once, in the content, however many pages the PDF has (E1-6). Until 1.3.4 they were in
    /// the page header, so they printed on every page.
    /// </summary>
    [Fact]
    public void Pdf_Notes_PrintOnce_OnAMultiPagePdf()
    {
        (string text, int pages) = PdfText(ChartPdfFormatter.Generate(CreateTestResult("E1-6 marker note")));

        pages.Should().BeGreaterThanOrEqualTo(2);
        Occurrences(text, "E1-6 marker note").Should().Be(1);
    }

    /// <summary>
    /// Notes taller than a page lay out across pages (E1-6). Until 1.3.4 they were in the page header, which
    /// cannot span a page, so the PDF could not be laid out.
    /// </summary>
    [Fact]
    public void Pdf_NotesTallerThanAPage_LayOut()
    {
        string notes = string.Join("\n", Enumerable.Range(1, 400).Select(n => $"Note line {n}"));

        byte[] pdf = ChartPdfFormatter.Generate(CreateTestResult(notes));

        pdf.Should().NotBeEmpty();
        (string text, _) = PdfText(pdf);
        text.Should().Contain("Note line 400");
    }

    [Fact]
    public void Generate_Should_ReturnNonEmptyBytes_When_ChartResultProvided()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        byte[] pdf = ChartPdfFormatter.Generate(result);

        // Assert
        pdf.Length.Should().BeGreaterThan(0,
            "generated PDF should contain data");
    }

    [Fact]
    public void Generate_Should_ProduceValidPdf_When_ChartResultProvided()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        byte[] pdf = ChartPdfFormatter.Generate(result);

        // Assert — PDF files begin with "%PDF" magic bytes
        string header = System.Text.Encoding.ASCII.GetString(pdf, 0, 4);
        header.Should().Be("%PDF",
            "the first 4 bytes of a valid PDF should be '%PDF'");
    }

    [Fact]
    public void Save_Should_CreateFile_When_PathProvided()
    {
        // Arrange
        ChartResult result = CreateTestResult();
        string tempPath = Path.Combine(Path.GetTempPath(), $"astrocast_test_{Guid.NewGuid():N}.pdf");

        try
        {
            // Act
            ChartPdfFormatter.Save(result, tempPath);

            // Assert
            File.Exists(tempPath).Should().BeTrue("Save should create a PDF file");
            new FileInfo(tempPath).Length.Should().BeGreaterThan(0,
                "the saved PDF file should not be empty");
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Fact]
    public void Generate_Should_ProduceValidPdf_When_ChartHasFixedStars()
    {
        // Arrange — a real chart will have fixed star conjunctions from ChartEngine
        ChartResult result = CreateTestResult();

        // Act — should not throw even with fixed star data
        byte[] pdf = ChartPdfFormatter.Generate(result);

        // Assert — PDF is valid and larger than without fixed stars
        string header = System.Text.Encoding.ASCII.GetString(pdf, 0, 4);
        header.Should().Be("%PDF",
            "the PDF should remain valid after adding fixed star sections");
        pdf.Length.Should().BeGreaterThan(1000,
            "the PDF should contain substantial content including fixed stars");
    }

    /// <summary>en-US and the three other cultures every culture-sensitive line runs under.</summary>
    public static TheoryData<string> AllCultures => new() { "en-US", "de-DE", "sv-SE", "th-TH" };

    /// <summary>
    /// Person A's inputs, cast at the explicit offset −5 over America/New_York (E1-3, branch
    /// O-4): the header's Date line carries the offset's own label, as the Markdown chart
    /// header's Date/Time line does, and there is no Timezone line, because the Markdown
    /// header names no zone for such a chart. The label is the input's own offset (Tier A).
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Header_ExplicitOffsetChart_DateCarriesItsOwnLabel_AndNamesNoZone(string culture)
    {
        using CultureScope scope = new(culture);

        ChartResult result = CreateHartwellInputs(explicitOffset: true);

        ChartPdfFormatter.HeaderDate(result).Should().Be("1947-10-05 09:30:00 UTC\u22125 (explicit)");
        ChartPdfFormatter.HeaderTimeZone(result).Should().BeNull(
            "the zone's rules were overridden, and the Markdown header names no zone for this chart");
    }

    /// <summary>
    /// A chart at an explicit offset prints its Date from its instant on the calendar in force (R4,
    /// branch R2, 1.3.4), as the Markdown's Date/Time line does: N1's 1301 solar return, cast by
    /// CalculateAtInstant at JD 2196322.2379282 at offset 0 with its input reading the proleptic
    /// instant 1301-03-23T17:42:37, as the services set it, reads "1301-03-15 17:42:37 UTC+0 (explicit)".
    /// Until R4 it read the input, 1301-03-23.
    /// </summary>
    [Fact]
    public void Header_ExplicitOffsetEventChart_DateIsItsInstantOnTheCalendarInForce()
    {
        ChartResult result = _engine.CalculateAtInstant(new ChartInput
        {
            Name = "Event",
            LocalDateTime = new DateTime(1301, 3, 23, 17, 42, 37),
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 51.4779,
            Longitude = -0.0015,
            LocationName = "Greenwich",
            EventType = "Solar Return",
        }, 2196322.2379282);

        ChartPdfFormatter.HeaderDate(result).Should().Be("1301-03-15 17:42:37 UTC+0 (explicit)");
    }

    /// <summary>
    /// The same inputs on the zone: the header is unchanged — the bare Date and the zone on
    /// its own Timezone line. The Markdown's Date/Time line carries the zone name, not an
    /// offset label, so the PDF's Date line gains nothing.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Header_ZoneChart_IsUnchanged(string culture)
    {
        using CultureScope scope = new(culture);

        ChartResult result = CreateHartwellInputs(explicitOffset: false);

        ChartPdfFormatter.HeaderDate(result).Should().Be("1947-10-05 09:30:00");
        ChartPdfFormatter.HeaderTimeZone(result).Should().Be("America/New_York");
    }

    /// <summary>
    /// Casts person A's inputs: 1947-10-05 09:30:00 at Harrisburg, Pennsylvania, in the zone
    /// America/New_York, with or without the explicit offset −5.
    /// </summary>
    private ChartResult CreateHartwellInputs(bool explicitOffset) => _engine.Calculate(new ChartInput
    {
        Name = "Explicit Offset Fixture",
        LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
        TimeZone = "America/New_York",
        UseExplicitOffset = explicitOffset,
        UtcOffsetHours = explicitOffset ? -5.0 : null,
        Latitude = 40.2737,
        Longitude = -76.8844,
        LocationName = "Harrisburg, Pennsylvania",
        EventType = "Birth",
    });

    #region Asteroids and Lilith (branch AsteroidsExport, 1.6.0)

    /// <summary>
    /// With the switch on the PDF prints the section after the positions table: its title, the opening line that
    /// defines Lilith, and the five's rows (J2000 NYC's Pallas retrograde and out of bounds among them).
    /// </summary>
    [Fact]
    public void Pdf_SwitchOn_PrintsAsteroidsAndLilith()
    {
        ChartResult result = _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));

        (string text, _) = PdfText(ChartPdfFormatter.Generate(result));

        text.Should().Contain("ASTEROIDS AND LILITH");
        text.Should().Contain("Lilith");
        text.Should().Contain("Black Moon Lilith, the mean lunar apogee");
        text.Should().Contain("Pallas");
        text.IndexOf("ASTEROIDS AND LILITH", StringComparison.Ordinal)
            .Should().BeGreaterThan(text.IndexOf("PLANETARY POSITIONS", StringComparison.Ordinal))
            .And.BeLessThan(text.IndexOf("ESSENTIAL DIGNITIES", StringComparison.Ordinal));
    }

    /// <summary>With the switch off the PDF holds neither the title nor the word Lilith (R9).</summary>
    [Fact]
    public void Pdf_SwitchOff_PrintsNeitherTheTitleNorLilith()
    {
        (string text, _) = PdfText(ChartPdfFormatter.Generate(CreateTestResult()));

        text.Should().NotContain("ASTEROIDS AND LILITH");
        text.Should().NotContain("Lilith");
    }

    #endregion
}
