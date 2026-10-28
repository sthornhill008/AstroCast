using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Services;

using AstroCast.Services;
using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Pins the map click's crossing report as the view model builds it (rule 3 of branch J): it
/// asks Core's crossing method for the report with Core's default orb, 334 km, and prints each
/// line's great-circle distance in kilometres; it no longer builds its own with a 3-degree orb
/// of longitude.
/// </summary>
/// <remarks>
/// Every service but astrocartography is a stub; the astrocartography service is the real one
/// on the shared ephemeris, so the text is what a click on the map shows. Person A's expected
/// line and distance at Boulder come from the meridian formula on Tier B values (44.57 km;
/// fixtures/truth/acg_hartwell.py); the place at 60° N, 100° E is 479 km from person A's
/// nearest line (Saturn IC, Tier B).
/// </remarks>
[Collection("Ephemeris")]
public sealed class AcgCrossingReportTextTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public AcgCrossingReportTextTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The cultures each test also runs under.</summary>
    public static TheoryData<string> AllCultures => new() { "en-US", "de-DE", "sv-SE", "th-TH" };

    /// <summary>
    /// Person A clicked at Boulder lists Mars MC at 45 km, and nothing else; until branch J a
    /// crossing was a difference of longitude printed in degrees.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllCultures))]
    public void UpdateCrossingReport_HartwellAtBoulder_ListsKilometresWithinTheDefaultOrb(string culture)
    {
        using CultureScope scope = new(culture);
        MainViewModel vm = CreateWithHartwell();

        vm.UpdateCrossingReport(40.0150, -105.2705);

        Lines(vm.Astrocartography.CrossingReportText).Should().Equal(
            "Crossing Report: 40.02°N, 105.27°W",
            "",
            "  Mars MC: 45 km");
    }

    /// <summary>A click with no line within 334 km says so, in kilometres.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllCultures))]
    public void UpdateCrossingReport_NoLineWithinTheOrb_SaysSoInKilometres(string culture)
    {
        using CultureScope scope = new(culture);
        MainViewModel vm = CreateWithHartwell();

        vm.UpdateCrossingReport(60.0, 100.0);

        Lines(vm.Astrocartography.CrossingReportText).Should().Equal(
            "Crossing Report: 60.00°N, 100.00°E",
            "No angular lines within 334 km.");
    }

    /// <summary>
    /// A view model built as <see cref="AstrocartographyViewModelTests"/> builds it, but with the
    /// real astrocartography service, holding person A's lines as the map would.
    /// </summary>
    private MainViewModel CreateWithHartwell()
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        AstrocartographyService service = new(_fixture.EphemerisService, logger);
        MainViewModel vm = new(
            new StubChartEngine(),
            new StubChartRepository(),
            new StubEphemerisService(),
            new StubSynastryService(),
            new StubCompositeService(),
            new StubSolarReturnService(),
            new StubLunarReturnService(),
            new StubPlanetaryReturnService(),
            new StubCycleTimelineService(),
            new StubGreatConjunctionService(),
            new StubIngressService(),
            new StubLunationService(),
            new StubRelocationService(),
            new StubMundaneAspectService(),
            new StubEclipseService(),
            new StubReturnIngressTimelineService(),
            new StubSynodicCycleService(),
            new StubTransitService(),
            new StubElectionalService(),
            service,
            new TestDialogService(),
            new OperationCancellationRegistry(),
            logger);

        ChartResult chart = GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
        {
            Name = "Eleanor Hartwell",
            LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
            TimeZone = "America/New_York",
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, Pennsylvania, United States",
            EventType = "Birth",
        });
        AcgResult acg = service.Calculate(chart);
        vm.Astrocartography.CurrentResult = acg;
        return vm;
    }

    /// <summary>The report's lines, whatever the line ending, without the trailing empty one.</summary>
    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
}
