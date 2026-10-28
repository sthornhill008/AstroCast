using AstroCast.Core.Data;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Repository for persisting and retrieving chart data from SQLite.
/// All methods are async and support cancellation.
/// </summary>
public interface IChartRepository
{
    /// <summary>
    /// Save a new chart and return the generated ID.
    /// </summary>
    /// <param name="chart">The chart entity to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The auto-generated primary key ID.</returns>
    /// <exception cref="Microsoft.Data.Sqlite.SqliteException">
    /// Thrown when a chart with the same Name already exists (unique constraint violation).
    /// </exception>
    Task<long> SaveAsync(ChartEntity chart, CancellationToken ct = default);

    /// <summary>
    /// Update an existing chart.
    /// </summary>
    /// <param name="chart">The chart entity with updated fields. Must have a valid Id.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(ChartEntity chart, CancellationToken ct = default);

    /// <summary>
    /// Delete a chart by ID.
    /// </summary>
    /// <param name="id">The chart ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if a chart was deleted, false if the ID was not found.</returns>
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Retrieve a chart by ID, or null if not found.
    /// </summary>
    /// <param name="id">The chart ID to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The chart entity, or null if not found.</returns>
    Task<ChartEntity?> GetByIdAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// Retrieve a chart by exact name, or null if not found.
    /// Case-insensitive comparison.
    /// </summary>
    /// <param name="name">The exact chart name to search for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The chart entity, or null if not found.</returns>
    Task<ChartEntity?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Search charts by name (partial match, case-insensitive).
    /// </summary>
    /// <param name="namePattern">The partial name to search for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching charts, ordered by name.</returns>
    Task<IReadOnlyList<ChartEntity>> SearchByNameAsync(string namePattern, CancellationToken ct = default);

    /// <summary>
    /// List all charts, ordered by name.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>All saved charts.</returns>
    Task<IReadOnlyList<ChartEntity>> ListAllAsync(CancellationToken ct = default);

    /// <summary>
    /// List charts by event type, ordered by name.
    /// Case-insensitive comparison.
    /// </summary>
    /// <param name="eventType">The event type to filter by (e.g., "Birth", "Event").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Matching charts, ordered by name.</returns>
    Task<IReadOnlyList<ChartEntity>> ListByEventTypeAsync(string eventType, CancellationToken ct = default);

    /// <summary>
    /// Ensure the database schema exists (create tables if needed).
    /// Must be called before any other repository operation.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Export all charts as a JSON string.
    /// Uses System.Text.Json with indented formatting for human readability.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A JSON string containing an array of all chart entities.</returns>
    Task<string> ExportAllAsJsonAsync(CancellationToken ct = default);

    /// <summary>
    /// Import charts from a JSON string. Each chart is upserted: if a chart
    /// with the same Name already exists, it is updated; otherwise it is inserted.
    /// </summary>
    /// <param name="json">A JSON string containing an array of chart entities.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of charts imported (inserted or updated).</returns>
    /// <exception cref="System.Text.Json.JsonException">
    /// Thrown when the JSON string cannot be deserialized.
    /// </exception>
    Task<int> ImportFromJsonAsync(string json, CancellationToken ct = default);

    /// <summary>
    /// Query charts with optional filtering and sorting.
    /// All filter fields in <paramref name="options"/> are optional — null or empty
    /// values are ignored, resulting in no filter for that field.
    /// </summary>
    /// <param name="options">Filter and sort options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Charts matching the query criteria, in the requested sort order.</returns>
    Task<IReadOnlyList<ChartEntity>> QueryAsync(ChartQueryOptions options, CancellationToken ct = default);
}
