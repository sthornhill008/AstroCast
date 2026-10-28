using AstroCast.Core.Data;

using FluentAssertions;

using Microsoft.Data.Sqlite;

namespace AstroCast.Tests.Data;

/// <summary>
/// Tests that <see cref="SqliteChartRepository"/> opens unpooled connections and keeps every
/// other setting of the connection string it is given.
/// </summary>
/// <remarks>
/// Microsoft.Data.Sqlite's pool can lend one native connection to two concurrent opens
/// (dotnet/efcore#39008), so the repository turns pooling off. These tests read the connection
/// string the repository uses, through <see cref="SqliteConnectionStringBuilder"/>; no database
/// is opened.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class SqliteChartRepositoryPoolingTests
{
    #region Helpers

    /// <summary>Create a repository over the given connection string, with a silent logger.</summary>
    private static SqliteChartRepository CreateRepo(string connectionString)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        return new SqliteChartRepository(connectionString, logger);
    }

    #endregion

    #region Tests

    /// <summary>
    /// A file database: pooling is off, and the data source is the path given.
    /// </summary>
    [Fact]
    public void Constructor_Should_TurnPoolingOff_When_FileDatabase()
    {
        string path = Path.Combine(Path.GetTempPath(), $"astrocast_pooling_{Guid.NewGuid():N}.db");

        SqliteChartRepository repo = CreateRepo($"Data Source={path}");
        SqliteConnectionStringBuilder effective = new(repo.EffectiveConnectionString);

        effective.Pooling.Should().BeFalse();
        effective.DataSource.Should().Be(path);
    }

    /// <summary>
    /// A shared in-memory database: pooling is off, and the mode, the cache and the database
    /// name are the ones given.
    /// </summary>
    [Fact]
    public void Constructor_Should_TurnPoolingOffAndKeepModeCacheAndName_When_SharedInMemoryDatabase()
    {
        string name = Guid.NewGuid().ToString("N");

        SqliteChartRepository repo = CreateRepo($"Data Source={name};Mode=Memory;Cache=Shared");
        SqliteConnectionStringBuilder effective = new(repo.EffectiveConnectionString);

        effective.Pooling.Should().BeFalse();
        effective.Mode.Should().Be(SqliteOpenMode.Memory);
        effective.Cache.Should().Be(SqliteCacheMode.Shared);
        effective.DataSource.Should().Be(name);
    }

    #endregion
}
