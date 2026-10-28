using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="IngressService"/> verifying sign-entry timing,
/// cardinal ingress charts, non-solar ingresses, and retrograde handling.
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class IngressServiceTests
{
    private readonly IngressService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the service.
    /// </summary>
    public IngressServiceTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        DignityService dignity = new();
        MoonConditionService moonCondition = new();
        ChartContextService chartContext = new(dignity, new SectService());
        FixedStarService fixedStars = new(fixture.EphemerisService, logger);

        ChartEngine engine = new(
            fixture.EphemerisService, aspects, midpoints, dignity,
            moonCondition, chartContext, fixedStars, new DeclinationAspectService(),
            new PlanetConditionService(), new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()), new AntisciaService(),
            new StationService(), new ParanService(fixture.EphemerisService, logger),
            logger);

        _service = new IngressService(
            fixture.EphemerisService, engine, logger);
    }

    #region FindIngresses — Cardinal Only Tests

    [Fact]
    public void FindIngresses_SunCardinalOnly2026_Returns4Ingresses()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        series.Count.Should().Be(4,
            "the Sun crosses four cardinal points each year");
        series.Ingresses.Should().HaveCount(4);
    }

    [Fact]
    public void FindIngresses_SunCardinal2026_AriesIngressInMarch()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        IngressResult aries = series.Ingresses
            .First(i => i.Sign == "Aries");

        aries.ExactUtc.Month.Should().Be(3,
            "the Aries ingress (Vernal Equinox) occurs in March");
        aries.ExactUtc.Day.Should().BeInRange(19, 21);
    }

    [Fact]
    public void FindIngresses_SunCardinal2026_CancerIngressInJune()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        IngressResult cancer = series.Ingresses
            .First(i => i.Sign == "Cancer");

        cancer.ExactUtc.Month.Should().Be(6,
            "the Cancer ingress (Summer Solstice) occurs in June");
        cancer.ExactUtc.Day.Should().BeInRange(20, 22);
    }

    [Fact]
    public void FindIngresses_SunCardinal2026_LibraIngressInSeptember()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        IngressResult libra = series.Ingresses
            .First(i => i.Sign == "Libra");

        libra.ExactUtc.Month.Should().Be(9,
            "the Libra ingress (Autumnal Equinox) occurs in September");
        libra.ExactUtc.Day.Should().BeInRange(22, 24);
    }

    [Fact]
    public void FindIngresses_SunCardinal2026_CapricornIngressInDecember()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        IngressResult capricorn = series.Ingresses
            .First(i => i.Sign == "Capricorn");

        capricorn.ExactUtc.Month.Should().Be(12,
            "the Capricorn ingress (Winter Solstice) occurs in December");
        capricorn.ExactUtc.Day.Should().BeInRange(21, 23);
    }

    [Fact]
    public void FindIngresses_SunCardinal2026_AllHaveSubArcSecondPrecision()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        foreach (IngressResult ingress in series.Ingresses)
        {
            ingress.PrecisionArcSeconds.Should().BeLessThan(1.0,
                $"{ingress.Sign} ingress should have sub-arc-second precision");
        }
    }

    [Fact]
    public void FindIngresses_SunCardinal2026_AllHaveCorrectSignNames()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        string[] expectedSigns = ["Aries", "Cancer", "Libra", "Capricorn"];
        series.Ingresses.Select(i => i.Sign).Should().BeEquivalentTo(expectedSigns);
    }

    [Fact]
    public void FindIngresses_SunCardinal2026_AllHaveSeasonNames()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        foreach (IngressResult ingress in series.Ingresses)
        {
            ingress.SeasonName.Should().NotBeNullOrWhiteSpace(
                $"{ingress.Sign} cardinal ingress should have a season name");
        }

        series.Ingresses.Select(i => i.SeasonName).Should().Contain("Vernal Equinox");
        series.Ingresses.Select(i => i.SeasonName).Should().Contain("Summer Solstice");
        series.Ingresses.Select(i => i.SeasonName).Should().Contain("Autumnal Equinox");
        series.Ingresses.Select(i => i.SeasonName).Should().Contain("Winter Solstice");
    }

    [Fact]
    public void FindIngresses_SunCardinal2026_AllAreCardinalIngress()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        foreach (IngressResult ingress in series.Ingresses)
        {
            ingress.IsCardinalIngress.Should().BeTrue(
                $"{ingress.Sign} ingress should be marked as cardinal");
        }
    }

    #endregion

    #region FindIngresses — All Signs Tests

    [Fact]
    public void FindIngresses_SunAllSigns2026_Returns12Ingresses()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: false);

        series.Count.Should().Be(12,
            "the Sun enters all 12 signs each year");
        series.Ingresses.Should().HaveCount(12);
    }

    [Fact]
    public void FindIngresses_SunAllSigns2026_ChronologicallyOrdered()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: false);

        for (int i = 1; i < series.Ingresses.Count; i++)
        {
            series.Ingresses[i].ExactUtc.Should().BeAfter(
                series.Ingresses[i - 1].ExactUtc,
                "ingresses should be chronologically ordered");
        }
    }

    [Fact]
    public void FindIngresses_SunAllSigns2026_EachHasCorrectElementAndModality()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: false);

        foreach (IngressResult ingress in series.Ingresses)
        {
            ingress.Element.Should().BeOneOf("Fire", "Earth", "Air", "Water",
                $"{ingress.Sign} should have a valid element");
            ingress.Modality.Should().BeOneOf("Cardinal", "Fixed", "Mutable",
                $"{ingress.Sign} should have a valid modality");
        }
    }

    [Fact]
    public void FindIngresses_SunAllSigns2026_NonCardinalIngressesHaveNoSeasonName()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: false);

        string[] cardinalSigns = ["Aries", "Cancer", "Libra", "Capricorn"];

        foreach (IngressResult ingress in series.Ingresses)
        {
            if (!cardinalSigns.Contains(ingress.Sign))
            {
                ingress.SeasonName.Should().BeNull(
                    $"{ingress.Sign} is not a cardinal sign and should have no season name");
                ingress.IsCardinalIngress.Should().BeFalse();
            }
        }
    }

    #endregion

    #region CalculateIngress — Single Chart Tests

    [Fact]
    public void CalculateIngress_AriesIngress2026_HasFullChart()
    {
        IngressResult result = _service.CalculateIngress(
            CelestialBodyId.Sun,
            ZodiacSign.Aries,
            2026,
            38.8977, -77.0365,
            "Washington, DC",
            "America/New_York");

        result.Chart.Should().NotBeNull("a full chart should be cast");
        result.Chart!.Input.EventType.Should().Be("Ingress");
        result.Chart.Positions.Should().NotBeEmpty();
        result.Chart.Aspects.Should().NotBeEmpty();
    }

    [Fact]
    public void CalculateIngress_AriesIngress2026_SunLongitudeNearZero()
    {
        IngressResult result = _service.CalculateIngress(
            CelestialBodyId.Sun,
            ZodiacSign.Aries,
            2026,
            38.8977, -77.0365,
            "Washington, DC",
            "America/New_York");

        result.ActualLongitude.Should().BeLessThan(0.001,
            "the Sun should be at ~0° Aries at the ingress moment");
    }

    [Fact]
    public void CalculateIngress_AriesIngress2026_HasSubArcSecondPrecision()
    {
        IngressResult result = _service.CalculateIngress(
            CelestialBodyId.Sun,
            ZodiacSign.Aries,
            2026,
            38.8977, -77.0365,
            "Washington, DC",
            "America/New_York");

        result.PrecisionArcSeconds.Should().BeLessThan(1.0);
    }

    [Fact]
    public void CalculateIngress_AriesIngress2026_HasCorrectMetadata()
    {
        IngressResult result = _service.CalculateIngress(
            CelestialBodyId.Sun,
            ZodiacSign.Aries,
            2026,
            38.8977, -77.0365,
            "Washington, DC",
            "America/New_York");

        result.Planet.Should().Be(CelestialBodyId.Sun);
        result.PlanetName.Should().Be("Sun");
        result.Sign.Should().Be("Aries");
        result.Element.Should().Be("Fire");
        result.Modality.Should().Be("Cardinal");
        result.IsCardinalIngress.Should().BeTrue();
        result.SeasonName.Should().Be("Vernal Equinox");
    }

    #endregion

    #region CalculateCardinalIngresses Tests

    [Fact]
    public void CalculateCardinalIngresses_2026_Returns4Results()
    {
        IReadOnlyList<IngressResult> results = _service.CalculateCardinalIngresses(
            2026, 38.8977, -77.0365, "Washington, DC", "America/New_York");

        results.Should().HaveCount(4);
    }

    [Fact]
    public void CalculateCardinalIngresses_2026_AllAreCardinal()
    {
        IReadOnlyList<IngressResult> results = _service.CalculateCardinalIngresses(
            2026, 38.8977, -77.0365, "Washington, DC", "America/New_York");

        foreach (IngressResult result in results)
        {
            result.IsCardinalIngress.Should().BeTrue();
            result.Chart.Should().NotBeNull(
                $"{result.Sign} cardinal ingress should have a full chart");
        }
    }

    [Fact]
    public void CalculateCardinalIngresses_2026_AllHaveValidCharts()
    {
        IReadOnlyList<IngressResult> results = _service.CalculateCardinalIngresses(
            2026, 38.8977, -77.0365, "Washington, DC", "America/New_York");

        foreach (IngressResult result in results)
        {
            result.Chart.Should().NotBeNull();
            result.Chart!.Positions.Should().NotBeEmpty();
            result.Chart.Houses.Should().NotBeNull();
            result.Chart.Aspects.Should().NotBeEmpty();
        }
    }

    #endregion

    #region Non-Solar Ingress Tests

    [Fact]
    public void FindIngresses_Jupiter2025To2026_FindsAtLeastOneIngress()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Jupiter, 2025, 2026, cardinalOnly: false);

        series.Count.Should().BeGreaterThanOrEqualTo(1,
            "Jupiter should enter at least one new sign in 2025-2026");
    }

    [Fact]
    public void FindIngresses_Saturn2025To2027_FindsAtLeastOneIngress()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Saturn, 2025, 2027, cardinalOnly: false);

        series.Count.Should().BeGreaterThanOrEqualTo(1,
            "Saturn should enter at least one new sign in a 3-year window");
    }

    #endregion

    #region Retrograde Ingress Tests

    [Fact]
    public void FindIngresses_UranusGemini2025To2026_MultipleCrossings()
    {
        // Uranus enters Gemini around 2025-2026 and may retrograde back to Taurus
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Uranus, 2025, 2026, cardinalOnly: false);

        // Filter to just Gemini ingresses (0° Gemini = 60°)
        List<IngressResult> geminiIngresses = series.Ingresses
            .Where(i => i.Sign == "Gemini")
            .ToList();

        // Uranus may cross Taurus/Gemini boundary multiple times due to retrograde
        // At minimum, it should cross at least once
        geminiIngresses.Count.Should().BeGreaterThanOrEqualTo(1,
            "Uranus should enter Gemini at least once in 2025-2026");

        // All crossings should be chronologically ordered
        for (int i = 1; i < geminiIngresses.Count; i++)
        {
            geminiIngresses[i].ExactUtc.Should().BeAfter(
                geminiIngresses[i - 1].ExactUtc);
        }
    }

    #endregion

    #region Guard Clause Tests

    [Fact]
    public void FindIngresses_StartAfterEnd_ThrowsArgumentException()
    {
        Action act = () => _service.FindIngresses(
            CelestialBodyId.Sun, 2025, 2020);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateIngress_EmptyLocationName_ThrowsArgumentException()
    {
        Action act = () => _service.CalculateIngress(
            CelestialBodyId.Sun, ZodiacSign.Aries, 2026,
            38.8977, -77.0365, "", "America/New_York");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateIngress_EmptyTimeZone_ThrowsArgumentException()
    {
        Action act = () => _service.CalculateIngress(
            CelestialBodyId.Sun, ZodiacSign.Aries, 2026,
            38.8977, -77.0365, "Washington, DC", "");

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void FindIngresses_EngineVersionNotEmpty()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        series.EngineVersion.Should().Contain("AstroCast");
    }

    [Fact]
    public void FindIngresses_PlanetNameMatchesPlanet()
    {
        IngressSeries series = _service.FindIngresses(
            CelestialBodyId.Sun, 2026, 2026, cardinalOnly: true);

        series.Planet.Should().Be(CelestialBodyId.Sun);
        series.PlanetName.Should().Be("Sun");
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="IngressService.FindIngresses"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before the sign-sweep begins.
    /// </summary>
    [Fact]
    public void FindIngresses_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _service.FindIngresses(
            CelestialBodyId.Sun, 2020, 2030, cardinalOnly: false,
            cancellationToken: cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the sign-sweep immediately");
    }

    #endregion
}
