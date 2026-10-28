using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProgressedDateCalculator"/>: the age in days is the target day's noon
/// Julian Day less the natal Julian Day, the progressed Julian Day the natal one plus the age in years,
/// and the progressed instant the natal instant plus the age in days (branch R2, 1.3.4). The target
/// is a proleptic day derived from the scan, converted by arithmetic (branch R3).
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ProgressedDateCalculatorTests
{
    /// <summary>J2000.0, 2000-01-01 12:00 UT.</summary>
    private const double J2000 = 2451545.0;

    /// <summary>N1, Julian 1300-03-15 12:00 UT at Greenwich (Tier B, r2_truth.out).</summary>
    private const double N1 = 2195957.0;

    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture, for the natal Julian Day of a reading.
    /// </summary>
    public ProgressedDateCalculatorTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// At age 36, the progressed Julian Day is 36 days after birth: 2036-01-01 noon is 13,149 days
    /// after J2000.0, 36.0 years exactly.
    /// </summary>
    [Fact]
    public void Calculate_Age36_Should_Return36DaysAfterBirth()
    {
        (DateTime progressedInstant, double progressedJd, double ageInYears) =
            ProgressedDateCalculator.Calculate(J2000, new DateOnly(2036, 1, 1));

        ageInYears.Should().BeApproximately(36.0, 0.01, "the target is ~36 years after birth");
        (progressedJd - J2000).Should().BeApproximately(36.0, 0.01, "day-for-a-year: 36 years = 36 progressed days");
        progressedInstant.Should().BeCloseTo(JulianDayUtility.JulianDayToInstant(progressedJd), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// At age 0, the progressed instant is the natal one.
    /// </summary>
    [Fact]
    public void Calculate_Age0_Should_ReturnNatalDate()
    {
        (DateTime progressedInstant, double progressedJd, double ageInYears) =
            ProgressedDateCalculator.Calculate(J2000, new DateOnly(2000, 1, 1));

        ageInYears.Should().BeApproximately(0.0, 0.01, "the target is the same day as birth");
        progressedJd.Should().BeApproximately(J2000, 1e-6);
        progressedInstant.Should().BeCloseTo(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc), TimeSpan.FromMinutes(1),
            "the progressed instant at age 0 is the natal instant");
    }

    /// <summary>
    /// A fractional age (10.5 years) gives a fractional progressed offset: born 2000-06-15 08:30 UT,
    /// read at 2010-12-15.
    /// </summary>
    [Fact]
    public void Calculate_FractionalAge_Should_HandleCorrectly()
    {
        double natalJd = _fixture.EphemerisService.DateToJulianDay(2000, 6, 15, 8.5);

        (_, double progressedJd, double ageInYears) =
            ProgressedDateCalculator.Calculate(natalJd, new DateOnly(2010, 12, 15));

        ageInYears.Should().BeApproximately(10.5, 0.05, "the target is ~10.5 years after birth");
        (progressedJd - natalJd).Should().BeApproximately(10.5, 0.05, "day-for-a-year: 10.5 years = 10.5 progressed days");
    }

    /// <summary>
    /// N1 (Julian 1300-03-15 12:00 UT, JD 2195957.0) at the scan's proleptic day 1600-03-15: the day's
    /// noon is JD 2305522.0, 109,565 days on, 299.972621 years, progressed JD 2196256.972621; and at
    /// proleptic 1330-03-23, the day of Julian 1330-03-15: 10,957 days, 29.998631 years, progressed JD
    /// 2195986.998631 (Tier B, r2_truth.out, r3_truth2.out). The target is the report's midpoint, a
    /// proleptic day derived from the scan (branch R3, 1.3.4), converted by arithmetic. Until 1.3.4 the
    /// age across the switch was counted from the Julian label's ticks, 8 days too many: 299.994524
    /// years and 2196256.994524.
    /// </summary>
    /// <param name="year">The proleptic day's year.</param>
    /// <param name="month">Its month.</param>
    /// <param name="day">Its day.</param>
    /// <param name="age">The age in years.</param>
    /// <param name="expectedJd">The progressed Julian Day.</param>
    [Theory]
    [InlineData(1600, 3, 15, 299.972621492, 2196256.972621492)]
    [InlineData(1330, 3, 23, 29.998631075, 2195986.998631075)]
    public void Calculate_N1_Should_CountTheAgeFromTheNatalInstantToTheDaysNoon(
        int year, int month, int day, double age, double expectedJd)
    {
        (DateTime progressedInstant, double progressedJd, double ageInYears) =
            ProgressedDateCalculator.Calculate(N1, new DateOnly(year, month, day));

        ageInYears.Should().BeApproximately(age, 1e-6);
        progressedJd.Should().BeApproximately(expectedJd, 1e-6);
        progressedInstant.Should().BeCloseTo(JulianDayUtility.JulianDayToInstant(progressedJd), TimeSpan.FromSeconds(1),
            "the instant is the natal instant plus the age in days, to the tick");
    }
}
