using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="AspectService"/> verifying aspect detection,
/// applying/separating determination, and correct pair iteration.
/// Uses J2000.0 epoch (JD 2451545.0) with New York City coordinates.
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class AspectServiceTests
{
    /// <summary>J2000.0 epoch — Julian Day 2451545.0 = 2000-Jan-1 12:00 UT.</summary>
    private const double J2000 = 2451545.0;

    /// <summary>New York City latitude (north positive).</summary>
    private const double NycLatitude = 40.7128;

    /// <summary>New York City longitude (east positive; west is negative).</summary>
    private const double NycLongitude = -74.006;

    private readonly EphemerisService _ephemerisService;
    private readonly AspectService _aspectService;

    /// <summary>
    /// Receives the shared ephemeris fixture and creates the aspect service.
    /// </summary>
    public AspectServiceTests(EphemerisFixture fixture)
    {
        _ephemerisService = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        _aspectService = new AspectService(_ephemerisService, logger);
    }

    #region Planet-to-Planet Aspects

    [Fact]
    public void CalculatePlanetAspects_Should_ReturnNonEmptyList_When_ValidPositions()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);

        // Act
        IReadOnlyList<AspectResult> aspects = _aspectService.CalculatePlanetAspects(positions, J2000);

        // Assert
        aspects.Should().NotBeEmpty(
            "with 12 bodies at J2000, there should be at least some aspects within orb");

        foreach (AspectResult aspect in aspects)
        {
            aspect.Orb.Should().BeGreaterThanOrEqualTo(0.0,
                "orb must be non-negative");
            aspect.Orb.Should().BeLessThanOrEqualTo(aspect.Aspect.Orb,
                $"orb {aspect.Orb:F2}° must not exceed the definition's maximum orb of {aspect.Aspect.Orb}°");
        }
    }

    [Fact]
    public void CalculatePlanetAspects_Should_NotContainSelfAspects_When_Calculated()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);

        // Act
        IReadOnlyList<AspectResult> aspects = _aspectService.CalculatePlanetAspects(positions, J2000);

        // Assert
        foreach (AspectResult aspect in aspects)
        {
            aspect.Body1Name.Should().NotBe(aspect.Body2Name,
                "no body should form an aspect with itself");
        }
    }

    [Fact]
    public void CalculatePlanetAspects_Should_NotContainDuplicatePairs_When_Calculated()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);

        // Act
        IReadOnlyList<AspectResult> aspects = _aspectService.CalculatePlanetAspects(positions, J2000);

        // Assert — check no two results share the same body pair (in either order)
        HashSet<string> seenPairs = [];

        foreach (AspectResult aspect in aspects)
        {
            // Normalise pair key so A-B and B-A are the same
            string key1 = $"{aspect.Body1Name}|{aspect.Body2Name}";
            string key2 = $"{aspect.Body2Name}|{aspect.Body1Name}";

            seenPairs.Should().NotContain(key1,
                $"duplicate aspect pair: {aspect.Body1Name} - {aspect.Body2Name}");
            seenPairs.Should().NotContain(key2,
                $"reversed duplicate aspect pair: {aspect.Body2Name} - {aspect.Body1Name}");

            seenPairs.Add(key1);
        }
    }

    [Fact]
    public void CalculatePlanetAspects_Should_HaveOrbWithinDefinitionMax_When_Calculated()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);

        // Act
        IReadOnlyList<AspectResult> aspects = _aspectService.CalculatePlanetAspects(positions, J2000);

        // Assert — every returned aspect must have orb <= its definition's maximum
        foreach (AspectResult aspect in aspects)
        {
            aspect.Orb.Should().BeLessThanOrEqualTo(aspect.Aspect.Orb,
                $"{aspect.Body1Name} {aspect.Aspect.Type} {aspect.Body2Name}: " +
                $"orb {aspect.Orb:F2}° exceeds max {aspect.Aspect.Orb}°");
        }
    }

    #endregion

    #region Planet-to-Angle Aspects

    [Fact]
    public void CalculateAngleAspects_Should_ReturnAspects_When_ValidInput()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData houses = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Act
        IReadOnlyList<AspectResult> aspects =
            _aspectService.CalculateAngleAspects(positions, houses, J2000);

        // Assert
        aspects.Should().NotBeEmpty(
            "with 12 bodies and 2 angle points, there should be some angle aspects");

        foreach (AspectResult aspect in aspects)
        {
            aspect.Body2Name.Should().BeOneOf(["ASC", "MC"],
                "angle aspects should only target ASC or MC");
        }
    }

    #endregion

    #region Combined Aspects

    [Fact]
    public void CalculateAllAspects_Should_CombineBothTypes_When_Calculated()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData houses = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Act
        IReadOnlyList<AspectResult> planetAspects =
            _aspectService.CalculatePlanetAspects(positions, J2000);
        IReadOnlyList<AspectResult> angleAspects =
            _aspectService.CalculateAngleAspects(positions, houses, J2000);
        IReadOnlyList<AspectResult> allAspects =
            _aspectService.CalculateAllAspects(positions, houses, J2000);

        // Assert
        allAspects.Should().HaveCount(planetAspects.Count + angleAspects.Count,
            "combined list should equal planet aspects + angle aspects");
    }

    [Fact]
    public void CalculateAllAspects_Should_HaveApplyingOrSeparating_When_Calculated()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData houses = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);

        // Act
        IReadOnlyList<AspectResult> allAspects =
            _aspectService.CalculateAllAspects(positions, houses, J2000);

        // Assert
        foreach (AspectResult aspect in allAspects)
        {
            aspect.ApplyingSeparating.Should().BeOneOf(["Ap", "Sp"],
                $"{aspect.Body1Name}-{aspect.Body2Name} should have a valid applying/separating indicator");
        }
    }

    #endregion

    #region Applying or Separating

    /// <summary>Five minutes, in days: the lead and lag either side of an exact aspect.</summary>
    private const double FiveMinutes = 5.0 / 1440.0;

    /// <summary>
    /// Every exact Sun–Moon major aspect of 2026: the aspect and the instant it perfects
    /// (Julian Day, UT). Typed from Tier B: the sign changes of the exactness function on
    /// direct Swiss Ephemeris calls, scanned every 15 minutes and refined by bisection.
    /// </summary>
    /// <remarks>
    /// The foundations investigation quoted 58 of these. Its scan had a 20-day horizon,
    /// shorter than the 29.5 days from one conjunction or opposition to the next and
    /// sometimes shorter than the gap between two sextiles or two trines, so it stopped
    /// early for those aspects. The full year is 99: 12 conjunctions, 13 oppositions,
    /// 24 sextiles, 25 squares and 25 trines.
    /// </remarks>
    public static TheoryData<AspectType, double> SunMoonExactAspects2026 => new()
    {
        { AspectType.Opposition, 2461043.9186899066 }, // 2026-01-03 10:02:54 UT
        { AspectType.Trine, 2461048.5445476864 }, // 2026-01-08 01:04:08 UT
        { AspectType.Square, 2461051.1586094061 }, // 2026-01-10 15:48:23 UT
        { AspectType.Sextile, 2461053.9086359851 }, // 2026-01-13 09:48:26 UT
        { AspectType.Conjunction, 2461059.3277686434 }, // 2026-01-18 19:51:59 UT
        { AspectType.Sextile, 2461064.3448229777 }, // 2026-01-23 20:16:32 UT
        { AspectType.Square, 2461066.6995808501 }, // 2026-01-26 04:47:23 UT
        { AspectType.Trine, 2461068.9628892643 }, // 2026-01-28 11:06:33 UT
        { AspectType.Opposition, 2461073.4230902027 }, // 2026-02-01 22:09:14 UT
        { AspectType.Trine, 2461078.3257413935 }, // 2026-02-06 19:49:04 UT
        { AspectType.Square, 2461081.0299391616 }, // 2026-02-09 12:43:06 UT
        { AspectType.Sextile, 2461083.7865789691 }, // 2026-02-12 06:52:40 UT
        { AspectType.Conjunction, 2461089.0008028811 }, // 2026-02-17 12:01:09 UT
        { AspectType.Sextile, 2461093.7364200335 }, // 2026-02-22 05:40:26 UT
        { AspectType.Square, 2461096.0191784678 }, // 2026-02-24 12:27:37 UT
        { AspectType.Trine, 2461098.2944083810 }, // 2026-02-26 19:03:56 UT
        { AspectType.Opposition, 2461102.9846532168 }, // 2026-03-03 11:37:54 UT
        { AspectType.Trine, 2461108.1538683642 }, // 2026-03-08 15:41:34 UT
        { AspectType.Square, 2461110.9017474838 }, // 2026-03-11 09:38:30 UT
        { AspectType.Sextile, 2461113.6121205371 }, // 2026-03-14 02:41:27 UT
        { AspectType.Conjunction, 2461118.5579749951 }, // 2026-03-19 01:23:29 UT
        { AspectType.Sextile, 2461123.0478216028 }, // 2026-03-23 13:08:51 UT
        { AspectType.Square, 2461125.3039703490 }, // 2026-03-25 19:17:43 UT
        { AspectType.Trine, 2461127.6362833083 }, // 2026-03-28 03:16:14 UT
        { AspectType.Opposition, 2461132.5916471379 }, // 2026-04-02 02:11:58 UT
        { AspectType.Trine, 2461137.9618629273 }, // 2026-04-07 11:05:04 UT
        { AspectType.Square, 2461140.7025393965 }, // 2026-04-10 04:51:39 UT
        { AspectType.Sextile, 2461143.3246018179 }, // 2026-04-12 19:47:25 UT
        { AspectType.Conjunction, 2461147.9943120480 }, // 2026-04-17 11:51:48 UT
        { AspectType.Sextile, 2461152.3266322240 }, // 2026-04-21 19:50:21 UT
        { AspectType.Square, 2461154.6053891582 }, // 2026-04-24 02:31:45 UT
        { AspectType.Trine, 2461157.0260239150 }, // 2026-04-26 12:37:28 UT
        { AspectType.Opposition, 2461162.2244331846 }, // 2026-05-01 17:23:11 UT
        { AspectType.Trine, 2461167.6975498227 }, // 2026-05-07 04:44:28 UT
        { AspectType.Square, 2461170.3822707525 }, // 2026-05-09 21:10:28 UT
        { AspectType.Sextile, 2461172.8909278456 }, // 2026-05-12 09:22:56 UT
        { AspectType.Conjunction, 2461177.3340639984 }, // 2026-05-16 20:01:03 UT
        { AspectType.Sextile, 2461181.6230162838 }, // 2026-05-21 02:57:08 UT
        { AspectType.Square, 2461183.9659436550 }, // 2026-05-23 11:10:57 UT
        { AspectType.Trine, 2461186.4871011879 }, // 2026-05-25 23:41:25 UT
        { AspectType.Opposition, 2461191.8647315810 }, // 2026-05-31 08:45:12 UT
        { AspectType.Trine, 2461197.3268808657 }, // 2026-06-05 19:50:42 UT
        { AspectType.Square, 2461199.9170346549 }, // 2026-06-08 10:00:31 UT
        { AspectType.Sextile, 2461202.3113188334 }, // 2026-06-10 19:28:17 UT
        { AspectType.Conjunction, 2461206.6209543059 }, // 2026-06-15 02:54:10 UT
        { AspectType.Sextile, 2461210.9794187285 }, // 2026-06-19 11:30:21 UT
        { AspectType.Square, 2461213.4134821119 }, // 2026-06-21 21:55:24 UT
        { AspectType.Trine, 2461216.0297457129 }, // 2026-06-24 12:42:50 UT
        { AspectType.Opposition, 2461221.4977029795 }, // 2026-06-29 23:56:41 UT
        { AspectType.Trine, 2461226.8342369050 }, // 2026-07-05 08:01:18 UT
        { AspectType.Square, 2461229.3118039854 }, // 2026-07-07 19:28:59 UT
        { AspectType.Sextile, 2461231.6158793624 }, // 2026-07-10 02:46:51 UT
        { AspectType.Conjunction, 2461235.9052925622 }, // 2026-07-14 09:43:37 UT
        { AspectType.Sextile, 2461240.4258588683 }, // 2026-07-18 22:13:14 UT
        { AspectType.Square, 2461242.9622272206 }, // 2026-07-21 11:05:36 UT
        { AspectType.Trine, 2461245.6550545553 }, // 2026-07-24 03:43:16 UT
        { AspectType.Opposition, 2461251.1081458023 }, // 2026-07-29 14:35:43 UT
        { AspectType.Trine, 2461256.2255448634 }, // 2026-08-03 17:24:47 UT
        { AspectType.Square, 2461258.5982600739 }, // 2026-08-06 02:21:29 UT
        { AspectType.Sextile, 2461260.8540188111 }, // 2026-08-08 08:29:47 UT
        { AspectType.Conjunction, 2461265.2338580964 }, // 2026-08-12 17:36:45 UT
        { AspectType.Sextile, 2461269.9795830548 }, // 2026-08-17 11:30:35 UT
        { AspectType.Square, 2461272.6155221136 }, // 2026-08-20 02:46:21 UT
        { AspectType.Trine, 2461275.3547717854 }, // 2026-08-22 20:30:52 UT
        { AspectType.Opposition, 2461280.6795417648 }, // 2026-08-28 04:18:32 UT
        { AspectType.Trine, 2461285.5299304370 }, // 2026-09-02 00:43:05 UT
        { AspectType.Square, 2461287.8272468038 }, // 2026-09-04 07:51:14 UT
        { AspectType.Sextile, 2461290.0831575403 }, // 2026-09-06 13:59:44 UT
        { AspectType.Conjunction, 2461294.6437533069 }, // 2026-09-11 03:27:00 UT
        { AspectType.Sextile, 2461299.6457666019 }, // 2026-09-16 03:29:54 UT
        { AspectType.Square, 2461302.3637416577 }, // 2026-09-18 20:43:47 UT
        { AspectType.Trine, 2461305.1051359493 }, // 2026-09-21 14:31:23 UT
        { AspectType.Opposition, 2461310.2007266385 }, // 2026-09-26 16:49:02 UT
        { AspectType.Trine, 2461314.7956419904 }, // 2026-10-01 07:05:43 UT
        { AspectType.Square, 2461317.0590737956 }, // 2026-10-03 13:25:03 UT
        { AspectType.Sextile, 2461319.3597481707 }, // 2026-10-05 20:38:02 UT
        { AspectType.Conjunction, 2461324.1597851394 }, // 2026-10-10 15:50:05 UT
        { AspectType.Sextile, 2461329.4136592010 }, // 2026-10-15 21:55:40 UT
        { AspectType.Square, 2461332.1754803471 }, // 2026-10-18 16:12:41 UT
        { AspectType.Trine, 2461334.8627683651 }, // 2026-10-21 08:42:23 UT
        { AspectType.Opposition, 2461339.6748744007 }, // 2026-10-26 04:11:49 UT
        { AspectType.Trine, 2461344.0801654980 }, // 2026-10-30 13:55:26 UT
        { AspectType.Square, 2461346.3530954169 }, // 2026-11-01 20:28:27 UT
        { AspectType.Sextile, 2461348.7317987615 }, // 2026-11-04 05:33:47 UT
        { AspectType.Conjunction, 2461353.7931399625 }, // 2026-11-09 07:02:07 UT
        { AspectType.Sextile, 2461359.2461687969 }, // 2026-11-14 17:54:28 UT
        { AspectType.Square, 2461361.9915511683 }, // 2026-11-17 11:47:50 UT
        { AspectType.Trine, 2461364.5737358760 }, // 2026-11-20 01:46:10 UT
        { AspectType.Opposition, 2461369.1205335176 }, // 2026-11-24 14:53:34 UT
        { AspectType.Trine, 2461373.4358986709 }, // 2026-11-28 22:27:41 UT
        { AspectType.Square, 2461375.7560214372 }, // 2026-12-01 06:08:40 UT
        { AspectType.Sextile, 2461378.2319647446 }, // 2026-12-03 17:34:01 UT
        { AspectType.Conjunction, 2461383.5360126141 }, // 2026-12-09 00:51:51 UT
        { AspectType.Sextile, 2461389.0740003102 }, // 2026-12-14 13:46:33 UT
        { AspectType.Square, 2461391.7379682474 }, // 2026-12-17 05:42:40 UT
        { AspectType.Trine, 2461394.1943610990 }, // 2026-12-19 16:39:52 UT
        { AspectType.Opposition, 2461398.5612788880 }, // 2026-12-24 01:28:14 UT
        { AspectType.Trine, 2461402.8948468892 }, // 2026-12-28 09:28:34 UT
        { AspectType.Square, 2461405.2913196096 }, // 2026-12-30 18:59:30 UT
    };

    /// <summary>
    /// Five minutes before an exact Sun–Moon aspect its orb is still closing, so it is
    /// applying.
    /// </summary>
    /// <param name="aspect">The aspect that perfects.</param>
    /// <param name="exactJulianDayUt">The instant it perfects (Julian Day, UT; Tier B).</param>
    /// <remarks>
    /// Until 1.3.3 the label compared the orb now with the orb 0.01 day (14.4 minutes)
    /// later. Inside the last 7.2 minutes the aspect perfects and passes within that
    /// step, the later orb is the larger, and the aspect was called separating.
    /// </remarks>
    [Theory]
    [MemberData(nameof(SunMoonExactAspects2026))]
    public void CalculatePlanetAspects_Should_LabelSunMoonApplying_When_FiveMinutesBeforeExact(
        AspectType aspect, double exactJulianDayUt)
    {
        double julianDayUt = exactJulianDayUt - FiveMinutes;
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(julianDayUt);

        IReadOnlyList<AspectResult> aspects = _aspectService.CalculatePlanetAspects(positions, julianDayUt);

        AspectResult? sunMoon = AspectService.FindAspectBetween(aspects, "Sun", "Moon", aspect);
        sunMoon.Should().NotBeNull($"the Sun and Moon are five minutes from an exact {aspect}");
        sunMoon!.IsApplying.Should().BeTrue(
            $"the {aspect} perfects five minutes later, so its orb ({sunMoon.Orb:F4}°) is closing");
    }

    /// <summary>
    /// Five minutes after an exact Sun–Moon aspect its orb is opening, so it is
    /// separating.
    /// </summary>
    /// <param name="aspect">The aspect that perfected.</param>
    /// <param name="exactJulianDayUt">The instant it perfected (Julian Day, UT; Tier B).</param>
    [Theory]
    [MemberData(nameof(SunMoonExactAspects2026))]
    public void CalculatePlanetAspects_Should_LabelSunMoonSeparating_When_FiveMinutesAfterExact(
        AspectType aspect, double exactJulianDayUt)
    {
        double julianDayUt = exactJulianDayUt + FiveMinutes;
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(julianDayUt);

        IReadOnlyList<AspectResult> aspects = _aspectService.CalculatePlanetAspects(positions, julianDayUt);

        AspectResult? sunMoon = AspectService.FindAspectBetween(aspects, "Sun", "Moon", aspect);
        sunMoon.Should().NotBeNull($"the Sun and Moon are five minutes past an exact {aspect}");
        sunMoon!.IsApplying.Should().BeFalse(
            $"the {aspect} perfected five minutes earlier, so its orb ({sunMoon.Orb:F4}°) is opening");
    }

    /// <summary>
    /// Saved chart 94 (Nancy health): its Sun sextile Moon is 0.0140° from exact and
    /// perfects 1.51 minutes after the chart's instant, so it is applying.
    /// </summary>
    /// <remarks>
    /// The instant is the saved input, 2026-09-06 09:58:14 in America/New_York (EDT,
    /// UTC−4), so 13:58:14 UT. Tier B (direct Swiss Ephemeris calls): orb
    /// 0.014030072° closing at 13.35°/day by a ±5-second symmetric difference; exact at
    /// 13:59:44.8 UT by bisection. The chart printed "Sp" until 1.3.3.
    /// </remarks>
    [Fact]
    public void CalculatePlanetAspects_Should_LabelSunSextileMoonApplying_When_Chart94Instant()
    {
        const double chart94JulianDayUt = 2461290.0821064813;
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(chart94JulianDayUt);

        IReadOnlyList<AspectResult> aspects = _aspectService.CalculatePlanetAspects(positions, chart94JulianDayUt);

        AspectResult? sextile = AspectService.FindAspectBetween(aspects, "Sun", "Moon", AspectType.Sextile);
        sextile.Should().NotBeNull("chart 94's Sun and Moon are 0.014° from an exact sextile");
        sextile!.Orb.Should().BeApproximately(0.014030072, 1e-8, "the orb Tier B measures");
        sextile.IsApplying.Should().BeTrue("the sextile perfects 1.51 minutes after the chart's instant");
    }

    /// <summary>
    /// When the two bodies' relative motion reverses inside the old 14.4-minute step,
    /// the label follows the orb's rate at the instant, not the orb a step later.
    /// </summary>
    /// <param name="julianDayUt">The instant (Julian Day, UT).</param>
    /// <param name="body1">The first body.</param>
    /// <param name="body2">The second body.</param>
    /// <param name="aspect">The aspect between them.</param>
    /// <param name="expectedApplying">Whether the orb is closing at the instant (Tier B).</param>
    /// <remarks>
    /// <para>
    /// Two of the eleven such cases in the foundations sweep, one each way, taken where
    /// the rate is largest so the sign is unambiguous. The instants are the sweep's grid,
    /// 1776-01-01 00:00 UT plus k × 10 days 1 hour. Tier B, by a ±5-second symmetric
    /// difference of direct Swiss Ephemeris longitudes, and the relative station by
    /// bisection on that rate:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// k = 9090, 2025-11-29 18:00 UT: Mercury trine Saturn, orb 4.45293°, opening at
    /// +1.72e-4°/day; the relative motion reverses 1.42 minutes later, so 14.4 minutes
    /// on the orb is smaller and the step called it applying.
    /// </description></item>
    /// <item><description>
    /// k = 2001, 1831-01-06 09:00 UT: True Node trine Chiron, orb 4.91855°, closing at
    /// −1.05e-4°/day; the reversal comes 5.17 minutes later, so the step called it
    /// separating.
    /// </description></item>
    /// </list>
    /// </remarks>
    [Theory]
    [InlineData(2461009.25, "Mercury", "Saturn", AspectType.Trine, false)]
    [InlineData(2389823.875, "True Node", "Chiron", AspectType.Trine, true)]
    public void CalculatePlanetAspects_Should_FollowOrbRateAtInstant_When_RelativeMotionReversesWithinFifteenMinutes(
        double julianDayUt, string body1, string body2, AspectType aspect, bool expectedApplying)
    {
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(julianDayUt);

        IReadOnlyList<AspectResult> aspects = _aspectService.CalculatePlanetAspects(positions, julianDayUt);

        AspectResult? result = AspectService.FindAspectBetween(aspects, body1, body2, aspect);
        result.Should().NotBeNull($"{body1} and {body2} are in {aspect} at the instant");
        result!.IsApplying.Should().Be(expectedApplying,
            "the label follows the sign of the orb's rate at the instant");
    }

    /// <summary>
    /// Ruling D5: a planet moving toward the Ascendant's degree is applying to it,
    /// whatever the angle's own motion.
    /// </summary>
    /// <remarks>
    /// The Ascendant is placed 1° ahead of the J2000 Sun, which moves forward at about
    /// 1°/day. A real Ascendant turns through the zodiac once a day, about 1° every four
    /// minutes, so a rule that let it move would leave the Sun behind and call this
    /// separating. The angle is held still: the Sun closes on its degree.
    /// </remarks>
    [Fact]
    public void CalculateAngleAspects_Should_LabelApplying_When_PlanetMovesTowardAscendantDegree()
    {
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData real = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);
        double sun = positions.Single(p => p.Name == "Sun").Longitude;
        HouseData houses = new()
        {
            Cusps = real.Cusps,
            Ascendant = (sun + 1.0) % 360.0,
            Midheaven = real.Midheaven,
            Armc = real.Armc,
            Vertex = real.Vertex,
        };

        IReadOnlyList<AspectResult> aspects = _aspectService.CalculateAngleAspects(positions, houses, J2000);

        AspectResult? conjunction = AspectService.FindAspectBetween(aspects, "Sun", "ASC", AspectType.Conjunction);
        conjunction.Should().NotBeNull("the Ascendant stands 1° ahead of the Sun");
        conjunction!.IsApplying.Should().BeTrue(
            "the Sun moves toward the Ascendant's degree, and the angle is held still (ruling D5)");
    }

    /// <summary>
    /// Ruling D5, the other way: a planet moving away from the Midheaven's degree is
    /// separating from it, whatever the angle's own motion.
    /// </summary>
    /// <remarks>
    /// The Midheaven is placed 1° behind the J2000 Sun. A moving Midheaven would close
    /// on the Sun at about 1° every four minutes; held still, the Sun draws away from
    /// its degree.
    /// </remarks>
    [Fact]
    public void CalculateAngleAspects_Should_LabelSeparating_When_PlanetMovesAwayFromMidheavenDegree()
    {
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData real = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);
        double sun = positions.Single(p => p.Name == "Sun").Longitude;
        HouseData houses = new()
        {
            Cusps = real.Cusps,
            Ascendant = real.Ascendant,
            Midheaven = (sun + 359.0) % 360.0,
            Armc = real.Armc,
            Vertex = real.Vertex,
        };

        IReadOnlyList<AspectResult> aspects = _aspectService.CalculateAngleAspects(positions, houses, J2000);

        AspectResult? conjunction = AspectService.FindAspectBetween(aspects, "Sun", "MC", AspectType.Conjunction);
        conjunction.Should().NotBeNull("the Midheaven stands 1° behind the Sun");
        conjunction!.IsApplying.Should().BeFalse(
            "the Sun moves away from the Midheaven's degree, and the angle is held still (ruling D5)");
    }

    #endregion

    #region Angular Separation

    [Fact]
    public void AngularSeparation_Should_HandleWraparound_When_CrossingZeroAries()
    {
        // Assert — 2° and 358° should give 4°, not 356°
        double separation = AspectService.AngularSeparation(2.0, 358.0);
        separation.Should().BeApproximately(4.0, 0.001,
            "shortest arc between 2° and 358° is 4° across 0° Aries");
    }

    [Fact]
    public void AngularSeparation_Should_ReturnZero_When_SameLongitude()
    {
        double separation = AspectService.AngularSeparation(100.0, 100.0);
        separation.Should().Be(0.0,
            "identical longitudes should have zero separation");
    }

    [Fact]
    public void AngularSeparation_Should_Return180_When_OppositeLongitudes()
    {
        double separation = AspectService.AngularSeparation(0.0, 180.0);
        separation.Should().Be(180.0,
            "diametrically opposite longitudes should have 180° separation");
    }

    #endregion

    #region Minor points (branch Asteroids, 1.6.0)

    /// <summary>A position built by hand, with latitude 0 and the speed given, for the minor-point aspect rules.</summary>
    private static PlanetPosition At(CelestialBodyId body, string name, double longitude, double speed) => new()
    {
        Body = body,
        Name = name,
        Longitude = longitude,
        Latitude = 0.0,
        Distance = 1.0,
        SpeedInLongitude = speed,
        SpeedInLatitude = 0.0,
        SpeedInDistance = 0.0,
    };

    /// <summary>Houses with the Ascendant and Midheaven where a test puts them, the cusps Equal from the Ascendant.</summary>
    private static HouseData HousesAt(double ascendant, double midheaven) => new()
    {
        Cusps = Enumerable.Range(0, 12).Select(i => (ascendant + 30.0 * i) % 360.0).ToList(),
        Ascendant = ascendant,
        Midheaven = midheaven,
        Armc = 0.0,
        Vertex = 0.0,
    };

    /// <summary>
    /// The orb is 2°, inclusive (R5): Ceres 62.0° from the Sun is a sextile at exactly 2°00′, 62.001° is no aspect.
    /// The angles stand clear of every aspect.
    /// </summary>
    /// <param name="sunLongitude">The Sun's longitude, Ceres being at 0°.</param>
    /// <param name="expected">Whether a sextile is found.</param>
    [Theory]
    [InlineData(62.0, true)]
    [InlineData(62.001, false)]
    public void CalculateMinorPointAspects_Orb2Inclusive(double sunLongitude, bool expected)
    {
        PlanetPosition[] minorPoints = [At(CelestialBodyId.Ceres, "Ceres", 0.0, 0.2)];
        PlanetPosition[] positions = [At(CelestialBodyId.Sun, "Sun", sunLongitude, 1.0)];

        IReadOnlyList<AspectResult> aspects =
            _aspectService.CalculateMinorPointAspects(minorPoints, positions, HousesAt(200.0, 110.0));

        if (expected)
        {
            AspectResult sextile = aspects.Should().ContainSingle("a separation of 62° is a sextile within the 2° orb").Which;
            sextile.Body1Name.Should().Be("Ceres");
            sextile.Body2Name.Should().Be("Sun");
            sextile.Aspect.Type.Should().Be(AspectType.Sextile);
            sextile.Orb.Should().BeApproximately(2.0, 1e-12, "the orb is the full 2°, inclusive");
        }
        else
        {
            aspects.Should().BeEmpty("a separation of 62.001° is outside the 2° orb");
        }
    }

    /// <summary>
    /// Never to the True Node, Chiron or another of the five, and never between the five (R5): two points in exact
    /// conjunction with the node, Chiron and a stray Vesta among the positions give no row; Mars at the same degree
    /// gives one row each. The Part of Fortune is a Lot, never a position, so it is excluded by construction.
    /// </summary>
    [Fact]
    public void CalculateMinorPointAspects_NeverToNodeChironFortuneOrEachOther()
    {
        PlanetPosition[] minorPoints =
        [
            At(CelestialBodyId.Ceres, "Ceres", 0.0, 0.2),
            At(CelestialBodyId.Pallas, "Pallas", 0.0, -0.1),
        ];
        List<PlanetPosition> positions =
        [
            At(CelestialBodyId.TrueNode, "True Node", 0.0, -0.05),
            At(CelestialBodyId.Chiron, "Chiron", 0.0, 0.03),
            At(CelestialBodyId.Vesta, "Vesta", 0.0, 0.4),
        ];
        HouseData houses = HousesAt(200.0, 110.0);

        _aspectService.CalculateMinorPointAspects(minorPoints, positions, houses)
            .Should().BeEmpty("the node, Chiron and the five are never targets, and the five never aspect each other");

        positions.Add(At(CelestialBodyId.Mars, "Mars", 0.0, 0.6));
        IReadOnlyList<AspectResult> aspects = _aspectService.CalculateMinorPointAspects(minorPoints, positions, houses);

        aspects.Should().HaveCount(2, "each of the two points conjoins Mars and nothing else");
        aspects.Select(a => a.Body2Name).Should().OnlyContain(name => name == "Mars");
        aspects.Select(a => a.Body1Name).Should().BeEquivalentTo(new[] { "Ceres", "Pallas" });
    }

    /// <summary>
    /// Applying by the speeds, the angle held still (R5, ruling D5): Ceres at 0.22° a day one degree behind the
    /// Ascendant's degree closes on it, applying; Vesta at the same speed one degree past it draws away, separating.
    /// </summary>
    [Fact]
    public void CalculateMinorPointAspects_AppliesByTheSpeeds()
    {
        HouseData houses = HousesAt(100.0, 300.0);
        PlanetPosition[] minorPoints =
        [
            At(CelestialBodyId.Ceres, "Ceres", 99.0, 0.22),
            At(CelestialBodyId.Vesta, "Vesta", 101.0, 0.22),
        ];

        IReadOnlyList<AspectResult> aspects = _aspectService.CalculateMinorPointAspects(minorPoints, [], houses);

        aspects.Should().HaveCount(2, "each point conjoins the Ascendant within 2°");
        AspectResult ceres = aspects.Single(a => a.Body1Name == "Ceres");
        ceres.Body2Name.Should().Be("ASC");
        ceres.Aspect.Type.Should().Be(AspectType.Conjunction);
        ceres.IsApplying.Should().BeTrue("Ceres moves toward the Ascendant's degree at 0.22° a day");
        AspectResult vesta = aspects.Single(a => a.Body1Name == "Vesta");
        vesta.Body2Name.Should().Be("ASC");
        vesta.IsApplying.Should().BeFalse("Vesta moves away from the Ascendant's degree");
    }

    #endregion
}
