using System.ComponentModel;

using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// <see cref="DirectionsViewModel"/>, the Directions panel's child (branch PrimaryPanel, 1.5.0): its four
/// settings and their defaults, the validity gate Calculate Directions reads, the settings it builds, the
/// result and the two texts derived from it, and <see cref="DirectionsViewModel.Clear"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DirectionsViewModelTests
{
    #region Test Infrastructure

    /// <summary>A stubbed parent and its Directions child.</summary>
    /// <returns>The parent and the child.</returns>
    private static (MainViewModel Parent, DirectionsViewModel Child) CreateChild()
    {
        MainViewModel parent = MainViewModelFactory.Create();
        return (parent, parent.Directions);
    }

    /// <summary>A chart from the stub engine named as the J2000 fixture is.</summary>
    /// <returns>The chart.</returns>
    private static ChartResult Chart() => new StubChartEngine().Calculate(new ChartInput
    {
        Name = "J2000 NYC Fixture",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    });

    /// <summary>The direct Moon conjunct the Midheaven of J2000 NYC, as a synthetic hit.</summary>
    /// <returns>The hit.</returns>
    private static PrimaryDirectionHit Hit() => new()
    {
        Significator = "Midheaven",
        Promissor = "Moon",
        PromissorKind = PrimaryDirectionPromissorKind.Body,
        Aspect = PrimaryDirectionAspect.Conjunction,
        AspectAngle = 0.0,
        Motion = PrimaryDirectionMotion.Direct,
        Arc = 16.001096,
        Years = 16.234098,
        JulianDayUt = 2457474.377752,
        InstantUtc = new DateTime(2016, 3, 26, 21, 3, 58, DateTimeKind.Utc),
        MonthsPerMinute = -2.9539,
    };

    /// <summary>A result of <paramref name="hits"/> hits with <paramref name="leftOut"/> left out, under the default settings.</summary>
    /// <param name="hits">The number of hits.</param>
    /// <param name="leftOut">The directions left out.</param>
    /// <returns>The result.</returns>
    private static PrimaryDirectionResult Result(int hits, int leftOut) => new()
    {
        NatalChart = Chart(),
        Settings = PrimaryDirectionSettings.Default,
        Hits = Enumerable.Range(0, hits).Select(_ => Hit()).ToList(),
        LeftOutCount = leftOut,
    };

    #endregion

    /// <summary>
    /// The defaults are the engine's: Naibod, no planets, age 100, no uncertainty; valid, with no result and
    /// both texts empty.
    /// </summary>
    [Fact]
    public void Defaults_AreTheEnginesDefaults()
    {
        (_, DirectionsViewModel child) = CreateChild();

        child.Key.Should().Be(PrimaryDirectionKey.Naibod);
        child.PlanetsAsSignificators.Should().BeFalse();
        child.AgeLimitYears.Should().Be(100);
        child.BirthTimeUncertaintyMinutes.Should().Be(0);
        child.HasValidSettings.Should().BeTrue();
        child.CurrentResult.Should().BeNull();
        child.HasResult.Should().BeFalse();
        child.ResultSummary.Should().BeEmpty();
        child.ListText.Should().BeEmpty();
    }

    /// <summary>
    /// Each setting notifies its own binding and asks the parent once for a command refresh; the two numbers
    /// also raise <see cref="DirectionsViewModel.HasValidSettings"/>.
    /// </summary>
    /// <param name="property">The setting's property name.</param>
    [Theory]
    [InlineData(nameof(DirectionsViewModel.Key))]
    [InlineData(nameof(DirectionsViewModel.PlanetsAsSignificators))]
    [InlineData(nameof(DirectionsViewModel.AgeLimitYears))]
    [InlineData(nameof(DirectionsViewModel.BirthTimeUncertaintyMinutes))]
    public void Setting_Set_NotifiesAndRequestsOneRefresh(string property)
    {
        (_, DirectionsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        bool isNumber = false;
        switch (property)
        {
            case nameof(DirectionsViewModel.Key):
                child.Key = PrimaryDirectionKey.Ptolemy;
                break;
            case nameof(DirectionsViewModel.PlanetsAsSignificators):
                child.PlanetsAsSignificators = true;
                break;
            case nameof(DirectionsViewModel.AgeLimitYears):
                child.AgeLimitYears = 90;
                isNumber = true;
                break;
            case nameof(DirectionsViewModel.BirthTimeUncertaintyMinutes):
                child.BirthTimeUncertaintyMinutes = 4;
                isNumber = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, "Not a setting.");
        }

        changed.Should().Contain(property, "the setter must notify its control's binding");
        refreshCount.Should().Be(1, "Calculate Directions' CanExecute reads the settings");
        if (isNumber)
            changed.Should().Contain(nameof(DirectionsViewModel.HasValidSettings));
    }

    /// <summary>The settings built for the engine carry the four the panel holds.</summary>
    [Fact]
    public void Settings_CarriesTheFour()
    {
        (_, DirectionsViewModel child) = CreateChild();
        child.Key = PrimaryDirectionKey.SolarArcRightAscension;
        child.PlanetsAsSignificators = true;
        child.AgeLimitYears = 90;
        child.BirthTimeUncertaintyMinutes = 4;

        PrimaryDirectionSettings settings = child.Settings;

        settings.Key.Should().Be(PrimaryDirectionKey.SolarArcRightAscension);
        settings.PlanetsAsSignificators.Should().BeTrue();
        settings.AgeLimitYears.Should().Be(90);
        settings.BirthTimeUncertaintyMinutes.Should().Be(4);
    }

    /// <summary>The settings are valid only within the engine's limits: an age of 1 to 120 years and an uncertainty of 0 to 120 minutes.</summary>
    /// <param name="age">The age limit.</param>
    /// <param name="uncertainty">The uncertainty.</param>
    /// <param name="valid">Whether the pair is valid.</param>
    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(120, 120, true)]
    [InlineData(0, 0, false)]
    [InlineData(121, 0, false)]
    [InlineData(100, -1, false)]
    [InlineData(100, 121, false)]
    public void HasValidSettings_FollowsTheEnginesLimits(int age, int uncertainty, bool valid)
    {
        (_, DirectionsViewModel child) = CreateChild();

        child.AgeLimitYears = age;
        child.BirthTimeUncertaintyMinutes = uncertainty;

        child.HasValidSettings.Should().Be(valid);
    }

    /// <summary>Setting the result raises it, <c>HasResult</c>, the summary and the list, and asks once for a refresh.</summary>
    [Fact]
    public void CurrentResult_Set_RaisesItsThreeAndARefresh()
    {
        (_, DirectionsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.CurrentResult = Result(2, 0);

        changed.Should().Contain(
        [
            nameof(DirectionsViewModel.CurrentResult),
            nameof(DirectionsViewModel.HasResult),
            nameof(DirectionsViewModel.ResultSummary),
            nameof(DirectionsViewModel.ListText),
        ]);
        refreshCount.Should().Be(1, "Export Directions MD's CanExecute reads HasResult");
        child.HasResult.Should().BeTrue();
    }

    /// <summary>
    /// The result line names the chart, the count to the age limit and the key, and the count left out only
    /// above 0; one direction reads in the singular.
    /// </summary>
    /// <param name="hits">The number of hits.</param>
    /// <param name="leftOut">The directions left out.</param>
    /// <param name="expected">The line.</param>
    [Theory]
    [InlineData(572, 60, "J2000 NYC Fixture: 572 directions to age 100, Naibod (0°59′08″ a year); 60 left out")]
    [InlineData(1, 1, "J2000 NYC Fixture: 1 direction to age 100, Naibod (0°59′08″ a year); 1 left out")]
    [InlineData(10, 0, "J2000 NYC Fixture: 10 directions to age 100, Naibod (0°59′08″ a year)")]
    public void ResultSummary_NamesTheChartTheCountAndTheKey(int hits, int leftOut, string expected)
    {
        (_, DirectionsViewModel child) = CreateChild();

        child.CurrentResult = Result(hits, leftOut);

        child.ResultSummary.Should().Be(expected);
    }

    /// <summary>The list is the whole-life report's table, so the screen and the file show the same rows.</summary>
    [Fact]
    public void ListText_IsTheReportsTable()
    {
        (_, DirectionsViewModel child) = CreateChild();
        PrimaryDirectionResult result = Result(3, 0);

        child.CurrentResult = result;

        child.ListText.Should().Be(PrimaryDirectionMarkdownFormatter.FormatTable(result));
        child.ListText.Should().StartWith("| Date (UTC) |");
    }

    /// <summary>Clear puts every setting back to its default and drops the result, with one refresh.</summary>
    [Fact]
    public void Clear_ResetsEverythingWithOneRefresh()
    {
        (_, DirectionsViewModel child) = CreateChild();
        child.Key = PrimaryDirectionKey.Ptolemy;
        child.PlanetsAsSignificators = true;
        child.AgeLimitYears = 0;
        child.BirthTimeUncertaintyMinutes = 4;
        child.CurrentResult = Result(2, 1);
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.Clear();

        child.Key.Should().Be(PrimaryDirectionKey.Naibod);
        child.PlanetsAsSignificators.Should().BeFalse();
        child.AgeLimitYears.Should().Be(100);
        child.BirthTimeUncertaintyMinutes.Should().Be(0);
        child.HasValidSettings.Should().BeTrue();
        child.CurrentResult.Should().BeNull();
        child.HasResult.Should().BeFalse();
        child.ResultSummary.Should().BeEmpty();
        child.ListText.Should().BeEmpty();
        changed.Should().Contain(
        [
            nameof(DirectionsViewModel.Key),
            nameof(DirectionsViewModel.PlanetsAsSignificators),
            nameof(DirectionsViewModel.AgeLimitYears),
            nameof(DirectionsViewModel.BirthTimeUncertaintyMinutes),
            nameof(DirectionsViewModel.HasValidSettings),
            nameof(DirectionsViewModel.CurrentResult),
            nameof(DirectionsViewModel.HasResult),
            nameof(DirectionsViewModel.ResultSummary),
            nameof(DirectionsViewModel.ListText),
        ]);
        refreshCount.Should().Be(1, "Clear bypasses the setters and asks once at the end");
    }

    /// <summary>The key box offers the three keys in order: Naibod, Ptolemy, the solar arc in right ascension.</summary>
    [Fact]
    public void AvailableKeys_AreTheThreeInOrder()
    {
        DirectionsViewModel.AvailableKeys.Should().Equal(
            PrimaryDirectionKey.Naibod, PrimaryDirectionKey.Ptolemy, PrimaryDirectionKey.SolarArcRightAscension);
    }
}
