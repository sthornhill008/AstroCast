using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates the progressed date using the secondary progression method
/// (one day of ephemeris motion = one year of life).
/// </summary>
/// <remarks>
/// <para>
/// Secondary progressions use the "day-for-a-year" rule: the planetary positions
/// N days after birth correspond to the progressed positions at age N years.
/// For example, the chart for 36 days after birth shows the progressed positions
/// for age 36.
/// </para>
/// <para>
/// The arithmetic is on Julian Days (branch R2, 1.3.4): the age in days is the target day's noon
/// Julian Day less the natal chart's Julian Day, so a natal chart before 1582-10-15 counts its days
/// from its instant. The target is a proleptic day derived from the scan, the report's midpoint
/// (branch R3), converted by arithmetic. Until 1.3.4 it was <see cref="DateTime"/> arithmetic on
/// the natal <c>UtcDateTime</c>, then a Julian label before the switch held as proleptic ticks,
/// which counted 8 days too many across the switch (N1 at 1600-03-15: 299.994524 years for
/// 299.972621).
/// </para>
/// </remarks>
public static class ProgressedDateCalculator
{
    /// <summary>
    /// The number of days per tropical year used for day-for-a-year conversion.
    /// </summary>
    private const double DaysPerYear = 365.25;

    /// <summary>
    /// Calculates the progressed instant for a natal chart and a target day.
    /// </summary>
    /// <param name="natalJulianDay">The natal chart's Julian Day (UT).</param>
    /// <param name="targetDate">
    /// The day to calculate progressions for: the report's midpoint, a proleptic day derived from the
    /// scan (<c>TransitInput.MidpointDate</c>), never a typed date.
    /// </param>
    /// <returns>
    /// The progressed instant, its Julian Day, and the age in decimal years.
    /// </returns>
    /// <remarks>
    /// The algorithm:
    /// <list type="number">
    /// <item>The age in days: the target day's noon Julian Day, by arithmetic
    /// (<see cref="JulianDayUtility.InstantToJulianDay"/>), less the natal Julian Day; in years, that
    /// over 365.25.</item>
    /// <item>The progressed Julian Day: the natal Julian Day plus that many days (day-for-a-year).</item>
    /// <item>The progressed instant: the natal instant (<see cref="JulianDayUtility.JulianDayToInstant"/>
    /// of the natal Julian Day, a whole second) plus the age in days, to the tick, so the instant the
    /// export prints to the minute is not rounded to the second first (2000-01-27 18:51:59.6 prints
    /// 18:51, as it always has).</item>
    /// </list>
    /// </remarks>
    public static (DateTime ProgressedInstant, double ProgressedJulianDay, double AgeInYears) Calculate(
        double natalJulianDay, DateOnly targetDate)
    {
        double targetNoonJd = JulianDayUtility.InstantToJulianDay(targetDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));
        double ageInYears = (targetNoonJd - natalJulianDay) / DaysPerYear;
        double progressedJd = natalJulianDay + ageInYears;

        return (JulianDayUtility.JulianDayToInstant(natalJulianDay).AddDays(ageInYears), progressedJd, ageInYears);
    }

    /// <summary>
    /// Converts a real-world date offset (in days from the report midpoint) to the
    /// corresponding progressed date offset. Each real-world year maps to one
    /// progressed day, so each real-world day maps to 1/365.25 of a progressed day.
    /// </summary>
    /// <param name="realWorldDays">Number of real-world days from the midpoint.</param>
    /// <returns>Number of progressed days from the midpoint progressed date.</returns>
    public static double RealDaysToProgressedDays(double realWorldDays) =>
        realWorldDays / DaysPerYear;

    /// <summary>
    /// Converts progressed days offset back to real-world days.
    /// </summary>
    /// <param name="progressedDays">Number of progressed days.</param>
    /// <returns>Number of real-world days.</returns>
    public static double ProgressedDaysToRealDays(double progressedDays) =>
        progressedDays * DaysPerYear;
}
