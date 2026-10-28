using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests pinning the contract the Electional panel actually
/// advertises: a search for N candidate windows returns N candidate windows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this class exists separately from <see cref="ElectionalServiceTests"/>.</b>
/// Every pre-existing electional integration fixture sets
/// <see cref="ElectionalInput.MinimumCandidateSeparationHours"/> to <c>0.0</c>,
/// which switches the separation filter off. That is a legitimate choice for
/// tests isolating scoring and ranking, but it means no test in the suite had
/// ever exercised the configuration the application actually ships:
/// <c>MainViewModel.CalculateElectionalAsync</c> never sets the property, so
/// every real search runs at the model default of 6 hours.
/// </para>
/// <para>
/// <b>What broke.</b> Commit <c>0e3f065</c> added the separation filter and,
/// in the same change, added <c>MinimumCandidateSeparationHours = 0.0</c> to the
/// two fixtures that would otherwise have caught the regression -- including
/// <c>Calculate_7DayRange_Should_ReturnRequestedCandidateCount</c>, whose name
/// promises exactly the property that broke. The engine sized its full-score
/// pool as <c>CandidateCount x 15</c> samples ranked by a Moon-only quick
/// score. Moon-favourable moments cluster in time, so that pool was temporally
/// narrow; the 6-hour separation filter then rejected most of it and nothing
/// topped the result back up. A request for five candidates returned three.
/// </para>
/// <para>
/// <b>Why the tests below deliberately omit
/// <see cref="ElectionalInput.MinimumCandidateSeparationHours"/>.</b> Setting it
/// -- to any value, including 6.0 -- would let the shipped default drift away
/// from what is tested. Omitting it means these tests bind to whatever the
/// application itself uses.
/// </para>
/// <para>
/// <b>Why both windows are asserted.</b> The shortfall is data-dependent and
/// non-monotonic: at the time of writing, the Richmond window returned 3 of 5
/// while a request of 5 against the NYC window returned 5 of 5. A single window
/// would let a fix that happens to suit one date range look complete. Both
/// windows were measured to admit far more separated moments than they are
/// asked for here (13 and 15 respectively when over-requested), so neither
/// target is at the mercy of the window's physical capacity.
/// </para>
/// <para>
/// <b>Assertion order is deliberate.</b> Count is asserted first because it is
/// the reported defect. Separation and score ordering follow because a fix that
/// met the count by weakening the 6-hour guard, or by returning candidates out
/// of score order, would be no fix at all -- those assertions exist to make that
/// shortcut fail loudly rather than pass quietly.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalCandidateCountTests
{
    private readonly EphemerisService _ephemeris;
    private readonly ChartEngine _chartEngine;
    private readonly ElectionalService _service;

    /// <summary>
    /// The separation the application ships with, taken from the
    /// <see cref="ElectionalInput"/> model default. Used only to verify the
    /// guard still holds -- never assigned to an input, so the tests keep
    /// binding to the real default rather than to a copy of it.
    /// </summary>
    private const double ShippedSeparationHours = 6.0;

    /// <summary>Richmond, VA latitude (Doctor Visit live-run location).</summary>
    private const double RichmondLatitude = 37.5385;

    /// <summary>Richmond, VA longitude.</summary>
    private const double RichmondLongitude = -77.4343;

    /// <summary>New York City latitude (golden-fixture election location).</summary>
    private const double NycLatitude = 40.7128;

    /// <summary>New York City longitude.</summary>
    private const double NycLongitude = -74.006;

    /// <summary>IANA timezone shared by both election locations.</summary>
    private const string TimeZone = "America/New_York";

    /// <summary>
    /// Cached natal chart shared across tests in this class. The chart is
    /// deterministic, so recalculating it per test only costs time.
    /// </summary>
    private static ChartResult? s_natalChart;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the full service graph.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public ElectionalCandidateCountTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        _ephemeris = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(_ephemeris, logger);
        MidpointService midpoints = new(logger);
        _chartEngine = new ChartEngine(_ephemeris, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(_ephemeris, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(_ephemeris), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
        _service = new ElectionalService(_ephemeris, _chartEngine);
    }

    #region Requested count is delivered at the shipped separation

    /// <summary>
    /// The Doctor Visit window from the live-run review: a request for five
    /// candidates must return five. Measured returning three before the fix.
    /// </summary>
    [Fact]
    public void Calculate_ShippedSeparation_RichmondFiveRequested_Should_ReturnFive()
    {
        // Arrange -- note the absence of MinimumCandidateSeparationHours
        ElectionalInput input = new()
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Best time to visit Doctor",
            RangeStart = new DateOnly(2026, 6, 11),
            RangeEnd = new DateOnly(2026, 6, 14),
            Latitude = RichmondLatitude,
            Longitude = RichmondLongitude,
            LocationName = "Richmond, VA",
            TimeZone = TimeZone,
            CandidateCount = 5,
        };

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert
        AssertRequestFullyMet(result, expectedCount: 5);
    }

    /// <summary>
    /// The J2000 NYC business-launch window used by the electional golden: a
    /// request for three candidates must return three. Measured returning two
    /// before the fix, and pinned here so the fix is not tuned to a single
    /// date range, location, or election type.
    /// </summary>
    [Fact]
    public void Calculate_ShippedSeparation_NycThreeRequested_Should_ReturnThree()
    {
        // Arrange -- note the absence of MinimumCandidateSeparationHours
        ElectionalInput input = new()
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch a new software product.",
            RangeStart = new DateOnly(2026, 4, 20),
            RangeEnd = new DateOnly(2026, 4, 25),
            Latitude = NycLatitude,
            Longitude = NycLongitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 3,
        };

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert
        AssertRequestFullyMet(result, expectedCount: 3);
    }

    #endregion

    #region Assertion helper

    /// <summary>
    /// Asserts the three properties a satisfied electional request must have:
    /// the requested number of candidates, all mutually separated by at least
    /// the shipped 6-hour minimum, ranked by descending overall score.
    /// </summary>
    /// <param name="result">The calculated electional result.</param>
    /// <param name="expectedCount">The number of candidates requested.</param>
    /// <remarks>
    /// The separation and ordering assertions are not incidental extras: they
    /// close the two ways a count-only assertion could be satisfied without
    /// fixing anything -- disabling the clustering guard, or returning an
    /// unranked list.
    /// </remarks>
    private static void AssertRequestFullyMet(ElectionalResult result, int expectedCount)
    {
        // Asserted on the scalar Count rather than via HaveCount: FluentAssertions
        // renders the whole collection on failure, and a CandidateWindow carries a
        // complete ChartResult, so the useful message would be buried under a
        // hundred lines of object dump.
        string delivered = string.Join(", ", result.Candidates.Select(
            c => c.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)));

        result.Candidates.Count.Should().Be(expectedCount,
            "a search for {0} candidate windows must return {0} candidate windows "
            + "at the shipped separation default, and this window was measured to admit "
            + "far more (delivered: {1})",
            expectedCount,
            delivered.Length == 0 ? "none" : delivered);

        for (int i = 0; i < result.Candidates.Count; i++)
        {
            for (int j = i + 1; j < result.Candidates.Count; j++)
            {
                double hoursApart = Math.Abs(
                    (result.Candidates[i].LocalDateTime - result.Candidates[j].LocalDateTime).TotalHours);

                hoursApart.Should().BeGreaterThanOrEqualTo(ShippedSeparationHours,
                    "candidates {0} ({1:yyyy-MM-dd HH:mm}) and {2} ({3:yyyy-MM-dd HH:mm}) must stay at "
                    + "least {4} hours apart -- the anti-clustering guard must survive any fix to the count",
                    i + 1, result.Candidates[i].LocalDateTime,
                    j + 1, result.Candidates[j].LocalDateTime,
                    ShippedSeparationHours);
            }
        }

        for (int i = 1; i < result.Candidates.Count; i++)
        {
            result.Candidates[i - 1].Score.Should().BeGreaterThanOrEqualTo(
                result.Candidates[i].Score,
                "candidates must remain ranked by descending overall score");
        }

        for (int i = 0; i < result.Candidates.Count; i++)
        {
            result.Candidates[i].Rank.Should().Be(i + 1,
                "ranks must be assigned 1..N in returned order");
        }
    }

    #endregion

    #region Natal fixture

    /// <summary>
    /// Calculates (once) and caches the J2000 NYC natal chart, the same
    /// reference chart the electional goldens are anchored to.
    /// </summary>
    /// <returns>The cached natal chart.</returns>
    private ChartResult GetNatalChart()
    {
        return s_natalChart ??= _chartEngine.Calculate(new ChartInput
        {
            Name = "J2000 NYC Fixture",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = TimeZone,
            Latitude = NycLatitude,
            Longitude = NycLongitude,
            LocationName = "New York City",
            EventType = "Birth",
            HouseSystem = HouseSystem.Placidus,
        });
    }

    #endregion
}
