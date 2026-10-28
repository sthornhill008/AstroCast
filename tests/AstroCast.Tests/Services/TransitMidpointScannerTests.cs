using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="TransitMidpointScanner"/> verifying midpoint activation
/// detection on the 45° dial, lifecycle calculation, priority flagging, and
/// duration constraints with the tight 10' orb.
/// Uses the real Swiss Ephemeris via the shared test fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitMidpointScannerTests
{
    private readonly TransitMidpointScanner _scanner;
    private readonly TransitEphemerisHelper _helper;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the scanner.
    /// </summary>
    public TransitMidpointScannerTests(EphemerisFixture fixture)
    {
        _helper = new TransitEphemerisHelper(fixture.EphemerisService);
        _scanner = new TransitMidpointScanner(_helper);
    }

    [Fact]
    public void FindActivations_TransitOnMidpoint_Should_Detect()
    {
        // Get Saturn's position and compute its 45° dial position
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);
        double saturnDial45 = TransitMidpointScanner.ReduceToDial45(saturnLon);

        // Place a natal midpoint right at Saturn's 45° dial position
        List<NatalMidpointTarget> midpoints =
        [
            new NatalMidpointTarget
            {
                Body1 = "Sun",
                Body2 = "Moon",
                Dial45Position = saturnDial45,
                IsPriority = true,
                PriorityReason = "Sun/Moon midpoint",
            },
        ];

        IReadOnlyList<TransitMidpointActivation> activations =
            _scanner.FindActivations(TransitBody.Saturn, midpoints, start, end);

        activations.Should().NotBeEmpty(
            "Saturn should activate a midpoint placed at its own 45° dial position");
    }

    [Fact]
    public void FindActivations_TransitFarFromMidpoint_Should_ReturnEmpty()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);
        double saturnDial45 = TransitMidpointScanner.ReduceToDial45(saturnLon);

        // Place midpoint 20° away on the 45° dial — far outside the 10' orb
        double farPosition = (saturnDial45 + 20.0) % 45.0;

        List<NatalMidpointTarget> midpoints =
        [
            new NatalMidpointTarget
            {
                Body1 = "Mars",
                Body2 = "Jupiter",
                Dial45Position = farPosition,
                IsPriority = false,
            },
        ];

        IReadOnlyList<TransitMidpointActivation> activations =
            _scanner.FindActivations(TransitBody.Saturn, midpoints, start, end);

        activations.Should().BeEmpty(
            "no activation should be found when midpoint is 20° away on the 45° dial");
    }

    [Fact]
    public void FindActivations_PriorityMidpoint_Should_FlagPriority()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);
        double saturnDial45 = TransitMidpointScanner.ReduceToDial45(saturnLon);

        // Create a priority Sun/Moon midpoint at Saturn's position
        List<NatalMidpointTarget> midpoints =
        [
            new NatalMidpointTarget
            {
                Body1 = "Sun",
                Body2 = "Moon",
                Dial45Position = saturnDial45,
                IsPriority = true,
                PriorityReason = "Sun/Moon midpoint",
            },
        ];

        IReadOnlyList<TransitMidpointActivation> activations =
            _scanner.FindActivations(TransitBody.Saturn, midpoints, start, end);

        activations.Should().NotBeEmpty();

        TransitMidpointActivation activation = activations[0];
        activation.IsPriority.Should().BeTrue(
            "activation of a priority midpoint should be flagged as priority");
        activation.PriorityReason.Should().Be("Sun/Moon midpoint",
            "priority reason should be passed through from the natal midpoint");
    }

    [Fact]
    public void FindActivations_Lifecycle_Should_HaveEntryAndExit()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);
        double saturnDial45 = TransitMidpointScanner.ReduceToDial45(saturnLon);

        List<NatalMidpointTarget> midpoints =
        [
            new NatalMidpointTarget
            {
                Body1 = "Venus",
                Body2 = "Mars",
                Dial45Position = saturnDial45,
                IsPriority = false,
            },
        ];

        IReadOnlyList<TransitMidpointActivation> activations =
            _scanner.FindActivations(TransitBody.Saturn, midpoints, start, end);

        activations.Should().NotBeEmpty();

        TransitMidpointActivation activation = activations[0];
        activation.Lifecycle.Should().NotBeEmpty(
            "lifecycle should contain events");

        activation.Lifecycle.Should().Contain(
            e => e.EventType == TransitLifecycleEventType.Entry,
            "lifecycle should contain an Entry event");

        activation.Lifecycle.Should().Contain(
            e => e.EventType == TransitLifecycleEventType.Exit,
            "lifecycle should contain an Exit event");

        // Entry first, Exit last
        activation.Lifecycle[0].EventType.Should().Be(TransitLifecycleEventType.Entry);
        activation.Lifecycle[^1].EventType.Should().Be(TransitLifecycleEventType.Exit);
    }

    /// <summary>
    /// A natal midpoint 0.1° ahead of Saturn's 2026-03-01 12:00 UT dial position, report March 2026:
    /// the activation is one passage over that longitude, 1°53′ Aries, with Saturn's retrograde
    /// return (branch D, 1.3.4): in orb 2025-06-27 to 07-29 around its July 2025 station and again
    /// 2026-02-28 to 03-03, a gap of 214 days, with passes on 2025-07-05, 2025-07-20 and 2026-03-02,
    /// Tier B to the second. Until branch D an activation ended at the first 12:00 UT sample out of
    /// orb, so this one was the March period alone, 3 days (the test expected 1 to 30).
    /// </summary>
    [Fact]
    public void FindActivations_SaturnOverOneLongitude_IsOnePassageWithItsRetrogradeReturn()
    {
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        double jd = _helper.DateToJulianDay(start);
        double saturnLon = _helper.GetLongitude(TransitBody.Saturn, jd);
        double saturnDial45 = TransitMidpointScanner.ReduceToDial45(saturnLon);

        // Place midpoint slightly ahead of Saturn on the dial so it crosses during the period
        double targetDial45 = (saturnDial45 + 0.1) % 45.0;

        List<NatalMidpointTarget> midpoints =
        [
            new NatalMidpointTarget
            {
                Body1 = "Jupiter",
                Body2 = "Saturn",
                Dial45Position = targetDial45,
                IsPriority = false,
            },
        ];

        IReadOnlyList<TransitMidpointActivation> activations =
            _scanner.FindActivations(TransitBody.Saturn, midpoints, start, end);

        TransitMidpointActivation activation = activations.Should().ContainSingle(
            "Saturn is within 10′ of the longitude at the 12:00 UT samples of 1 to 3 March 2026").Subject;

        activation.Lifecycle.Should().HaveCount(5);
        AssertEvent(activation.Lifecycle[0], TransitLifecycleEventType.Entry, new DateTime(2025, 6, 27, 1, 29, 10, DateTimeKind.Utc));
        AssertEvent(activation.Lifecycle[1], TransitLifecycleEventType.Exact, new DateTime(2025, 7, 5, 9, 54, 49, DateTimeKind.Utc));
        AssertEvent(activation.Lifecycle[2], TransitLifecycleEventType.Exact, new DateTime(2025, 7, 20, 22, 20, 35, DateTimeKind.Utc));
        AssertEvent(activation.Lifecycle[3], TransitLifecycleEventType.Exact, new DateTime(2026, 3, 2, 8, 4, 11, DateTimeKind.Utc));
        AssertEvent(activation.Lifecycle[4], TransitLifecycleEventType.Exit, new DateTime(2026, 3, 3, 17, 23, 24, DateTimeKind.Utc));
        activation.Lifecycle[0].BeforeReportPeriod.Should().BeTrue();
        activation.Lifecycle[4].BeyondReportPeriod.Should().BeFalse();
        activation.Lifecycle.Where(e => e.EventType == TransitLifecycleEventType.Exact)
            .Select(e => e.PassNumber).Should().Equal(1, 2, 3);
    }

    /// <summary>
    /// T10 (R8). A natal midpoint at 25.909147133929366 on the 45° dial stands for the eight
    /// longitudes 25.909° + 45k; Jupiter passes the one at 115.909° (25°55′ Cancer) on 2026-06-10 at
    /// 11:50:00 UT, within the 10′ orb from 2026-06-09 15:41:03 to 2026-06-11 07:53:54 (Tier B), with
    /// the noon of 2026-06-10 its only in-orb 12:00 UT sample. The activation is one passage over that
    /// longitude, its entry and exit bisected to the second and its exact pass found by the half-day
    /// scan from the sample before the entry. Until branch D the entry and exit were that noon and
    /// the pass, which precedes it, was not found.
    /// </summary>
    [Fact]
    public void FindActivations_JupiterOverOneLongitudeOfTheDial_HasRefinedEntryExactAndExit()
    {
        List<NatalMidpointTarget> midpoints =
        [
            new NatalMidpointTarget
            {
                Body1 = "Sun",
                Body2 = "Moon",
                Dial45Position = 25.909147133929366,
                IsPriority = true,
                PriorityReason = "Sun/Moon midpoint",
            },
        ];

        IReadOnlyList<TransitMidpointActivation> activations =
            _scanner.FindActivations(TransitBody.Jupiter, midpoints, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        TransitMidpointActivation activation = activations.Should().ContainSingle(
            "Jupiter is within 10′ of one of the dial's eight longitudes at one noon of June 2026").Subject;

        activation.Lifecycle.Should().HaveCount(3);
        AssertEvent(activation.Lifecycle[0], TransitLifecycleEventType.Entry, new DateTime(2026, 6, 9, 15, 41, 3, DateTimeKind.Utc));
        AssertEvent(activation.Lifecycle[1], TransitLifecycleEventType.Exact, new DateTime(2026, 6, 10, 11, 50, 0, DateTimeKind.Utc));
        activation.Lifecycle[1].PassNumber.Should().Be(1);
        AssertEvent(activation.Lifecycle[2], TransitLifecycleEventType.Exit, new DateTime(2026, 6, 11, 7, 53, 54, DateTimeKind.Utc));
        activation.Lifecycle.Should().OnlyContain(e => e.TransitLongitude == null, "a midpoint lifecycle carries no longitude");
    }

    /// <summary>Asserts an event's type and its instant within five seconds of Tier B's.</summary>
    /// <param name="evt">The lifecycle event.</param>
    /// <param name="type">The expected type.</param>
    /// <param name="expectedUtc">The Tier B instant.</param>
    private static void AssertEvent(TransitLifecycleEvent evt, TransitLifecycleEventType type, DateTime expectedUtc)
    {
        evt.EventType.Should().Be(type);
        evt.Date.Should().BeCloseTo(expectedUtc, TimeSpan.FromSeconds(5), $"{type} is at {expectedUtc:yyyy-MM-dd HH:mm:ss} UT");
    }
}
