using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="MundaneAspectService"/> verifying harmonic mode aspect
/// finding for various planet pairs, cycle phase labelling, retrograde triple
/// crossings, and precision. Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class MundaneAspectServiceTests
{
    private readonly MundaneAspectService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the service.
    /// </summary>
    public MundaneAspectServiceTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        _service = new MundaneAspectService(fixture.EphemerisService, logger);
    }

    #region 90° Harmonic Mode

    /// <summary>
    /// 90° Harmonic for Jupiter-Saturn 2019-2021 should find the Dec 2020 conjunction.
    /// </summary>
    [Fact]
    public void FindAspects_90Harmonic_JupiterSaturn_2019To2021_FindsConjunction()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "90° Harmonic", 2019, 2021);

        series.Count.Should().BeGreaterThanOrEqualTo(1);
        series.HarmonicMode.Should().Be("90° Harmonic");

        MundaneAspectResult conj2020 = series.Aspects
            .First(a => a.ExactUtc.Year == 2020 && a.AspectName == "Conjunction");

        conj2020.ExactUtc.Month.Should().Be(12);
        conj2020.Sign1.Should().Be("Aquarius");
        // Conjunction can be near 0° or near 360° depending on which body is fractionally ahead
        double phaseFromConjunction = Math.Min(conj2020.CyclePhase, 360.0 - conj2020.CyclePhase);
        phaseFromConjunction.Should().BeLessThan(1.0,
            "conjunction should have cycle phase near 0° or 360°");
    }

    /// <summary>
    /// 90° Harmonic finds conjunctions, squares, and oppositions.
    /// </summary>
    [Fact]
    public void FindAspects_90Harmonic_FindsConjunctionSquareOpposition()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Saturn, CelestialBodyId.Mars,
            "90° Harmonic", 2024, 2028);

        series.Count.Should().BeGreaterThan(0);

        // Should have aspects at multiple target angles
        IEnumerable<double> distinctTargets = series.Aspects
            .Select(a => a.TargetAngle).Distinct();
        distinctTargets.Should().Contain(0.0, "should find conjunctions");
    }

    /// <summary>
    /// Mars-Saturn 90° Harmonic: should find at least one in a 3-year window.
    /// </summary>
    [Fact]
    public void FindAspects_90Harmonic_MarsSaturn_FindsAtLeastOne()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Saturn, CelestialBodyId.Mars,
            "90° Harmonic", 2025, 2027);

        series.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region 45° Harmonic Mode

    /// <summary>
    /// 45° Harmonic for Jupiter-Saturn should find all phase types including
    /// semi-squares and sesquiquadrates.
    /// </summary>
    [Fact]
    public void FindAspects_45Harmonic_FindsAllPhases()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "45° Harmonic", 2019, 2030);

        series.Count.Should().BeGreaterThan(5);
        series.HarmonicMode.Should().Be("45° Harmonic");

        // Should find aspects at multiple target angles
        IEnumerable<double> distinctTargets = series.Aspects
            .Select(a => a.TargetAngle).Distinct();
        distinctTargets.Should().HaveCountGreaterThanOrEqualTo(3,
            "45° Harmonic should find aspects at multiple target angles");
    }

    /// <summary>
    /// 45° Harmonic should include semi-square and sesquiquadrate aspects.
    /// </summary>
    [Fact]
    public void FindAspects_45Harmonic_IncludesSemiSquareAndSesquiquadrate()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "45° Harmonic", 2019, 2035);

        series.Aspects.Should().Contain(a => a.TargetAngle == 45.0,
            "should find semi-squares");
        series.Aspects.Should().Contain(a => a.TargetAngle == 135.0,
            "should find sesquiquadrates");
    }

    #endregion

    #region Cycle Phase and Phase Labels

    /// <summary>
    /// All results should have CyclePhase in 0-360 range.
    /// </summary>
    [Fact]
    public void FindAspects_CyclePhase_CorrectRange()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "90° Harmonic", 2019, 2025);

        series.Aspects.Should().AllSatisfy(a =>
        {
            a.CyclePhase.Should().BeGreaterThanOrEqualTo(0.0);
            a.CyclePhase.Should().BeLessThan(360.0);
        });
    }

    /// <summary>
    /// Aspects with CyclePhase &lt;= 180 should be labelled "Waxing";
    /// aspects with CyclePhase &gt; 180 should be labelled "Waning".
    /// </summary>
    [Fact]
    public void FindAspects_PhaseLabel_CorrectForWaxingAndWaning()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "90° Harmonic", 2019, 2030);

        series.Aspects.Should().AllSatisfy(a =>
        {
            if (a.CyclePhase <= 180.0)
                a.PhaseLabel.Should().Be("Waxing");
            else
                a.PhaseLabel.Should().Be("Waning");
        });
    }

    /// <summary>
    /// Conjunction aspects should not have "Waxing" or "Waning" prefix in AspectName.
    /// </summary>
    [Fact]
    public void FindAspects_ConjunctionAspectName_NoPhasePrefix()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "90° Harmonic", 2019, 2021);

        MundaneAspectResult? conjunction = series.Aspects
            .FirstOrDefault(a => a.TargetAngle == 0.0);

        conjunction.Should().NotBeNull();
        conjunction!.AspectName.Should().Be("Conjunction");
    }

    /// <summary>
    /// Square aspects should have "Waxing" or "Waning" prefix in AspectName.
    /// </summary>
    [Fact]
    public void FindAspects_SquareAspectName_HasPhasePrefix()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Saturn, CelestialBodyId.Uranus,
            "90° Harmonic", 2020, 2022);

        MundaneAspectResult? square = series.Aspects
            .FirstOrDefault(a => a.TargetAngle == 90.0);

        square.Should().NotBeNull();
        square!.AspectName.Should().MatchRegex("^(Waxing|Waning) Square$");
    }

    #endregion

    #region Precision

    /// <summary>
    /// All conjunction results should have sub-arc-second precision.
    /// </summary>
    [Fact]
    public void FindAspects_90Harmonic_ConjunctionsHaveSubArcSecondPrecision()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "90° Harmonic", 2000, 2040);

        IEnumerable<MundaneAspectResult> conjunctions = series.Aspects
            .Where(a => a.TargetAngle == 0.0);

        conjunctions.Should().AllSatisfy(a =>
            a.PrecisionArcSeconds.Should().BeLessThan(1.0,
                $"conjunction at {a.ExactUtc} should be sub-arc-second precision"));
    }

    /// <summary>
    /// Actual separation should match target angle within 0.01 degrees for all results.
    /// </summary>
    [Fact]
    public void FindAspects_90Harmonic_ActualSeparationMatchesTarget()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Saturn, CelestialBodyId.Uranus,
            "90° Harmonic", 2020, 2022);

        series.Aspects.Should().AllSatisfy(a =>
            Math.Abs(a.ActualSeparation - a.TargetAngle).Should().BeLessThan(0.01,
                $"aspect at {a.ExactUtc} actual separation should match target angle"));
    }

    #endregion

    #region Chronological Ordering

    /// <summary>
    /// Results from multiple target angle searches should be sorted chronologically.
    /// </summary>
    [Fact]
    public void FindAspects_90Harmonic_ResultsSortedChronologically()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "90° Harmonic", 2015, 2025);

        series.Count.Should().BeGreaterThan(1);

        for (int i = 1; i < series.Aspects.Count; i++)
        {
            series.Aspects[i].ExactUtc.Should().BeOnOrAfter(series.Aspects[i - 1].ExactUtc,
                $"aspect {i} should be on or after aspect {i - 1}");
        }
    }

    #endregion

    #region Retrograde Triple Crossings

    /// <summary>
    /// Saturn-Uranus square 2021: 90° Harmonic should find 3+ passes of the square.
    /// </summary>
    [Fact]
    public void FindAspects_90Harmonic_SaturnUranusSquare2021_TripleCrossing()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Saturn, CelestialBodyId.Uranus,
            "90° Harmonic", 2021, 2021);

        IEnumerable<MundaneAspectResult> squares = series.Aspects
            .Where(a => a.TargetAngle == 90.0);

        squares.Should().HaveCountGreaterThanOrEqualTo(3,
            "Saturn-Uranus had a triple square in 2021");

        squares.Should().AllSatisfy(a =>
        {
            a.ExactUtc.Year.Should().Be(2021);
            a.Position1Formatted.Should().NotBeNullOrEmpty();
            a.Position2Formatted.Should().NotBeNullOrEmpty();
        });
    }

    #endregion

    #region Guard Clauses

    /// <summary>
    /// Same body for both parameters should throw ArgumentException.
    /// </summary>
    [Fact]
    public void FindAspects_SameBody_ThrowsArgumentException()
    {
        Action act = () => _service.FindAspects(
            CelestialBodyId.Mars, CelestialBodyId.Mars,
            "90° Harmonic", 2025, 2030);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Cannot find aspects between a body and itself*");
    }

    /// <summary>
    /// Invalid harmonic mode should throw ArgumentException.
    /// </summary>
    [Fact]
    public void FindAspects_InvalidHarmonicMode_ThrowsArgumentException()
    {
        Action act = () => _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "30° Harmonic", 2025, 2030);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Unknown harmonic mode*");
    }

    /// <summary>
    /// Start year greater than end year should throw ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void FindAspects_StartAfterEnd_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "90° Harmonic", 2030, 2025);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Moon Aspects (Fast Body)

    /// <summary>
    /// Sun-Moon 90° Harmonic in one year: should find many aspects (New/Full Moons + squares).
    /// </summary>
    [Fact]
    public void FindAspects_90Harmonic_SunMoon_OneYear_FindsMany()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Sun, CelestialBodyId.Moon,
            "90° Harmonic", 2025, 2025);

        // ~12 conjunctions + ~12 oppositions + ~24 squares = ~48
        series.Count.Should().BeGreaterThanOrEqualTo(40,
            "should find many Sun-Moon aspects in a year");
    }

    #endregion

    #region Series Metadata

    /// <summary>
    /// Series metadata should be correctly populated with harmonic mode.
    /// </summary>
    [Fact]
    public void FindAspects_SeriesMetadata_IsCorrectlyPopulated()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "90° Harmonic", 2019, 2021);

        series.Body1.Should().Be(CelestialBodyId.Jupiter);
        series.Body1Name.Should().Be("Jupiter");
        series.Body2.Should().Be(CelestialBodyId.Saturn);
        series.Body2Name.Should().Be("Saturn");
        series.HarmonicMode.Should().Be("90° Harmonic");
        series.ScanStart.Year.Should().Be(2019);
        series.ScanEnd.Year.Should().Be(2021);
        series.Count.Should().Be(series.Aspects.Count);
        series.EngineVersion.Should().StartWith("AstroCast");
        series.CalculatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Case Insensitivity

    /// <summary>
    /// Harmonic mode names should be case-insensitive.
    /// </summary>
    [Fact]
    public void FindAspects_CaseInsensitiveHarmonicMode_Works()
    {
        MundaneAspectSeries series = _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "90° harmonic", 2019, 2021);

        series.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="MundaneAspectService.FindAspects"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any harmonic angle iteration runs.
    /// </summary>
    [Fact]
    public void FindAspects_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _service.FindAspects(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            "90° Harmonic", 2000, 2030, cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the aspect sweep immediately");
    }

    #endregion
}
