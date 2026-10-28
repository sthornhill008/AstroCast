using AstroCast.Core.Data;
using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Data;

/// <summary>
/// Smoke tests that exercise <see cref="SqliteChartRepository"/> under
/// concurrent access against a real temp-file database.
/// </summary>
/// <remarks>
/// <para>
/// The non-concurrency tests in <see cref="SqliteChartRepositoryTests"/> use
/// <c>Mode=Memory;Cache=Shared</c>, which serialises access through a single
/// shared cache and hides file-level locking semantics. These tests use a
/// file-backed database (SQLite's default rollback journal, <c>delete</c>;
/// nothing in the app sets WAL) so the same code path that ships to users is
/// exercised when two tasks race on the same file.
/// </para>
/// <para>
/// These tests caught the pool defect of dotnet/efcore#39008, and the
/// repository opens unpooled connections because of it.
/// </para>
/// <para>
/// Scope is deliberately narrow — the goal is a "does it deadlock / does it
/// corrupt" smoke signal, not a transactional correctness test.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class SqliteChartRepositoryConcurrencyTests : IDisposable
{
    #region Test Infrastructure

    /// <summary>
    /// Unique temp-file path for this test instance. File is created lazily
    /// when the first connection opens and deleted in <see cref="Dispose"/>.
    /// </summary>
    private readonly string _dbPath;
    private readonly string _connectionString;

    public SqliteChartRepositoryConcurrencyTests()
    {
        _dbPath = Path.Combine(
            Path.GetTempPath(),
            $"astrocast_concurrency_{Guid.NewGuid():N}.db");
        _connectionString = $"Data Source={_dbPath}";
    }

    /// <summary>
    /// Clean up the temp-file database and any <c>-wal</c> or <c>-shm</c> file beside it. The repository
    /// runs SQLite's rollback journal (<c>delete</c> mode), whose <c>-journal</c> file SQLite removes at
    /// each commit, so those two suffixes are a guard.
    /// </summary>
    public void Dispose()
    {
        foreach (string suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            string path = _dbPath + suffix;
            if (File.Exists(path))
            {
                try { File.Delete(path); }
                catch { /* best-effort cleanup */ }
            }
        }
    }

    private SqliteChartRepository CreateRepo()
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        return new SqliteChartRepository(_connectionString, logger);
    }

    private static ChartEntity CreateTestEntity(string name) => new()
    {
        Name = name,
        EventType = "Birth",
        LocalDateTime = "2000-01-01T07:00:00",
        TimeZone = "America/New_York",
        UtcDateTime = "2000-01-01T12:00:00Z",
        JulianDay = 2451545.0,
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        CreatedAt = DateTime.UtcNow.ToString("o"),
        UpdatedAt = DateTime.UtcNow.ToString("o"),
    };

    #endregion

    #region Smoke Tests

    /// <summary>
    /// Ten concurrent SaveAsync calls against the same file-backed database
    /// must all succeed, produce distinct row IDs, and leave ListAllAsync
    /// reporting exactly ten rows. If SQLite file locking is misconfigured,
    /// we'll see either a database-is-locked exception or a row-count drift.
    /// </summary>
    [Fact]
    public async Task SaveAsync_TenConcurrentWrites_AllSucceedAndListAllSeesAllRows()
    {
        // Initialise the schema once up front; concurrent SaveAsync relies on it.
        SqliteChartRepository initRepo = CreateRepo();
        await initRepo.InitializeAsync();

        const int writerCount = 10;
        Task<long>[] saves = new Task<long>[writerCount];
        for (int i = 0; i < writerCount; i++)
        {
            int capture = i;
            saves[i] = Task.Run(async () =>
            {
                SqliteChartRepository repo = CreateRepo();
                return await repo.SaveAsync(CreateTestEntity($"Concurrent {capture}"));
            });
        }

        long[] ids = await Task.WhenAll(saves);

        ids.Should().HaveCount(writerCount);
        ids.Should().OnlyHaveUniqueItems(
            "SQLite autoincrement must hand out distinct rowids under concurrency");
        ids.Should().OnlyContain(id => id > 0);

        SqliteChartRepository listRepo = CreateRepo();
        IReadOnlyList<ChartEntity> all = await listRepo.ListAllAsync();
        all.Should().HaveCount(writerCount,
            "every concurrent write must be durable — none silently dropped by locking");
    }

    /// <summary>
    /// Interleaved readers and writers against the same file-backed database
    /// must not deadlock or throw. Five tasks each do a SaveAsync followed by
    /// a ListAllAsync, executed in parallel. The final row count must include
    /// every successful write.
    /// </summary>
    [Fact]
    public async Task SaveAndList_InterleavedReadersAndWriters_DoNotDeadlock()
    {
        SqliteChartRepository initRepo = CreateRepo();
        await initRepo.InitializeAsync();

        const int pairCount = 5;
        Task[] pairs = new Task[pairCount];
        for (int i = 0; i < pairCount; i++)
        {
            int capture = i;
            pairs[i] = Task.Run(async () =>
            {
                SqliteChartRepository repo = CreateRepo();
                await repo.SaveAsync(CreateTestEntity($"Interleaved {capture}"));
                IReadOnlyList<ChartEntity> snapshot = await repo.ListAllAsync();
                snapshot.Should().NotBeNull();
            });
        }

        Func<Task> act = () => Task.WhenAll(pairs);
        await act.Should().NotThrowAsync(
            "interleaved read/write must not produce database-is-locked errors");

        SqliteChartRepository listRepo = CreateRepo();
        IReadOnlyList<ChartEntity> all = await listRepo.ListAllAsync();
        all.Should().HaveCount(pairCount);
    }

    #endregion
}
