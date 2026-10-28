using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Pins the electional export's wording of a candidate (rulings 7 and 4, branch O2, 1.3.3): each candidate is a
/// "Candidate instant", the header counts "Candidate instants", and the line after it states the separation the
/// search kept between them.
/// </summary>
/// <remarks>
/// <para>
/// A candidate is one sampled instant on a 15- or 30-minute grid, scored there; its score can move several
/// points within its own step (the O investigation's Y6-2: 29 of 80 real windows by more than a point). The word
/// says so. The separation, six hours unless the search is told otherwise, was applied but never printed.
/// </para>
/// <para>
/// Lines are compared without their terminators: the formatter writes <see cref="Environment.NewLine"/>, which
/// differs between Windows and macOS.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalCandidateInstantWordingTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalCandidateInstantWordingTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>The NYC golden's search: its first candidate's heading is a candidate instant.</summary>
    [Fact]
    public void ToMarkdown_CandidateHeading_ReadsCandidateInstant()
    {
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(NycGoldenSearch());

        Lines(markdown).Should().Contain("## Candidate instant 1 — Thu, 23 Apr 2026 12:15 AM");
    }

    /// <summary>The NYC golden's search, three asked and three found: the header counts candidate instants.</summary>
    [Fact]
    public void ToMarkdown_MetRequest_HeaderCountsCandidateInstants()
    {
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(NycGoldenSearch());

        Lines(markdown).Should().Contain("**Candidate instants:** 3");
    }

    /// <summary>
    /// The separation line, directly after the count: "at least" the search's hours, "1 hour" in the singular,
    /// and "none" when the search keeps no separation.
    /// </summary>
    /// <param name="hours">The search's minimum separation, in hours.</param>
    /// <param name="expected">The line's value.</param>
    [Theory]
    [InlineData(6.0, "at least 6 hours between candidate instants")]
    [InlineData(4.5, "at least 4.5 hours between candidate instants")]
    [InlineData(1.0, "at least 1 hour between candidate instants")]
    [InlineData(0.0, "none")]
    public void ToMarkdown_Header_StatesTheSeparation(double hours, string expected)
    {
        ElectionalResult result = new()
        {
            Input = new ElectionalInput
            {
                NatalChart = GoldenFixtures.J2000Nyc(_fixture),
                ElectionType = ElectionType.Other,
                ElectionGoal = "Separation line",
                RangeStart = new DateOnly(2026, 6, 11),
                RangeEnd = new DateOnly(2026, 6, 14),
                Latitude = 37.5385,
                Longitude = -77.4343,
                LocationName = "Richmond, VA",
                TimeZone = "America/New_York",
                CandidateCount = 0,
                MinimumCandidateSeparationHours = hours,
            },
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            Candidates = [],
            RequestedCandidateCount = 0,
            SamplesWithoutPlanetaryHour = 0,
            DisqualifiedPeriods = [],
            VoidOfCoursePeriods = [],
            CalculatedAt = new DateTime(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc),
        };

        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(result));

        int count = lines.FindIndex(l => l.StartsWith("**Candidate instants:**", StringComparison.Ordinal));
        count.Should().BeGreaterThanOrEqualTo(0, "every export counts its candidate instants");
        lines[count + 1].Should().Be($"**Separation:** {expected}");
    }

    /// <summary>The NYC golden's search (BusinessLaunch, 20 to 25 April 2026, three candidates).</summary>
    /// <returns>Its result.</returns>
    private ElectionalResult NycGoldenSearch() =>
        new ElectionalService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture)).Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch a new software product.",
            RangeStart = new DateOnly(2026, 4, 20),
            RangeEnd = new DateOnly(2026, 4, 25),
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            TimeZone = "America/New_York",
            CandidateCount = 3,
        });

    /// <summary>The export's lines, without their terminators.</summary>
    /// <param name="markdown">The export.</param>
    /// <returns>Its lines.</returns>
    private static List<string> Lines(string markdown) => [.. markdown.Split('\n').Select(l => l.TrimEnd('\r'))];
}
