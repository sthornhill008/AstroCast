using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the side rule for declination aspects (branch F7, card D13, 1.3.3): a parallel needs both bodies on the same
/// side of the celestial equator and declinations within 1° of each other, its orb |a − b|; a contraparallel needs
/// them on opposite sides and declinations within 1° of equal and opposite, its orb |a + b|. North is a declination
/// of 0 or more, south below 0, so a body at exactly 0° counts as north. Each pair forms at most one aspect, within
/// a chart (<see cref="DeclinationAspectService.Calculate"/>) and across two
/// (<see cref="DeclinationAspectService.FindCrossChartDeclinationAspects"/>).
/// </summary>
/// <remarks>
/// <para>
/// Until 1.3.3 the two tests were independent, so a pair within a degree of the equator passed both and was listed
/// twice, once as an aspect it is not (foundations F5-3: 37 pairs in 29 saved charts; synthesis Y1-3, M1, in the
/// cross-chart table). The expected values are Tier A, the rule's arithmetic, on Tier B declinations where a saved
/// chart's are used: chart 86's Sun and Neptune (foundations, example row 56) and the declinations of chart 63's
/// Saturn and chart 31's Mercury (synthesis, check-table row 8).
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class DeclinationSideTests
{
    private readonly DeclinationAspectService _service = new();

    /// <summary>
    /// Within a chart, each pair forms exactly one aspect, by side of the equator.
    /// </summary>
    /// <param name="sunDeclination">The Sun's declination, degrees.</param>
    /// <param name="neptuneDeclination">Neptune's declination, degrees.</param>
    /// <param name="isParallel">True for a parallel, false for a contraparallel.</param>
    /// <param name="orb">The orb, degrees.</param>
    [Theory]
    [InlineData(0.3, -0.4, false, 0.1)]
    [InlineData(0.3, 0.4, true, 0.1)]
    [InlineData(-0.3, -0.4, true, 0.1)]
    [InlineData(0.0, 0.5, true, 0.5)]
    [InlineData(0.0, -0.5, false, 0.5)]
    [InlineData(0.47876347706630973, -0.47495523609875867, false, 0.0038082409675510576)]
    public void Calculate_PairNearTheEquator_FormsOneAspectBySide(
        double sunDeclination, double neptuneDeclination, bool isParallel, double orb)
    {
        List<PlanetPosition> positions =
        [
            Position(CelestialBodyId.Sun, sunDeclination),
            Position(CelestialBodyId.Neptune, neptuneDeclination),
        ];

        IReadOnlyList<DeclinationAspect> aspects = _service.Calculate(positions);

        DeclinationAspect aspect = aspects.Should().ContainSingle().Subject;
        aspect.Body1Name.Should().Be("Sun");
        aspect.Body2Name.Should().Be("Neptune");
        aspect.IsParallel.Should().Be(isParallel);
        aspect.Orb.Should().BeApproximately(orb, 1e-9);
    }

    /// <summary>
    /// Across two charts, each pair forms exactly one aspect, by side of the equator: the first body in chart A, the
    /// second in chart B.
    /// </summary>
    /// <param name="declinationA">The body's declination in chart A, degrees.</param>
    /// <param name="declinationB">The body's declination in chart B, degrees.</param>
    /// <param name="isParallel">True for a parallel, false for a contraparallel.</param>
    /// <param name="orb">The orb, degrees.</param>
    [Theory]
    [InlineData(0.3, -0.4, false, 0.1)]
    [InlineData(0.3, 0.4, true, 0.1)]
    [InlineData(-0.3, -0.4, true, 0.1)]
    [InlineData(0.0, 0.5, true, 0.5)]
    [InlineData(0.0, -0.5, false, 0.5)]
    public void FindCrossChart_PairNearTheEquator_FormsOneAspectBySide(
        double declinationA, double declinationB, bool isParallel, double orb)
    {
        IReadOnlyList<DeclinationAspect> aspects = DeclinationAspectService.FindCrossChartDeclinationAspects(
            [Position(CelestialBodyId.Sun, declinationA)],
            [Position(CelestialBodyId.Neptune, declinationB)]);

        DeclinationAspect aspect = aspects.Should().ContainSingle().Subject;
        aspect.Body1Name.Should().Be("Sun");
        aspect.Body2Name.Should().Be("Neptune");
        aspect.IsParallel.Should().Be(isParallel);
        aspect.Orb.Should().BeApproximately(orb, 1e-9);
    }

    /// <summary>
    /// M1 (synthesis Y1-3): chart 63's Saturn at +0.2978° against chart 31's Mercury at −0.1655° is a contraparallel
    /// alone, orb 0.1323°. Until 1.3.3 a parallel, orb 0.4633°, was listed beside it.
    /// </summary>
    [Fact]
    public void FindCrossChart_M1_SaturnAndMercuryAcrossTheEquator_AreAContraparallelOnly()
    {
        IReadOnlyList<DeclinationAspect> aspects = DeclinationAspectService.FindCrossChartDeclinationAspects(
            [Position(CelestialBodyId.Saturn, 0.2978)],
            [Position(CelestialBodyId.Mercury, -0.1655)]);

        DeclinationAspect aspect = aspects.Should().ContainSingle().Subject;
        aspect.Body1Name.Should().Be("Saturn");
        aspect.Body2Name.Should().Be("Mercury");
        aspect.IsParallel.Should().BeFalse();
        aspect.Orb.Should().BeApproximately(0.1323, 1e-9);
    }

    /// <summary>A position with the given declination, at a longitude of 0°, its name the body's.</summary>
    /// <param name="body">The body.</param>
    /// <param name="declination">The declination, degrees.</param>
    /// <returns>The position.</returns>
    private static PlanetPosition Position(CelestialBodyId body, double declination) => new()
    {
        Body = body,
        Name = body.ToString(),
        Longitude = 0.0,
        Latitude = 0.0,
        Distance = 1.0,
        SpeedInLongitude = 1.0,
        SpeedInLatitude = 0.0,
        SpeedInDistance = 0.0,
        Declination = declination,
    };
}
