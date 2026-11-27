using System.Globalization;
using System.Text.RegularExpressions;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Pins how the electional export prints a candidate's score (branch Q, 1.3.3): once and exactly. The candidate line,
/// the breakdown's weighted cells and its Overall row print to two decimals at most, so every print of one score
/// agrees and the cells add up to the Overall. The header states the score's ceiling for the type and names the
/// clock the export's times are on.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.3.3 the candidate line printed <c>F0</c> with "/100", the cells and the Overall <c>F1</c>, and .NET's
/// standard formats round an exact midpoint to even: Richmond's first candidate, exactly 82.25, printed "82/100",
/// "**82.2**" and cells adding up to 82.3 (the Q investigation: three prints, three numbers, in 529 of 1,855
/// reported windows). Every dimension score is a whole number and every weight a whole percent, so two decimals
/// print every cell and every score exactly. The expected values are Tier A arithmetic on the dimension scores the
/// investigation's harness reads at <c>e8ec6c5</c>.
/// </para>
/// <para>
/// Lines are compared without their terminators: the formatter writes <see cref="Environment.NewLine"/>, which
/// differs between Windows and macOS.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalScorePrintTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalScorePrintTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// The candidate line prints the score as it is, to two decimals at most and with no "/100": 82.25, 88.5 and 91,
    /// where it printed "82/100", "88/100" and "91/100".
    /// </summary>
    /// <param name="score">The candidate's score.</param>
    /// <param name="expected">The candidate line.</param>
    [Theory]
    [InlineData(82.25, "**Score:** 82.25")]
    [InlineData(88.5, "**Score:** 88.5")]
    [InlineData(91.0, "**Score:** 91")]
    public void ToMarkdown_CandidateLine_PrintsTheScoreExactly(double score, string expected)
    {
        ElectionalResult result = WithFirstCandidateScored(Richmond(), score);

        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(result));

        lines.Should().ContainSingle(l => l.StartsWith("**Score:**", StringComparison.Ordinal), "the result has one candidate")
            .Which.Should().Be(expected);
    }

    /// <summary>
    /// Richmond's first candidate (the golden's: Moon 90, Ascendant 50, Significator 85, Natal Harmony 50, Malefic
    /// Avoidance 100, Mercury 80, Planetary Hour 100, with Other's weights): every weighted cell prints exactly, the
    /// Overall prints 82.25, and the cells add up to it.
    /// </summary>
    [Fact]
    public void ToMarkdown_RichmondCandidate1_Breakdown_PrintsEachCellExactly_AndTheCellsAddUpToTheOverall()
    {
        ElectionalResult result = Richmond();
        ScoreBreakdown b = result.Candidates[0].ScoreBreakdown;
        new[] { b.MoonScore, b.AscendantScore, b.SignificatorScore, b.NatalHarmonyScore, b.MaleficAvoidanceScore, b.MercuryScore, b.PlanetaryHourScore }
            .Should().Equal(new double[] { 90.0, 50.0, 85.0, 50.0, 100.0, 80.0, 100.0 }, "the golden's first candidate, as the harness reads it at e8ec6c5");
        b.Weights.Should().Be(ElectionalWeights.GetWeights(ElectionType.Other));

        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(result));
        int header = lines.IndexOf("### Score Breakdown");
        header.Should().BeGreaterThanOrEqualTo(0, "the first candidate has a breakdown");
        List<string> rows = lines.Skip(header + 4).Take(8).ToList();

        rows.Should().Equal(
            "| Moon | 90 | 25% | 22.5 |",
            "| Ascendant | 50 | 10% | 5 |",
            "| Significator | 85 | 15% | 12.75 |",
            "| Natal Harmony | 50 | 10% | 5 |",
            "| Malefic Avoidance | 100 | 15% | 15 |",
            "| Mercury | 80 | 15% | 12 |",
            "| Planetary Hour | 100 | 10% | 10 |",
            "| **Overall** | | | **82.25** |");
        decimal cells = rows.Take(7).Sum(r => decimal.Parse(r.Split('|', StringSplitOptions.TrimEntries)[4], NumberStyles.Float, CultureInfo.InvariantCulture));
        cells.Should().Be(82.25m, "the cells add up to the Overall");
    }

    /// <summary>
    /// The header states the type's score ceiling, BusinessLaunch's 93.5 (Tier A), on the line directly after the
    /// Election Type line.
    /// </summary>
    [Fact]
    public void ToMarkdown_Header_StatesTheScoreCeiling_DirectlyAfterTheElectionType()
    {
        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(NoCandidates(0)));

        // the type prints by its display name since branch ExportText (1.7.2; finding N11), until then "BusinessLaunch"
        int electionType = lines.IndexOf("**Election Type:** Business Launch");
        electionType.Should().BeGreaterThanOrEqualTo(0, "every export states its election type");
        lines[electionType + 1].Should().Be("**Score ceiling:** 93.5 (each of the seven dimensions at its highest)");
    }

    /// <summary>
    /// A candidate's summary lines print display names, not the code's (branch ExportText, 1.7.2; finding N11):
    /// the Moon's phase in words ("Waning Crescent", not "WaningCrescent"), and the Ascendant ruler's house as
    /// the other exports write it ("in the 7th house", not "in House 7").
    /// </summary>
    [Fact]
    public void ToMarkdown_Candidate_PrintsDisplayNames_NotCodeNames()
    {
        ElectionalResult result = Richmond();
        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(result));

        List<string> moonLines = [.. lines.Where(l => l.StartsWith("**Moon:**", StringComparison.Ordinal))];
        moonLines.Should().HaveCount(result.Candidates.Count);
        for (int i = 0; i < moonLines.Count; i++)
        {
            MoonCondition moon = result.Candidates[i].Moon;
            moonLines[i].Should().StartWith($"**Moon:** {moon.Sign}, {moon.Phase.DisplayName} ({moon.PhasePosition}-phase), ");
        }

        moonLines.Should().OnlyContain(l => !Regex.IsMatch(l, "(Waxing|Waning)(Crescent|Gibbous)|NewMoon|FullMoon|(First|Last)Quarter"));
        lines.Where(l => l.StartsWith("**Ascendant:**", StringComparison.Ordinal))
            .Should().HaveCount(result.Candidates.Count)
            .And.OnlyContain(l => Regex.IsMatch(l, @", ruler \w+ in the \d+(st|nd|rd|th) house$"));
        string.Join('\n', lines).Should().NotContain(" in House ");
    }

    /// <summary>The Moon's phases by their display names, each two words (finding N11).</summary>
    /// <param name="phase">The phase.</param>
    /// <param name="expected">Its display name.</param>
    [Theory]
    [InlineData(MoonPhaseType.NewMoon, "New Moon")]
    [InlineData(MoonPhaseType.WaxingCrescent, "Waxing Crescent")]
    [InlineData(MoonPhaseType.FirstQuarter, "First Quarter")]
    [InlineData(MoonPhaseType.WaxingGibbous, "Waxing Gibbous")]
    [InlineData(MoonPhaseType.FullMoon, "Full Moon")]
    [InlineData(MoonPhaseType.WaningGibbous, "Waning Gibbous")]
    [InlineData(MoonPhaseType.LastQuarter, "Last Quarter")]
    [InlineData(MoonPhaseType.WaningCrescent, "Waning Crescent")]
    public void MoonPhase_DisplayName_IsInWords(MoonPhaseType phase, string expected)
    {
        phase.DisplayName.Should().Be(expected);
    }

    /// <summary>
    /// The header names the event's time zone, as entered, on the line directly after the Event Location line: the
    /// clock every candidate instant and period table is printed on.
    /// </summary>
    [Fact]
    public void ToMarkdown_Header_NamesTheEventTimeZone_DirectlyAfterTheEventLocation()
    {
        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(NoCandidates(0)));

        int location = lines.FindIndex(l => l.StartsWith("**Event Location:**", StringComparison.Ordinal));
        location.Should().BeGreaterThanOrEqualTo(0, "every export states its event location");
        lines[location + 1].Should().Be(
            "**Event Time Zone:** America/New_York (candidate instants and the period tables are on this clock)");
    }

    /// <summary>
    /// A count of one sample without a planetary hour reads in the singular on the header line after the Separation
    /// line. (A search finds a single one only with a narrow time limit on a polar day; the print is what is pinned.)
    /// </summary>
    [Fact]
    public void ToMarkdown_OneSampleWithoutHour_HeaderLineIsSingular()
    {
        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(NoCandidates(1)));

        int separation = lines.FindIndex(l => l.StartsWith("**Separation:**", StringComparison.Ordinal));
        separation.Should().BeGreaterThanOrEqualTo(0, "every export states its separation");
        lines[separation + 1].Should().Be(
            "**Samples without a planetary hour:** 1, not scored (the Sun does not rise or set there on that day)");
    }

    /// <summary>
    /// A count of one sample without a planetary hour reads in the singular in the "No Candidates Found" notice.
    /// </summary>
    [Fact]
    public void ToMarkdown_OneSampleWithoutHour_NoticeSentenceIsSingular()
    {
        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(NoCandidates(1)));

        lines.Should().Contain(
            "No viable election moments satisfied the configured constraints within 2026-04-20 to 2026-04-25. "
            + "Constraints applied: None. "
            + "1 eligible sample had no planetary hour, because the Sun does not rise or set there on that day, and "
            + "was not scored.");
    }

    /// <summary>
    /// None delivered of three requested: the shortfall says no moment qualified, since with nothing delivered the
    /// separation cannot be the cause, whatever emptied the range.
    /// </summary>
    [Fact]
    public void ToMarkdown_NothingDelivered_ShortfallSaysNoMomentQualified()
    {
        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(NoCandidates(0)));

        lines.Should().Contain("**Candidate instants:** 0 of 3 requested — no moment in the date range qualified");
    }

    /// <summary>
    /// The Richmond golden's search: the J2000 New York natal chart, type Other, Richmond, 11–14 June 2026, two
    /// candidates.
    /// </summary>
    /// <returns>The result.</returns>
    private ElectionalResult Richmond()
    {
        ElectionalService service = new(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture));

        return service.Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Best time to visit Doctor",
            RangeStart = new DateOnly(2026, 6, 11),
            RangeEnd = new DateOnly(2026, 6, 14),
            Latitude = 37.5385,
            Longitude = -77.4343,
            LocationName = "Richmond, VA",
            TimeZone = "America/New_York",
            CandidateCount = 2,
        });
    }

    /// <summary>
    /// The result with its first candidate alone, carrying the given score. Only the candidate line reads the score;
    /// the breakdown is the candidate's own.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="score">The score.</param>
    /// <returns>The result with one candidate.</returns>
    private static ElectionalResult WithFirstCandidateScored(ElectionalResult result, double score)
    {
        CandidateWindow first = result.Candidates[0];

        return new ElectionalResult
        {
            Input = result.Input,
            NatalChart = result.NatalChart,
            Candidates =
            [
                new CandidateWindow
                {
                    Rank = 1,
                    LocalDateTime = first.LocalDateTime,
                    Chart = first.Chart,
                    Moon = first.Moon,
                    PlanetaryHour = first.PlanetaryHour,
                    Score = score,
                    ScoreBreakdown = first.ScoreBreakdown,
                    NatalHarmonyAspects = first.NatalHarmonyAspects,
                    Strengths = first.Strengths,
                    Compromises = first.Compromises,
                },
            ],
            RequestedCandidateCount = 1,
            SamplesWithoutPlanetaryHour = result.SamplesWithoutPlanetaryHour,
            DisqualifiedPeriods = result.DisqualifiedPeriods,
            VoidOfCoursePeriods = result.VoidOfCoursePeriods,
            CalculatedAt = result.CalculatedAt,
        };
    }

    /// <summary>
    /// A BusinessLaunch search in New York, 20–25 April 2026, that delivered none of the three candidates asked for,
    /// with the given count of samples without a planetary hour.
    /// </summary>
    /// <param name="samplesWithoutPlanetaryHour">The count.</param>
    /// <returns>The result.</returns>
    private ElectionalResult NoCandidates(int samplesWithoutPlanetaryHour) => new()
    {
        Input = new ElectionalInput
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
    private static List<string> Lines(string markdown) => [.. markdown.Split('\n').Select(l => l.TrimEnd('\r', ' '))];
}
