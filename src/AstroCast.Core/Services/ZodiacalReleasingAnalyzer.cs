using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.MedicalAstrology;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Computes zodiacal releasing timelines from the Lot of Spirit.
/// Pure static function — no ephemeris calls, no engine changes.
/// </summary>
/// <remarks>
/// <para>
/// Zodiacal releasing (Vettius Valens, <i>Anthology</i> IV) divides life into periods based on the Lot of
/// Spirit's zodiac sign. Each sign rules a period whose length comes from the "minor years" table (Aries 15,
/// Taurus 8, Gemini 20, Cancer 25, Leo 19, Virgo 20, Libra 8, Scorpio 15, Sagittarius 12, Capricorn 27,
/// Aquarius 30, Pisces 12; 211 years in all), the year 360 days and the month 30 (branch H, 1.3.4; ruling
/// T-D10).
/// </para>
/// <para>
/// Level 1 (major) periods advance through the zodiac from the Spirit sign, each for its years of 360 days
/// from the birth day (<see cref="FirdariaAnalyzer.BirthDayAndAge"/>: the local birth reading's day as
/// proleptic ticks; a reference date before it is refused, T6-6); the timeline closes with the first period
/// after the current one. Level 2 (sub) periods run within the current Level 1 period: from the Level 1 sign,
/// each sign for its years as 30-day months in zodiacal order; after one round of the twelve, 211 months,
/// the sequence passes to the sign opposite the Level 1 sign, the loosing of the bond, and continues in
/// order; the last sub-period is clamped to the Level 1 end. A sub-period's <see cref="ZRPeriod.Years"/> is
/// its months over 12.
/// </para>
/// <para>
/// Signs angular to the Lot of Fortune (the 1st, 4th, 7th and 10th from its sign) mark "peak" periods; the
/// 2nd, 5th, 8th and 11th are succedent and the 3rd, 6th, 9th and 12th cadent, "quiet" periods (T6-4).
/// </para>
/// <para>
/// Until 1.3.4 a year was 365.25 days from local midnight of the birth date, so every Level 1 date ran later
/// and later (3.5 to 5 months by age 30, 8 to 10 by 60); the Level 1 span was shared out among the twelve
/// signs in proportion to their years, with no loosing of the bond, so the Level 2 sign differed from
/// Valens's at 76% of dates (T6-3); the 5th and 11th from Fortune were called cadent and the 6th and 12th
/// succedent (T6-4); and a boundary before 1582-10-15 printed as proleptic ticks.
/// </para>
/// </remarks>
public static class ZodiacalReleasingAnalyzer
{
    #region Zodiacal Year Table

    /// <summary>Invariant culture used for every digit.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Minor years for each zodiac sign, used for period duration computation.
    /// Total cycle = 211 years.
    /// </summary>
    private static readonly Dictionary<ZodiacSign, int> ZodiacalYears = new()
    {
        [ZodiacSign.Aries] = 15,
        [ZodiacSign.Taurus] = 8,
        [ZodiacSign.Gemini] = 20,
        [ZodiacSign.Cancer] = 25,
        [ZodiacSign.Leo] = 19,
        [ZodiacSign.Virgo] = 20,
        [ZodiacSign.Libra] = 8,
        [ZodiacSign.Scorpio] = 15,
        [ZodiacSign.Sagittarius] = 12,
        [ZodiacSign.Capricorn] = 27,
        [ZodiacSign.Aquarius] = 30,
        [ZodiacSign.Pisces] = 12,
    };

    /// <summary>Sum of all zodiacal years (one full cycle): the months of one round of Level 2.</summary>
    private const int TotalCycleYears = 211;

    /// <summary>The days of a year of releasing (Valens).</summary>
    private const int DaysPerYear = 360;

    /// <summary>The days of a month of releasing (Valens).</summary>
    private const int DaysPerMonth = 30;

    #endregion

    /// <summary>
    /// Compute the zodiacal releasing timeline for a natal chart at a given reference date.
    /// </summary>
    /// <param name="chart">The natal chart result (must contain Arabic Parts with Lot of Spirit).</param>
    /// <param name="referenceDate">
    /// The date to compute releasing for (typically today); an instant of <see cref="DateTimeKind.Utc"/> is
    /// read on the chart's clock (<see cref="FirdariaAnalyzer.BirthDayAndAge"/>).
    /// </param>
    /// <returns>
    /// A <see cref="ZodiacalReleasingResult"/> with the full timeline, or a result with
    /// empty periods and a diagnostic summary if the Lot of Spirit is not available.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chart"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="referenceDate"/> is before the birth day (T6-6).
    /// </exception>
    public static ZodiacalReleasingResult Analyze(ChartResult chart, DateTime referenceDate)
    {
        ArgumentNullException.ThrowIfNull(chart);

        (DateOnly birthDay, DateOnly referenceDay, _) = FirdariaAnalyzer.BirthDayAndAge(chart, referenceDate);
        DateTime birthDate = birthDay.ToDateTime(TimeOnly.MinValue);
        DateTime referenceClock = referenceDay.ToDateTime(TimeOnly.MinValue);

        // Find Lot of Spirit and Lot of Fortune
        ArabicPart? lotOfSpirit = FindLot(chart, "Spirit");
        ArabicPart? lotOfFortune = FindLot(chart, "Fortune");

        if (lotOfSpirit is null)
            return CreateEmptyResult(birthDate, referenceClock);

        ZodiacSign spiritSign = GetSignFromLongitude(lotOfSpirit.Longitude);
        ZodiacSign fortuneSign = lotOfFortune is not null
            ? GetSignFromLongitude(lotOfFortune.Longitude)
            : ZodiacSign.Aries; // Fallback; peak detection will be less meaningful

        string spiritSignName = spiritSign.ToString();
        string fortuneSignName = fortuneSign.ToString();

        // Build Level 1 timeline
        List<ZRPeriod> level1Periods = BuildLevel1Timeline(
            spiritSign, fortuneSign, birthDate, referenceClock);

        // The current Level 1 period: the reference date is on or after the birth day, so one holds it.
        ZRPeriod? currentL1 = level1Periods.FirstOrDefault(p => p.IsCurrent);
        if (currentL1 is null && level1Periods.Count > 0)
            currentL1 = level1Periods[^1]; // Fallback to last

        // Build Level 2 sub-periods for current Level 1
        List<ZRPeriod> l2Periods = [];
        ZRPeriod? currentL2 = null;

        if (currentL1 is not null)
        {
            ZodiacSign l1Sign = Enum.Parse<ZodiacSign>(currentL1.Sign);
            l2Periods = BuildLevel2Timeline(
                l1Sign, fortuneSign, currentL1.StartDate, currentL1.EndDate, referenceClock);
            currentL2 = l2Periods.FirstOrDefault(p => p.IsCurrent);
        }

        string summary = BuildSummary(currentL1, currentL2, spiritSignName, fortuneSignName);

        return new ZodiacalReleasingResult
        {
            LotOfSpiritSign = spiritSignName,
            LotOfSpiritLongitude = lotOfSpirit.Longitude,
            LotOfFortuneSign = fortuneSignName,
            BirthDate = birthDate,
            ReferenceDate = referenceClock,
            CurrentL1Sign = currentL1?.Sign ?? string.Empty,
            CurrentL1Ruler = currentL1?.Ruler ?? string.Empty,
            CurrentL1Start = currentL1?.StartDate ?? birthDate,
            CurrentL1End = currentL1?.EndDate ?? birthDate,
            CurrentL1Years = currentL1 is not null ? (int)currentL1.Years : 0,
            CurrentL1IsPeak = currentL1?.IsPeak ?? false,
            CurrentL1Angularity = currentL1?.Angularity ?? string.Empty,
            CurrentL2Sign = currentL2?.Sign ?? string.Empty,
            CurrentL2Ruler = currentL2?.Ruler ?? string.Empty,
            CurrentL2Start = currentL2?.StartDate ?? birthDate,
            CurrentL2End = currentL2?.EndDate ?? birthDate,
            CurrentL2IsPeak = currentL2?.IsPeak ?? false,
            CurrentL2Angularity = currentL2?.Angularity ?? string.Empty,
            Level1Periods = level1Periods,
            CurrentL2Periods = l2Periods,
            Summary = summary,
        };
    }

    #region Level 1 Timeline

    /// <summary>
    /// Builds the Level 1 (major period) timeline starting from the Spirit sign: each sign for its years of
    /// 360 days from the birth day, the next sign following, until the first period after the current one.
    /// </summary>
    private static List<ZRPeriod> BuildLevel1Timeline(
        ZodiacSign spiritSign,
        ZodiacSign fortuneSign,
        DateTime birthDate,
        DateTime referenceDate)
    {
        List<ZRPeriod> periods = [];
        DateTime cursor = birthDate;
        ZodiacSign currentSign = spiritSign;

        while (true)
        {
            int years = ZodiacalYears[currentSign];
            DateTime periodStart = cursor;
            DateTime periodEnd = cursor.AddDays(years * DaysPerYear);

            bool isCurrent = referenceDate.Date >= periodStart.Date &&
                             referenceDate.Date < periodEnd.Date;

            string angularity = GetAngularity(currentSign, fortuneSign);
            bool isPeak = angularity == "Angular";
            string ruler = GetTraditionalRulerName(currentSign);

            periods.Add(new ZRPeriod
            {
                Sign = currentSign.ToString(),
                Ruler = ruler,
                Years = years,
                StartDate = periodStart,
                EndDate = periodEnd,
                IsCurrent = isCurrent,
                IsPeak = isPeak,
                Angularity = angularity,
            });

            cursor = periodEnd;

            // The timeline closes with the first period after the current one.
            if (!isCurrent && periods.Any(p => p.IsCurrent))
                break;

            // Advance to next sign
            currentSign = NextSign(currentSign);
        }

        return periods;
    }

    #endregion

    #region Level 2 Timeline

    /// <summary>
    /// Builds Level 2 sub-periods within a Level 1 period as Valens has them: from the Level 1 sign, each
    /// sign for its years as 30-day months in zodiacal order; after one round of 211 months the sequence
    /// passes to the sign opposite the Level 1 sign (the loosing of the bond) and continues in order; the
    /// last sub-period is clamped to the Level 1 end. A sub-period's Years is its months over 12.
    /// </summary>
    /// <remarks>
    /// The residual fold (E1-4) is kept as it was: where a clamped sub-period would be shorter than a day, the
    /// previous sub-period runs to the Level 1 end instead. It is unreachable now that whole months tile a
    /// Level 1 period of whole years (a clamped remainder is a whole number of months or nothing); until
    /// 1.3.3 a residual of less than a day, left by summing fractional days, was printed as a last
    /// sub-period starting and ending on the Level 1 end's date.
    /// </remarks>
    /// <param name="l1Sign">The Level 1 period's sign, where the Level 2 sequence starts.</param>
    /// <param name="fortuneSign">The Lot of Fortune's sign, for angularity.</param>
    /// <param name="l1Start">The Level 1 period's start.</param>
    /// <param name="l1End">The Level 1 period's end.</param>
    /// <param name="referenceDate">The date the current sub-period is marked for.</param>
    /// <returns>The Level 2 sub-periods, the last ending on <paramref name="l1End"/>.</returns>
    private static List<ZRPeriod> BuildLevel2Timeline(
        ZodiacSign l1Sign,
        ZodiacSign fortuneSign,
        DateTime l1Start,
        DateTime l1End,
        DateTime referenceDate)
    {
        List<ZRPeriod> periods = [];
        DateTime cursor = l1Start;
        ZodiacSign currentSign = l1Sign;
        int monthsRun = 0;

        while (cursor < l1End)
        {
            int signMonths = ZodiacalYears[currentSign];
            DateTime subStart = cursor;
            DateTime subEnd = cursor.AddDays(signMonths * DaysPerMonth);

            // Clamp to L1 end
            bool clamped = subEnd > l1End;
            if (clamped)
                subEnd = l1End;

            // A clamped residual of less than a day, left by the fractional sum, is not a period:
            // the previous sub-period runs to the Level 1 end instead (E1-4; unreachable now that months
            // tile the period, kept as it is).
            if (clamped && (subEnd - subStart).TotalDays < 1.0 && periods.Count > 0)
            {
                ZRPeriod previous = periods[^1];
                periods[^1] = new ZRPeriod
                {
                    Sign = previous.Sign,
                    Ruler = previous.Ruler,
                    Years = (l1End - previous.StartDate).TotalDays / DaysPerMonth / 12.0,
                    StartDate = previous.StartDate,
                    EndDate = l1End,
                    IsCurrent = referenceDate.Date >= previous.StartDate.Date &&
                                referenceDate.Date < l1End.Date,
                    IsPeak = previous.IsPeak,
                    Angularity = previous.Angularity,
                };
                break;
            }

            bool isCurrent = referenceDate.Date >= subStart.Date &&
                             referenceDate.Date < subEnd.Date;

            string angularity = GetAngularity(currentSign, fortuneSign);
            bool isPeak = angularity == "Angular";
            string ruler = GetTraditionalRulerName(currentSign);
            double months = (subEnd - subStart).TotalDays / DaysPerMonth;

            periods.Add(new ZRPeriod
            {
                Sign = currentSign.ToString(),
                Ruler = ruler,
                Years = months / 12.0,
                StartDate = subStart,
                EndDate = subEnd,
                IsCurrent = isCurrent,
                IsPeak = isPeak,
                Angularity = angularity,
            });

            cursor = subEnd;
            monthsRun += signMonths;

            // After one round of the twelve signs, 211 months, the bond is loosed: the sequence passes to the
            // sign opposite the Level 1 sign. (A Level 1 period is at most 360 months, so one round at most.)
            currentSign = monthsRun == TotalCycleYears
                ? OppositeSign(l1Sign)
                : NextSign(currentSign);
        }

        return periods;
    }

    #endregion

    #region Angularity

    /// <summary>
    /// Determines the angularity of a period sign relative to the Fortune sign: angular in the 1st, 4th, 7th
    /// and 10th places from it (same sign, square, opposition), succedent in the 2nd, 5th, 8th and 11th,
    /// cadent in the 3rd, 6th, 9th and 12th.
    /// </summary>
    /// <param name="periodSign">The sign of the current period.</param>
    /// <param name="fortuneSign">The sign of the Lot of Fortune.</param>
    /// <returns>"Angular", "Succedent", or "Cadent".</returns>
    internal static string GetAngularity(ZodiacSign periodSign, ZodiacSign fortuneSign)
    {
        int distance = (((int)periodSign - (int)fortuneSign) % 12 + 12) % 12;
        return distance switch
        {
            0 or 3 or 6 or 9 => "Angular",
            1 or 4 or 7 or 10 => "Succedent",
            2 or 5 or 8 or 11 => "Cadent",
            _ => "Unknown"
        };
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Finds a Lot (Arabic Part) by partial name match in the chart's Arabic Parts.
    /// </summary>
    private static ArabicPart? FindLot(ChartResult chart, string keyword)
    {
        return chart.ArabicParts.FirstOrDefault(ap =>
            ap.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Converts a longitude to a zodiac sign.
    /// </summary>
    private static ZodiacSign GetSignFromLongitude(double longitude)
    {
        double normalized = ((longitude % 360.0) + 360.0) % 360.0;
        return (ZodiacSign)((int)(normalized / 30.0) + 1);
    }

    /// <summary>
    /// Advances to the next zodiac sign (Pisces wraps to Aries).
    /// </summary>
    private static ZodiacSign NextSign(ZodiacSign sign)
    {
        int next = (int)sign + 1;
        return next > 12 ? ZodiacSign.Aries : (ZodiacSign)next;
    }

    /// <summary>
    /// The sign opposite another, six signs on.
    /// </summary>
    private static ZodiacSign OppositeSign(ZodiacSign sign) =>
        (ZodiacSign)(((int)sign - 1 + 6) % 12 + 1);

    /// <summary>
    /// Gets the traditional (Ptolemaic) ruler name for a zodiac sign.
    /// </summary>
    private static string GetTraditionalRulerName(ZodiacSign sign)
    {
        CelestialBodyId ruler = ElementHelper.GetTraditionalRuler(sign);
        return ruler switch
        {
            CelestialBodyId.Sun => "Sun",
            CelestialBodyId.Moon => "Moon",
            CelestialBodyId.Mercury => "Mercury",
            CelestialBodyId.Venus => "Venus",
            CelestialBodyId.Mars => "Mars",
            CelestialBodyId.Jupiter => "Jupiter",
            CelestialBodyId.Saturn => "Saturn",
            _ => ruler.ToString()
        };
    }

    /// <summary>
    /// Creates an empty result when the Lot of Spirit is not available.
    /// </summary>
    private static ZodiacalReleasingResult CreateEmptyResult(
        DateTime birthDate, DateTime referenceDate)
    {
        return new ZodiacalReleasingResult
        {
            LotOfSpiritSign = string.Empty,
            LotOfSpiritLongitude = 0.0,
            LotOfFortuneSign = string.Empty,
            BirthDate = birthDate,
            ReferenceDate = referenceDate.Date,
            CurrentL1Sign = string.Empty,
            CurrentL1Ruler = string.Empty,
            CurrentL1Start = birthDate,
            CurrentL1End = birthDate,
            CurrentL1Years = 0,
            CurrentL1IsPeak = false,
            CurrentL1Angularity = string.Empty,
            CurrentL2Sign = string.Empty,
            CurrentL2Ruler = string.Empty,
            CurrentL2Start = birthDate,
            CurrentL2End = birthDate,
            CurrentL2IsPeak = false,
            CurrentL2Angularity = string.Empty,
            Level1Periods = [],
            CurrentL2Periods = [],
            Summary = "Zodiacal Releasing from Spirit: Lot of Spirit not available in this chart.",
        };
    }

    /// <summary>
    /// Builds the narrative summary for the current zodiacal releasing state, its dates on the calendar in
    /// force.
    /// </summary>
    private static string BuildSummary(
        ZRPeriod? l1, ZRPeriod? l2, string spiritSign, string fortuneSign)
    {
        if (l1 is null)
            return "Zodiacal Releasing from Spirit: Lot of Spirit not available in this chart.";

        string peakNote = l1.IsPeak
            ? " This is a peak (angular) period — heightened activity and visibility."
            : " This is a background period — quieter, preparatory themes.";

        string l2Part = l2 is not null
            ? string.Create(Inv, $" Level 2: {l2.Sign} sub-period ({CalendarDateFormatter.Date(l2.StartDate)}–{CalendarDateFormatter.Date(l2.EndDate)}, " +
              $"ruled by {l2.Ruler}, {l2.Angularity}).")
            : string.Empty;

        return string.Create(Inv, $"Zodiacal Releasing from Spirit ({spiritSign}): Currently in a {l1.Sign} " +
               $"Level 1 period ({CalendarDateFormatter.Format(l1.StartDate, "yyyy")}–{CalendarDateFormatter.Format(l1.EndDate, "yyyy")}, {(int)l1.Years}y, " +
               $"{l1.Angularity}), ruled by {l1.Ruler}.{l2Part}{peakNote}");
    }

    #endregion
}
