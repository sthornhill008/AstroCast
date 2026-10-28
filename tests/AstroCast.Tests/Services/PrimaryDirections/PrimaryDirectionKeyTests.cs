using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Services.PrimaryDirections;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services.PrimaryDirections;

/// <summary>
/// The keys and the dates of rule R6 (branch Primary, 1.5.0): Naibod, Ptolemy, the true solar arc in right
/// ascension, and the instant a direction's years denote.
/// </summary>
/// <param name="fixture">The shared ephemeris fixture.</param>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class PrimaryDirectionKeyTests(EphemerisFixture fixture)
{
    /// <summary>J2000.0: 2000-01-01 12:00 UT, the J2000 NYC birth instant.</summary>
    private const double J2000 = 2451545.0;

    /// <summary>Naibod: 25° is 25 × 365.2422 / 360 = 25.364042 years.</summary>
    [Fact]
    public void Naibod_IsTheArcOverTheSunsMeanMotion()
    {
        double? years = Service().YearsFor(25.0, PrimaryDirectionKey.Naibod, J2000, 100);

        years.Should().NotBeNull();
        years.Should().BeApproximately(25.364042, 1e-6);
    }

    /// <summary>Ptolemy: a degree a year, so 25° is 25 years.</summary>
    [Fact]
    public void Ptolemy_IsADegreeAYear()
    {
        double? years = Service().YearsFor(25.0, PrimaryDirectionKey.Ptolemy, J2000, 100);

        years.Should().Be(25.0);
    }

    /// <summary>
    /// The true solar arc in right ascension: from J2000 NYC the Sun's right ascension advances 25° in 23.1572 days,
    /// read as years (Tier B and Moira agree).
    /// </summary>
    [Fact]
    public void SolarArcInRightAscension_InvertsTheSunsRightAscension()
    {
        double? years = Service().YearsFor(25.0, PrimaryDirectionKey.SolarArcRightAscension, J2000, 100);

        years.Should().NotBeNull();
        years.Should().BeApproximately(23.1572, 1e-4);
    }

    /// <summary>The date of a direction is the birth instant plus its years at 365.2422 days a year: 16.2330 years from J2000 is 2016-03-26 UT.</summary>
    [Fact]
    public void JulianDayAt_IsTheBirthPlusTheYears()
    {
        double jd = PrimaryDirectionService.JulianDayAt(J2000, 16.2330);

        jd.Should().BeApproximately(J2000 + 16.2330 * 365.2422, 1e-9);
        DateOnly.FromDateTime(JulianDayUtility.JulianDayToInstant(jd)).Should().Be(new DateOnly(2016, 3, 26));
    }

    /// <summary>A service over the shared ephemeris.</summary>
    private PrimaryDirectionService Service() => new(fixture.EphemerisService);
}
