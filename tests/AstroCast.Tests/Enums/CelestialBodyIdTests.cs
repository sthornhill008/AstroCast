using AstroCast.Core.Enums;

using FluentAssertions;

namespace AstroCast.Tests.Enums;

/// <summary>
/// The five minor points' members of <see cref="CelestialBodyId"/> (branch Asteroids, 1.6.0) carry the Swiss
/// Ephemeris body numbers, as every member of the enum does (swephexp.h): the mean apogee SE_MEAN_APOG 12, Ceres
/// SE_CERES 17, Pallas SE_PALLAS 18, Juno SE_JUNO 19 and Vesta SE_VESTA 20.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CelestialBodyIdTests
{
    /// <summary>
    /// Each new member's value is its Swiss Ephemeris number, and the enum holds the twelve bodies and the five.
    /// </summary>
    [Fact]
    public void FiveNewMembers_CarryTheSwissEphemerisNumbers()
    {
        ((int)CelestialBodyId.MeanApogee).Should().Be(12, "SE_MEAN_APOG is 12");
        ((int)CelestialBodyId.Ceres).Should().Be(17, "SE_CERES is 17");
        ((int)CelestialBodyId.Pallas).Should().Be(18, "SE_PALLAS is 18");
        ((int)CelestialBodyId.Juno).Should().Be(19, "SE_JUNO is 19");
        ((int)CelestialBodyId.Vesta).Should().Be(20, "SE_VESTA is 20");

        Enum.GetValues<CelestialBodyId>().Should().HaveCount(17, "the twelve bodies and the five minor points");
    }
}
