using System.Text.Json;

using Microsoft.Data.Sqlite;
using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Data;

/// <summary>
/// SQLite implementation of <see cref="IChartRepository"/>.
/// Stores chart input data in a single Charts table. Each method opens and
/// closes its own unpooled connection — no persistent connection is held.
/// </summary>
/// <remarks>
/// All queries use parameterized SQL to prevent injection.
/// The unique index on Name enforces chart name uniqueness.
/// </remarks>
public sealed class SqliteChartRepository : IChartRepository
{
    private readonly string _connectionString;
    private readonly ILogger _logger;

    /// <summary>
    /// JSON serializer options for export/import with indented formatting
    /// and case-insensitive property matching for robust deserialization.
    /// </summary>
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Initialises a new instance of <see cref="SqliteChartRepository"/>.
    /// </summary>
    /// <param name="connectionString">
    /// SQLite connection string (e.g., "Data Source=astrocast.db"). Every setting in it is kept,
    /// except pooling, which is turned off.
    /// </param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="connectionString"/> or <paramref name="logger"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="connectionString"/> is empty, white space, or not a valid SQLite
    /// connection string.
    /// </exception>
    /// <remarks>
    /// <para>
    /// Every connection the repository opens is unpooled. Microsoft.Data.Sqlite's pool (10.0.x
    /// through 10.0.12) can lend one native connection to two <see cref="SqliteConnection"/>
    /// objects when two opens coincide (dotnet/efcore#39008).
    /// </para>
    /// <para>
    /// On a shared connection, <c>SELECT last_insert_rowid()</c> can read the other save's Id, and
    /// a busy reset can roll back the other save's row.
    /// </para>
    /// <para>
    /// With pooling off, each call opens its own native connection, which costs about 0.2 ms per
    /// call. Pooling stays off after the library is fixed (Steve's decision of 2026-10-11).
    /// </para>
    /// </remarks>
    public SqliteChartRepository(string connectionString, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(logger);

        _connectionString = new SqliteConnectionStringBuilder(connectionString) { Pooling = false }.ToString();
        _logger = logger.ForContext<SqliteChartRepository>();
    }

    /// <summary>
    /// Gets the connection string every method opens its connection with: the caller's string,
    /// with pooling turned off.
    /// </summary>
    /// <remarks>
    /// Internal, for the tests; <c>AstroCast.Tests</c> sees Core's internals.
    /// </remarks>
    internal string EffectiveConnectionString => _connectionString;

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Charts (
                Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                Name                TEXT NOT NULL,
                EventType           TEXT NOT NULL,
                EventDescription    TEXT,
                LocalDateTime       TEXT NOT NULL,
                TimeZone            TEXT NOT NULL,
                UtcDateTime         TEXT NOT NULL,
                JulianDay           REAL NOT NULL,
                Latitude            REAL NOT NULL,
                Longitude           REAL NOT NULL,
                LocationName        TEXT NOT NULL,
                Gender              TEXT,
                SourceNotes         TEXT,
                UtcOffsetHours      REAL,
                UseExplicitOffset   INTEGER DEFAULT 0,
                Notes               TEXT,
                CreatedAt           TEXT NOT NULL,
                UpdatedAt           TEXT NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS IX_Charts_Name ON Charts (Name);
            """;

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Migrate existing databases: add new columns if they don't exist
        await MigrateAddColumnAsync(connection, "Gender", ct).ConfigureAwait(false);
        await MigrateAddColumnAsync(connection, "SourceNotes", ct).ConfigureAwait(false);
        await MigrateAddColumnAsync(connection, "UtcOffsetHours", "REAL", ct).ConfigureAwait(false);
        await MigrateAddColumnAsync(connection, "UseExplicitOffset", "INTEGER DEFAULT 0", ct).ConfigureAwait(false);
        await MigrateAddColumnAsync(connection, "HoraryQuestion", ct).ConfigureAwait(false);
        await MigrateAddColumnAsync(connection, "Keywords", ct).ConfigureAwait(false);
        await MigrateAddColumnAsync(connection, "HoraryCategory", ct).ConfigureAwait(false);
        await MigrateAddColumnAsync(connection, "HouseSystem", ct).ConfigureAwait(false);

        _logger.Debug("Database schema initialized");
    }

    /// <inheritdoc />
    public async Task<string> ExportAllAsJsonAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ChartEntity> charts = await ListAllAsync(ct).ConfigureAwait(false);
        string json = JsonSerializer.Serialize(charts, s_jsonOptions);
        _logger.Information("Exported {Count} charts as JSON", charts.Count);
        return json;
    }

    /// <inheritdoc />
    public async Task<int> ImportFromJsonAsync(string json, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        List<ChartEntity>? charts = JsonSerializer.Deserialize<List<ChartEntity>>(json, s_jsonOptions);

        if (charts is null || charts.Count == 0)
        {
            _logger.Warning("Import JSON contained no charts");
            return 0;
        }

        int count = 0;

        foreach (ChartEntity chart in charts)
        {
            ChartEntity? existing = await GetByNameAsync(chart.Name, ct).ConfigureAwait(false);

            if (existing is not null)
            {
                // Preserve the existing database ID and creation timestamp
                chart.Id = existing.Id;
                chart.CreatedAt = existing.CreatedAt;
                chart.UpdatedAt = DateTime.UtcNow.ToString("o");
                await UpdateAsync(chart, ct).ConfigureAwait(false);
            }
            else
            {
                chart.Id = 0; // Reset ID so SQLite auto-generates
                chart.CreatedAt = DateTime.UtcNow.ToString("o");
                chart.UpdatedAt = DateTime.UtcNow.ToString("o");
                await SaveAsync(chart, ct).ConfigureAwait(false);
            }

            count++;
        }

        _logger.Information("Imported {Count} charts from JSON", count);
        return count;
    }

    /// <inheritdoc />
    public async Task<long> SaveAsync(ChartEntity chart, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chart);

        string now = DateTime.UtcNow.ToString("o");
        chart.CreatedAt = now;
        chart.UpdatedAt = now;

        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Charts (
                Name, EventType, EventDescription, LocalDateTime, TimeZone,
                UtcDateTime, JulianDay, Latitude, Longitude, LocationName,
                Gender, SourceNotes, UtcOffsetHours, UseExplicitOffset,
                Notes, Keywords, HoraryQuestion, HoraryCategory, HouseSystem,
                CreatedAt, UpdatedAt
            ) VALUES (
                @Name, @EventType, @EventDescription, @LocalDateTime, @TimeZone,
                @UtcDateTime, @JulianDay, @Latitude, @Longitude, @LocationName,
                @Gender, @SourceNotes, @UtcOffsetHours, @UseExplicitOffset,
                @Notes, @Keywords, @HoraryQuestion, @HoraryCategory, @HouseSystem,
                @CreatedAt, @UpdatedAt
            );
            """;

        AddChartParameters(command, chart);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        // Retrieve the auto-generated ID
        await using SqliteCommand idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid()";
        object? rawId = await idCommand.ExecuteScalarAsync(ct).ConfigureAwait(false);
        long id = rawId is long longId
            ? longId
            : throw new InvalidOperationException("Failed to retrieve last_insert_rowid after INSERT.");
        chart.Id = id;

        _logger.Information("Saved chart with ID {Id}", id);
        _logger.Debug("Saved chart '{Name}' with ID {Id}", chart.Name, id);
        return id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(ChartEntity chart, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chart);

        chart.UpdatedAt = DateTime.UtcNow.ToString("o");

        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Charts SET
                Name = @Name,
                EventType = @EventType,
                EventDescription = @EventDescription,
                LocalDateTime = @LocalDateTime,
                TimeZone = @TimeZone,
                UtcDateTime = @UtcDateTime,
                JulianDay = @JulianDay,
                Latitude = @Latitude,
                Longitude = @Longitude,
                LocationName = @LocationName,
                Gender = @Gender,
                SourceNotes = @SourceNotes,
                UtcOffsetHours = @UtcOffsetHours,
                UseExplicitOffset = @UseExplicitOffset,
                Notes = @Notes,
                Keywords = @Keywords,
                HoraryQuestion = @HoraryQuestion,
                HoraryCategory = @HoraryCategory,
                HouseSystem = @HouseSystem,
                UpdatedAt = @UpdatedAt
            WHERE Id = @Id
            """;

        AddChartParameters(command, chart);
        command.Parameters.AddWithValue("@Id", chart.Id);

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        _logger.Information("Updated chart (ID {Id})", chart.Id);
        _logger.Debug("Updated chart '{Name}' (ID {Id})", chart.Name, chart.Id);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Charts WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        int rowsAffected = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        if (rowsAffected > 0)
            _logger.Information("Deleted chart ID {Id}", id);

        return rowsAffected > 0;
    }

    /// <inheritdoc />
    public async Task<ChartEntity?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Charts WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? MapToEntity(reader) : null;
    }

    /// <inheritdoc />
    public async Task<ChartEntity?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Charts WHERE Name = @Name COLLATE NOCASE";
        command.Parameters.AddWithValue("@Name", name);

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false) ? MapToEntity(reader) : null;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChartEntity>> SearchByNameAsync(
        string namePattern, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namePattern);

        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT * FROM Charts WHERE Name LIKE @Pattern COLLATE NOCASE ORDER BY Name";
        command.Parameters.AddWithValue("@Pattern", $"%{namePattern}%");

        return await ReadMultipleAsync(command, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChartEntity>> ListAllAsync(CancellationToken ct = default)
    {
        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Charts ORDER BY Name";

        return await ReadMultipleAsync(command, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChartEntity>> ListByEventTypeAsync(
        string eventType, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();
        command.CommandText =
            "SELECT * FROM Charts WHERE EventType = @EventType COLLATE NOCASE ORDER BY Name";
        command.Parameters.AddWithValue("@EventType", eventType);

        return await ReadMultipleAsync(command, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChartEntity>> QueryAsync(
        ChartQueryOptions options, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        await using SqliteConnection connection = new(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using SqliteCommand command = connection.CreateCommand();

        // Build dynamic WHERE clause
        List<string> clauses = [];

        if (!string.IsNullOrWhiteSpace(options.KeywordSearch))
        {
            clauses.Add("""
                (Name LIKE @Keyword COLLATE NOCASE
                 OR LocationName LIKE @Keyword COLLATE NOCASE
                 OR Notes LIKE @Keyword COLLATE NOCASE
                 OR EventDescription LIKE @Keyword COLLATE NOCASE
                 OR SourceNotes LIKE @Keyword COLLATE NOCASE
                 OR Keywords LIKE @Keyword COLLATE NOCASE)
                """);
            command.Parameters.AddWithValue("@Keyword", $"%{options.KeywordSearch}%");
        }

        if (!string.IsNullOrWhiteSpace(options.EventType))
        {
            clauses.Add("EventType = @EventType COLLATE NOCASE");
            command.Parameters.AddWithValue("@EventType", options.EventType);
        }

        if (options.DateFrom.HasValue)
        {
            clauses.Add("LocalDateTime >= @DateFrom");
            command.Parameters.AddWithValue("@DateFrom",
                options.DateFrom.Value.ToString("o"));
        }

        if (options.DateTo.HasValue)
        {
            // Include the entire end-of-day
            clauses.Add("LocalDateTime <= @DateTo");
            command.Parameters.AddWithValue("@DateTo",
                options.DateTo.Value.Date.AddDays(1).AddTicks(-1).ToString("o"));
        }

        string where = clauses.Count > 0
            ? "WHERE " + string.Join(" AND ", clauses)
            : string.Empty;

        // Build ORDER BY clause
        string sortColumn = options.SortField switch
        {
            ChartSortField.Date => "LocalDateTime",
            ChartSortField.EventType => "EventType",
            ChartSortField.CreatedAt => "CreatedAt",
            _ => "Name",
        };

        string sortDirection = options.SortAscending ? "ASC" : "DESC";

        command.CommandText = $"SELECT * FROM Charts {where} ORDER BY {sortColumn} {sortDirection}";

        return await ReadMultipleAsync(command, ct).ConfigureAwait(false);
    }

    #region Private Helpers

    /// <summary>
    /// Add all chart column parameters to a command.
    /// </summary>
    private static void AddChartParameters(SqliteCommand command, ChartEntity chart)
    {
        command.Parameters.AddWithValue("@Name", chart.Name);
        command.Parameters.AddWithValue("@EventType", chart.EventType);
        command.Parameters.AddWithValue("@EventDescription",
            (object?)chart.EventDescription ?? DBNull.Value);
        command.Parameters.AddWithValue("@LocalDateTime", chart.LocalDateTime);
        command.Parameters.AddWithValue("@TimeZone", chart.TimeZone);
        command.Parameters.AddWithValue("@UtcDateTime", chart.UtcDateTime);
        command.Parameters.AddWithValue("@JulianDay", chart.JulianDay);
        command.Parameters.AddWithValue("@Latitude", chart.Latitude);
        command.Parameters.AddWithValue("@Longitude", chart.Longitude);
        command.Parameters.AddWithValue("@LocationName", chart.LocationName);
        command.Parameters.AddWithValue("@Gender",
            (object?)chart.Gender ?? DBNull.Value);
        command.Parameters.AddWithValue("@SourceNotes",
            (object?)chart.SourceNotes ?? DBNull.Value);
        command.Parameters.AddWithValue("@UtcOffsetHours",
            chart.UtcOffsetHours.HasValue ? (object)chart.UtcOffsetHours.Value : DBNull.Value);
        command.Parameters.AddWithValue("@UseExplicitOffset",
            chart.UseExplicitOffset ? 1 : 0);
        command.Parameters.AddWithValue("@Notes",
            (object?)chart.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("@Keywords",
            (object?)chart.Keywords ?? DBNull.Value);
        command.Parameters.AddWithValue("@HoraryQuestion",
            (object?)chart.HoraryQuestion ?? DBNull.Value);
        command.Parameters.AddWithValue("@HoraryCategory",
            (object?)chart.HoraryCategory ?? DBNull.Value);
        command.Parameters.AddWithValue("@HouseSystem",
            (object?)chart.HouseSystem ?? DBNull.Value);
        command.Parameters.AddWithValue("@CreatedAt", chart.CreatedAt);
        command.Parameters.AddWithValue("@UpdatedAt", chart.UpdatedAt);
    }

    /// <summary>
    /// Execute a command and map all result rows to a list of chart entities.
    /// </summary>
    private static async Task<IReadOnlyList<ChartEntity>> ReadMultipleAsync(
        SqliteCommand command, CancellationToken ct)
    {
        List<ChartEntity> results = [];

        await using SqliteDataReader reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            results.Add(MapToEntity(reader));
        }

        return results.AsReadOnly();
    }

    /// <summary>
    /// Map a single data reader row to a <see cref="ChartEntity"/>.
    /// </summary>
    private static ChartEntity MapToEntity(SqliteDataReader reader)
    {
        return new ChartEntity
        {
            Id = reader.GetInt64(reader.GetOrdinal("Id")),
            Name = reader.GetString(reader.GetOrdinal("Name")),
            EventType = reader.GetString(reader.GetOrdinal("EventType")),
            EventDescription = reader.IsDBNull(reader.GetOrdinal("EventDescription"))
                ? null
                : reader.GetString(reader.GetOrdinal("EventDescription")),
            LocalDateTime = reader.GetString(reader.GetOrdinal("LocalDateTime")),
            TimeZone = reader.GetString(reader.GetOrdinal("TimeZone")),
            UtcDateTime = reader.GetString(reader.GetOrdinal("UtcDateTime")),
            JulianDay = reader.GetDouble(reader.GetOrdinal("JulianDay")),
            Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
            Longitude = reader.GetDouble(reader.GetOrdinal("Longitude")),
            LocationName = reader.GetString(reader.GetOrdinal("LocationName")),
            Gender = reader.IsDBNull(reader.GetOrdinal("Gender"))
                ? null
                : reader.GetString(reader.GetOrdinal("Gender")),
            SourceNotes = reader.IsDBNull(reader.GetOrdinal("SourceNotes"))
                ? null
                : reader.GetString(reader.GetOrdinal("SourceNotes")),
            UtcOffsetHours = reader.IsDBNull(reader.GetOrdinal("UtcOffsetHours"))
                ? null
                : reader.GetDouble(reader.GetOrdinal("UtcOffsetHours")),
            UseExplicitOffset = !reader.IsDBNull(reader.GetOrdinal("UseExplicitOffset"))
                && reader.GetInt64(reader.GetOrdinal("UseExplicitOffset")) != 0,
            Notes = reader.IsDBNull(reader.GetOrdinal("Notes"))
                ? null
                : reader.GetString(reader.GetOrdinal("Notes")),
            Keywords = reader.IsDBNull(reader.GetOrdinal("Keywords"))
                ? null
                : reader.GetString(reader.GetOrdinal("Keywords")),
            HoraryQuestion = reader.IsDBNull(reader.GetOrdinal("HoraryQuestion"))
                ? null
                : reader.GetString(reader.GetOrdinal("HoraryQuestion")),
            HoraryCategory = reader.IsDBNull(reader.GetOrdinal("HoraryCategory"))
                ? null
                : reader.GetString(reader.GetOrdinal("HoraryCategory")),
            HouseSystem = reader.IsDBNull(reader.GetOrdinal("HouseSystem"))
                ? null
                : reader.GetString(reader.GetOrdinal("HouseSystem")),
            CreatedAt = reader.GetString(reader.GetOrdinal("CreatedAt")),
            UpdatedAt = reader.GetString(reader.GetOrdinal("UpdatedAt")),
        };
    }

    /// <summary>
    /// Safely add a TEXT column to the Charts table if it does not already exist.
    /// Used for migrating existing databases to the current schema.
    /// </summary>
    /// <param name="connection">An open SQLite connection.</param>
    /// <param name="columnName">The column name to add.</param>
    /// <param name="ct">Cancellation token.</param>
    private Task MigrateAddColumnAsync(
        SqliteConnection connection, string columnName, CancellationToken ct) =>
        MigrateAddColumnAsync(connection, columnName, "TEXT", ct);

    /// <summary>
    /// Safely add a column with a specified type to the Charts table
    /// if it does not already exist.
    /// </summary>
    /// <param name="connection">An open SQLite connection.</param>
    /// <param name="columnName">The column name to add.</param>
    /// <param name="columnType">
    /// The SQLite column type definition (e.g., "TEXT", "REAL", "INTEGER DEFAULT 0").
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    private async Task MigrateAddColumnAsync(
        SqliteConnection connection, string columnName, string columnType, CancellationToken ct)
    {
        // Check if column exists in the table schema
        await using SqliteCommand checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('Charts') WHERE name = @ColumnName";
        checkCommand.Parameters.AddWithValue("@ColumnName", columnName);

        long exists = (long)(await checkCommand.ExecuteScalarAsync(ct).ConfigureAwait(false))!;

        if (exists == 0)
        {
            // SECURITY NOTE: This DDL uses string interpolation with hardcoded column/table
            // names only. No user input reaches this code path. All DML queries use
            // parameterised commands.
            await using SqliteCommand alterCommand = connection.CreateCommand();
            alterCommand.CommandText = $"ALTER TABLE Charts ADD COLUMN {columnName} {columnType}";
            await alterCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            _logger.Information("Migrated Charts table: added column {Column} ({Type})", columnName, columnType);
        }
    }

    #endregion
}
