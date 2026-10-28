using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Data;

/// <summary>
/// Tests for <see cref="ChartMapper"/> verifying timezone conversion,
/// Julian Day computation, and round-trip mapping between
/// <see cref="ChartInput"/> and <see cref="ChartEntity"/>.
/// Uses the shared "Ephemeris" collection fixture for Julian Day computation.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartMapperTests
{
    private readonly EphemerisService _ephemerisService;

    /// <summary>
    /// Receives the shared ephemeris fixture.
    /// </summary>
    public ChartMapperTests(EphemerisFixture fixture)
    {
        _ephemerisService = fixture.EphemerisService;
    }

    [Fact]
    public void ToEntity_Should_ComputeJulianDay_When_InputProvided()
    {
        // Arrange — J2000.0 epoch: 2000-01-01 12:00 UT = 2000-01-01 07:00 EST
        ChartInput input = new()
        {
            Name = "JD Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
        };

        // Act
        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);

        // Assert — J2000.0 = JD 2451545.0
        entity.JulianDay.Should().BeApproximately(2451545.0, 0.001,
            "2000-01-01 07:00 EST converts to 12:00 UT, which is JD 2451545.0");
    }

    /// <summary>
    /// A chart before 1582-10-15 stores its UtcDateTime as the Julian label in the round-trip form, the
    /// text the column always held (R-D3, branch R2, 1.3.4): N1, Julian 1300-03-15 12:00 UT at UTC+0,
    /// stores "1300-03-15T12:00:00.0000000Z" and its Julian Day 2195957.0.
    /// </summary>
    [Fact]
    public void ToEntity_N1_Should_StoreTheJulianLabel_AndItsJulianDay()
    {
        ChartInput input = new()
        {
            Name = "N1",
            LocalDateTime = new DateTime(1300, 3, 15, 12, 0, 0),
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 51.4779,
            Longitude = -0.0015,
            LocationName = "Greenwich",
        };

        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);

        entity.UtcDateTime.Should().Be("1300-03-15T12:00:00.0000000Z");
        entity.JulianDay.Should().BeApproximately(2195957.0, 1e-8);
    }

    [Fact]
    public void ToEntity_Should_ConvertToUtc_When_TimezoneProvided()
    {
        // Arrange — 2000-01-01 12:00 EST should become 17:00 UTC
        ChartInput input = new()
        {
            Name = "UTC Test",
            LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
        };

        // Act
        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);

        // Assert — EST is UTC-5, so 12:00 EST = 17:00 UTC
        entity.UtcDateTime.Should().Contain("17:00",
            "12:00 EST should convert to 17:00 UTC (EST is UTC-5)");
    }

    [Fact]
    public void ToEntity_Should_MapGender_When_Provided()
    {
        // Arrange
        ChartInput input = new()
        {
            Name = "Gender Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            Gender = Gender.Female,
        };

        // Act
        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);

        // Assert
        entity.Gender.Should().Be("Female",
            "Gender.Female should map to the string 'Female' in the entity");
    }

    [Fact]
    public void ToInput_Should_RoundTrip_When_EntityProvided()
    {
        // Arrange
        ChartEntity entity = new()
        {
            Id = 42,
            Name = "Round Trip Test",
            EventType = "Birth",
            EventDescription = "Test description",
            LocalDateTime = "2000-01-01T07:00:00",
            TimeZone = "America/New_York",
            UtcDateTime = "2000-01-01T12:00:00Z",
            JulianDay = 2451545.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            Gender = "Female",
            SourceNotes = "Birth certificate",
            Notes = "Test notes",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

        // Act
        ChartInput input = ChartMapper.ToInput(entity);

        // Assert
        input.Name.Should().Be(entity.Name);
        input.Latitude.Should().Be(entity.Latitude);
        input.Longitude.Should().Be(entity.Longitude);
        input.LocationName.Should().Be(entity.LocationName);
        input.TimeZone.Should().Be(entity.TimeZone);
        input.EventType.Should().Be(entity.EventType);
        input.EventDescription.Should().Be(entity.EventDescription);
        input.Gender.Should().Be(Gender.Female);
        input.SourceNotes.Should().Be("Birth certificate");
        input.Notes.Should().Be(entity.Notes);
    }

    [Fact]
    public void ToInput_Should_ParseGender_When_Valid()
    {
        // Arrange
        ChartEntity entity = new()
        {
            Id = 1,
            Name = "Parse Gender Test",
            EventType = "Birth",
            LocalDateTime = "2000-01-01T07:00:00",
            TimeZone = "America/New_York",
            UtcDateTime = "2000-01-01T12:00:00Z",
            JulianDay = 2451545.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            Gender = "Male",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

        // Act
        ChartInput input = ChartMapper.ToInput(entity);

        // Assert
        input.Gender.Should().Be(Gender.Male,
            "a valid Gender string 'Male' should parse to Gender.Male");
    }

    [Fact]
    public void ToInput_Should_DefaultGender_When_Invalid()
    {
        // Arrange
        ChartEntity entity = new()
        {
            Id = 1,
            Name = "Invalid Gender Test",
            EventType = "Birth",
            LocalDateTime = "2000-01-01T07:00:00",
            TimeZone = "America/New_York",
            UtcDateTime = "2000-01-01T12:00:00Z",
            JulianDay = 2451545.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            Gender = "InvalidValue",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

        // Act
        ChartInput input = ChartMapper.ToInput(entity);

        // Assert
        input.Gender.Should().Be(Gender.Unspecified,
            "an invalid Gender string should default to Gender.Unspecified");
    }

    [Fact]
    public void ToEntity_Should_UseExplicitOffset_When_FlagIsTrue()
    {
        // Arrange — 1947-10-05 09:30 local, explicit UTC-5 (EST)
        // Expected UTC = 14:30
        ChartInput input = new()
        {
            Name = "Explicit Offset Test",
            LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, PA",
            UseExplicitOffset = true,
            UtcOffsetHours = -5.0,
        };

        // Act
        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);

        // Assert — UTC should be 14:30 (not 13:30 which IANA might produce with DST)
        entity.UtcDateTime.Should().Contain("14:30",
            "09:30 local with explicit UTC-5 should produce 14:30 UTC");
        entity.UtcOffsetHours.Should().Be(-5.0,
            "the explicit offset should be preserved in the entity");
        entity.UseExplicitOffset.Should().BeTrue(
            "the UseExplicitOffset flag should be preserved in the entity");
    }

    [Fact]
    public void ToInput_Should_RoundTripExplicitOffset_When_EntityHasOffset()
    {
        // Arrange
        ChartEntity entity = new()
        {
            Id = 99,
            Name = "Offset Round Trip",
            EventType = "Birth",
            LocalDateTime = "1947-10-05T09:30:00",
            TimeZone = "America/New_York",
            UtcDateTime = "1947-10-05T14:30:00",
            JulianDay = 2432101.0,
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, PA",
            UtcOffsetHours = -5.0,
            UseExplicitOffset = true,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

        // Act
        ChartInput input = ChartMapper.ToInput(entity);

        // Assert
        input.UseExplicitOffset.Should().BeTrue(
            "UseExplicitOffset should round-trip from entity to input");
        input.UtcOffsetHours.Should().Be(-5.0,
            "UtcOffsetHours should round-trip from entity to input");
    }

    [Fact]
    public void ToInput_Should_DefaultOffset_When_EntityHasNoOffset()
    {
        // Arrange — entity without explicit offset (legacy chart)
        ChartEntity entity = new()
        {
            Id = 1,
            Name = "No Offset Test",
            EventType = "Birth",
            LocalDateTime = "2000-01-01T07:00:00",
            TimeZone = "America/New_York",
            UtcDateTime = "2000-01-01T12:00:00Z",
            JulianDay = 2451545.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            UtcOffsetHours = null,
            UseExplicitOffset = false,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

        // Act
        ChartInput input = ChartMapper.ToInput(entity);

        // Assert
        input.UseExplicitOffset.Should().BeFalse(
            "UseExplicitOffset should default to false for legacy charts");
        input.UtcOffsetHours.Should().BeNull(
            "UtcOffsetHours should be null for legacy charts");
    }

    #region House System Persistence Tests

    [Fact]
    public void ToEntity_Should_StoreNull_When_HouseSystemIsPlacidus()
    {
        ChartInput input = new()
        {
            Name = "Placidus Default",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            HouseSystem = HouseSystem.Placidus,
        };

        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);

        entity.HouseSystem.Should().BeNull(
            "Placidus is the default and should be stored as null for compact storage");
    }

    [Fact]
    public void ToEntity_Should_StoreCharCode_When_HouseSystemIsNonDefault()
    {
        ChartInput input = new()
        {
            Name = "Regiomontanus Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            HouseSystem = HouseSystem.Regiomontanus,
        };

        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);

        entity.HouseSystem.Should().Be("R",
            "Regiomontanus should be stored as its Swiss Ephemeris char code 'R'");
    }

    [Fact]
    public void ToInput_Should_RestoreHouseSystem_When_EntityHasCharCode()
    {
        ChartEntity entity = new()
        {
            Name = "Koch Test",
            EventType = "Birth",
            LocalDateTime = "2000-01-01T07:00:00",
            TimeZone = "America/New_York",
            UtcDateTime = "2000-01-01T12:00:00Z",
            JulianDay = 2451545.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            HouseSystem = "K",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

        ChartInput input = ChartMapper.ToInput(entity);

        input.HouseSystem.Should().Be(HouseSystem.Koch,
            "char code 'K' should map to HouseSystem.Koch");
    }

    [Fact]
    public void ToInput_Should_DefaultToPlacidus_When_EntityHouseSystemIsNull()
    {
        ChartEntity entity = new()
        {
            Name = "Legacy Chart",
            EventType = "Birth",
            LocalDateTime = "2000-01-01T07:00:00",
            TimeZone = "America/New_York",
            UtcDateTime = "2000-01-01T12:00:00Z",
            JulianDay = 2451545.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            HouseSystem = null,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

        ChartInput input = ChartMapper.ToInput(entity);

        input.HouseSystem.Should().Be(HouseSystem.Placidus,
            "null HouseSystem (pre-v1.0.175 data) should default to Placidus");
    }

    [Fact]
    public void RoundTrip_Should_PreserveHouseSystem_When_NonDefault()
    {
        ChartInput original = new()
        {
            Name = "Equal House Round Trip",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            HouseSystem = HouseSystem.Equal,
        };

        ChartEntity entity = ChartMapper.ToEntity(original, _ephemerisService);
        ChartInput restored = ChartMapper.ToInput(entity);

        restored.HouseSystem.Should().Be(HouseSystem.Equal,
            "house system should survive the ToEntity → ToInput round trip");
    }

    #endregion

    #region Stored local time

    /// <summary>
    /// A London chart saved on a New York machine before 1.3.2 carries that
    /// machine's <c>-04:00</c> stamp. It must read back as the clock as written —
    /// no offset applied, Kind <see cref="DateTimeKind.Unspecified"/> — and save
    /// back without a stamp, whatever time zone the test runs in.
    /// </summary>
    [Fact]
    public void ToInput_Should_KeepClockAsWritten_When_StoredWithMinusFourHourOffset() =>
        AssertStoredClockRoundTrips("2026-07-19T09:06:30.0000000-04:00");

    /// <summary>
    /// A <c>+09:00</c> stamp is ignored the same way: the clock as written
    /// survives the round trip, with no offset applied.
    /// </summary>
    [Fact]
    public void ToInput_Should_KeepClockAsWritten_When_StoredWithPlusNineHourOffset() =>
        AssertStoredClockRoundTrips("2026-07-19T09:06:30.0000000+09:00");

    /// <summary>
    /// A row stored without an offset — the shape every row takes from 1.3.2 on —
    /// reads back and saves back unchanged.
    /// </summary>
    [Fact]
    public void ToInput_Should_KeepClockAsWritten_When_StoredWithoutOffset() =>
        AssertStoredClockRoundTrips("2026-07-19T09:06:30.0000000");

    /// <summary>
    /// A local date/time with Kind <see cref="DateTimeKind.Local"/> — what a
    /// reloaded chart carried — is stored as the wall clock alone: no machine
    /// offset is stamped on new rows.
    /// </summary>
    [Fact]
    public void ToEntity_Should_StoreClockWithoutOffset_When_LocalDateTimeKindIsLocal()
    {
        ChartInput input = new()
        {
            Name = "Stored Clock Write Test",
            LocalDateTime = DateTime.SpecifyKind(new DateTime(2026, 7, 19, 9, 6, 30), DateTimeKind.Local),
            TimeZone = "Europe/London",
            Latitude = 51.4613531,
            Longitude = -0.3032767,
            LocationName = "Richmond, Greater London",
        };

        ChartEntity entity = ChartMapper.ToEntity(input, _ephemerisService);

        entity.LocalDateTime.Should().Be("2026-07-19T09:06:30.0000000",
            "the stored local time is the chart's wall clock, with no machine offset stamped on it");
        entity.UtcDateTime.Should().Be("2026-07-19T08:06:30.0000000Z",
            "09:06:30 BST is 08:06:30 UT");
    }

    /// <summary>
    /// Reads <paramref name="stored"/> through <see cref="ChartMapper.ToInput"/>
    /// and writes the result back through <see cref="ChartMapper.ToEntity"/>,
    /// asserting that 09:06:30 on 19 July 2026 survives both ways with no offset.
    /// </summary>
    /// <param name="stored">The stored local date/time string.</param>
    private void AssertStoredClockRoundTrips(string stored)
    {
        ChartEntity entity = new()
        {
            Name = "Stored Clock Test",
            EventType = "Event",
            LocalDateTime = stored,
            TimeZone = "Europe/London",
            UtcDateTime = "2026-07-19T08:06:30.0000000Z",
            JulianDay = 2461240.8378472222,
            Latitude = 51.4613531,
            Longitude = -0.3032767,
            LocationName = "Richmond, Greater London",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

        ChartInput input = ChartMapper.ToInput(entity);

        input.LocalDateTime.Should().Be(new DateTime(2026, 7, 19, 9, 6, 30),
            $"the clock as written in '{stored}' must survive, with no offset applied");
        input.LocalDateTime.Kind.Should().Be(DateTimeKind.Unspecified,
            "a stored local time is a wall-clock reading in the chart's zone, not an instant on this machine");
        ChartMapper.ToEntity(input, _ephemerisService).LocalDateTime.Should().Be("2026-07-19T09:06:30.0000000",
            "the clock saves back as written, with no offset stamped on it");
    }

    #endregion

    #region Minor points (branch Asteroids, 1.6.0)

    /// <summary>J2000 NYC's input with the switch as given.</summary>
    /// <param name="includeMinorPoints">The switch.</param>
    /// <returns>The input.</returns>
    private static ChartInput SwitchInput(bool includeMinorPoints) => new()
    {
        Name = "Switch Test",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        IncludeMinorPoints = includeMinorPoints,
    };

    /// <summary>
    /// The switch is a session value, never stored (R2): the entity has no member for it, and two inputs that differ
    /// only in it map to the same row.
    /// </summary>
    [Fact]
    public void ToEntity_IgnoresTheSwitch()
    {
        typeof(ChartEntity).GetProperties().Select(p => p.Name).Should().NotContain(
            name => name.Contains("MinorPoint", StringComparison.OrdinalIgnoreCase),
            "the row has no column for the switch");

        ChartEntity off = ChartMapper.ToEntity(SwitchInput(includeMinorPoints: false), _ephemerisService);
        ChartEntity on = ChartMapper.ToEntity(SwitchInput(includeMinorPoints: true), _ephemerisService);

        on.Should().BeEquivalentTo(
            off,
            options => options.Excluding(e => e.CreatedAt).Excluding(e => e.UpdatedAt),
            "nothing of the switch reaches the row");
    }

    /// <summary>
    /// <see cref="ChartMapper.ToInput"/> takes the switch from its caller, never from the row: off by default, the
    /// caller's value otherwise.
    /// </summary>
    [Fact]
    public void ToInput_DefaultsTheSwitchOff_AndTakesTheCallersValue()
    {
        ChartEntity entity = ChartMapper.ToEntity(SwitchInput(includeMinorPoints: true), _ephemerisService);

        ChartMapper.ToInput(entity).IncludeMinorPoints.Should().BeFalse(
            "the row holds no switch, so a load without the session value is off");
        ChartMapper.ToInput(entity, includeMinorPoints: false).IncludeMinorPoints.Should().BeFalse("the caller's value is off");
        ChartMapper.ToInput(entity, includeMinorPoints: true).IncludeMinorPoints.Should().BeTrue("the caller's session value is on");
    }

    #endregion
}
