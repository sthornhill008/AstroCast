using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests that an electional search which cannot supply the requested number of
/// candidate windows says so in its export, instead of silently returning a
/// short list.
/// </summary>
/// <remarks>
/// <para>
/// Some windows genuinely cannot meet a request: a single day cannot hold ten
/// moments six hours apart, and a range fully covered by Mercury retrograde can
/// hold none at all. Returning fewer candidates is the correct answer in those
/// cases -- but before <see cref="ElectionalResult.RequestedCandidateCount"/>
/// existed, the export rendered <c>Candidates: 3</c> whether three was
/// everything the window had or three was a symptom of the engine starving. The
/// reader could not tell the two apart, which is what let the starvation bug go
/// unnoticed in live use.
/// </para>
/// <para>
/// Every case below is engineered to under-deliver by construction rather than
/// by ephemeris coincidence -- a one-day window cannot physically supply the
/// counts requested -- so the tests do not depend on the sky on any given date.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalShortfallHeaderTests
{
    private readonly ChartEngine _chartEngine;
    private readonly ElectionalService _service;

    /// <summary>Richmond, VA latitude.</summary>
    private const double Latitude = 37.5385;

    /// <summary>Richmond, VA longitude.</summary>
    private const double Longitude = -77.4343;

    /// <summary>IANA timezone for the election location.</summary>
    private const string TimeZone = "America/New_York";

    /// <summary>Cached natal chart shared across the tests in this class.</summary>
    private static ChartResult? s_natalChart;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the service graph.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public ElectionalShortfallHeaderTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _chartEngine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
        _service = new ElectionalService(fixture.EphemerisService, _chartEngine);
    }

    #region Unmet request

    /// <summary>
    /// A single day cannot hold ten moments six hours apart, so the request goes
    /// unmet and both the result and the export must say so, naming the
    /// separation that made it impossible.
    /// </summary>
    [Fact]
    public void ToMarkdown_RequestExceedsWhatWindowCanHold_Should_ReportShortfallWithSeparation()
    {
        // Arrange -- one day, ten requested, shipped 6h separation
        ElectionalInput input = BuildInput(
            new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 12), candidateCount: 10);

        // Act
        ElectionalResult result = _service.Calculate(input);
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        result.Candidates.Count.Should().BeLessThan(10,
            "a 24-hour window cannot hold ten moments six hours apart");
        result.IsRequestUnmet.Should().BeTrue();
        result.RequestedCandidateCount.Should().Be(10);

        string header = CandidatesHeaderLine(markdown);

        header.Should().StartWith($"**Candidate instants:** {result.Candidates.Count} of 10 requested",
            "the header must show what was delivered against what was asked for");
        header.Should().EndWith("could not supply 10 qualifying moments at least 6 hours apart",
            "the header must say why the request could not be met");
    }

    /// <summary>
    /// A separation of exactly one hour reads "1 hour apart", as the Separation line already reads "at least
    /// 1 hour" (branch ExportText, 1.7.2; found beside finding N12). Until 1.7.2: "1 hours apart".
    /// </summary>
    [Fact]
    public void ToMarkdown_OneHourSeparationAndRequestUnmet_Should_ReadOneHour()
    {
        // Arrange -- one day cannot hold a hundred moments an hour apart
        ElectionalInput input = BuildInput(
            new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 12),
            candidateCount: 100, separationHours: 1.0);

        // Act
        ElectionalResult result = _service.Calculate(input);
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        result.IsRequestUnmet.Should().BeTrue();
        CandidatesHeaderLine(markdown).TrimEnd().Should().EndWith(
            "could not supply 100 qualifying moments at least 1 hour apart");
    }

    /// <summary>
    /// With separation disabled, a shortfall is still reported but the wording
    /// must not blame a separation rule that was not applied.
    /// </summary>
    [Fact]
    public void ToMarkdown_SeparationDisabledAndRequestUnmet_Should_OmitSeparationClause()
    {
        // Arrange -- one day at a 15-minute grid holds at most 96 moments
        ElectionalInput input = BuildInput(
            new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 12),
            candidateCount: 500, separationHours: 0.0);

        // Act
        ElectionalResult result = _service.Calculate(input);
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        result.IsRequestUnmet.Should().BeTrue();

        string header = CandidatesHeaderLine(markdown);

        header.Should().EndWith("could not supply 500 qualifying moments",
            "the shortfall is still reported when separation is disabled");
        header.Should().NotContain("hours apart",
            "no separation was applied, so none may be cited as the cause");
    }

    #endregion

    #region Met request

    /// <summary>
    /// A met request renders exactly as it always has -- a bare count, with no
    /// shortfall clause -- so the common case gains no noise and downstream
    /// consumers that read the header are unaffected.
    /// </summary>
    [Fact]
    public void ToMarkdown_RequestMet_Should_RenderBareCount()
    {
        // Arrange -- four days comfortably supply three separated moments
        ElectionalInput input = BuildInput(
            new DateOnly(2026, 6, 11), new DateOnly(2026, 6, 14), candidateCount: 3);

        // Act
        ElectionalResult result = _service.Calculate(input);
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert
        result.Candidates.Count.Should().Be(3);
        result.IsRequestUnmet.Should().BeFalse();

        CandidatesHeaderLine(markdown).Should().Be("**Candidate instants:** 3",
            "a met request renders the bare count, with no shortfall clause");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Extracts the Candidate instants header line from a rendered export.
    /// </summary>
    /// <param name="markdown">The full markdown export.</param>
    /// <returns>The header line, trimmed of its line terminator.</returns>
    /// <remarks>
    /// Split on the line content rather than asserting against an embedded
    /// newline: the formatter writes <see cref="Environment.NewLine"/>, which
    /// differs between Windows and macOS, and both platforms run this suite.
    /// </remarks>
    private static string CandidatesHeaderLine(string markdown)
    {
        string? line = markdown
            .Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .FirstOrDefault(l => l.StartsWith("**Candidate instants:**", StringComparison.Ordinal));

        line.Should().NotBeNull("every export carries a Candidate instants header line");

        return line!;
    }

    /// <summary>
    /// Builds a Richmond electional input for the given range and count.
    /// </summary>
    /// <param name="start">First acceptable date.</param>
    /// <param name="end">Last acceptable date.</param>
    /// <param name="candidateCount">Number of candidate windows to request.</param>
    /// <param name="separationHours">
    /// Minimum separation to apply, or null to leave the shipped model default
    /// in place.
    /// </param>
    /// <returns>A populated <see cref="ElectionalInput"/>.</returns>
    private ElectionalInput BuildInput(
        DateOnly start,
        DateOnly end,
        int candidateCount,
        double? separationHours = null)
    {
        if (separationHours is null)
        {
            return new ElectionalInput
            {
                NatalChart = GetNatalChart(),
                ElectionType = ElectionType.Other,
                ElectionGoal = "Best time to visit Doctor",
                RangeStart = start,
                RangeEnd = end,
                Latitude = Latitude,
                Longitude = Longitude,
                LocationName = "Richmond, VA",
                TimeZone = TimeZone,
                CandidateCount = candidateCount,
            };
        }

        return new ElectionalInput
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Best time to visit Doctor",
            RangeStart = start,
            RangeEnd = end,
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "Richmond, VA",
            TimeZone = TimeZone,
            CandidateCount = candidateCount,
            MinimumCandidateSeparationHours = separationHours.Value,
        };
    }

    /// <summary>
    /// Calculates (once) and caches the J2000 NYC natal chart.
    /// </summary>
    /// <returns>The cached natal chart.</returns>
    private ChartResult GetNatalChart()
    {
        return s_natalChart ??= _chartEngine.Calculate(new ChartInput
        {
            Name = "J2000 NYC Fixture",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = TimeZone,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
            HouseSystem = HouseSystem.Placidus,
        });
    }

    #endregion
}
