using System.Text.Json;

using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;
using AstroCast.Tests.Formatting.Golden;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="ChartJsonFormatter"/> verifying JSON structure,
/// camelCase naming, value rounding, and section completeness.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartJsonFormatterTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ChartEngine.
    /// </summary>
    public ChartJsonFormatterTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    /// <summary>
    /// Create a ChartResult using the J2000.0 test input.
    /// </summary>
    private ChartResult CreateTestResult(string? notes = null) => _engine.Calculate(new ChartInput
    {
        Name = "JSON Test",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        Notes = notes,
    });

    [Fact]
    public void ToJson_Should_ReturnValidJson_When_ChartResultProvided()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string json = ChartJsonFormatter.ToJson(result);

        // Assert — parsing should not throw
        Action parse = () => JsonDocument.Parse(json);
        parse.Should().NotThrow("the output should be valid JSON");
    }

    [Fact]
    public void ToJson_Should_ContainAllSections_When_ChartResultProvided()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        // Assert
        root.TryGetProperty("chart", out _).Should().BeTrue("JSON should contain 'chart' section");
        root.TryGetProperty("positions", out _).Should().BeTrue("JSON should contain 'positions' section");
        root.TryGetProperty("houses", out _).Should().BeTrue("JSON should contain 'houses' section");
        root.TryGetProperty("aspects", out _).Should().BeTrue("JSON should contain 'aspects' section");
        root.TryGetProperty("midpointActivations", out _).Should().BeTrue(
            "JSON should contain 'midpointActivations' section");
        root.TryGetProperty("metadata", out _).Should().BeTrue("JSON should contain 'metadata' section");
    }

    [Fact]
    public void ToJson_Should_UseCamelCase_When_Serialized()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement chart = doc.RootElement.GetProperty("chart");

        // Assert — should have camelCase "name", not PascalCase "Name"
        chart.TryGetProperty("name", out _).Should().BeTrue(
            "properties should use camelCase naming (name, not Name)");
        chart.TryGetProperty("Name", out _).Should().BeFalse(
            "PascalCase properties should not exist in camelCase JSON");
    }

    [Fact]
    public void ToJson_Should_RoundValues_When_Serialized()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement positions = doc.RootElement.GetProperty("positions");

        // Assert — check that longitude has at most 4 decimal places
        JsonElement firstPosition = positions.EnumerateArray().First();
        double longitude = firstPosition.GetProperty("longitude").GetDouble();
        string longitudeStr = longitude.ToString("G");

        // If there's a decimal point, check decimal places
        int decimalIndex = longitudeStr.IndexOf('.');
        if (decimalIndex >= 0)
        {
            int decimalPlaces = longitudeStr.Length - decimalIndex - 1;
            decimalPlaces.Should().BeLessThanOrEqualTo(4,
                "longitude values should be rounded to at most 4 decimal places");
        }
    }

    [Fact]
    public void ToJson_Should_ContainNotes_When_Provided()
    {
        // Arrange
        ChartResult result = CreateTestResult(notes: "From birth certificate");

        // Act
        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement chart = doc.RootElement.GetProperty("chart");

        // Assert
        chart.TryGetProperty("notes", out JsonElement notesElement).Should().BeTrue(
            "JSON chart object should contain 'notes' when provided");
        notesElement.GetString().Should().Be("From birth certificate");
    }

    /// <summary>
    /// A chart before 1582-10-15 writes its utcDateTime as the Julian label in the round-trip form, the
    /// text it always held, and names the calendar after it, "calendar": "Julian" (R-D3, branch R2, 1.3.4):
    /// N1, Julian 1300-03-15 12:00 UT at UTC+0, whose instant is 1300-03-23T12:00:00Z as ticks, writes
    /// "1300-03-15T12:00:00.0000000Z"; the dateTime is the reading as typed. Until 1.3.4 the JSON named no
    /// calendar.
    /// </summary>
    [Fact]
    public void ToJson_N1_Should_WriteTheJulianLabel_AndNameTheCalendar()
    {
        ChartResult result = _engine.Calculate(new ChartInput
        {
            Name = "N1",
            LocalDateTime = new DateTime(1300, 3, 15, 12, 0, 0),
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 51.4779,
            Longitude = -0.0015,
            LocationName = "Greenwich",
        });

        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement chart = doc.RootElement.GetProperty("chart");

        chart.GetProperty("utcDateTime").GetString().Should().Be("1300-03-15T12:00:00.0000000Z");
        chart.GetProperty("dateTime").GetString().Should().Be("1300-03-15T12:00:00.0000000");
        chart.GetProperty("calendar").GetString().Should().Be("Julian");
        int calendarAt = json.IndexOf("\"calendar\"", StringComparison.Ordinal);
        calendarAt.Should().BeGreaterThan(json.IndexOf("\"utcDateTime\"", StringComparison.Ordinal), "calendar follows utcDateTime");
        calendarAt.Should().BeLessThan(json.IndexOf("\"julianDay\"", StringComparison.Ordinal), "calendar precedes julianDay");
    }

    /// <summary>
    /// A chart on the Gregorian calendar writes no calendar property (R-D3): the J2000 JSON is unchanged.
    /// </summary>
    [Fact]
    public void ToJson_J2000_Should_HaveNoCalendarProperty()
    {
        ChartResult result = CreateTestResult();

        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("chart").TryGetProperty("calendar", out _).Should().BeFalse();
    }

    [Fact]
    public void ToJson_Should_OmitNotes_When_Null()
    {
        // Arrange
        ChartResult result = CreateTestResult(notes: null);

        // Act
        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement chart = doc.RootElement.GetProperty("chart");

        // Assert
        chart.TryGetProperty("notes", out _).Should().BeFalse(
            "JSON chart object should not contain 'notes' when null");
    }

    [Fact]
    public void ToJson_Should_ContainFixedStarConjunctions_When_ChartHasStars()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);

        // Assert
        doc.RootElement.TryGetProperty("fixedStarConjunctions", out JsonElement stars).Should().BeTrue(
            "JSON should contain 'fixedStarConjunctions' array");
        stars.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public void ToJson_Should_ContainFixedStarCount_InMetadata()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement metadata = doc.RootElement.GetProperty("metadata");

        // Assert
        metadata.TryGetProperty("totalFixedStarConjunctions", out JsonElement count).Should().BeTrue(
            "JSON metadata should contain 'totalFixedStarConjunctions'");
        count.GetInt32().Should().Be(result.FixedStarConjunctions.Count);
    }

    [Fact]
    public void ToJson_Should_ContainStarProperties_When_ConjunctionsExist()
    {
        // Arrange
        ChartResult result = CreateTestResult();

        // Act
        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement stars = doc.RootElement.GetProperty("fixedStarConjunctions");

        // Assert — if there are conjunctions, verify required properties
        if (stars.GetArrayLength() > 0)
        {
            JsonElement first = stars[0];
            first.TryGetProperty("star", out _).Should().BeTrue("each star should have 'star' property");
            first.TryGetProperty("magnitude", out _).Should().BeTrue("each star should have 'magnitude' property");
            first.TryGetProperty("conjunct", out _).Should().BeTrue("each star should have 'conjunct' property");
            first.TryGetProperty("orb", out _).Should().BeTrue("each star should have 'orb' property");
            first.TryGetProperty("nature", out _).Should().BeTrue("each star should have 'nature' property");
            first.TryGetProperty("keywords", out _).Should().BeTrue("each star should have 'keywords' property");
        }
    }

    [Fact]
    public void ToJson_Stations_ShouldCarryNearestStationEvent_When_PlanetIsStationing()
    {
        // The Venus-station sample moment (place-independent): Venus direct and
        // decelerating toward the 2026-10-03 07:15:54 UT station retrograde at
        // 218.4912° (08°29′28″ Scorpio).
        ChartResult result = _engine.Calculate(new ChartInput
        {
            Name = "Parcel enquiry 1001",
            LocalDateTime = new DateTime(2026, 10, 1, 6, 56, 30),
            TimeZone = "America/New_York",
            Latitude = 39.9612,
            Longitude = -82.9988,
            LocationName = "Columbus, Ohio",
            EventType = "Horary",
            HoraryCategory = "Travel",
        });

        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement stations = doc.RootElement.GetProperty("stations");

        stations.GetArrayLength().Should().Be(1);
        JsonElement venus = stations[0];
        venus.GetProperty("planet").GetString().Should().Be("Venus");
        venus.GetProperty("stationType").GetString().Should().Be("Station Retrograde");
        venus.GetProperty("phase").GetString().Should().Be("Approaching");
        venus.GetProperty("stationTimeUtc").GetString().Should().StartWith("2026-10-03T07:15:5");
        venus.GetProperty("stationFormattedPosition").GetString().Should().Be("08°29′28″ Scorpio");
        venus.GetProperty("stationLongitude").GetDouble().Should().BeApproximately(218.4912, 0.0003);
        venus.GetProperty("daysToStation").GetDouble().Should().BeApproximately(1.8468, 0.001);
        venus.GetProperty("dailySpeed").GetDouble().Should().BePositive("the current direction stays on the signed speed");
        venus.GetProperty("stationProximity").GetDouble().Should().Be(0.5874);
    }

    /// <summary>
    /// A station before 1582-10-15 writes its Julian label, as <c>utcDateTime</c> and the hour bounds do
    /// (R-D3): JD 2287356.0 is proleptic Gregorian 1550-06-20 12:00 and Julian 1550-06-10 12:00. The chart
    /// is cast at Julian 1550-06-10 12:00 UT, the same instant, and given that one station. Until Docs-2
    /// the field was <c>ToString("o")</c>, the proleptic date ten days from the chart's own label.
    /// </summary>
    [Fact]
    public void ToJson_PreSwitchStation_Should_WriteTheJulianLabel()
    {
        ChartResult cast = _engine.Calculate(new ChartInput
        {
            Name = "Pre-switch station",
            LocalDateTime = new DateTime(1550, 6, 10, 12, 0, 0),
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 51.4779,
            Longitude = -0.0015,
            LocationName = "Greenwich",
        });
        cast.JulianDay.Should().Be(2287356.0);

        ChartResult result = new()
        {
            Input = cast.Input,
            JulianDay = cast.JulianDay,
            Obliquity = cast.Obliquity,
            UtcDateTime = cast.UtcDateTime,
            Positions = cast.Positions,
            Houses = cast.Houses,
            Aspects = cast.Aspects,
            Midpoints = cast.Midpoints,
            MidpointActivations = cast.MidpointActivations,
            Dignities = cast.Dignities,
            MoonCondition = cast.MoonCondition,
            ChartContext = cast.ChartContext,
            FixedStarConjunctions = cast.FixedStarConjunctions,
            Stations =
            [
                new StationInfo
                {
                    PlanetName = "Mars",
                    StationType = "Station Retrograde",
                    IsStationing = true,
                    DailySpeed = 0.01,
                    StationThreshold = 0.05,
                    StationTimeUtc = new DateTime(1550, 6, 20, 12, 0, 0, DateTimeKind.Utc),
                },
            ],
            ResolvedUtcOffsetHours = cast.ResolvedUtcOffsetHours,
            ResolvedOffsetLabel = cast.ResolvedOffsetLabel,
            EngineVersion = cast.EngineVersion,
            CalculatedAt = cast.CalculatedAt,
        };

        string json = ChartJsonFormatter.ToJson(result);
        using JsonDocument doc = JsonDocument.Parse(json);

        doc.RootElement.GetProperty("chart").GetProperty("utcDateTime").GetString().Should().Be("1550-06-10T12:00:00.0000000Z");
        json.Should().Contain("\"stationTimeUtc\": \"1550-06-10T12:00:00.0000000Z\"");
    }

    /// <summary>
    /// A chart whose parans were not computed (the calculation failed, so <see cref="ChartResult.Parans"/> is null)
    /// has no paran count, where it used to report 0 parans found (E1-9): the serializer leaves null members out, so
    /// the metadata carries no <c>totalParans</c>. A chart with parans still reports their count.
    /// </summary>
    [Fact]
    public void ChartJson_TotalParans_NullWhenNotComputed()
    {
        ChartResult computed = CreateTestResult();
        ChartResult notComputed = new()
        {
            Input = computed.Input,
            JulianDay = computed.JulianDay,
            Obliquity = computed.Obliquity,
            UtcDateTime = computed.UtcDateTime,
            Positions = computed.Positions,
            Houses = computed.Houses,
            Aspects = computed.Aspects,
            Midpoints = computed.Midpoints,
            MidpointActivations = computed.MidpointActivations,
            Dignities = computed.Dignities,
            MoonCondition = computed.MoonCondition,
            ChartContext = computed.ChartContext,
            FixedStarConjunctions = computed.FixedStarConjunctions,
            ResolvedUtcOffsetHours = computed.ResolvedUtcOffsetHours,
            ResolvedOffsetLabel = computed.ResolvedOffsetLabel,
            EngineVersion = computed.EngineVersion,
            CalculatedAt = computed.CalculatedAt,
        };

        using JsonDocument withParans = JsonDocument.Parse(ChartJsonFormatter.ToJson(computed));
        using JsonDocument withoutParans = JsonDocument.Parse(ChartJsonFormatter.ToJson(notComputed));

        computed.Parans.Should().NotBeNull();
        notComputed.Parans.Should().BeNull();
        withParans.RootElement.GetProperty("metadata").GetProperty("totalParans").GetInt32()
            .Should().Be(computed.Parans?.Parans.Count);
        withoutParans.RootElement.GetProperty("metadata").TryGetProperty("totalParans", out _)
            .Should().BeFalse("a paran count that was never computed is left out, not reported as 0");
    }

    /// <summary>
    /// The planetary hour carries its two bounds as instants, so a bound in a repeated or skipped wall hour is
    /// one instant: J2000 NYC's hour runs 11:07:57 to 12:21:44 UT, 06:07:57 to 07:21:44 on the New York clock.
    /// A zone chart names its zone and no offset label.
    /// </summary>
    [Fact]
    public void ToJson_PlanetaryHour_CarriesItsUtcBounds()
    {
        using JsonDocument doc = JsonDocument.Parse(ChartJsonFormatter.ToJson(CreateTestResult()));
        JsonElement hour = doc.RootElement.GetProperty("planetaryHour");

        string start = hour.GetProperty("hourStartUtc").GetString() ?? string.Empty;
        string end = hour.GetProperty("hourEndUtc").GetString() ?? string.Empty;
        start.Should().StartWith("2000-01-01T11:07:57").And.EndWith("Z");
        end.Should().StartWith("2000-01-01T12:21:44").And.EndWith("Z");
        hour.GetProperty("hourTimeZone").GetString().Should().Be("America/New_York");
        hour.TryGetProperty("hourOffsetLabel", out _).Should().BeFalse();
    }

    /// <summary>
    /// A chart cast at an explicit offset reads its hour on that offset's clock, not a zone's: person A's inputs
    /// (1947-10-05 09:30 at UTC−5) name no zone and carry the offset label. Until 1.3.4 the JSON named the zone
    /// the offset overrode.
    /// </summary>
    [Fact]
    public void ToJson_PlanetaryHour_ExplicitOffsetChart_NamesItsOffset_NotAZone()
    {
        ChartResult result = _engine.Calculate(new ChartInput
        {
            Name = "Explicit Offset Fixture",
            LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
            TimeZone = "America/New_York",
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, Pennsylvania",
            EventType = "Birth",
        });

        using JsonDocument doc = JsonDocument.Parse(ChartJsonFormatter.ToJson(result));
        JsonElement hour = doc.RootElement.GetProperty("planetaryHour");

        hour.TryGetProperty("hourTimeZone", out _).Should().BeFalse();
        hour.GetProperty("hourOffsetLabel").GetString().Should().Be("UTC\u22125 (explicit)");
    }

    /// <summary>
    /// Across the fall-back the wall-clock bounds cannot say how long the hour is; the UTC bounds can: New York
    /// 2026-11-01 01:30 is in Mercury's hour, the eighth of the night, 05:47:57 to 06:55:57 UT (01:47:57 EDT to
    /// 01:55:57 EST), 68 minutes (Tier B, HourPeriodClockChangeTests).
    /// </summary>
    [Fact]
    public void ToJson_PlanetaryHour_AcrossTheFallBack_UtcBoundsSpanTheHour()
    {
        ChartResult result = _engine.Calculate(new ChartInput
        {
            Name = "Hour Period Fixture",
            LocalDateTime = new DateTime(2026, 11, 1, 1, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "America/New_York",
            EventType = "Event",
        });

        using JsonDocument doc = JsonDocument.Parse(ChartJsonFormatter.ToJson(result));
        JsonElement hour = doc.RootElement.GetProperty("planetaryHour");

        string start = hour.GetProperty("hourStartUtc").GetString() ?? string.Empty;
        string end = hour.GetProperty("hourEndUtc").GetString() ?? string.Empty;
        start.Should().StartWith("2026-11-01T05:47:57");
        end.Should().StartWith("2026-11-01T06:55:57");
        DateTime startSecond = DateTime.ParseExact(start[..19], "yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        DateTime endSecond = DateTime.ParseExact(end[..19], "yyyy-MM-ddTHH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        (endSecond - startSecond).Should().Be(TimeSpan.FromMinutes(68));
    }

    #region Asteroids and Lilith (branch AsteroidsExport, 1.6.0)

    /// <summary>
    /// With the switch on the JSON carries <c>minorPoints</c> directly after <c>positions</c> (scoping decision
    /// 4): one element per point in R1's order, with the keys of a <c>positions</c> element plus
    /// <c>isOutOfBounds</c> and an <c>aspects</c> array of <c>{ body, aspect, angle, orb, isApplying }</c> in the
    /// chart's minor-point aspect order; the metadata gains no key. J2000 NYC: five aspects, Ceres's first its
    /// square to the Ascendant at 0.2110°, separating (Tier B, j2000nyc).
    /// </summary>
    [Fact]
    public void ToJson_SwitchOn_MinorPointsHoldTheFiveInOrder_EachWithItsAspects()
    {
        ChartResult result = _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));

        using JsonDocument doc = JsonDocument.Parse(ChartJsonFormatter.ToJson(result));
        JsonElement root = doc.RootElement;

        List<string> keys = [.. root.EnumerateObject().Select(p => p.Name)];
        keys.IndexOf("minorPoints").Should().Be(keys.IndexOf("positions") + 1, "the array follows positions");

        JsonElement points = root.GetProperty("minorPoints");
        points.GetArrayLength().Should().Be(5);
        points.EnumerateArray().Select(p => p.GetProperty("body").GetString())
            .Should().Equal("Ceres", "Pallas", "Juno", "Vesta", "Lilith");

        List<string> positionKeys = [.. root.GetProperty("positions")[0].EnumerateObject().Select(p => p.Name)];
        foreach (JsonElement point in points.EnumerateArray())
        {
            point.EnumerateObject().Select(p => p.Name).Should().Equal([.. positionKeys, "isOutOfBounds", "aspects"]);
        }

        points.EnumerateArray().Sum(p => p.GetProperty("aspects").GetArrayLength())
            .Should().Be(result.MinorPointAspects.Count).And.Be(5);

        JsonElement first = points[0].GetProperty("aspects")[0];
        first.EnumerateObject().Select(p => p.Name).Should().Equal("body", "aspect", "angle", "orb", "isApplying");
        first.GetProperty("body").GetString().Should().Be("ASC");
        first.GetProperty("aspect").GetString().Should().Be("Square");
        first.GetProperty("angle").GetDouble().Should().Be(90.0);
        first.GetProperty("orb").GetDouble().Should().Be(0.211);
        first.GetProperty("isApplying").GetBoolean().Should().BeFalse();

        root.GetProperty("metadata").EnumerateObject().Select(p => p.Name).Should().NotContain(k => k.Contains("Minor", StringComparison.Ordinal));
    }

    /// <summary>With the switch off there is no <c>minorPoints</c> key at all, so the JSON is as before (R9).</summary>
    [Fact]
    public void ToJson_SwitchOff_HasNoMinorPointsKey()
    {
        ChartResult result = _engine.Calculate(GoldenFixtures.J2000NycInput());

        using JsonDocument doc = JsonDocument.Parse(ChartJsonFormatter.ToJson(result));

        doc.RootElement.TryGetProperty("minorPoints", out _).Should().BeFalse();
        ChartJsonFormatter.ToJson(result).Should().NotContain("Lilith");
    }

    /// <summary>
    /// J2000 NYC's Pallas is out of bounds (declination −29.7074° against the obliquity 23.4377°) and retrograde;
    /// the other four are in bounds (Tier B, j2000nyc).
    /// </summary>
    [Fact]
    public void ToJson_SwitchOn_PallasIsOutOfBounds_AtJ2000Nyc()
    {
        ChartResult result = _engine.Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));

        using JsonDocument doc = JsonDocument.Parse(ChartJsonFormatter.ToJson(result));
        JsonElement points = doc.RootElement.GetProperty("minorPoints");

        JsonElement pallas = points[1];
        pallas.GetProperty("body").GetString().Should().Be("Pallas");
        pallas.GetProperty("isOutOfBounds").GetBoolean().Should().BeTrue();
        pallas.GetProperty("isRetrograde").GetBoolean().Should().BeTrue();
        pallas.GetProperty("direction").GetString().Should().Be("R");
        pallas.GetProperty("declination").GetDouble().Should().Be(-29.7074);
        points.EnumerateArray().Where(p => p.GetProperty("body").GetString() != "Pallas")
            .Should().AllSatisfy(p => p.GetProperty("isOutOfBounds").GetBoolean().Should().BeFalse());
    }

    #endregion
}
