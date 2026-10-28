using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Services;
using AstroCast.Core.Services.PrimaryDirections;
using AstroCast.Services;
using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// What the panels' export commands write: the medical export carries the panel's Health Context (O-D8 (b), E3-3);
/// Export Both names its Markdown from the chosen PDF (E3-4); the electional, medical and cardinal ingress exports
/// offer default names from the result being exported (E3-5, E2-8).
/// </summary>
/// <remarks>
/// The commands run against service doubles, with the J2000 New York chart (2000-01-01 12:00 UT, 40.7128 N 74.006 W,
/// Placidus) cast through the engine as the loaded result, and write under the temporary folder.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class PanelExportTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public PanelExportTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// With " Recovering from knee surgery\nwalks daily " in the Medical panel's Health Context, the exported report
    /// has the whole line <c>**Health Context:** Recovering from knee surgery walks daily</c>, with two trailing
    /// spaces. Until 1.3.3 the export passed nothing, so the line never printed.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ExportMedical_WritesThePanelsHealthContext()
    {
        string path = Path.Combine(Path.GetTempPath(), $"medical-context-{Guid.NewGuid():N}.md");
        try
        {
            MainViewModel main = ViewModel(new SaveDialogDouble(path));
            SeedResult(main, GoldenFixtures.J2000Nyc(_fixture));
            main.MedicalHealthContext = " Recovering from knee surgery\nwalks daily ";

            await RunAsync(main.ExportMedicalMarkdownCommand);

            main.StatusText.Should().Be($"Medical report exported: {path}");
            (await File.ReadAllLinesAsync(path)).Should().Contain(
                "**Health Context:** Recovering from knee surgery walks daily  ");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Export Both, with "Chosen name.pdf" picked in an empty folder, writes the PDF and "Chosen name.md" beside it,
    /// and nothing else. Until 1.3.3 the Markdown took the chart's name, "J2000_NYC_Fixture.md", whatever the PDF
    /// was called (E3-4).
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ExportBoth_WritesTheMarkdownBesideThePdfUnderItsName()
    {
        string directory = NewDirectory("export-both");
        try
        {
            string pdf = Path.Combine(directory, "Chosen name.pdf");
            MainViewModel main = ViewModel(new SaveDialogDouble(pdf));
            SeedResult(main, GoldenFixtures.J2000Nyc(_fixture));

            await RunAsync(main.ExportBothCommand);

            main.StatusText.Should().Be($"Exported to: {directory}");
            Directory.GetFiles(directory).Select(Path.GetFileName).Should().BeEquivalentTo("Chosen name.pdf", "Chosen name.md");
            (await File.ReadAllTextAsync(Path.Combine(directory, "Chosen name.md"))).Should().StartWith("# ");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Export Both, with "Chosen name.md" already beside the chosen "Chosen name.pdf", asks before replacing it; on No
    /// the Markdown is untouched, the PDF is written, and the status line says the Markdown was not written.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ExportBoth_ExistingMarkdown_OnNo_KeepsItAndWritesThePdf()
    {
        string directory = NewDirectory("export-both-existing");
        try
        {
            string pdf = Path.Combine(directory, "Chosen name.pdf");
            string markdown = Path.Combine(directory, "Chosen name.md");
            await File.WriteAllTextAsync(markdown, "kept");
            TestDialogService dialog = new() { ConfirmResult = false };
            MainViewModel main = ViewModel(new SaveDialogDouble(pdf), dialog);
            SeedResult(main, GoldenFixtures.J2000Nyc(_fixture));

            await RunAsync(main.ExportBothCommand);

            dialog.ConfirmCallCount.Should().Be(1);
            dialog.LastConfirmMessage.Should().Contain("Chosen name.md");
            (await File.ReadAllTextAsync(markdown)).Should().Be("kept");
            File.Exists(pdf).Should().BeTrue();
            Directory.GetFiles(directory).Should().HaveCount(2);
            main.StatusText.Should().Be($"PDF exported: {pdf} (Markdown not written: Chosen name.md already exists)");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// With the chart form's Name blank, the electional export offers a name from the natal chart the search was cast
    /// for, "J2000_NYC_Fixture_Electional_20260420_20260425.md". Until 1.3.3 it took the form's Name, so a blank form
    /// failed in the sanitiser and no dialog opened (E3-5).
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ExportElectional_BlankFormName_NamesTheNatalChart()
    {
        SaveDialogDouble fileDialog = new(null);
        MainViewModel main = ViewModel(fileDialog);
        main.ChartEntry.Name = "";
        main.Electional.StartDate = new DateTime(2026, 4, 20);
        main.Electional.EndDate = new DateTime(2026, 4, 25);
        main.Electional.ElectionalResult = Electional(GoldenFixtures.J2000Nyc(_fixture));

        await RunAsync(main.ExportElectionalMarkdownCommand);

        fileDialog.DefaultFileName.Should().Be("J2000_NYC_Fixture_Electional_20260420_20260425.md");
    }

    /// <summary>
    /// For a chart named "Chart: John &lt;Doe&gt; 2024" the medical export offers "Chart_John_Doe_2024_Medical.md".
    /// Until 1.3.3 the name was unsanitised and had no extension, "Chart: John &lt;Doe&gt; 2024_Medical" (E3-5).
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ExportMedical_DefaultName_IsSanitisedWithExtension()
    {
        SaveDialogDouble fileDialog = new(null);
        MainViewModel main = ViewModel(fileDialog);
        SeedResult(main, ChartNamed("Chart: John <Doe> 2024"));

        await RunAsync(main.ExportMedicalMarkdownCommand);

        fileDialog.DefaultFileName.Should().Be("Chart_John_Doe_2024_Medical.md");
    }

    /// <summary>
    /// Cardinal ingresses calculated for 2026, then the form's year changed to 2031: the export offers
    /// "CardinalIngresses_2026.md" and its title reads "Cardinal Sun Ingresses: 2026", the year of the first
    /// ingress's instant in UTC. Until 1.3.3 both took the form's year at export time (E2-8).
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ExportCardinal_NamesTheYearOfTheResults()
    {
        string directory = NewDirectory("cardinal");
        try
        {
            string path = Path.Combine(directory, "cardinal.md");
            SaveDialogDouble fileDialog = new(path);
            MainViewModel main = ViewModel(fileDialog);
            main.Mundane.IngressYear = 2026;
            main.Mundane.CurrentCardinalIngresses = [VernalEquinox2026()];
            main.Mundane.HasCardinalIngresses = true;
            main.Mundane.IngressYear = 2031;

            await RunAsync(main.ExportCardinalIngressesMarkdownCommand);

            fileDialog.DefaultFileName.Should().Be("CardinalIngresses_2026.md");
            (await File.ReadAllLinesAsync(path))[0].Should().Be("# Cardinal Sun Ingresses: 2026");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// The chart JSON export writes the loaded chart's JSON (O-D2 option b): a chart named "A/B Chart" is offered
    /// "AB_Chart.json", the sanitiser's name (the slash removed, the space an underscore), and the file written
    /// parses with the chart's own name. Until 1.3.4 neither head could write a chart's JSON.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ExportChartJson_WritesTheChartsJsonUnderItsName()
    {
        string path = Path.Combine(Path.GetTempPath(), $"chart-json-{Guid.NewGuid():N}.json");
        try
        {
            SaveDialogDouble fileDialog = new(path);
            MainViewModel main = ViewModel(fileDialog);
            SeedResult(main, ChartNamed("A/B Chart"));

            await RunAsync(main.ExportChartJsonCommand);

            fileDialog.DefaultFileName.Should().Be("AB_Chart.json");
            main.StatusText.Should().Be($"JSON exported: {path}");
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
            doc.RootElement.GetProperty("chart").GetProperty("name").GetString().Should().Be("A/B Chart");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Export Backup's status counts the charts in the file it wrote, every saved chart, not the
    /// saved-charts list, which the filter narrows (E3-6). Until 1.4.0 it printed
    /// <c>SavedCharts.Count</c>: "Exported 1 charts" for a file of three.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ExportBackup_StatusCountsTheChartsWritten()
    {
        string path = Path.Combine(Path.GetTempPath(), $"backup-{Guid.NewGuid():N}.json");
        try
        {
            SaveDialogDouble fileDialog = new(path);
            MainViewModel main = ViewModel(fileDialog, repository: new SavedChartsRepository("Alpha", "Beta", "Gamma"));
            await main.InitializeAsync();
            main.SearchText = "Alpha";
            await RunAsync(main.SearchChartsCommand);
            main.SavedCharts.Should().ContainSingle("the filter shows one chart");

            await RunAsync(main.ExportDatabaseCommand);

            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(await File.ReadAllTextAsync(path));
            doc.RootElement.GetArrayLength().Should().Be(3, "the backup holds every chart");
            main.StatusText.Should().Be($"Exported 3 charts to {path}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Export Directions MD offers "J2000_NYC_Fixture_PrimaryDirections_0-100.md" under the dialog title "Export Primary
    /// Directions as Markdown" and writes the whole-life Primary Directions Report (branch PrimaryPanel, 1.5.0).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ExportDirections_WritesTheReportUnderItsDefaultName()
    {
        string path = Path.Combine(Path.GetTempPath(), $"directions-{Guid.NewGuid():N}.md");
        try
        {
            SaveDialogDouble fileDialog = new(path);
            MainViewModel main = ViewModel(fileDialog);
            ChartResult chart = GoldenFixtures.J2000Nyc(_fixture);
            SeedResult(main, chart);
            main.Directions.CurrentResult = new PrimaryDirectionService(_fixture.EphemerisService)
                .Calculate(chart, PrimaryDirectionSettings.Default);

            await RunAsync(main.ExportDirectionsMarkdownCommand);

            fileDialog.DefaultFileName.Should().Be("J2000_NYC_Fixture_PrimaryDirections_0-100.md");
            fileDialog.Title.Should().Be("Export Primary Directions as Markdown");
            main.StatusText.Should().Be($"Primary directions exported: {path}");
            (await File.ReadAllTextAsync(path)).Should().StartWith("# Primary Directions Report");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Export Davison MD offers "J2000_NYC_Fixture_Secondary_Fixture_Davison.md" under the dialog title "Export
    /// Davison as Markdown" and writes the Davison chart report (branch DavisonPanel, 1.7.0).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ExportDavisonMarkdown_UsesTheDefaultName()
    {
        string path = Path.Combine(Path.GetTempPath(), $"davison-{Guid.NewGuid():N}.md");
        try
        {
            SaveDialogDouble fileDialog = new(path);
            MainViewModel main = ViewModel(fileDialog);
            ChartResult chartA = GoldenFixtures.J2000Nyc(_fixture);
            ChartResult chartB = GoldenFixtures.SecondaryNatal(_fixture);
            SeedResult(main, chartA);
            main.Synastry.DavisonResult = new DavisonService(GoldenFixtures.CreateChartEngine(_fixture), new LoggerConfiguration().CreateLogger())
                .Calculate(chartA, chartB, includeMinorPoints: false);

            await RunAsync(main.ExportDavisonMarkdownCommand);

            fileDialog.DefaultFileName.Should().Be("J2000_NYC_Fixture_Secondary_Fixture_Davison.md");
            fileDialog.Title.Should().Be("Export Davison as Markdown");
            main.StatusText.Should().Be($"Davison exported: {path}");
            (await File.ReadAllTextAsync(path)).Should().StartWith("# Davison Chart: J2000 NYC Fixture & Secondary Fixture");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Runs a command and waits for it to finish.</summary>
    /// <param name="command">The command.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    private static async Task RunAsync(AsyncRelayCommand command)
    {
        command.CanExecute(null).Should().BeTrue("the command's result is loaded");
        command.Execute(null);

        int waited = 0;
        while (command.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        command.IsExecuting.Should().BeFalse("the command finishes within 5 s");
    }

    /// <summary>A view model over service doubles whose file dialog is the given double.</summary>
    /// <param name="fileDialog">The file dialog double.</param>
    /// <param name="dialog">The confirmation dialog double, or a new one that confirms.</param>
    /// <param name="repository">The chart repository, or a stub that stores nothing.</param>
    /// <returns>The view model.</returns>
    private static MainViewModel ViewModel(
        SaveDialogDouble fileDialog, TestDialogService? dialog = null, IChartRepository? repository = null) => new(
        new StubChartEngine(),
        repository ?? new StubChartRepository(),
        new StubEphemerisService(),
        new StubSynastryService(),
        new StubCompositeService(),
        new StubSolarReturnService(),
        new StubLunarReturnService(),
        new StubPlanetaryReturnService(),
        new StubCycleTimelineService(),
        new StubGreatConjunctionService(),
        new StubIngressService(),
        new StubLunationService(),
        new StubRelocationService(),
        new StubMundaneAspectService(),
        new StubEclipseService(),
        new StubReturnIngressTimelineService(),
        new StubSynodicCycleService(),
        new StubTransitService(),
        new StubElectionalService(),
        new StubAstrocartographyService(),
        dialog ?? new TestDialogService(),
        new OperationCancellationRegistry(),
        new LoggerConfiguration().CreateLogger(),
        fileDialogService: fileDialog);

    /// <summary>Creates a new, empty folder under the temporary folder.</summary>
    /// <param name="prefix">The start of the folder's name.</param>
    /// <returns>The folder's path.</returns>
    private static string NewDirectory(string prefix)
    {
        string directory = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>The J2000 New York chart's input under another name, cast through the engine.</summary>
    /// <param name="name">The chart's name.</param>
    /// <returns>The chart.</returns>
    private ChartResult ChartNamed(string name) => GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
    {
        Name = name,
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    });

    /// <summary>An electional result with no candidates for the given natal chart, 2026-04-20 to 2026-04-25.</summary>
    /// <param name="natal">The natal chart the search was cast for.</param>
    /// <returns>The result.</returns>
    private static ElectionalResult Electional(ChartResult natal)
    {
        ElectionalInput input = new()
        {
            NatalChart = natal,
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch",
            RangeStart = new DateOnly(2026, 4, 20),
            RangeEnd = new DateOnly(2026, 4, 25),
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York",
            TimeZone = "America/New_York",
        };
        return new ElectionalResult
        {
            Input = input,
            NatalChart = natal,
            Candidates = [],
            RequestedCandidateCount = input.CandidateCount,
            SamplesWithoutPlanetaryHour = 0,
            DisqualifiedPeriods = [],
            VoidOfCoursePeriods = [],
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>The Sun's ingress into Aries of 2026, at 2026-03-20 14:46 UT, with no chart.</summary>
    /// <returns>The ingress.</returns>
    private static IngressResult VernalEquinox2026() => new()
    {
        Planet = CelestialBodyId.Sun,
        PlanetName = "Sun",
        Sign = "Aries",
        Element = "Fire",
        Modality = "Cardinal",
        TargetLongitude = 0.0,
        ActualLongitude = 0.0,
        PrecisionArcSeconds = 0.0,
        ExactUtc = new DateTime(2026, 3, 20, 14, 46, 0, DateTimeKind.Utc),
        IsCardinalIngress = true,
        SeasonName = "Vernal Equinox",
        EngineVersion = "AstroCast 1.0.0 (SwissEph 2.10.03)",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// Loads a chart as the view model's result, through the private setters of <see cref="MainViewModel.LastResult"/>
    /// and <see cref="MainViewModel.HasResult"/>, as <c>CrossVmReadIntegrationTests</c> does.
    /// </summary>
    /// <param name="main">The view model.</param>
    /// <param name="chart">The chart to load.</param>
    private static void SeedResult(MainViewModel main, ChartResult chart)
    {
        SetProperty(main, nameof(MainViewModel.LastResult), chart);
        SetProperty(main, nameof(MainViewModel.HasResult), true);
    }

    /// <summary>Sets a property of the view model through its setter, public or not.</summary>
    /// <param name="main">The view model.</param>
    /// <param name="name">The property's name.</param>
    /// <param name="value">The value.</param>
    private static void SetProperty(MainViewModel main, string name, object value) =>
        (typeof(MainViewModel).GetProperty(name) ?? throw new InvalidOperationException($"MainViewModel has no {name}."))
            .SetValue(main, value);

    /// <summary>
    /// A file dialog whose save answers with one path, or with none, and records the default file name it was offered;
    /// its open answers nothing.
    /// </summary>
    /// <param name="path">The path every save returns, or <c>null</c> to cancel.</param>
    private sealed class SaveDialogDouble(string? path) : IFileDialogService
    {
        /// <summary>Gets the default file name the last save offered, or <c>null</c> if none was shown.</summary>
        public string? DefaultFileName { get; private set; }

        /// <summary>Gets the title of the last save dialog, or <c>null</c> if none was shown.</summary>
        public string? Title { get; private set; }

        /// <inheritdoc />
        public Task<string?> ShowSaveAsync(
            string title, IReadOnlyList<FileDialogFileType> fileTypes, string defaultFileName, string defaultExt)
        {
            DefaultFileName = defaultFileName;
            Title = title;
            return Task.FromResult(path);
        }

        /// <inheritdoc />
        public Task<string?> ShowOpenAsync(string title, IReadOnlyList<FileDialogFileType> fileTypes, string defaultExt) =>
            Task.FromResult<string?>(null);
    }
}
