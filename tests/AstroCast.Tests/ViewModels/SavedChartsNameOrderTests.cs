using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Tests.Fixtures;
using AstroCast.ViewModels;

using FluentAssertions;

using Microsoft.Data.Sqlite;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// The order of the saved-charts list and the three chart pickers when sorted by name (branch InputChecks, 1.7.2,
/// finding N18): as a person reads names, case ignored and an accented letter beside its base letter.
/// </summary>
/// <remarks>
/// The database orders names by their bytes: every capital before every small letter, and every accented letter
/// after "z". Seven invented charts read Daniel Whitfield, Eleanor Hartwell, Zoe Carter, anna lind, bruno weiss,
/// then the two names with an accented capital. Since 1.7.2 the view model orders the charts after the read, on
/// the user's own culture; the tests set the invariant culture, where the order is anna lind, bruno weiss, Daniel
/// Whitfield, Eleanor Hartwell, the accented E name, the accented O name, Zoe Carter. The search and the same-name
/// check on save still compare in the database and are not changed here.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class SavedChartsNameOrderTests
{
    private const string Elise = "\u00C9lise Martin";
    private const string Ozlem = "\u00D6zlem Kaya";

    /// <summary>The seven invented names, in the order saved (Ids 1 to 7).</summary>
    private static readonly string[] Saved =
        ["Zoe Carter", "anna lind", "Daniel Whitfield", Elise, "Eleanor Hartwell", Ozlem, "bruno weiss"];

    /// <summary>The seven names as a person reads them, A to Z.</summary>
    private static readonly string[] Reading =
        ["anna lind", "bruno weiss", "Daniel Whitfield", "Eleanor Hartwell", Elise, Ozlem, "Zoe Carter"];

    /// <summary>
    /// Sorted by name, A to Z: the list and the pickers ignore case and put the accented E name among the E names
    /// and the accented O name among the O names.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SortedByName_AToZ_IgnoresCaseAndPutsAnAccentedLetterBesideItsBase()
    {
        using CultureScope scope = new(string.Empty);
        MainViewModel main = MainViewModelFactory.Create(repository: new SavedChartsRepository(Saved));

        await main.InitializeAsync();

        main.SortField.Should().Be(ChartSortField.Name, "the list is sorted by name unless the user chooses otherwise");
        Names(main.SavedCharts).Should().Equal(Reading);
        Names(main.AllSavedCharts).Should().Equal(Reading);
    }

    /// <summary>Sorted by name, Z to A: the same order reversed in the list; the pickers always read A to Z.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SortedByName_ZToA_IsTheSameOrderReversed()
    {
        using CultureScope scope = new(string.Empty);
        MainViewModel main = MainViewModelFactory.Create(repository: new SavedChartsRepository(Saved));
        await main.InitializeAsync();

        await RunAsync(main.ToggleSortDirectionCommand);

        main.SortAscending.Should().BeFalse();
        Names(main.SavedCharts).Should().Equal(Reading.Reverse());
        Names(main.AllSavedCharts).Should().Equal(Reading, "the pickers always read A to Z");
    }

    /// <summary>Sorted by another key the list is as the read gave it, in both directions.</summary>
    /// <param name="field">The sort key.</param>
    /// <param name="ascending">The direction.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(ChartSortField.Date, true)]
    [InlineData(ChartSortField.Date, false)]
    [InlineData(ChartSortField.EventType, true)]
    [InlineData(ChartSortField.CreatedAt, true)]
    [InlineData(ChartSortField.CreatedAt, false)]
    public async Task SortedByAnotherKey_TheListIsAsTheReadGaveIt(ChartSortField field, bool ascending)
    {
        using CultureScope scope = new(string.Empty);
        MainViewModel main = MainViewModelFactory.Create(repository: new SavedChartsRepository(Saved));
        await main.InitializeAsync();
        main.SortField = field;
        main.SortAscending = ascending;

        await RunAsync(main.SearchChartsCommand);

        Names(main.SavedCharts).Should().Equal(Saved, "the repository double reads in the order saved");
    }

    /// <summary>
    /// Names that read the same, differing only in case or an accent, keep the order the read gave them, in both
    /// directions.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SortedByName_NamesThatReadTheSame_KeepTheOrderRead()
    {
        using CultureScope scope = new(string.Empty);
        MainViewModel main = MainViewModelFactory.Create(
            repository: new SavedChartsRepository("elise martin", Elise, "ELISE MARTIN", "Anna Lind"));

        await main.InitializeAsync();
        Names(main.SavedCharts).Should().Equal("Anna Lind", "elise martin", Elise, "ELISE MARTIN");

        await RunAsync(main.ToggleSortDirectionCommand);
        Names(main.SavedCharts).Should().Equal("elise martin", Elise, "ELISE MARTIN", "Anna Lind");
    }

    /// <summary>A search sorted by name orders what it finds the same way.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Search_SortedByName_OrdersWhatItFinds()
    {
        using CultureScope scope = new(string.Empty);
        MainViewModel main = MainViewModelFactory.Create(repository: new SavedChartsRepository(Saved));
        await main.InitializeAsync();
        main.SearchText = "a";

        await RunAsync(main.SearchChartsCommand);

        Names(main.SavedCharts).Should().Equal(
            "anna lind", "Daniel Whitfield", "Eleanor Hartwell", Elise, Ozlem, "Zoe Carter");
    }

    /// <summary>
    /// Over the SQLite repository, whose own order is by bytes, the list and the pickers read as a person reads
    /// them, A to Z and Z to A.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SortedByName_OverTheDatabase_ReadsAsAPersonReads()
    {
        using CultureScope scope = new(string.Empty);
        string connectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
        using SqliteConnection sentinel = new(connectionString);
        sentinel.Open();
        SqliteChartRepository repository = new(connectionString, new LoggerConfiguration().CreateLogger());
        await repository.InitializeAsync();
        foreach (string name in Saved)
            await repository.SaveAsync(SavedChartsRepository.NewChart(0, name));
        Names(await repository.ListAllAsync()).Should().Equal(
            ["Daniel Whitfield", "Eleanor Hartwell", "Zoe Carter", "anna lind", "bruno weiss", Elise, Ozlem],
            "the database orders names by their bytes");
        MainViewModel main = MainViewModelFactory.Create(repository: repository);

        await main.InitializeAsync();
        Names(main.SavedCharts).Should().Equal(Reading);
        Names(main.AllSavedCharts).Should().Equal(Reading);

        await RunAsync(main.ToggleSortDirectionCommand);
        Names(main.SavedCharts).Should().Equal(Reading.Reverse());
    }

    /// <summary>The names of a list of saved charts, in order.</summary>
    /// <param name="charts">The charts.</param>
    /// <returns>Their names.</returns>
    private static IEnumerable<string> Names(IEnumerable<ChartEntity> charts) => charts.Select(chart => chart.Name);

    /// <summary>Runs a command and waits for its handler to finish.</summary>
    /// <param name="command">The command.</param>
    /// <returns>A task that completes when the command has finished, and fails after 5 s.</returns>
    private static async Task RunAsync(AsyncRelayCommand command)
    {
        command.Execute(null);

        int waited = 0;
        while (command.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        command.IsExecuting.Should().BeFalse("the command finishes within 5 s");
    }
}
