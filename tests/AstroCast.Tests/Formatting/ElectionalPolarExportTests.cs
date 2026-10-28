using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models.Electional;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Pins how the electional export reports samples with no planetary hour (branch O3, 1.3.3): where the Sun does not
/// rise or set on a sample's day, the sample is not scored, and the export says how many there were.
/// </summary>
/// <remarks>
/// <para>
/// The header gains a line directly after the Separation line only when the count is above 0, so every export of a
/// search where the Sun rises and sets every day reads as it did before. A search that found no candidate says why
/// in its "No Candidates Found" notice.
/// </para>
/// <para>
/// Lines are compared without their terminators: the formatter writes <see cref="Environment.NewLine"/>, which
/// differs between Windows and macOS.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalPolarExportTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalPolarExportTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>A count above 0: the header states it on the line directly after the Separation line.</summary>
    [Fact]
    public void ToMarkdown_SamplesWithoutHour_HeaderStatesTheCountAfterTheSeparation()
    {
        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(Result(416)));

        int separation = lines.FindIndex(l => l.StartsWith("**Separation:**", StringComparison.Ordinal));
        separation.Should().BeGreaterThanOrEqualTo(0, "every export states its separation");
        lines[separation + 1].Should().Be(
            "**Samples without a planetary hour:** 416, not scored (the Sun does not rise or set there on those days)");
    }

    /// <summary>A count of 0: no such line, and the Generated line follows the Separation line as before.</summary>
    [Fact]
    public void ToMarkdown_NoSampleWithoutHour_HeaderHasNoSuchLine()
    {
        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(Result(0)));

        lines.Should().NotContain(l => l.StartsWith("**Samples without a planetary hour:**", StringComparison.Ordinal));
        int separation = lines.FindIndex(l => l.StartsWith("**Separation:**", StringComparison.Ordinal));
        lines[separation + 1].Should().StartWith("**Generated:**", "the header is as it was before 1.3.3");
    }

    /// <summary>
    /// No candidates and a count above 0: the "No Candidates Found" notice says, after its constraints sentence, how
    /// many eligible samples had no planetary hour and why.
    /// </summary>
    [Fact]
    public void ToMarkdown_NoCandidatesAndSamplesWithoutHour_NoticeSaysWhy()
    {
        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(Result(637)));

        lines.Should().Contain(
            "No viable election moments satisfied the configured constraints within 2026-06-18 to 2026-06-24. "
            + "Constraints applied: None. "
            + "637 eligible samples had no planetary hour, because the Sun does not rise or set there on those "
            + "days, and were not scored.");
    }

    /// <summary>
    /// A result with no candidates, three asked and none delivered at 66.3° N in midsummer, and the given count of
    /// samples without a planetary hour.
    /// </summary>
    /// <param name="samplesWithoutPlanetaryHour">The count.</param>
    /// <returns>The result.</returns>
    private ElectionalResult Result(int samplesWithoutPlanetaryHour) => new()
    {
        Input = new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Polar export",
            RangeStart = new DateOnly(2026, 6, 18),
            RangeEnd = new DateOnly(2026, 6, 24),
            Latitude = 66.3,
            Longitude = 25.0,
            LocationName = "66.3\u00b0 N",
            TimeZone = "Europe/Helsinki",
            CandidateCount = 3,
        },
        NatalChart = GoldenFixtures.J2000Nyc(_fixture),
        Candidates = [],
        RequestedCandidateCount = 3,
        SamplesWithoutPlanetaryHour = samplesWithoutPlanetaryHour,
        DisqualifiedPeriods = [],
        VoidOfCoursePeriods = [],
        CalculatedAt = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>The export's lines, without their terminators.</summary>
    /// <param name="markdown">The export.</param>
    /// <returns>Its lines.</returns>
    private static List<string> Lines(string markdown) => [.. markdown.Split('\n').Select(l => l.TrimEnd('\r'))];
}
