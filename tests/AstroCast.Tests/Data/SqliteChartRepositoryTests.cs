using System.Text.Json;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;

using FluentAssertions;

using Microsoft.Data.Sqlite;

namespace AstroCast.Tests.Data;

/// <summary>
/// Tests for <see cref="SqliteChartRepository"/> using in-memory SQLite
/// databases with shared cache to allow multiple connections per test.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SqliteChartRepositoryTests
{
    #region Helpers

    /// <summary>
    /// Manages a shared-cache in-memory SQLite database for a single test.
    /// A sentinel connection stays open to prevent the database from being
    /// destroyed when the repository's per-call connections close.
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

    /// <summary>Create a test chart entity with sensible defaults.</summary>
    private static ChartEntity CreateTestEntity(
        string name = "Test Person",
        string eventType = "Birth",
        string? gender = null,
        string? sourceNotes = null,
        string? keywords = null,
        string? eventDescription = null,
        string? notes = null,
        string locationName = "New York City") => new()
    {
        Name = name,
        EventType = eventType,
        EventDescription = eventDescription,
        LocalDateTime = "2000-01-01T07:00:00",
        TimeZone = "America/New_York",
        UtcDateTime = "2000-01-01T12:00:00Z",
        JulianDay = 2451545.0,
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = locationName,
        Gender = gender,
        SourceNotes = sourceNotes,
        Notes = notes,
        Keywords = keywords,
        CreatedAt = DateTime.UtcNow.ToString("o"),
        UpdatedAt = DateTime.UtcNow.ToString("o"),
    };

    #endregion

    #region InitializeAsync

    [Fact]
    public async Task InitializeAsync_Should_CreateTable_When_DatabaseIsEmpty()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);

        // Act
        await repo.InitializeAsync();

        // Assert — query sqlite_master to verify the Charts table exists
        await using SqliteConnection conn = new(db.ConnectionString);
        await conn.OpenAsync();
        await using SqliteCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Charts'";
        object? result = await cmd.ExecuteScalarAsync();
        result.Should().Be("Charts", "InitializeAsync should create the Charts table");
    }

    #endregion

    #region SaveAsync

    [Fact]
    public async Task SaveAsync_Should_ReturnId_When_ChartSaved()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        long id = await repo.SaveAsync(CreateTestEntity());

        // Assert
        id.Should().BeGreaterThan(0, "saved chart should receive a positive auto-generated ID");
    }

    [Fact]
    public async Task SaveAsync_Should_ThrowOnDuplicateName_When_UniqueConstraintViolated()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Duplicate"));

        // Act
        Func<Task> act = async () => await repo.SaveAsync(CreateTestEntity("Duplicate"));

        // Assert
        await act.Should().ThrowAsync<SqliteException>(
            "inserting a chart with a duplicate name should violate the unique index");
    }

    [Fact]
    public async Task SaveAsync_Should_PersistGender_When_Provided()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        long id = await repo.SaveAsync(CreateTestEntity(gender: "Female"));

        // Assert
        ChartEntity? retrieved = await repo.GetByIdAsync(id);
        retrieved.Should().NotBeNull();
        retrieved!.Gender.Should().Be("Female",
            "saved Gender should be persisted and retrievable");
    }

    [Fact]
    public async Task SaveAsync_Should_PersistSourceNotes_When_Provided()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        long id = await repo.SaveAsync(CreateTestEntity(sourceNotes: "Birth certificate"));

        // Assert
        ChartEntity? retrieved = await repo.GetByIdAsync(id);
        retrieved.Should().NotBeNull();
        retrieved!.SourceNotes.Should().Be("Birth certificate",
            "saved SourceNotes should be persisted and retrievable");
    }

    [Fact]
    public async Task SaveAsync_Should_HandleNullGender_When_NotProvided()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        long id = await repo.SaveAsync(CreateTestEntity());

        // Assert
        ChartEntity? retrieved = await repo.GetByIdAsync(id);
        retrieved.Should().NotBeNull();
        retrieved!.Gender.Should().BeNull(
            "Gender should be null when not explicitly set");
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_Should_ReturnChart_When_Exists()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        ChartEntity original = CreateTestEntity(gender: "Male", sourceNotes: "From memory");
        long id = await repo.SaveAsync(original);

        // Act
        ChartEntity? retrieved = await repo.GetByIdAsync(id);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(id);
        retrieved.Name.Should().Be(original.Name);
        retrieved.EventType.Should().Be(original.EventType);
        retrieved.JulianDay.Should().BeApproximately(original.JulianDay, 0.001);
        retrieved.Latitude.Should().BeApproximately(original.Latitude, 0.0001);
        retrieved.Longitude.Should().BeApproximately(original.Longitude, 0.0001);
        retrieved.LocationName.Should().Be(original.LocationName);
        retrieved.TimeZone.Should().Be(original.TimeZone);
        retrieved.Gender.Should().Be("Male");
        retrieved.SourceNotes.Should().Be("From memory");
    }

    [Fact]
    public async Task GetByIdAsync_Should_ReturnNull_When_NotExists()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        ChartEntity? result = await repo.GetByIdAsync(999);

        // Assert
        result.Should().BeNull("querying a non-existent ID should return null");
    }

    #endregion

    #region GetByNameAsync

    [Fact]
    public async Task GetByNameAsync_Should_ReturnChart_When_NameMatches()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Test Person"));

        // Act
        ChartEntity? result = await repo.GetByNameAsync("Test Person");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Person");
    }

    [Fact]
    public async Task GetByNameAsync_Should_BeCaseInsensitive_When_Searching()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Test Person"));

        // Act
        ChartEntity? result = await repo.GetByNameAsync("test person");

        // Assert
        result.Should().NotBeNull(
            "GetByNameAsync should find charts with case-insensitive comparison");
        result!.Name.Should().Be("Test Person");
    }

    #endregion

    #region SearchByNameAsync

    [Fact]
    public async Task SearchByNameAsync_Should_ReturnPartialMatches_When_PatternGiven()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("John Smith"));
        await repo.SaveAsync(CreateTestEntity("Jane Smith"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.SearchByNameAsync("Smith");

        // Assert
        results.Should().HaveCount(2,
            "searching 'Smith' should match both 'John Smith' and 'Jane Smith'");
    }

    [Fact]
    public async Task SearchByNameAsync_Should_ReturnEmpty_When_NoMatches()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("John Smith"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.SearchByNameAsync("ZZZZZ");

        // Assert
        results.Should().BeEmpty("searching for 'ZZZZZ' should find no matches");
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_Should_ModifyFields_When_Called()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        ChartEntity entity = CreateTestEntity("Original Name");
        long id = await repo.SaveAsync(entity);

        // Act
        entity.Name = "Updated Name";
        await repo.UpdateAsync(entity);

        // Assert
        ChartEntity? retrieved = await repo.GetByIdAsync(id);
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("Updated Name",
            "the chart name should be updated after calling UpdateAsync");
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_Should_RemoveChart_When_Exists()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        long id = await repo.SaveAsync(CreateTestEntity());

        // Act
        bool deleted = await repo.DeleteAsync(id);

        // Assert
        deleted.Should().BeTrue("deleting an existing chart should return true");
        ChartEntity? result = await repo.GetByIdAsync(id);
        result.Should().BeNull("the chart should no longer exist after deletion");
    }

    [Fact]
    public async Task DeleteAsync_Should_ReturnFalse_When_NotExists()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        bool result = await repo.DeleteAsync(999);

        // Assert
        result.Should().BeFalse("deleting a non-existent ID should return false");
    }

    #endregion

    #region ListAllAsync

    [Fact]
    public async Task ListAllAsync_Should_ReturnAllCharts_When_MultipleSaved()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Chart A"));
        await repo.SaveAsync(CreateTestEntity("Chart B"));
        await repo.SaveAsync(CreateTestEntity("Chart C"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.ListAllAsync();

        // Assert
        results.Should().HaveCount(3, "ListAllAsync should return all 3 saved charts");
    }

    #endregion

    #region ListByEventTypeAsync

    [Fact]
    public async Task ListByEventTypeAsync_Should_FilterCorrectly_When_TypeGiven()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Birth 1", eventType: "Birth"));
        await repo.SaveAsync(CreateTestEntity("Birth 2", eventType: "Birth"));
        await repo.SaveAsync(CreateTestEntity("Event 1", eventType: "Event"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.ListByEventTypeAsync("Birth");

        // Assert
        results.Should().HaveCount(2,
            "filtering by 'Birth' should return only the 2 birth charts");
    }

    #endregion

    #region ExportAllAsJsonAsync

    [Fact]
    public async Task ExportAllAsJsonAsync_Should_ReturnValidJson_When_ChartsExist()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Chart A"));
        await repo.SaveAsync(CreateTestEntity("Chart B"));

        // Act
        string json = await repo.ExportAllAsJsonAsync();

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        List<ChartEntity>? deserialized = JsonSerializer.Deserialize<List<ChartEntity>>(json);
        deserialized.Should().NotBeNull();
        deserialized!.Should().HaveCount(2);
        deserialized.Select(c => c.Name).Should().BeEquivalentTo(["Chart A", "Chart B"]);
    }

    [Fact]
    public async Task ExportAllAsJsonAsync_Should_ReturnEmptyArray_When_NoCharts()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        string json = await repo.ExportAllAsJsonAsync();

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        List<ChartEntity>? deserialized = JsonSerializer.Deserialize<List<ChartEntity>>(json);
        deserialized.Should().NotBeNull();
        deserialized!.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportAllAsJsonAsync_Should_IncludeAllFields_When_FullyPopulated()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Full Chart", gender: "Female", sourceNotes: "Birth certificate"));

        // Act
        string json = await repo.ExportAllAsJsonAsync();

        // Assert
        List<ChartEntity>? deserialized = JsonSerializer.Deserialize<List<ChartEntity>>(json);
        deserialized.Should().ContainSingle();
        ChartEntity chart = deserialized![0];
        chart.Name.Should().Be("Full Chart");
        chart.Gender.Should().Be("Female");
        chart.SourceNotes.Should().Be("Birth certificate");
        chart.EventType.Should().Be("Birth");
        chart.LocationName.Should().Be("New York City");
        chart.JulianDay.Should().BeApproximately(2451545.0, 0.001);
    }

    #endregion

    #region ImportFromJsonAsync

    [Fact]
    public async Task ImportFromJsonAsync_Should_InsertNewCharts_When_NoneExist()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        string json = JsonSerializer.Serialize(new[]
        {
            CreateTestEntity("Import A"),
            CreateTestEntity("Import B"),
        });

        // Act
        int count = await repo.ImportFromJsonAsync(json);

        // Assert
        count.Should().Be(2);
        IReadOnlyList<ChartEntity> all = await repo.ListAllAsync();
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportFromJsonAsync_Should_UpdateExisting_When_NameMatches()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Existing", eventType: "Birth"));

        ChartEntity updated = CreateTestEntity("Existing", eventType: "Event");
        string json = JsonSerializer.Serialize(new[] { updated });

        // Act
        int count = await repo.ImportFromJsonAsync(json);

        // Assert
        count.Should().Be(1);
        ChartEntity? result = await repo.GetByNameAsync("Existing");
        result.Should().NotBeNull();
        result!.EventType.Should().Be("Event",
            "import should update existing chart's EventType");
    }

    [Fact]
    public async Task ImportFromJsonAsync_Should_PreserveCreatedAt_When_Updating()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        long id = await repo.SaveAsync(CreateTestEntity("Preserve Dates"));
        ChartEntity? original = await repo.GetByIdAsync(id);
        string originalCreatedAt = original!.CreatedAt;

        // Small delay to ensure timestamps differ
        await Task.Delay(50);

        ChartEntity importEntity = CreateTestEntity("Preserve Dates", eventType: "Event");
        string json = JsonSerializer.Serialize(new[] { importEntity });

        // Act
        await repo.ImportFromJsonAsync(json);

        // Assert
        ChartEntity? result = await repo.GetByNameAsync("Preserve Dates");
        result.Should().NotBeNull();
        result!.CreatedAt.Should().Be(originalCreatedAt,
            "import should preserve the original CreatedAt timestamp");
        result.UpdatedAt.Should().NotBe(originalCreatedAt,
            "import should update the UpdatedAt timestamp");
    }

    [Fact]
    public async Task ImportFromJsonAsync_Should_RoundTrip_When_ExportedThenImported()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("RoundTrip A", gender: "Male", sourceNotes: "Birth cert"));
        await repo.SaveAsync(CreateTestEntity("RoundTrip B", eventType: "Horary"));

        // Act — export then import into a fresh database
        string json = await repo.ExportAllAsJsonAsync();

        using TestDatabase db2 = new();
        SqliteChartRepository repo2 = CreateRepo(db2);
        await repo2.InitializeAsync();
        int count = await repo2.ImportFromJsonAsync(json);

        // Assert
        count.Should().Be(2);
        ChartEntity? a = await repo2.GetByNameAsync("RoundTrip A");
        a.Should().NotBeNull();
        a!.Gender.Should().Be("Male");
        a.SourceNotes.Should().Be("Birth cert");

        ChartEntity? b = await repo2.GetByNameAsync("RoundTrip B");
        b.Should().NotBeNull();
        b!.EventType.Should().Be("Horary");
    }

    [Fact]
    public async Task ImportFromJsonAsync_Should_ThrowOnInvalidJson_When_Malformed()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        Func<Task> act = async () => await repo.ImportFromJsonAsync("not valid json");

        // Assert
        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task ImportFromJsonAsync_Should_ReturnZero_When_EmptyArray()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        int count = await repo.ImportFromJsonAsync("[]");

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task ImportFromJsonAsync_Should_HandleMixedInsertAndUpdate_When_SomeExist()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Existing Chart"));

        string json = JsonSerializer.Serialize(new[]
        {
            CreateTestEntity("Existing Chart", eventType: "Event"),  // update
            CreateTestEntity("New Chart"),                           // insert
        });

        // Act
        int count = await repo.ImportFromJsonAsync(json);

        // Assert
        count.Should().Be(2);
        IReadOnlyList<ChartEntity> all = await repo.ListAllAsync();
        all.Should().HaveCount(2);

        ChartEntity? existing = await repo.GetByNameAsync("Existing Chart");
        existing!.EventType.Should().Be("Event", "existing chart should be updated");

        ChartEntity? newChart = await repo.GetByNameAsync("New Chart");
        newChart.Should().NotBeNull("new chart should be inserted");
    }

    #endregion

    #region Keywords

    [Fact]
    public async Task SaveAsync_Should_PersistKeywords_When_Provided()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        long id = await repo.SaveAsync(CreateTestEntity(keywords: "celebrity, politician"));

        // Assert
        ChartEntity? retrieved = await repo.GetByIdAsync(id);
        retrieved.Should().NotBeNull();
        retrieved!.Keywords.Should().Be("celebrity, politician",
            "saved Keywords should be persisted and retrievable");
    }

    [Fact]
    public async Task SaveAsync_Should_HandleNullKeywords_When_NotProvided()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        // Act
        long id = await repo.SaveAsync(CreateTestEntity());

        // Assert
        ChartEntity? retrieved = await repo.GetByIdAsync(id);
        retrieved.Should().NotBeNull();
        retrieved!.Keywords.Should().BeNull(
            "Keywords should be null when not explicitly set");
    }

    [Fact]
    public async Task UpdateAsync_Should_PersistKeywords_When_Changed()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        long id = await repo.SaveAsync(CreateTestEntity(keywords: "original"));

        // Act
        ChartEntity? entity = await repo.GetByIdAsync(id);
        entity!.Keywords = "updated, keywords";
        await repo.UpdateAsync(entity);

        // Assert
        ChartEntity? updated = await repo.GetByIdAsync(id);
        updated!.Keywords.Should().Be("updated, keywords",
            "updated Keywords should be persisted");
    }

    [Fact]
    public async Task ExportImportRoundTrip_Should_PreserveKeywords_When_Present()
    {
        // Arrange
        using TestDatabase dbA = new();
        SqliteChartRepository repoA = CreateRepo(dbA);
        await repoA.InitializeAsync();
        await repoA.SaveAsync(CreateTestEntity("Keywords Person", keywords: "family, historical"));

        string json = await repoA.ExportAllAsJsonAsync();

        using TestDatabase dbB = new();
        SqliteChartRepository repoB = CreateRepo(dbB);
        await repoB.InitializeAsync();

        // Act
        int count = await repoB.ImportFromJsonAsync(json);

        // Assert
        count.Should().Be(1);
        ChartEntity? imported = await repoB.GetByNameAsync("Keywords Person");
        imported.Should().NotBeNull();
        imported!.Keywords.Should().Be("family, historical",
            "Keywords should survive JSON export/import round-trip");
    }

    #endregion

    #region QueryAsync

    [Fact]
    public async Task QueryAsync_Should_ReturnAll_When_NoFilters()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Alice"));
        await repo.SaveAsync(CreateTestEntity("Bob"));
        await repo.SaveAsync(CreateTestEntity("Charlie"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(new ChartQueryOptions());

        // Assert
        results.Should().HaveCount(3, "no filters should return all charts");
    }

    [Fact]
    public async Task QueryAsync_Should_FilterByEventType_When_Specified()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Alice", eventType: "Birth"));
        await repo.SaveAsync(CreateTestEntity("Bob", eventType: "Event"));
        await repo.SaveAsync(CreateTestEntity("Charlie", eventType: "Birth"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions { EventType = "Birth" });

        // Assert
        results.Should().HaveCount(2, "only Birth charts should be returned");
        results.Should().OnlyContain(c => c.EventType == "Birth");
    }

    [Fact]
    public async Task QueryAsync_Should_FilterByKeywordSearch_When_NameMatches()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Alice Smith"));
        await repo.SaveAsync(CreateTestEntity("Bob Jones"));
        await repo.SaveAsync(CreateTestEntity("Alice Johnson"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions { KeywordSearch = "Alice" });

        // Assert
        results.Should().HaveCount(2, "only charts with 'Alice' in the name should be returned");
    }

    [Fact]
    public async Task QueryAsync_Should_FindByKeywords_When_KeywordFieldMatches()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Alice", keywords: "astrology, natal"));
        await repo.SaveAsync(CreateTestEntity("Bob", keywords: "horary, question"));
        await repo.SaveAsync(CreateTestEntity("Charlie"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions { KeywordSearch = "natal" });

        // Assert
        results.Should().HaveCount(1, "only chart with 'natal' in Keywords should be returned");
        results[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task QueryAsync_Should_FindByLocationName_When_LocationMatches()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Alice", locationName: "London, UK"));
        await repo.SaveAsync(CreateTestEntity("Bob", locationName: "New York City"));
        await repo.SaveAsync(CreateTestEntity("Charlie", locationName: "London, Ontario"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions { KeywordSearch = "London" });

        // Assert
        results.Should().HaveCount(2, "charts with 'London' in LocationName should be returned");
    }

    [Fact]
    public async Task QueryAsync_Should_FindByNotes_When_NotesMatch()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Alice", notes: "Rectified birth time"));
        await repo.SaveAsync(CreateTestEntity("Bob", notes: "Approximate time"));
        await repo.SaveAsync(CreateTestEntity("Charlie"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions { KeywordSearch = "Rectified" });

        // Assert
        results.Should().HaveCount(1, "only chart with 'Rectified' in Notes should be returned");
        results[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task QueryAsync_Should_FindBySourceNotes_When_SourceNotesMatch()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Alice", sourceNotes: "Birth certificate"));
        await repo.SaveAsync(CreateTestEntity("Bob", sourceNotes: "From memory"));
        await repo.SaveAsync(CreateTestEntity("Charlie"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions { KeywordSearch = "certificate" });

        // Assert
        results.Should().HaveCount(1, "only chart with 'certificate' in SourceNotes should be returned");
        results[0].Name.Should().Be("Alice");
    }

    [Fact]
    public async Task QueryAsync_Should_FindAcrossMultipleFields_When_DifferentFieldsMatch()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Solar Return", notes: "Annual chart"));
        await repo.SaveAsync(CreateTestEntity("Bob", keywords: "solar, return"));
        await repo.SaveAsync(CreateTestEntity("Charlie", locationName: "Solar Beach"));
        await repo.SaveAsync(CreateTestEntity("Diana", eventDescription: "Solar eclipse event"));
        await repo.SaveAsync(CreateTestEntity("Eve"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions { KeywordSearch = "Solar" });

        // Assert
        results.Should().HaveCount(4,
            "charts matching 'Solar' in Name, Keywords, LocationName, or EventDescription should all be returned");
    }

    [Fact]
    public async Task QueryAsync_Should_FilterByDateRange_When_Specified()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        ChartEntity early = CreateTestEntity("Early");
        early.LocalDateTime = "1980-01-01T12:00:00";
        await repo.SaveAsync(early);

        ChartEntity mid = CreateTestEntity("Mid");
        mid.LocalDateTime = "2000-06-15T12:00:00";
        await repo.SaveAsync(mid);

        ChartEntity late = CreateTestEntity("Late");
        late.LocalDateTime = "2020-12-31T12:00:00";
        await repo.SaveAsync(late);

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions
            {
                DateFrom = new DateTime(1990, 1, 1),
                DateTo = new DateTime(2010, 12, 31),
            });

        // Assert
        results.Should().HaveCount(1, "only charts within the date range should be returned");
        results[0].Name.Should().Be("Mid");
    }

    [Fact]
    public async Task QueryAsync_Should_SortByDate_When_SortFieldIsDate()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();

        ChartEntity chart1 = CreateTestEntity("Zara");
        chart1.LocalDateTime = "2020-01-01T12:00:00";
        await repo.SaveAsync(chart1);

        ChartEntity chart2 = CreateTestEntity("Alice");
        chart2.LocalDateTime = "2010-01-01T12:00:00";
        await repo.SaveAsync(chart2);

        ChartEntity chart3 = CreateTestEntity("Megan");
        chart3.LocalDateTime = "2015-01-01T12:00:00";
        await repo.SaveAsync(chart3);

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions { SortField = ChartSortField.Date, SortAscending = true });

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("Alice", "earliest date should be first");
        results[1].Name.Should().Be("Megan");
        results[2].Name.Should().Be("Zara", "latest date should be last");
    }

    [Fact]
    public async Task QueryAsync_Should_SortDescending_When_SortAscendingIsFalse()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Alice"));
        await repo.SaveAsync(CreateTestEntity("Charlie"));
        await repo.SaveAsync(CreateTestEntity("Bob"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions { SortField = ChartSortField.Name, SortAscending = false });

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("Charlie", "descending name sort should put C first");
        results[1].Name.Should().Be("Bob");
        results[2].Name.Should().Be("Alice", "descending name sort should put A last");
    }

    [Fact]
    public async Task QueryAsync_Should_CombineFilters_When_MultipleSpecified()
    {
        // Arrange
        using TestDatabase db = new();
        SqliteChartRepository repo = CreateRepo(db);
        await repo.InitializeAsync();
        await repo.SaveAsync(CreateTestEntity("Alice Smith", eventType: "Birth"));
        await repo.SaveAsync(CreateTestEntity("Alice Jones", eventType: "Event"));
        await repo.SaveAsync(CreateTestEntity("Bob Smith", eventType: "Birth"));

        // Act
        IReadOnlyList<ChartEntity> results = await repo.QueryAsync(
            new ChartQueryOptions { KeywordSearch = "Alice", EventType = "Birth" });

        // Assert
        results.Should().HaveCount(1, "only Alice with Birth event type should match");
        results[0].Name.Should().Be("Alice Smith");
    }

    #endregion
}
