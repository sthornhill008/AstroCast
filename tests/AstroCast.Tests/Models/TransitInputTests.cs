using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// Tests for <see cref="TransitInput"/> verifying the computed MidpointDate property.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransitInputTests
{
    [Fact]
    public void MidpointDate_30DayRange_Should_Return_Day15()
    {
        // Arrange
        TransitInput input = new()
        {
            NatalChart = null!,
            ReportStartDate = new DateOnly(2026, 3, 1),
            ReportEndDate = new DateOnly(2026, 3, 31),
        };

        // Act & Assert
        input.MidpointDate.Should().Be(new DateOnly(2026, 3, 16));
    }

    [Fact]
    public void MidpointDate_1DayRange_Should_Return_StartDate()
    {
        // Arrange
        TransitInput input = new()
        {
            NatalChart = null!,
            ReportStartDate = new DateOnly(2026, 3, 1),
            ReportEndDate = new DateOnly(2026, 3, 1),
        };

        // Act & Assert
        input.MidpointDate.Should().Be(new DateOnly(2026, 3, 1));
    }

    /// <summary>
    /// The midpoint is a day derived from the scan, not typed arithmetic (branch R3, 1.3.4; R-D7): the
    /// proleptic day of the start plus half the days to the proleptic day of the end. Typed Julian
    /// 1400-02-28 to 1400-03-02 (proleptic 1400-03-08 to 03-11) gives 1400-03-09, the Julian 29 February
    /// no typed date can hold (today 1400-03-01); typed 1582-09-25 to 1582-10-25 (proleptic 1582-10-05 to
    /// 10-25) gives 1582-10-15 (today 1582-10-10, a date that never occurred). Tier B, r3_truth2.out.
    /// </summary>
    /// <param name="start">The typed start.</param>
    /// <param name="end">The typed end.</param>
    /// <param name="midpoint">The proleptic midpoint day.</param>
    [Theory]
    [InlineData("1400-02-28", "1400-03-02", "1400-03-09")]
    [InlineData("1582-09-25", "1582-10-25", "1582-10-15")]
    public void MidpointDate_BeforeTheSwitch_IsTheProlepticDayDerivedFromTheScan(string start, string end, string midpoint)
    {
        TransitInput input = new()
        {
            NatalChart = null!,
            ReportStartDate = DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture),
            ReportEndDate = DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture),
        };

        input.MidpointDate.Should().Be(DateOnly.Parse(midpoint, System.Globalization.CultureInfo.InvariantCulture));
        CalendarDateFormatter.Format(input.MidpointDate, "yyyy-MM-dd").Should().Be(
            start == "1400-02-28" ? "1400-02-29" : "1582-10-15");
    }

    /// <summary>A typed bound in 1582-10-05 to 1582-10-14 never occurred and is refused.</summary>
    [Fact]
    public void MidpointDate_BoundInTheTenDays_IsRefused()
    {
        TransitInput input = new()
        {
            NatalChart = null!,
            ReportStartDate = new DateOnly(1582, 10, 10),
            ReportEndDate = new DateOnly(1582, 10, 20),
        };

        Action act = () => _ = input.MidpointDate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*1582-10-10 does not exist*");
    }

    [Fact]
    public void MidpointDate_OddRange_Should_RoundDown()
    {
        // Arrange — 9-day range (Mar 1 to Mar 10): midpoint = 4.5, rounds down to 4
        TransitInput input = new()
        {
            NatalChart = null!,
            ReportStartDate = new DateOnly(2026, 3, 1),
            ReportEndDate = new DateOnly(2026, 3, 10),
        };

        // Act & Assert
        input.MidpointDate.Should().Be(new DateOnly(2026, 3, 5));
    }

    /// <summary>
    /// The three techniques default to on (branch Modes, 1.4.0 item 2; scoping C5a): a caller that sets
    /// none gets everything, as the one <c>IncludeProgressions</c> flag gave until 1.4.0.
    /// </summary>
    [Fact]
    public void Techniques_DefaultToAll()
    {
        TransitInput input = new()
        {
            NatalChart = MinimalChart(),
            ReportStartDate = new DateOnly(2026, 3, 1),
            ReportEndDate = new DateOnly(2026, 3, 31),
        };

        (input.IncludeTransits, input.IncludeSolarArcs, input.IncludeSecondaryProgressions).Should().Be((true, true, true));
    }

    /// <summary>
    /// The primary directions default to off with the default settings (branch PrimaryExport, 1.5.0; scoping
    /// decision 1), so every caller that sets nothing gets the three techniques it got in 1.4.0.
    /// </summary>
    [Fact]
    public void PrimaryDirections_DefaultOff()
    {
        TransitInput input = new()
        {
            NatalChart = MinimalChart(),
            ReportStartDate = new DateOnly(2026, 3, 1),
            ReportEndDate = new DateOnly(2026, 3, 31),
        };

        input.IncludePrimaryDirections.Should().BeFalse();
        input.PrimaryDirectionSettings.Should().BeSameAs(AstroCast.Core.Models.PrimaryDirections.PrimaryDirectionSettings.Default);
    }

    /// <summary>A minimally populated natal chart for the required <see cref="TransitInput.NatalChart"/>.</summary>
    /// <returns>The chart; its values are arbitrary but valid.</returns>
    private static ChartResult MinimalChart() => new()
    {
        Input = new ChartInput
        {
            Name = "Stub",
            LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Unspecified),
            TimeZone = "UTC",
            Latitude = 51.5,
            Longitude = -0.1,
            LocationName = "London",
        },
        JulianDay = 2451545.0,
        Obliquity = 23.4367,
        UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        Positions = [],
        Houses = new HouseData
        {
            Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
            Ascendant = 0.0,
            Midheaven = 270.0,
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
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}
