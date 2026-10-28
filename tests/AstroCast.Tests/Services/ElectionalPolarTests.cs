using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the electional search beyond the polar circles (Steve's ruling 2 of 2026-10-09, the O investigation's
/// question 5, Y6-9; branch O3, 1.3.3): a sample with no planetary hour is dropped and counted, and a natal chart
/// whose house system has no houses at the place is refused before the search scans.
/// </summary>
/// <remarks>
/// <para>
/// A sample on a day when the Sun does not rise or set at the place has no planetary hour: it is not scored, and
/// <see cref="ElectionalResult.SamplesWithoutPlanetaryHour"/> counts it. Until 1.3.3 the first such sample ended
/// the whole search with "Sunrise does not occur at latitude …".
/// </para>
/// <para>
/// Candidates are cast in the natal chart's house system, and Placidus and Koch have no houses beyond the polar
/// circles. The search casts the houses once, before it scans, and refuses with the system and the latitude named.
/// Until 1.3.3 the first candidate failed the whole search with the engine's house error.
/// </para>
/// <para>
/// The expected results are the O3 harness's at <c>2dc2cf9</c> (O2's mirror, every eligible sample scored, with
/// the rule's switches), on the GoldenFixtures J2000 New York natal chart: its own Placidus, or the same input in
/// another house system.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalPolarTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalPolarTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// A week with no sunrise or sunset on any day, three candidates asked: every eligible sample has no planetary
    /// hour, so none is scored, and the result counts them. 66.3° N (637 eligible samples) and 66.3° S (618), each
    /// in its midsummer; Placidus has houses at 66.3°. Before 1.3.3 both searches failed with "Sunrise does not
    /// occur at latitude …".
    /// </summary>
    /// <param name="latitude">The place's latitude.</param>
    /// <param name="longitude">The place's longitude.</param>
    /// <param name="timeZone">The place's IANA zone.</param>
    /// <param name="start">The first acceptable date, yyyy-MM-dd.</param>
    /// <param name="end">The last acceptable date, yyyy-MM-dd.</param>
    /// <param name="expectedWithoutHour">The eligible samples, every one without a planetary hour.</param>
    [Theory]
    [InlineData(66.3, 25.0, "Europe/Helsinki", "2026-06-18", "2026-06-24", 637)]
    [InlineData(-66.3, 25.0, "UTC", "2026-12-18", "2026-12-24", 618)]
    public void Calculate_NoSunriseOrSunsetAllWeek_ScoresNothingAndCountsEverySample(
        double latitude, double longitude, string timeZone, string start, string end, int expectedWithoutHour)
    {
        ElectionalResult result = Service().Calculate(Search(
            GoldenFixtures.J2000Nyc(_fixture), latitude, longitude, timeZone,
            DateOnly.ParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly.ParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture)));

        result.Candidates.Should().BeEmpty("no sample has a planetary hour, so none is scored");
        result.SamplesWithoutPlanetaryHour.Should().Be(expectedWithoutHour);
    }

    /// <summary>
    /// Tromsø, 10 to 25 May 2026, on the J2000 natal chart in Regiomontanus, which has houses there: 416 of the
    /// 1,044 eligible samples fall on days the Sun does not rise or set there and have no planetary hour; the other
    /// 628 are scored, and the three windows are the best of them, each with its hour: 15 May 19:45 CEST (79.2),
    /// 16 May 01:45 CEST (79.2) and 16 May 09:15 CEST (75.25). Before 1.3.3 the search failed. The second window
    /// scored 75.45 until houses followed the longitude (branch F6): its significator, the Moon, at latitude +4.6°,
    /// is in the 1st house by longitude, where the library's house position put it in the 12th, so the significator
    /// term rises from 60 to 85.
    /// </summary>
    [Fact]
    public void Calculate_SomeDaysWithoutSunriseOrSunset_ScoresTheRestAndCountsTheDropped()
    {
        ElectionalResult result = Service().Calculate(Search(
            NatalIn(HouseSystem.Regiomontanus), 69.6492, 18.9553, "Europe/Oslo",
            new DateOnly(2026, 5, 10), new DateOnly(2026, 5, 25)));

        result.SamplesWithoutPlanetaryHour.Should().Be(416);
        result.Candidates.Select(c => (c.Chart.UtcDateTime, c.Score)).Should().Equal(
            (new DateTime(2026, 5, 15, 17, 45, 0, DateTimeKind.Utc), 79.2),
            (new DateTime(2026, 5, 15, 23, 45, 0, DateTimeKind.Utc), 79.2),
            (new DateTime(2026, 5, 16, 7, 15, 0, DateTimeKind.Utc), 75.25));
        result.Candidates.Should().OnlyContain(
            c => !string.IsNullOrEmpty(c.PlanetaryHour.RulingPlanet), "a reported window always has a planetary hour");
    }

    /// <summary>
    /// A natal chart whose house system has no houses at the place is refused before the search scans, with the
    /// system and the latitude named: Placidus at Tromsø (69.6492° N), Koch at 66.6° S. Before 1.3.3 each failed at
    /// its first sample with the engine's "House calculation (system 'P') failed …" (or 'K').
    /// </summary>
    /// <param name="system">The natal chart's house system.</param>
    /// <param name="latitude">The place's latitude.</param>
    /// <param name="longitude">The place's longitude.</param>
    /// <param name="timeZone">The place's IANA zone.</param>
    /// <param name="start">The first acceptable date, yyyy-MM-dd.</param>
    /// <param name="end">The last acceptable date, yyyy-MM-dd.</param>
    /// <param name="systemName">The system's name, as the natal chart reports it.</param>
    /// <param name="latitudeText">The latitude as the message prints it.</param>
    [Theory]
    [InlineData(HouseSystem.Placidus, 69.6492, 18.9553, "Europe/Oslo", "2026-05-10", "2026-05-25", "Placidus", "69.6492° N")]
    [InlineData(HouseSystem.Koch, -66.6, 25.0, "UTC", "2026-12-18", "2026-12-24", "Koch", "66.6000° S")]
    public void Calculate_NatalHouseSystemWithoutHousesAtThePlace_IsRefused(
        HouseSystem system, double latitude, double longitude, string timeZone, string start, string end,
        string systemName, string latitudeText)
    {
        ElectionalInput input = Search(
            NatalIn(system), latitude, longitude, timeZone,
            DateOnly.ParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly.ParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        string expected =
            $"Electional candidates are cast in the natal chart's house system, {systemName}, which has no houses "
            + $"at latitude {latitudeText}. Choose a natal chart in a house system that exists there, such "
            + "as Equal, Whole Sign or Regiomontanus.";

        Action act = () => Service().Calculate(input);

        ArgumentException refusal = act.Should().Throw<ArgumentException>().Which;
        refusal.ParamName.Should().Be("input");
        refusal.Message.Should().Be(new ArgumentException(expected, "input").Message);
    }

    /// <summary>
    /// Reykjavik on 21 June 2026, where the Sun rises and sets: the search returns as it did before 1.3.3
    /// (00:15, 70.5; 17:30, 70.25, GMT), and no sample lacks a planetary hour. The third window is 09:00 at 68.25:
    /// since houses follow the longitude (branch F6) its Moon, the significator, is in the 2nd house, where the
    /// library's house position put it in the 3rd, so it ties 09:15 and, the earlier, takes the place 09:15 had.
    /// </summary>
    [Fact]
    public void Calculate_SunRisesAndSets_ReturnsAsBeforeWithNoSampleDropped()
    {
        ElectionalResult result = Service().Calculate(Search(
            GoldenFixtures.J2000Nyc(_fixture), 64.1466, -21.9426, "Atlantic/Reykjavik",
            new DateOnly(2026, 6, 21), new DateOnly(2026, 6, 21)));

        result.SamplesWithoutPlanetaryHour.Should().Be(0);
        result.Candidates.Select(c => (c.LocalDateTime, c.Score)).Should().Equal(
            (new DateTime(2026, 6, 21, 0, 15, 0), 70.5),
            (new DateTime(2026, 6, 21, 17, 30, 0), 70.25),
            (new DateTime(2026, 6, 21, 9, 0, 0), 68.25));
    }

    /// <summary>A search of type Other, three candidates asked, Mercury retrograde avoided.</summary>
    /// <param name="natal">The natal chart.</param>
    /// <param name="latitude">The place's latitude.</param>
    /// <param name="longitude">The place's longitude.</param>
    /// <param name="timeZone">The place's IANA zone.</param>
    /// <param name="start">The first acceptable date.</param>
    /// <param name="end">The last acceptable date.</param>
    /// <returns>The search's input.</returns>
    private static ElectionalInput Search(
        ChartResult natal, double latitude, double longitude, string timeZone, DateOnly start, DateOnly end) => new()
        {
            NatalChart = natal,
            ElectionType = ElectionType.Other,
            ElectionGoal = "Polar search",
            RangeStart = start,
            RangeEnd = end,
            Latitude = latitude,
            Longitude = longitude,
            LocationName = "Polar search",
            TimeZone = timeZone,
            CandidateCount = 3,
        };

    /// <summary>The J2000 New York natal chart (GoldenFixtures' input) in the given house system.</summary>
    /// <param name="system">The house system.</param>
    /// <returns>The natal chart, cast in New York, where every system has houses.</returns>
    private ChartResult NatalIn(HouseSystem system)
    {
        ChartInput nyc = GoldenFixtures.J2000NycInput();
        return GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
        {
            Name = nyc.Name,
            LocalDateTime = nyc.LocalDateTime,
            TimeZone = nyc.TimeZone,
            Latitude = nyc.Latitude,
            Longitude = nyc.Longitude,
            LocationName = nyc.LocationName,
            EventType = nyc.EventType,
            HouseSystem = system,
        });
    }

    /// <summary>The service, on the shared ephemeris and a GoldenFixtures chart engine.</summary>
    /// <returns>A new electional service.</returns>
    private ElectionalService Service() =>
        new(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture));
}
