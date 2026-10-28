using AstroCast.Core.Models;
using AstroCast.Core.Services.PrimaryDirections;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services.PrimaryDirections;

/// <summary>
/// The speculum of rule R1 (branch Primary, 1.5.0): semi-arcs by hand, a circumpolar star, an angle as its ecliptic
/// degree, the chart's own Part of Fortune, and an antiscion unrounded.
/// </summary>
/// <param name="fixture">The shared ephemeris fixture.</param>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class SpeculumTests(EphemerisFixture fixture)
{
    /// <summary>
    /// J2000 NYC's Sun: declination −23.0324°, so at 40.7128° N its ascensional difference is
    /// asin(tan 40.7128° · tan −23.0324°) = −21.460°, its diurnal semi-arc 68.540°, and its hour angle from the RAMC
    /// 206.4511° less its right ascension 281.2784° is −74.827°.
    /// </summary>
    [Fact]
    public void J2000Sun_HasTheHandComputedSemiArcs()
    {
        Speculum speculum = J2000Speculum();

        SpeculumPoint sun = speculum.Significators.Single(s => s.Name == "Sun").Point;

        sun.Declination.Should().BeApproximately(-23.0324, 1e-3);
        sun.HasSemiArcs.Should().BeTrue();
        (sun.DiurnalSemiArc - 90.0).Should().BeApproximately(-21.460, 1e-3, "the ascensional difference is DSA − 90°");
        sun.DiurnalSemiArc.Should().BeApproximately(68.540, 1e-3);
        sun.HourAngle.Should().BeApproximately(-74.827, 1e-3);
        sun.IsUpper.Should().BeFalse("the Sun is below the horizon at 07:00 in New York on 1 January");
    }

    /// <summary>Polaris, declination +89.2670°, never rises or sets at New York: no semi-arcs.</summary>
    [Fact]
    public void Polaris_NeverRisesOrSets_HasNoSemiArcs()
    {
        Speculum speculum = J2000Speculum();

        SpeculumPoint polaris = speculum.Promissors.Single(p => p.Source == "Polaris").Point;

        polaris.Declination.Should().BeApproximately(89.2670, 1e-3);
        polaris.HasSemiArcs.Should().BeFalse();
        polaris.DiurnalSemiArc.Should().BeNull();
        polaris.NocturnalSemiArc.Should().BeNull();
        polaris.IsUpper.Should().BeTrue("a point that never sets is always above the horizon");
    }

    /// <summary>
    /// The Midheaven as a significator is its ecliptic degree at latitude 0: its right ascension is the RAMC and its
    /// declination that of 28°28′ Scorpio, −10.929525°.
    /// </summary>
    [Fact]
    public void Midheaven_AsSignificator_IsItsEclipticDegree()
    {
        Speculum speculum = J2000Speculum();

        SpeculumPoint midheaven = speculum.Significators.Single(s => s.Name == "Midheaven").Point;

        midheaven.EclipticLatitude.Should().Be(0.0);
        midheaven.RightAscension.Should().BeApproximately(speculum.Armc, 1e-6);
        midheaven.Declination.Should().BeApproximately(-10.929525, 1e-6);
        midheaven.HourAngle.Should().BeApproximately(0.0, 1e-6);
    }

    /// <summary>
    /// The speculum's Part of Fortune is the chart's own, from its Arabic Parts: Asc + Sun − Moon by night (J2000 NYC,
    /// 331.2872°) and Asc + Moon − Sun by day (person A, 116.7361°).
    /// </summary>
    /// <param name="chart">The fixture.</param>
    /// <param name="expected">The Part of Fortune's longitude.</param>
    /// <param name="isDayChart">Whether the chart is a day chart.</param>
    [Theory]
    [InlineData("J2000Nyc", 331.2872, false)]
    [InlineData("Hartwell", 116.7361, true)]
    public void PartOfFortune_IsTheChartsOwn(string chart, double expected, bool isDayChart)
    {
        ChartInput input = chart == "J2000Nyc" ? GoldenFixtures.J2000NycInput() : GoldenFixtures.HartwellInput();
        ChartResult natal = GoldenFixtures.CreateChartEngine(fixture).Calculate(input);
        double fromParts = natal.ArabicParts.Single(p => p.Name == "Part of Fortune").Longitude;

        Speculum speculum = Speculum.FromChart(natal, fixture.EphemerisService, planetsAsSignificators: false);

        speculum.IsDayChart.Should().Be(isDayChart);
        speculum.PartOfFortune.Should().Be(fromParts);
        speculum.PartOfFortune.Should().BeApproximately(expected, 1e-4);
        speculum.Significators.Single(s => s.Name == "Part of Fortune").Point.Longitude.Should().Be(fromParts);
    }

    /// <summary>The engine's antiscion is unrounded: 123.954012345° reflects to 56.045987655°, where the chart's antiscia table rounds to 0.0001°.</summary>
    [Fact]
    public void Antiscion_IsUnrounded()
    {
        Speculum.Antiscion(123.954012345).Should().BeApproximately(56.045987655, 1e-9);
        Speculum.ContraAntiscion(123.954012345).Should().BeApproximately(236.045987655, 1e-9);
    }

    /// <summary>J2000 NYC's speculum with the default significators.</summary>
    private Speculum J2000Speculum() =>
        Speculum.FromChart(GoldenFixtures.J2000Nyc(fixture), fixture.EphemerisService, planetsAsSignificators: false);
}
