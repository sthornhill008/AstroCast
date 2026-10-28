using AstroCast.Core.Data;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Microsoft.Data.Sqlite;

namespace AstroCast.Tests.UI;

/// <summary>
/// Tests for the horary chart workflow verifying data flow through
/// ChartMapper, ChartMarkdownFormatter, and SqliteChartRepository.
/// These tests validate the horary-specific HoraryQuestion field
/// across the full save/load/export lifecycle.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class HoraryWorkflowTests
{
    private readonly EphemerisService _ephemerisService;
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine.
    /// </summary>
    public HoraryWorkflowTests(EphemerisFixture fixture)
    {
        _ephemerisService = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    #region Helpers

    /// <summary>
    /// Manages a shared-cache in-memory SQLite database for a single test.
    /// </summary>
    private sealed class TestDatabase : IDisposable
    {
        private readonly SqliteConnection _sentinel;

        /// <summary>Connection string for the in-memory database.</summary>
        public string ConnectionString { get; }

        public TestDatabase()
        {
            string name = Guid.NewGuid().ToString("N");
            ConnectionString = $"Data Source={name};Mode=Memory;Cache=Shared";
            _sentinel = new SqliteConnection(ConnectionString);
            _sentinel.Open();
        }

        public void Dispose() => _sentinel.Dispose();
    }

    /// <summary>Create a repository backed by the given test database.</summary>
    private static SqliteChartRepository CreateRepo(TestDatabase db)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        return new SqliteChartRepository(db.ConnectionString, logger);
    }

    /// <summary>Create a horary chart input with a question.</summary>
    private static ChartInput CreateHoraryInput(string question = "Should I buy a new MacBook?") =>
        new()
        {
            Name = "Horary Test",
            LocalDateTime = new DateTime(2025, 6, 15, 14, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 37.4138,
            Longitude = -79.1422,
            LocationName = "Lynchburg, VA",
            EventType = "Horary",
            HoraryQuestion = question,
        };

    /// <summary>Create a birth chart input (non-horary).</summary>
    private static ChartInput CreateBirthInput() =>
        new()
        {
            Name = "Birth Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        };

    #endregion

    #region ChartMapper Tests

    [Fact]
    public void ChartInput_HoraryQuestion_Should_MapToEntity()
    {
        // Arrange
        ChartInput input = CreateHoraryInput();

        // Act
        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);

        // Assert
        entity.HoraryQuestion.Should().Be("Should I buy a new MacBook?",
            "HoraryQuestion should map from ChartInput to ChartEntity");
    }

    [Fact]
    public void ChartEntity_HoraryQuestion_Should_MapToInput()
    {
        // Arrange
        ChartEntity entity = new()
        {
            Id = 1,
            Name = "Horary Round Trip",
            EventType = "Horary",
            LocalDateTime = "2025-06-15T14:30:00",
            TimeZone = "America/New_York",
            UtcDateTime = "2025-06-15T18:30:00Z",
            JulianDay = 2460841.0,
            Latitude = 37.4138,
            Longitude = -79.1422,
            LocationName = "Lynchburg, VA",
            HoraryQuestion = "Should I accept the job offer?",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

        // Act
        ChartInput input = ChartMapper.ToInput(entity);

        // Assert
        input.HoraryQuestion.Should().Be("Should I accept the job offer?",
            "HoraryQuestion should map from ChartEntity to ChartInput");
    }

    [Fact]
    public void ChartInput_NonHorary_HoraryQuestion_Should_BeNull()
    {
        // Arrange
        ChartInput input = CreateBirthInput();

        // Assert
        input.HoraryQuestion.Should().BeNull(
            "HoraryQuestion should be null for non-horary chart inputs");
    }

    #endregion

    #region ChartMarkdownFormatter Tests

    [Fact]
    public void ChartMarkdownFormatter_Horary_Should_ContainQuestion()
    {
        // Arrange
        ChartInput input = CreateHoraryInput("Should I buy a new MacBook?\nContext: My current one is 5 years old.");
        ChartResult result = _engine.Calculate(input);

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("Should I buy a new MacBook?",
            "Horary markdown should contain the question text");
    }

    [Fact]
    public void ChartMarkdownFormatter_Horary_Should_HaveHoraryTitle()
    {
        // Arrange
        ChartInput input = CreateHoraryInput("Should I buy a new MacBook?");
        ChartResult result = _engine.Calculate(input);

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().Contain("# Should I buy a new MacBook?",
            "Horary markdown should use the question as the H1 title");
        markdown.Should().NotStartWith("# Horary Test",
            "Horary markdown should not use the Name as the H1 title when a question is provided");
    }

    [Fact]
    public void ChartMarkdownFormatter_NonHorary_Should_NotContainHoraryFields()
    {
        // Arrange
        ChartInput input = CreateBirthInput();
        ChartResult result = _engine.Calculate(input);

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        // Assert
        markdown.Should().StartWith("# Birth Test",
            "Non-horary markdown should use the Name as the H1 title");
        markdown.Should().Contain("**Type:** Birth",
            "Non-horary markdown should show the Birth event type");
    }

    #endregion

    #region SqliteChartRepository Tests

    [Fact]
    public async Task SqliteChartRepository_HoraryQuestion_Should_Persist()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        ChartInput input = CreateHoraryInput("Will I get the promotion?");
        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);

        // Act
        long id = await repo.SaveAsync(entity);
        ChartEntity? retrieved = await repo.GetByIdAsync(id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.HoraryQuestion.Should().Be("Will I get the promotion?",
            "HoraryQuestion should survive a save/retrieve round-trip");
    }

    [Fact]
    public async Task SqliteChartRepository_Migration_Should_AddColumn()
    {
        // Arrange — initialize creates the table with the HoraryQuestion column
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act — save and retrieve a chart with HoraryQuestion populated
        ChartInput input = CreateHoraryInput("Is this the right time to move?");
        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);
        long id = await repo.SaveAsync(entity);
        ChartEntity? retrieved = await repo.GetByIdAsync(id);

        // Assert — if the column didn't exist, this would fail
        retrieved.Should().NotBeNull();
        retrieved!.HoraryQuestion.Should().Be("Is this the right time to move?",
            "HoraryQuestion column should exist after migration and persist data correctly");
        retrieved.EventType.Should().Be("Horary",
            "EventType should be preserved as 'Horary'");
    }

    #endregion
}
