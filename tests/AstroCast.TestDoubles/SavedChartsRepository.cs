using System.Text.Json;

using AstroCast.Core.Data;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// An in-memory <see cref="IChartRepository"/> that hands out new <see cref="ChartEntity"/> instances on every
/// read, as the SQLite repository does.
/// </summary>
/// <remarks>
/// <see cref="ChartEntity"/> compares by reference, so a list refilled from this repository holds none of the
/// instances it held before: what a picker bound to it sees after a refresh. <see cref="StubChartRepository"/>
/// stores nothing and cannot show that. Reads filter by <see cref="ChartQueryOptions.KeywordSearch"/> (a
/// case-insensitive substring of the name) and <see cref="ChartQueryOptions.EventType"/> only, and list in Id
/// order.
/// </remarks>
public sealed class SavedChartsRepository : IChartRepository
{
    private readonly List<ChartEntity> _rows = [];
    private long _nextId = 1;

    /// <summary>
    /// Initialises the repository with one saved Birth chart per name, given Ids 1, 2, 3 … in order.
    /// </summary>
    /// <param name="names">The charts' names.</param>
    public SavedChartsRepository(params string[] names)
    {
        foreach (string name in names)
        {
            _rows.Add(NewChart(_nextId++, name));
        }
    }

    /// <summary>Gets the number of <see cref="QueryAsync"/> calls, so a test can see that a refresh ran.</summary>
    public int QueryCount { get; private set; }

    /// <summary>Gets the number of charts stored.</summary>
    public int Count => _rows.Count;

    /// <summary>Builds a minimal valid saved chart, person A (invented): 1947-10-05 09:30 at Harrisburg, Pennsylvania.</summary>
    /// <param name="id">The chart's Id.</param>
    /// <param name="name">The chart's name.</param>
    /// <returns>The chart.</returns>
    public static ChartEntity NewChart(long id, string name) => new()
    {
        Id = id,
        Name = name,
        EventType = "Birth",
        LocalDateTime = "1947-10-05T09:30:00.0000000",
        TimeZone = "America/New_York",
        UtcDateTime = "1947-10-05T14:30:00Z",
        JulianDay = 2432464.1041666665,
        Latitude = 40.2737,
        Longitude = -76.8844,
        LocationName = "Harrisburg, Pennsylvania, United States",
        CreatedAt = "2026-10-16T00:00:00Z",
        UpdatedAt = "2026-10-16T00:00:00Z",
    };

    /// <summary>Copies a chart into a new instance.</summary>
    /// <param name="c">The chart to copy.</param>
    /// <returns>A new instance with the same values.</returns>
    private static ChartEntity Copy(ChartEntity c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        EventType = c.EventType,
        EventDescription = c.EventDescription,
        LocalDateTime = c.LocalDateTime,
        TimeZone = c.TimeZone,
        UtcDateTime = c.UtcDateTime,
        JulianDay = c.JulianDay,
        Latitude = c.Latitude,
        Longitude = c.Longitude,
        LocationName = c.LocationName,
        Gender = c.Gender,
        SourceNotes = c.SourceNotes,
        UtcOffsetHours = c.UtcOffsetHours,
        UseExplicitOffset = c.UseExplicitOffset,
        Notes = c.Notes,
        Keywords = c.Keywords,
        HoraryQuestion = c.HoraryQuestion,
        HoraryCategory = c.HoraryCategory,
        HouseSystem = c.HouseSystem,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };

    /// <summary>Copies every stored chart, in Id order.</summary>
    /// <returns>New instances.</returns>
    private IReadOnlyList<ChartEntity> All() => _rows.OrderBy(r => r.Id).Select(Copy).ToList();

    /// <inheritdoc />
    public Task<long> SaveAsync(ChartEntity chart, CancellationToken ct = default)
    {
        ChartEntity row = Copy(chart);
        row.Id = _nextId++;
        _rows.Add(row);
        return Task.FromResult(row.Id);
    }

    /// <inheritdoc />
    public Task UpdateAsync(ChartEntity chart, CancellationToken ct = default)
    {
        int index = _rows.FindIndex(r => r.Id == chart.Id);
        if (index >= 0)
        {
            _rows[index] = Copy(chart);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(_rows.RemoveAll(r => r.Id == id) > 0);

    /// <inheritdoc />
    public Task<ChartEntity?> GetByIdAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(_rows.Where(r => r.Id == id).Select(Copy).FirstOrDefault());

    /// <inheritdoc />
    public Task<ChartEntity?> GetByNameAsync(string name, CancellationToken ct = default) =>
        Task.FromResult(_rows.Where(r => r.Name == name).Select(Copy).FirstOrDefault());

    /// <inheritdoc />
    public Task<IReadOnlyList<ChartEntity>> SearchByNameAsync(string namePattern, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChartEntity>>(
            All().Where(r => r.Name.Contains(namePattern, StringComparison.OrdinalIgnoreCase)).ToList());

    /// <inheritdoc />
    public Task<IReadOnlyList<ChartEntity>> ListAllAsync(CancellationToken ct = default) =>
        Task.FromResult(All());

    /// <inheritdoc />
    public Task<IReadOnlyList<ChartEntity>> ListByEventTypeAsync(string eventType, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChartEntity>>(All().Where(r => r.EventType == eventType).ToList());

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<string> ExportAllAsJsonAsync(CancellationToken ct = default) =>
        Task.FromResult(JsonSerializer.Serialize(All()));

    /// <inheritdoc />
    public Task<int> ImportFromJsonAsync(string json, CancellationToken ct = default) => Task.FromResult(0);

    /// <inheritdoc />
    public Task<IReadOnlyList<ChartEntity>> QueryAsync(ChartQueryOptions options, CancellationToken ct = default)
    {
        QueryCount++;
        IEnumerable<ChartEntity> rows = All();
        if (!string.IsNullOrWhiteSpace(options.KeywordSearch))
        {
            rows = rows.Where(r => r.Name.Contains(options.KeywordSearch, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(options.EventType))
        {
            rows = rows.Where(r => r.EventType == options.EventType);
        }

        return Task.FromResult<IReadOnlyList<ChartEntity>>(rows.ToList());
    }
}
