using AstroCast.Core.Data;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Microsoft.Data.Sqlite;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// What Import Backup says of a file that is not a chart backup (branch InputChecks, 1.7.2, finding N17), and
/// that it imports nothing from one.
/// </summary>
/// <remarks>
/// A backup is the JSON Export Backup writes: an array of saved charts. Until 1.7.2 any other file read "Import
/// error: Invalid JSON format" followed by the serializer's own message, a type name and a path into the document,
/// even when the file was sound JSON, as one chart's JSON export is. The command runs against the SQLite repository
/// over an in-memory database, so what was imported is read from the database itself.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ImportBackupTests
{
    private const string SingleChartExport = "This file is one chart's JSON export, not a chart backup.";
    private const string NotABackup = "This file is not an AstroCast chart backup.";
    private const string NotJson = "This file is not valid JSON.";

    /// <summary>One chart's JSON export, as Export JSON writes it: named for what it is, and nothing imported.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ImportBackup_OneChartsJsonExport_SaysSoAndImportsNothing()
    {
        ChartResult chart = new StubChartEngine().Calculate(new ChartInput
        {
            Name = "Eleanor Hartwell",
            LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, Pennsylvania, United States",
            EventType = "Birth",
        });
        using Harness harness = await Harness.CreateAsync(ChartJsonFormatter.ToJson(chart));

        await harness.ImportAsync();

        harness.Main.StatusText.Should().Be(SingleChartExport);
        (await harness.Repository.ListAllAsync()).Should().BeEmpty();
        harness.Log.Events.Should().Contain(e => e.Exception != null, "the log keeps the exception");
    }

    /// <summary>Any other JSON that is not a backup: said so, and nothing imported.</summary>
    /// <param name="json">The file's text.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("""{"name":"Eleanor Hartwell","latitude":40.2737}""")]
    [InlineData("""{"chart":"not an object"}""")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""[{"name":"Eleanor Hartwell"}]""")]
    [InlineData("\"a string\"")]
    [InlineData("42")]
    public async Task ImportBackup_OtherJson_SaysItIsNotABackupAndImportsNothing(string json)
    {
        using Harness harness = await Harness.CreateAsync(json);

        await harness.ImportAsync();

        harness.Main.StatusText.Should().Be(NotABackup);
        (await harness.Repository.ListAllAsync()).Should().BeEmpty();
        harness.Log.Events.Should().Contain(e => e.Exception != null, "the log keeps the exception");
    }

    /// <summary>Text that is not JSON at all, an empty file included: said so, and nothing imported.</summary>
    /// <param name="text">The file's text.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("# Natal Chart: Eleanor Hartwell\n\n**Type:** Birth")]
    [InlineData("""{"chart": {"name": "Eleanor Hartwell" """)]
    [InlineData("[{\"Name\": \"Eleanor Hartwell\"},")]
    [InlineData("")]
    [InlineData("  \r\n ")]
    public async Task ImportBackup_NotJson_SaysSoAndImportsNothing(string text)
    {
        using Harness harness = await Harness.CreateAsync(text);

        await harness.ImportAsync();

        harness.Main.StatusText.Should().Be(NotJson);
        (await harness.Repository.ListAllAsync()).Should().BeEmpty();
    }

    /// <summary>
    /// A backup whose second chart is missing what a chart needs is not a backup, and its first chart is not
    /// imported either: the whole file is read before anything is saved.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ImportBackup_BackupWithABrokenChart_ImportsNothing()
    {
        string backup = await BackupOfAsync("Eleanor Hartwell");
        string broken = backup.TrimEnd().TrimEnd(']') + """, {"Name": "Daniel Whitfield"}]""";
        using Harness harness = await Harness.CreateAsync(broken);

        await harness.ImportAsync();

        harness.Main.StatusText.Should().Be(NotABackup);
        (await harness.Repository.ListAllAsync()).Should().BeEmpty();
    }

    /// <summary>A real backup, written by Export Backup's own writer from invented charts, still imports.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ImportBackup_ARealBackup_StillImports()
    {
        string backup = await BackupOfAsync("Eleanor Hartwell", "Daniel Whitfield");
        using Harness harness = await Harness.CreateAsync(backup);

        await harness.ImportAsync();

        harness.Main.StatusText.Should().Be($"Imported 2 chart(s) from {harness.Path}");
        (await harness.Repository.ListAllAsync()).Select(chart => chart.Name)
            .Should().BeEquivalentTo("Eleanor Hartwell", "Daniel Whitfield");
        harness.Main.SavedCharts.Should().HaveCount(2, "the saved-charts list is refreshed");
    }

    /// <summary>The backup another database holding the named charts writes.</summary>
    /// <param name="names">The invented charts' names.</param>
    /// <returns>The backup's JSON.</returns>
    private static async Task<string> BackupOfAsync(params string[] names)
    {
        using TestDatabase database = new();
        SqliteChartRepository source = new(database.ConnectionString, new LoggerConfiguration().CreateLogger());
        await source.InitializeAsync();
        foreach (string name in names)
            await source.SaveAsync(SavedChartsRepository.NewChart(0, name));

        return await source.ExportAllAsJsonAsync();
    }

    /// <summary>
    /// A view model over an empty SQLite database whose Import Backup opens one file, with the file, the database
    /// and the log.
    /// </summary>
    /// <param name="Main">The view model.</param>
    /// <param name="Repository">The repository the view model imports into.</param>
    /// <param name="Path">The file Import Backup opens.</param>
    /// <param name="Log">The view model's log events.</param>
    /// <param name="Database">The in-memory database, kept open for the test's life.</param>
    private sealed record Harness(
        MainViewModel Main, SqliteChartRepository Repository, string Path, RecordingSink Log, TestDatabase Database)
        : IDisposable
    {
        /// <summary>Writes the text to a file under the temporary folder and builds the view model that opens it.</summary>
        /// <param name="fileText">The text of the file to import.</param>
        /// <returns>The harness.</returns>
        public static async Task<Harness> CreateAsync(string fileText)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"import-backup-{Guid.NewGuid():N}.json");
            await File.WriteAllTextAsync(path, fileText);

            TestDatabase database = new();
            RecordingSink log = new();
            ILogger logger = new LoggerConfiguration().MinimumLevel.Warning().WriteTo.Sink(log).CreateLogger();
            SqliteChartRepository repository = new(database.ConnectionString, logger);
            await repository.InitializeAsync();

            MainViewModel main = new(
                new StubChartEngine(),
                repository,
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
                new TestDialogService(),
                new OperationCancellationRegistry(),
                logger,
                fileDialogService: new OpenDialogDouble(path));

            return new Harness(main, repository, path, log, database);
        }

        /// <summary>Runs Import Backup and waits for it to finish.</summary>
        /// <returns>A task that completes when the command has finished, and fails after 5 s.</returns>
        public async Task ImportAsync()
        {
            Main.ImportDatabaseCommand.Execute(null);

            int waited = 0;
            while (Main.ImportDatabaseCommand.IsExecuting && waited < 5000)
            {
                await Task.Delay(10);
                waited += 10;
            }

            Main.ImportDatabaseCommand.IsExecuting.Should().BeFalse("the command finishes within 5 s");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Database.Dispose();
            File.Delete(Path);
        }
    }

    /// <summary>
    /// A shared-cache in-memory SQLite database: a sentinel connection stays open so the database outlives the
    /// repository's per-call connections.
    /// </summary>
    private sealed class TestDatabase : IDisposable
    {
        private readonly SqliteConnection _sentinel;

        /// <summary>Initializes a new instance of the <see cref="TestDatabase"/> class.</summary>
        public TestDatabase()
        {
            ConnectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            _sentinel = new SqliteConnection(ConnectionString);
            _sentinel.Open();
        }

        /// <summary>Gets the connection string of the in-memory database.</summary>
        public string ConnectionString { get; }

        /// <inheritdoc />
        public void Dispose() => _sentinel.Dispose();
    }

    /// <summary>A file dialog whose open answers with one path; its save answers nothing.</summary>
    /// <param name="path">The path every open returns.</param>
    private sealed class OpenDialogDouble(string path) : IFileDialogService
    {
        /// <inheritdoc />
        public Task<string?> ShowSaveAsync(
            string title, IReadOnlyList<FileDialogFileType> fileTypes, string defaultFileName, string defaultExt) =>
            Task.FromResult<string?>(null);

        /// <inheritdoc />
        public Task<string?> ShowOpenAsync(string title, IReadOnlyList<FileDialogFileType> fileTypes, string defaultExt) =>
            Task.FromResult<string?>(path);
    }

    /// <summary>Collects every log event written to it.</summary>
    private sealed class RecordingSink : ILogEventSink
    {
        /// <summary>Gets the events, in the order written.</summary>
        public List<LogEvent> Events { get; } = [];

        /// <inheritdoc />
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
