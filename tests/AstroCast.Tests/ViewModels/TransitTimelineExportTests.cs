using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// The transit-timeline export from the Transits panel carries the panel's interpretation context and the natal
/// chart's birth-time source, as the transit export from the same panel does (E2-6, ruling 2026-10-12).
/// </summary>
/// <remarks>
/// Until 1.3.3 the export passed neither, so the timeline printed no <c>**Context:**</c> line whatever the panel held,
/// and no <c>**Birth Time Source:**</c> line (the source appeared only inside the embedded chart, as
/// <c>**Source:**</c>). The command runs against service doubles and writes to a file under the temporary folder.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class TransitTimelineExportTests
{
    /// <summary>
    /// With "  Career change  " in the panel's context and "Birth certificate" as the chart's source notes, the file
    /// has the whole lines <c>**Birth Time Source:** Birth certificate</c> and <c>**Context:** Career change</c>.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ExportTransitTimeline_WithContext_PrintsContextAndSource()
    {
        string path = Path.Combine(Path.GetTempPath(), $"timeline-context-{Guid.NewGuid():N}.md");
        try
        {
            MainViewModel main = ViewModelWithTransit(path, "Birth certificate");
            main.Transits.InterpretationContext = "  Career change  ";

            await RunAsync(main);

            main.StatusText.Should().Be($"Transit timeline exported: {path}");
            // without each line's trailing spaces: a header line that another follows ends with a hard break
            List<string> lines = [.. (await File.ReadAllLinesAsync(path)).Select(l => l.TrimEnd(' '))];
            lines.Should().Contain("**Birth Time Source:** Birth certificate");
            lines.Should().Contain("**Context:** Career change");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// With a blank context, the file has no <c>**Context:**</c> line.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task ExportTransitTimeline_WithoutContext_OmitsContext()
    {
        string path = Path.Combine(Path.GetTempPath(), $"timeline-no-context-{Guid.NewGuid():N}.md");
        try
        {
            MainViewModel main = ViewModelWithTransit(path, "Birth certificate");
            main.Transits.InterpretationContext = "   ";

            await RunAsync(main);

            main.StatusText.Should().Be($"Transit timeline exported: {path}");
            string[] lines = await File.ReadAllLinesAsync(path);
            lines.Should().NotContain(l => l.StartsWith("**Context:**", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Runs the timeline export command and waits for it to finish.</summary>
    /// <param name="main">The view model.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    private static async Task RunAsync(MainViewModel main)
    {
        main.ExportTransitTimelineMarkdownCommand.CanExecute(null).Should().BeTrue("a transit result is loaded");
        main.ExportTransitTimelineMarkdownCommand.Execute(null);

        int waited = 0;
        while (main.ExportTransitTimelineMarkdownCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        main.ExportTransitTimelineMarkdownCommand.IsExecuting.Should().BeFalse("the command finishes within 5 s");
    }

    /// <summary>
    /// A view model whose save dialog answers with the given path, holding a transit result for a natal chart with
    /// the given source notes.
    /// </summary>
    /// <param name="path">The path the save dialog returns.</param>
    /// <param name="sourceNotes">The natal chart's source notes (its birth-time source).</param>
    /// <returns>The view model.</returns>
    private static MainViewModel ViewModelWithTransit(string path, string sourceNotes)
    {
        MainViewModel main = new(
            new StubChartEngine(),
            new StubChartRepository(),
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
            new LoggerConfiguration().CreateLogger(),
            fileDialogService: new SaveDialogDouble(path));

        main.Transits.TransitResult = new TransitResult
        {
            NatalChart = NatalChart(sourceNotes),
            ReportStartDate = new DateOnly(2026, 3, 1),
            ReportEndDate = new DateOnly(2026, 3, 31),
            MidpointDate = new DateOnly(2026, 3, 16),
            IncludeMinorAspects = false,
            TransitPositions = new Dictionary<TransitBody, TransitPlanetPosition>(),
            Stations = [],
            Ingresses = [],
            ActiveTransits = [],
            HouseOverlays = [],
            MidpointActivations = [],
            TransitToTransitAspects = [],
            MarsIncluded = false,
        };

        return main;
    }

    /// <summary>A minimal natal chart (Sun and Moon) with the given source notes.</summary>
    /// <param name="sourceNotes">The chart's source notes.</param>
    /// <returns>The chart.</returns>
    private static ChartResult NatalChart(string sourceNotes) => new()
    {
        Input = new ChartInput
        {
            Name = "Timeline Export",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
            SourceNotes = sourceNotes,
        },
        JulianDay = 2451545.0,
        Obliquity = 23.4367,
        UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        Positions =
        [
            Position(CelestialBodyId.Sun, "Sun", 280.0, 1.017, 10),
            Position(CelestialBodyId.Moon, "Moon", 112.0, 13.2, 4),
        ],
        Houses = new HouseData
        {
            Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
            Ascendant = 0.0,
            Midheaven = 270.0,
            Armc = 270.0,
            Vertex = 180.0,
        },
        Aspects = [],
        Midpoints = [],
        MidpointActivations = [],
        Dignities = [],
        MoonCondition = null,
        ChartContext = null,
        FixedStarConjunctions = [],
        ResolvedUtcOffsetHours = -5.0,
        ResolvedOffsetLabel = "EST (UTC-5)",
        EngineVersion = "AstroCast 1.0.0 (SwissEph 2.10.03)",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <summary>A minimal position.</summary>
    /// <param name="body">The body.</param>
    /// <param name="name">Its name.</param>
    /// <param name="longitude">Its longitude.</param>
    /// <param name="speed">Its speed in longitude.</param>
    /// <param name="house">Its house.</param>
    /// <returns>The position.</returns>
    private static PlanetPosition Position(CelestialBodyId body, string name, double longitude, double speed, int house) =>
        new()
        {
            Body = body,
            Name = name,
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = speed,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
            House = house,
        };

    /// <summary>A file dialog whose save answers with one path and whose open answers nothing.</summary>
    /// <param name="path">The path every save returns.</param>
    private sealed class SaveDialogDouble(string path) : IFileDialogService
    {
        /// <inheritdoc />
        public Task<string?> ShowSaveAsync(
            string title, IReadOnlyList<FileDialogFileType> fileTypes, string defaultFileName, string defaultExt) =>
            Task.FromResult<string?>(path);

        /// <inheritdoc />
        public Task<string?> ShowOpenAsync(string title, IReadOnlyList<FileDialogFileType> fileTypes, string defaultExt) =>
            Task.FromResult<string?>(null);
    }
}
