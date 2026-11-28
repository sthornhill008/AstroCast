using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.MedicalAstrology;
using AstroCast.Core.Models;
using FluentAssertions;
using Xunit;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for the <see cref="MedicalMarkdownFormatter"/> static formatter.
/// </summary>
public sealed class MedicalMarkdownFormatterTests
{
    /// <summary>
    /// Formatter produces non-null, non-empty output.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ValidInputs_ReturnsNonEmptyString()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        string result = MedicalMarkdownFormatter.Format(chart, profile);

        result.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Output contains the subject name.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ValidInputs_ContainsSubjectName()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        string result = MedicalMarkdownFormatter.Format(chart, profile);

        result.Should().Contain("Test Subject");
    }

    /// <summary>
    /// Output contains the full natal chart section (from ChartMarkdownFormatter).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ValidInputs_ContainsNatalChartSection()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        string result = MedicalMarkdownFormatter.Format(chart, profile);

        result.Should().Contain("## Natal Chart");
        result.Should().Contain("Planetary Positions");
    }

    /// <summary>
    /// Output contains temperament assessment section with scores table.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ValidInputs_ContainsTemperamentSection()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        string result = MedicalMarkdownFormatter.Format(chart, profile);

        result.Should().Contain("## Temperament Assessment");
        result.Should().Contain("Elemental Scores");
        result.Should().Contain("Choleric");
        result.Should().Contain("Hot & Dry");
    }

    /// <summary>
    /// A profile scoring Fire 2, Earth 3, Air 5, Water 5 (hand-set) prints its tie for first as a tie, and the
    /// secondary as the temperament below it (Y5-1, ruling S-D9; the texts counted by hand from the scores). Until
    /// 1.3.3 the assessment printed "Sanguine" and "Phlegmatic".
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_DominantTie_PrintsTheTie()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();
        HealthProfile tied = new()
        {
            SubjectName = profile.SubjectName,
            Temperament = new TemperamentProfile
            {
                FireScore = 2,
                EarthScore = 3,
                AirScore = 5,
                WaterScore = 5,
                DominantTemperament = Temperament.Sanguine,
                SecondaryTemperament = Temperament.Phlegmatic
            },
            SignEmphases = profile.SignEmphases,
            MedicalAspects = profile.MedicalAspects,
            SixthHouseAnalysis = profile.SixthHouseAnalysis,
            SixthHouseSign = profile.SixthHouseSign,
            PlanetsInSixth = profile.PlanetsInSixth,
            EighthHouseAnalysis = profile.EighthHouseAnalysis,
            TwelfthHouseAnalysis = profile.TwelfthHouseAnalysis
        };

        List<string> lines = Lines(MedicalMarkdownFormatter.Format(chart, tied));

        lines.Should().Contain("**Dominant Temperament:** Sanguine and Phlegmatic (tied, 5 each)  ");
        lines.Should().Contain("**Secondary Temperament:** Melancholic  ");
    }

    /// <summary>
    /// The Temperament Assessment's scoring line names the scheme as AstroCast's own and says which bodies count as
    /// angular: each of the twelve in houses 1, 4, 7 or 10 (ruling S-D9; Y5-2, Y5-3).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ScoringLine_NamesTheAppsOwnScheme()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        List<string> lines = Lines(MedicalMarkdownFormatter.Format(chart, profile));

        lines.Should().Contain(
            "Scoring (AstroCast's own scheme): Ascendant sign = 3 pts, Ascendant ruler sign = 2 pts, Moon sign = 3 pts, Moon dispositor sign = 2 pts, Sun sign = 1 pt, each body in houses 1, 4, 7 or 10 = 1 pt.");
    }

    /// <summary>
    /// The medical export prints its correspondences once (branch ExportText, 1.7.2; finding N14): the natal chart
    /// it embeds leaves its own Medical Astrology Correspondences section out, and the line that says the tables
    /// are not medical advice, which stood only in that section, is printed once, in the export's header: a
    /// paragraph of its own directly under the Tradition line, before the natal chart, where a reader meets it
    /// first (branch DirectionNotes, 1.7.2; Steve's ruling of 2026-11-27). ExportText had put it directly before
    /// the Temperament Assessment, after the embedded chart. Until 1.7.2 the temperament, the planetary
    /// correspondences, the zodiacal anatomy and the health houses printed twice, in two wordings.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_PrintsItsCorrespondencesOnce_WithTheNotMedicalAdviceLineInTheHeader()
    {
        const string basis = "*Traditional correspondences for educational and historical study. Not medical advice.*";
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        List<string> lines = Lines(MedicalMarkdownFormatter.Format(chart, profile));

        lines.Should().NotContain("## Medical Astrology Correspondences");
        lines.Count(l => l == basis).Should().Be(1);
        int tradition = lines.FindIndex(l => l.StartsWith("**Tradition:**", StringComparison.Ordinal));
        lines[tradition + 1].Should().BeEmpty();
        lines[tradition + 2].Should().Be(basis);
        lines[tradition + 3].Should().BeEmpty();
        lines[tradition + 4].Should().Be("## Natal Chart");
        int temperament = lines.IndexOf("## Temperament Assessment");
        temperament.Should().BeGreaterThan(tradition + 4);
        lines.Skip(tradition + 4).Should().NotContain(basis, "the line is in the header, not before the Temperament Assessment");
        lines[temperament - 1].Should().BeEmpty();
        lines.Count(l => l.StartsWith("| Element | Temperament | Qualities | Score |", StringComparison.Ordinal)).Should().Be(1);
        lines.Count(l => l.StartsWith("## Zodiacal Anatomy", StringComparison.Ordinal)).Should().Be(1);
        lines.Should().NotContain("### Zodiacal Anatomy", "that is the sub-heading of the chart's own section");

        // the chart's own export keeps its section, with the line
        ChartMarkdownFormatter.ToMarkdown(chart).Should().Contain("## Medical Astrology Correspondences").And.Contain(basis);
    }

    /// <summary>
    /// Output contains zodiacal anatomy section with body region tables.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ValidInputs_ContainsZodiacalAnatomySection()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        string result = MedicalMarkdownFormatter.Format(chart, profile);

        result.Should().Contain("## Zodiacal Anatomy");
        result.Should().Contain("Body Region");
        result.Should().Contain("Traditional Correspondence");
    }

    /// <summary>
    /// Output contains planetary body correspondences table.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ValidInputs_ContainsPlanetaryCorrespondencesTable()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        string result = MedicalMarkdownFormatter.Format(chart, profile);

        result.Should().Contain("## Planetary Body Correspondences");
        result.Should().Contain("Health Role");
    }

    /// <summary>
    /// Output contains health houses table.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ValidInputs_ContainsHealthHousesTable()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        string result = MedicalMarkdownFormatter.Format(chart, profile);

        result.Should().Contain("## Health Houses");
        result.Should().Contain("6th");
        result.Should().Contain("8th");
        result.Should().Contain("12th");
    }

    /// <summary>
    /// Output contains hard aspects table with body region columns.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ValidInputs_ContainsHardAspectsTable()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        string result = MedicalMarkdownFormatter.Format(chart, profile);

        result.Should().Contain("## Hard Aspects");
        result.Should().Contain("Body 1 Regions");
        result.Should().Contain("Body 2 Regions");
    }

    /// <summary>
    /// A hard-aspect row prints its orb as truncated D°MM′ through one function (branch O-D3, 1.3.4):
    /// 2.5° is 2°30′. Until 1.3.4 the row printed the orb to two decimals, "| 2.50 |".
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_HardAspectRow_PrintsTheOrbAsTruncatedDegreesAndMinutes()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        string result = MedicalMarkdownFormatter.Format(chart, profile);

        result.Should().Contain("| Sun Square Saturn | Square | 2\u00b030\u2032 | Ap | High |");
    }

    /// <summary>
    /// Output contains the Generated by footer stamp.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ValidInputs_ContainsGeneratedByStamp()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        string result = MedicalMarkdownFormatter.Format(chart, profile);

        result.Should().Contain("*Generated by");
    }

    /// <summary>
    /// Null ChartResult throws ArgumentNullException.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_NullChartResult_ThrowsArgumentNullException()
    {
        (_, HealthProfile profile) = CreateTestData();

        Action act = () => MedicalMarkdownFormatter.Format(null!, profile);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Null HealthProfile throws ArgumentNullException.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_NullProfile_ThrowsArgumentNullException()
    {
        (ChartResult chart, _) = CreateTestData();

        Action act = () => MedicalMarkdownFormatter.Format(chart, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// A health context of " Recovering from knee surgery\nwalks daily " is trimmed and its line break becomes a
    /// space: the whole line <c>**Health Context:** Recovering from knee surgery walks daily</c>, with two trailing
    /// spaces, follows the Tradition line (O-D8 (b), E3-3). Until 1.3.3 the formatter took no context, though the
    /// panel promised it in the report header.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_WithHealthContext_PrintsItInTheHeader()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        List<string> lines = Lines(MedicalMarkdownFormatter.Format(
            chart, profile, healthContext: " Recovering from knee surgery\nwalks daily "));

        int tradition = lines.FindIndex(l => l.StartsWith("**Tradition:**", StringComparison.Ordinal));
        tradition.Should().BePositive();
        lines[tradition + 1].Should().Be("**Health Context:** Recovering from knee surgery walks daily  ");
    }

    /// <summary>
    /// With a health context the header's labelled lines stay together, and the line that says the tables are not
    /// medical advice follows them as its own paragraph, before the natal chart (branch DirectionNotes, 1.7.2).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_WithHealthContext_PrintsTheNotMedicalAdviceLineAfterTheHeaderLines()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        List<string> lines = Lines(MedicalMarkdownFormatter.Format(chart, profile, healthContext: "Walks daily"));

        int context = lines.FindIndex(l => l.StartsWith("**Health Context:**", StringComparison.Ordinal));
        lines[context - 1].Should().StartWith("**Tradition:**");
        lines[context + 1].Should().BeEmpty();
        lines[context + 2].Should().Be("*Traditional correspondences for educational and historical study. Not medical advice.*");
        lines[context + 3].Should().BeEmpty();
        lines[context + 4].Should().Be("## Natal Chart");
    }

    /// <summary>
    /// A null, empty or blank health context prints no <c>**Health Context:**</c> line, and the Tradition line is
    /// followed by the blank line that ends the header.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Format_WithoutHealthContext_PrintsNoLine()
    {
        (ChartResult chart, HealthProfile profile) = CreateTestData();

        foreach (string? context in new[] { null, "", " \n\t " })
        {
            List<string> lines = Lines(MedicalMarkdownFormatter.Format(chart, profile, healthContext: context));

            lines.Should().NotContain(
                l => l.StartsWith("**Health Context:**", StringComparison.Ordinal),
                $"the context {(context is null ? "null" : $"\"{context}\"")} is blank");
            int tradition = lines.FindIndex(l => l.StartsWith("**Tradition:**", StringComparison.Ordinal));
            lines[tradition + 1].Should().BeEmpty();
        }
    }

    #region Test Helpers

    /// <summary>The export's lines, each without its line terminator.</summary>
    /// <param name="markdown">A Markdown export.</param>
    /// <returns>The export's lines, in order.</returns>
    private static List<string> Lines(string markdown) => [.. markdown.Split('\n').Select(l => l.TrimEnd('\r'))];

    private static (ChartResult Chart, HealthProfile Profile) CreateTestData()
    {
        ChartResult chart = new()
        {
            Input = new ChartInput
            {
                Name = "Test Subject",
                LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
                TimeZone = "UTC",
                Latitude = 51.5,
                Longitude = -0.1,
                LocationName = "London"
            },
            JulianDay = 2451545.0,
            Obliquity = 23.4367,
            UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            Positions =
            [
                CreatePosition(CelestialBodyId.Sun, 130.0, house: 5),
                CreatePosition(CelestialBodyId.Moon, 100.0, house: 4),
                CreatePosition(CelestialBodyId.Saturn, 280.0, house: 10),
            ],
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12).Select(i => (5.0 + i * 30.0) % 360.0).ToList(),
                Ascendant = 5.0,
                Midheaven = 275.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = 0.0,
            ResolvedOffsetLabel = "UTC",
            EngineVersion = "AstroCast Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };

        HealthProfile profile = new()
        {
            SubjectName = "Test Subject",
            Temperament = new TemperamentProfile
            {
                FireScore = 5,
                EarthScore = 3,
                AirScore = 2,
                WaterScore = 4,
                DominantTemperament = Temperament.Choleric,
                SecondaryTemperament = Temperament.Phlegmatic
            },
            SignEmphases =
            [
                new SignEmphasis
                {
                    Sign = ZodiacSign.Aries,
                    Reason = "Ascendant",
                    BodyRegions = ZodiacBodyMap.GetBodyRegions(ZodiacSign.Aries),
                    HealthSummary = ZodiacBodyMap.GetHealthSummary(ZodiacSign.Aries)
                },
                new SignEmphasis
                {
                    Sign = ZodiacSign.Leo,
                    Reason = "Sun sign",
                    BodyRegions = ZodiacBodyMap.GetBodyRegions(ZodiacSign.Leo),
                    HealthSummary = ZodiacBodyMap.GetHealthSummary(ZodiacSign.Leo)
                }
            ],
            MedicalAspects =
            [
                new MedicalAspectInfo
                {
                    AspectDescription = "Sun Square Saturn",
                    AspectType = "Square",
                    Orb = 2.5,
                    IsApplying = true,
                    MedicalInterpretation = "Traditional focus on heart and bones.",
                    Body1Regions = PlanetaryHealthMap.GetBodyRegions(CelestialBodyId.Sun),
                    Body2Regions = PlanetaryHealthMap.GetBodyRegions(CelestialBodyId.Saturn),
                    Significance = "High"
                }
            ],
            SixthHouseAnalysis = "6th house in Virgo.",
            SixthHouseSign = ZodiacSign.Virgo,
            PlanetsInSixth = ["Saturn"],
            EighthHouseAnalysis = "8th house in Scorpio.",
            TwelfthHouseAnalysis = "12th house in Pisces."
        };

        return (chart, profile);
    }

    private static PlanetPosition CreatePosition(CelestialBodyId body, double longitude, int? house = null)
    {
        return new PlanetPosition
        {
            Body = body,
            Name = body.ToString(),
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = 1.0,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
            House = house,
        };
    }

    #endregion
}
