using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="TransitIngressDetector"/> verifying sign ingress
/// detection for outer planets using the real Swiss Ephemeris.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitIngressDetectorTests
{
    private readonly TransitIngressDetector _detector;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the detector.
    /// </summary>
    public TransitIngressDetectorTests(EphemerisFixture fixture)
    {
        TransitEphemerisHelper helper = new(fixture.EphemerisService);
        _detector = new TransitIngressDetector(helper);
    }

    [Fact]
    public void FindIngresses_Saturn_PiscesToAries_2025_Should_Detect()
    {
        // Saturn enters Aries around late May 2025.
        // Scan April–July 2025 to capture the ingress.
        DateOnly start = new(2025, 4, 1);
        DateOnly end = new(2025, 7, 31);

        IReadOnlyList<TransitIngress> ingresses =
            _detector.FindIngresses(TransitBody.Saturn, start, end);

        ingresses.Should().NotBeEmpty(
            "Saturn should enter Aries from Pisces around May 2025");

        ingresses.Should().Contain(i => i.FromSign == "Pisces" && i.ToSign == "Aries",
            "the ingress should be from Pisces to Aries");
    }

    [Fact]
    public void FindIngresses_NoIngress_Should_ReturnEmpty()
    {
        // A short 14-day window — no outer planet sign change expected
        // Pluto moves ~2° per year, so won't change signs in 2 weeks
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 14);

        IReadOnlyList<TransitIngress> ingresses =
            _detector.FindIngresses(TransitBody.Pluto, start, end);

        ingresses.Should().BeEmpty(
            "no Pluto sign ingress should occur in a 14-day window");
    }

    /// <summary>
    /// T9 (R7). Neptune enters Aries on 2026-01-26 at 17:34:29 UT (Tier B): the ingress is dated by
    /// its instant, bisected to the second between the two 12:00 UT samples around it, and its Date
    /// is the UT date of that instant. Until branch D the Date was the first noon in the new sign,
    /// 2026-01-27, and no instant was kept.
    /// </summary>
    [Fact]
    public void FindIngresses_NeptuneIntoAries2026_IsDatedByItsInstant()
    {
        IReadOnlyList<TransitIngress> ingresses =
            _detector.FindIngresses(TransitBody.Neptune, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28));

        TransitIngress ingress = ingresses.Should().ContainSingle("Neptune changes sign once in the window").Subject;
        ingress.FromSign.Should().Be("Pisces");
        ingress.ToSign.Should().Be("Aries");
        ingress.Date.Should().Be(new DateOnly(2026, 1, 26));
        ingress.DateTimeUtc.Should().NotBeNull()
            .And.BeCloseTo(new DateTime(2026, 1, 26, 17, 34, 29, DateTimeKind.Utc), TimeSpan.FromSeconds(5));
    }
}
