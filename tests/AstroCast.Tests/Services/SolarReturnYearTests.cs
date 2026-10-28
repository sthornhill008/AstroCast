using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the year of a solar return (ruling T-D7, finding T2-1, branch A): "Solar Return Y" is the
/// Sun's return to its natal longitude nearest the birthday in Y, the birthday being 12:00 UT on
/// the birth's local month and day in Y. Until branch A the first return from 1 December of the
/// year before won, so every December native got the previous December's return.
/// </summary>
/// <remarks>
/// Every expected instant is Tier B: the Sun's crossings of its natal longitude found by direct
/// <c>swe_calc_ut</c> root-finding (a 0.05-day grid refined to 1e-9 day, the timing investigation's
/// truth library), and the nearest to the birthday taken by hand. Tier C (Skyfield, DE440) agrees
/// with person B's two returns to 0.003 s. The app casts a return chart at the instant rounded to the
/// second, so each is held to within one second.
/// </remarks>
[Collection("Ephemeris")]
public sealed class SolarReturnYearTests
{
    /// <summary>One second, in days.</summary>
    private const double OneSecond = 1.0 / 86400.0;

    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public SolarReturnYearTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Person B (born 5 December 1964): each year's return is that December's, and the birth year's
    /// is the birth instant itself (JD 2438735.312500; fixtures/truth/solar_returns.py). Until branch A "2025" gave the December before
    /// its birthday, "2026" the December 2025 return, and "1964" the return of December 1963, a year before birth.
    /// </summary>
    /// <param name="year">The requested year.</param>
    /// <param name="expectedJd">The Tier B return nearest the birthday in that year (Julian Day, UT).</param>
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(2025, 2461015.110792)]
    [InlineData(2026, 2461380.353274)]
    [InlineData(1964, 2438735.312500)]
    public void PersonB_ReturnOfAYear_IsTheReturnNearestThatYearsBirthday(int year, double expectedJd)
    {
        SolarReturnResult result = Calculate(PersonB(), year);

        result.ReturnChart.JulianDay.Should().BeApproximately(expectedJd, OneSecond);
        result.ReturnChart.UtcDateTime.Year.Should().Be(year);
        result.ReturnChart.UtcDateTime.Month.Should().Be(12);
    }

    /// <summary>
    /// Person C (born 23 December 1964): "2026" is the return of 2026-12-23 17:01:28 UT (Tier B), in
    /// December 2026; until branch A it was the December 2025 return.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void PersonC_Return2026_IsInDecember2026()
    {
        SolarReturnResult result = Calculate(PersonC(), 2026);

        result.ReturnChart.JulianDay.Should().BeApproximately(2461398.209352, OneSecond);
        result.ReturnChart.UtcDateTime.Should().BeCloseTo(new DateTime(2026, 12, 23, 17, 1, 28, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// A 1 January birth keeps the return of the last day of the year before: the golden's J2000
    /// chart (born 2000-01-01 12:00 UT) gets 2025-12-31 19:17:48 UT (Tier B) for "2026", 0.7 days
    /// before its birthday and 364.6 before the next. Unchanged by branch A.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void FirstOfJanuaryBirth_Return2026_IsStill31December2025()
    {
        SolarReturnResult result = Calculate(GoldenFixtures.J2000Nyc(_fixture), 2026);

        result.ReturnChart.JulianDay.Should().BeApproximately(2461041.304030, OneSecond);
        result.ReturnChart.UtcDateTime.Date.Should().Be(new DateTime(2025, 12, 31));
    }

    /// <summary>
    /// The export of person B's birth-year return completes: the return is the birth instant, age 0,
    /// so the annual profection that follows no longer meets a return a year before birth (it threw
    /// <see cref="ArgumentOutOfRangeException"/> until branch A).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void PersonB_BirthYearExport_Completes()
    {
        SolarReturnResult result = Calculate(PersonB(), 1964);

        string markdown = SolarReturnMarkdownFormatter.ToMarkdown(result);

        markdown.Should().Contain("**Return Date/Time:** 1964-12-05 19:30:00 UTC")
            .And.Contain("**Age:** 0 ");
    }

    #region Helpers

    private SolarReturnResult Calculate(ChartResult natal, int year) =>
        new SolarReturnService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger)
            .Calculate(natal, year, natal.Input.Latitude, natal.Input.Longitude, natal.Input.LocationName, natal.Input.TimeZone);

    /// <summary>Person B (invented): 1964-12-05 14:30 America/New_York (19:30 UT), Albany, New York.</summary>
    private ChartResult PersonB() => SavedBirth("Margaret Whitfield", new DateTime(1964, 12, 5, 14, 30, 0), 2438735.3125);

    /// <summary>Person C (invented): 1964-12-23 11:00 America/New_York (16:00 UT), Albany, New York.</summary>
    private ChartResult PersonC() => SavedBirth("Daniel Whitfield", new DateTime(1964, 12, 23, 11, 0, 0), 2438753.1666666665);

    private ChartResult SavedBirth(string name, DateTime local, double expectedJd)
    {
        ChartResult chart = GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
        {
            Name = name,
            LocalDateTime = local,
            TimeZone = "America/New_York",
            Latitude = 42.6526,
            Longitude = -73.7562,
            LocationName = "Albany, New York, United States",
            EventType = "Birth",
        });
        chart.JulianDay.Should().BeApproximately(expectedJd, 1e-9, "the chart is cast at its saved instant");
        return chart;
    }

    #endregion
}
