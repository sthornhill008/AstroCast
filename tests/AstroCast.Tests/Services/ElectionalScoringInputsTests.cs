using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins <see cref="IChartEngine.CalculateElectionalScoringInputs"/> (branch O2, 1.3.3): the chart members
/// <see cref="ElectionalScorer.Score"/> reads, cast at the instant <see cref="IChartEngine.CalculateForElectional(ChartInput, DateTime, CancellationToken)"/>
/// casts at, equal to the full chart's on everything the scorer reads.
/// </summary>
/// <remarks>
/// <para>
/// The electional search scores every eligible grid sample from these inputs and casts the full chart only
/// for the instants it reports. The two paths make the same calls in the same order, so they agree by
/// construction; these tests hold them to it at every sample of a month in three places, for every election
/// type, and at the two edges where the instant itself is in question: an hour the clocks repeat, and an
/// instant outside the ephemeris files.
/// </para>
/// <para>
/// The places are those of the O investigation's equality runs (question 2): Richmond, Reykjavik at 64° N,
/// where the polar summer stretches the planetary hours, and Sydney, south of the equator. The O investigation
/// found the scorer's inputs equal to the full chart's at all 740,216 eligible samples of its sets.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalScoringInputsTests
{
    /// <summary>The seven traditional bodies, the ones whose positions the scorer reads.</summary>
    private static readonly string[] SevenBodies = ["Sun", "Moon", "Mercury", "Venus", "Mars", "Jupiter", "Saturn"];

    /// <summary>The spacing of the samples: 2 h 15 min, so that the month's samples walk round the clock.</summary>
    private const double SampleStepDays = 135.0 / 1440.0;

    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalScoringInputsTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// At every sample of June 2026, every 2 h 15 min from the month's first instant on the place's clock,
    /// the scoring inputs score exactly as the full chart does for all nine election types, on every
    /// dimension and the total, against the J2000 New York natal chart, in Placidus. The members the scorer
    /// reads are equal too: the seven bodies' sign, longitude, retrograde flag and house; the Ascendant;
    /// Mars's and Saturn's dignity score; the sect; the chart ruler.
    /// </summary>
    /// <param name="place">The place's name, for the case's display name and the chart input.</param>
    /// <param name="latitude">The place's latitude.</param>
    /// <param name="longitude">The place's longitude.</param>
    /// <param name="timeZone">The place's IANA zone.</param>
    [Theory]
    [InlineData("Richmond", 37.5407, -77.436, "America/New_York")]
    [InlineData("Reykjavik", 64.1466, -21.9426, "Atlantic/Reykjavik")]
    [InlineData("Sydney", -33.8688, 151.2093, "Australia/Sydney")]
    public void CalculateElectionalScoringInputs_ScoresAsTheFullChart_AtEverySampleOfJune2026(
        string place, double latitude, double longitude, string timeZone)
    {
        TzdbTimeZoneRules rules = new();
        IEphemerisService ephemeris = _fixture.EphemerisService;
        ChartEngine engine = GoldenFixtures.CreateChartEngine(_fixture);
        ChartResult natal = GoldenFixtures.J2000Nyc(_fixture);
        MoonConditionEvaluator evaluator = new(ephemeris, rules);
        PlanetaryHourCalculator hourCalculator = new(ephemeris, rules);
        ElectionalScorer scorer = new();

        double startJd = JulianDayUtility.LocalDayStartToJulianDay(ephemeris, new DateOnly(2026, 6, 1), rules, timeZone);
        double endJd = JulianDayUtility.LocalDayStartToJulianDay(ephemeris, new DateOnly(2026, 7, 1), rules, timeZone);
        DateTime endUtc = JulianDayUtility.ToDateTimeUtc(ephemeris, endJd);
        IReadOnlyList<VoidOfCoursePeriod> voidPeriods = new VoidOfCourseScanner(ephemeris, rules).Scan(startJd, endJd, timeZone);

        List<string> mismatches = [];
        int samples = 0;
        for (int index = 0; ; index++)
        {
            double jd = startJd + (index * SampleStepDays);
            DateTime utc = JulianDayUtility.ToDateTimeUtc(ephemeris, jd);
            if (utc >= endUtc)
            {
                break;
            }

            samples++;
            DateTime local = JulianDayUtility.ToLocalDateTime(ephemeris, jd, rules, timeZone);
            ChartInput input = CandidateInput(local, timeZone, latitude, longitude, place);
            ChartResult light = engine.CalculateElectionalScoringInputs(input, utc);
            ChartResult full = engine.CalculateForElectional(input, utc);
            MoonCondition moon = evaluator.Evaluate(jd, voidPeriods, timeZone);
            PlanetaryHour hour = hourCalculator.Calculate(jd, latitude, longitude, timeZone);
            string at = string.Create(CultureInfo.InvariantCulture, $"{utc:yyyy-MM-dd HH:mm} UT");

            CompareMembers(light, full, at, mismatches);
            foreach (ElectionType type in Enum.GetValues<ElectionType>())
            {
                CompareScores(
                    scorer.Score(light, moon, hour, type, natal),
                    scorer.Score(full, moon, hour, type, natal),
                    $"{at} {type}",
                    mismatches);
            }
        }

        samples.Should().Be(320, "June has 720 hours, 320 samples 2 h 15 min apart");
        mismatches.Should().BeEmpty("the scoring inputs come from the same calls, in the same order, as the full chart's");
    }

    /// <summary>
    /// Sydney's repeated hour: 02:00 AEDT on 5 April 2026, the first pass, 15:00 UT on 4 April. The scoring
    /// inputs are cast at that instant, not the later one the reading also names, and say so as the full
    /// chart does.
    /// </summary>
    [Fact]
    public void CalculateElectionalScoringInputs_InARepeatedHour_IsCastAtTheFullChartsInstant()
    {
        ChartEngine engine = GoldenFixtures.CreateChartEngine(_fixture);
        ChartInput input = CandidateInput(new DateTime(2026, 4, 5, 2, 0, 0), "Australia/Sydney", -33.8688, 151.2093, "Sydney");
        DateTime firstPass = new(2026, 4, 4, 15, 0, 0, DateTimeKind.Utc);

        ChartResult light = engine.CalculateElectionalScoringInputs(input, firstPass);
        ChartResult full = engine.CalculateForElectional(input, firstPass);

        full.LocalTimeMapping.Should().Be(LocalTimeMapping.Repeated, "02:00 AEDT is read twice on 5 April 2026");
        light.UtcDateTime.Should().Be(full.UtcDateTime).And.Be(firstPass);
        light.JulianDay.Should().Be(full.JulianDay);
        light.LocalTimeMapping.Should().Be(full.LocalTimeMapping);
        light.EarlierUtcDateTime.Should().Be(full.EarlierUtcDateTime);
    }

    /// <summary>
    /// An instant outside the years the ephemeris files cover is refused as the full chart refuses it: with
    /// <see cref="ArgumentOutOfRangeException"/> and the same message.
    /// </summary>
    [Fact]
    public void CalculateElectionalScoringInputs_OutsideTheFiles_IsRefusedAsTheFullChartIs()
    {
        ChartEngine engine = GoldenFixtures.CreateChartEngine(_fixture);
        ChartInput input = CandidateInput(new DateTime(2400, 6, 15, 12, 0, 0), "Etc/UTC", 37.5407, -77.436, "Richmond");
        DateTime instant = new(2400, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        Action full = () => engine.CalculateForElectional(input, instant);
        Action light = () => engine.CalculateElectionalScoringInputs(input, instant);

        string message = full.Should().ThrowExactly<ArgumentOutOfRangeException>().Which.Message;
        light.Should().ThrowExactly<ArgumentOutOfRangeException>().Which.Message.Should().Be(message);
    }

    /// <summary>A candidate's chart input, built as <see cref="ElectionalService"/> builds it.</summary>
    /// <param name="local">The sample's reading on the place's clock.</param>
    /// <param name="timeZone">The place's IANA zone.</param>
    /// <param name="latitude">The place's latitude.</param>
    /// <param name="longitude">The place's longitude.</param>
    /// <param name="place">The place's name.</param>
    /// <returns>The chart input.</returns>
    private static ChartInput CandidateInput(DateTime local, string timeZone, double latitude, double longitude, string place) => new()
    {
        Name = string.Create(CultureInfo.InvariantCulture, $"Election {local:yyyy-MM-dd HH:mm}"),
        LocalDateTime = local,
        TimeZone = timeZone,
        Latitude = latitude,
        Longitude = longitude,
        LocationName = place,
        EventType = "Event",
        HouseSystem = HouseSystem.Placidus,
    };

    /// <summary>Records every member the scorer reads that differs between the two results.</summary>
    /// <param name="light">The scoring inputs.</param>
    /// <param name="full">The full chart.</param>
    /// <param name="at">The sample, for the record.</param>
    /// <param name="mismatches">The record.</param>
    private static void CompareMembers(ChartResult light, ChartResult full, string at, List<string> mismatches)
    {
        foreach (string body in SevenBodies)
        {
            PlanetPosition l = light.Positions.Single(p => p.Name == body);
            PlanetPosition f = full.Positions.Single(p => p.Name == body);
            if (l.Sign != f.Sign || l.Longitude != f.Longitude || l.IsRetrograde != f.IsRetrograde || l.House != f.House)
            {
                mismatches.Add($"{at}: {body}");
            }
        }

        if (light.Houses.Ascendant != full.Houses.Ascendant)
        {
            mismatches.Add($"{at}: Ascendant");
        }

        foreach (string malefic in new[] { "Mars", "Saturn" })
        {
            if (light.Dignities.Single(d => d.PlanetName == malefic).Score != full.Dignities.Single(d => d.PlanetName == malefic).Score)
            {
                mismatches.Add($"{at}: {malefic}'s dignity");
            }
        }

        if (light.ChartContext?.IsDayChart != full.ChartContext?.IsDayChart || light.ChartContext?.ChartRuler != full.ChartContext?.ChartRuler)
        {
            mismatches.Add($"{at}: sect or chart ruler");
        }
    }

    /// <summary>Records every dimension, and the total, on which the two scores differ.</summary>
    /// <param name="light">The score from the scoring inputs.</param>
    /// <param name="full">The score from the full chart.</param>
    /// <param name="at">The sample and type, for the record.</param>
    /// <param name="mismatches">The record.</param>
    private static void CompareScores(ScoreBreakdown light, ScoreBreakdown full, string at, List<string> mismatches)
    {
        (string Name, double Light, double Full)[] dimensions =
        [
            ("Moon", light.MoonScore, full.MoonScore),
            ("Ascendant", light.AscendantScore, full.AscendantScore),
            ("Significator", light.SignificatorScore, full.SignificatorScore),
            ("Natal Harmony", light.NatalHarmonyScore, full.NatalHarmonyScore),
            ("Malefic Avoidance", light.MaleficAvoidanceScore, full.MaleficAvoidanceScore),
            ("Mercury", light.MercuryScore, full.MercuryScore),
            ("Planetary Hour", light.PlanetaryHourScore, full.PlanetaryHourScore),
            ("Overall", light.OverallScore, full.OverallScore),
        ];
        foreach ((string name, double l, double f) in dimensions)
        {
            if (l != f)
            {
                mismatches.Add(string.Create(CultureInfo.InvariantCulture, $"{at}: {name} {l} against {f}"));
            }
        }
    }
}
