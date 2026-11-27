using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.MedicalAstrology;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Computes annual profection analysis for a natal chart.
/// Pure static function — no ephemeris calls, no engine changes.
/// </summary>
/// <remarks>
/// <para>
/// Annual profections advance the chart one sign a year from the sign of the Ascendant (branch H, 1.3.4;
/// ruling T-D9 option b): the profected house is age mod 12 + 1, its sign the Ascendant's sign plus
/// (house − 1) signs, counted in whole signs, and the traditional ruler of that sign is the Lord of the Year,
/// governing the primary themes of the year. The house cusps are not read. The age is the count of completed
/// anniversaries of the birth day (<see cref="FirdariaAnalyzer.BirthDayAndAge"/>, R-D6), and a reference date
/// before the birth day is refused (T6-6).
/// </para>
/// <para>
/// Until 1.3.4 the profected sign was the sign on the profected house's cusp, so with any quadrant system
/// signs were skipped and repeated (T6-2: a saved quadrant chart read the cusp's sign and ruler where one
/// sign a year gives another), and a reference before birth threw from a cusp index.
/// </para>
/// </remarks>
public static class ProfectionAnalyzer
{
    #region House Themes

    /// <summary>
    /// Returns the traditional house themes for a given house number.
    /// </summary>
    /// <param name="house">House number (1–12).</param>
    /// <returns>A comma-separated string of themes.</returns>
    private static string GetHouseThemes(int house) => house switch
    {
        1 => "Self, body, identity, personal initiatives",
        2 => "Money, possessions, values, personal resources",
        3 => "Siblings, neighbors, communication, short travel, learning",
        4 => "Home, family, father, real estate, private life, endings",
        5 => "Children, creativity, romance, pleasure, speculation",
        6 => "Health, daily work, service, illness, routines, small animals",
        7 => "Partnerships, marriage, open enemies, contracts, the other person",
        8 => "Death, inheritance, shared resources, transformation, surgery, debt",
        9 => "Travel, higher education, philosophy, religion, publishing, law",
        10 => "Career, reputation, authority, public standing, mother",
        11 => "Friends, groups, hopes and wishes, community, income from career",
        12 => "Hidden enemies, isolation, self-undoing, spirituality, large animals, secrets",
        _ => string.Empty,
    };

    #endregion

    /// <summary>
    /// Compute the profection year analysis for a natal chart at a given reference date.
    /// </summary>
    /// <param name="chart">The natal chart result.</param>
    /// <param name="referenceDate">
    /// The date to compute profections for (typically today); an instant of <see cref="DateTimeKind.Utc"/>
    /// is read on the chart's clock (<see cref="FirdariaAnalyzer.BirthDayAndAge"/>).
    /// </param>
    /// <returns>A <see cref="ProfectionYearResult"/> with the full profection analysis.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chart"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="referenceDate"/> is before the birth day (T6-6).
    /// </exception>
    /// <remarks>
    /// Uses natal profection age semantics: the age is the count of completed anniversaries of the birth day
    /// at the reference date (<see cref="FirdariaAnalyzer.BirthDayAndAge"/>): the year running at the
    /// reference date. For a solar return, where the year-of-life boundary is the return's instant rather
    /// than the calendar anniversary, use <see cref="AnalyzeForReturn"/> instead; a lunar or planetary
    /// return reads the year running at the return, this method.
    /// </remarks>
    public static ProfectionYearResult Analyze(ChartResult chart, DateTime referenceDate)
    {
        ArgumentNullException.ThrowIfNull(chart);

        // Step 1 — Compute age using natal semantics: the completed anniversaries of the birth day at the
        // reference date (R-D6), refusing a reference before the birth day.
        (DateOnly birthDay, DateOnly referenceDay, int age) = FirdariaAnalyzer.BirthDayAndAge(chart, referenceDate);

        return BuildResult(chart, referenceDay, age, birthDay);
    }

    /// <summary>
    /// Compute the profection year analysis for a Solar Return chart, where the
    /// return's instant defines the year-of-life boundary regardless of the
    /// calendar relationship to the natal anniversary.
    /// </summary>
    /// <param name="chart">The natal chart result.</param>
    /// <param name="returnIngressUtc">The exact UTC moment of the return ingress.</param>
    /// <returns>A <see cref="ProfectionYearResult"/> computed using SR-correct age semantics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chart"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="returnIngressUtc"/> is before the birth day (T6-6).
    /// </exception>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="Analyze"/>, this method reads the year the return opens,
    /// which <see cref="YearOpenedBySolarReturn"/> decides: the age is the count of
    /// years from the birth instant rounded to the nearest whole, right even when
    /// the return falls a day or two before or after the calendar birthday, a
    /// regular occurrence because the return drifts by hours each year; and
    /// <see cref="ProfectionYearResult.ReferenceDate"/>, the "as of" date the
    /// export prints, is the day that year is read on, the birthday it begins on
    /// when the return falls before it (branch ReturnAges, 1.7.2; until then the
    /// return's own day). Only the solar return export reads this way, in all
    /// three of its time-lord sections: a lunar or planetary return's profection
    /// is the year running at the return, <see cref="Analyze"/> (branch H, 1.3.4).
    /// </para>
    /// <para>
    /// Worked example: a native born 1947-10-05 with a solar return at 2026-10-04
    /// 17:18:50 UTC, the day before the calendar birthday. Natal
    /// <see cref="Analyze"/> at the return gives age 78, because the return's day
    /// is the day before the 79th anniversary. <see cref="AnalyzeForReturn"/>
    /// gives age 79, as of 2026-10-05: the return opens the 80th year of life,
    /// entered at age 79 on that birthday.
    /// </para>
    /// </remarks>
    public static ProfectionYearResult AnalyzeForReturn(
        ChartResult chart, DateTime returnIngressUtc)
    {
        ArgumentNullException.ThrowIfNull(chart);

        (DateOnly birthDay, int age, _, DateOnly readingDay) = YearOpenedBySolarReturn(chart, returnIngressUtc);

        return BuildResult(chart, readingDay, age, birthDay);
    }

    /// <summary>
    /// The year of life a solar return opens, and the day its time lords are read on: the one place that
    /// decides both (branch ReturnAges, 1.7.2; finding A1 of the 1.7.2 code audit, Steve's ruling).
    /// <see cref="AnalyzeForReturn"/> takes its age and its reference day from here, and the solar return
    /// export reads its Firdaria and its Zodiacal Releasing on the same day, so its three sections are one
    /// year's.
    /// </summary>
    /// <param name="chart">The natal chart.</param>
    /// <param name="returnIngressUtc">The solar return's instant, UTC.</param>
    /// <returns>
    /// The birth day as proleptic ticks; the age of the year the return opens; the birthday that year begins
    /// on, the birth day plus that many years; and the day the year is read on, the later of that birthday and
    /// the return's day on the chart's clock.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the return's day on the chart's clock is before the birth day (T6-6).
    /// </exception>
    /// <remarks>
    /// <para>
    /// The age is the count of years from the birth instant, <see cref="ChartResult.UtcDateTime"/>, to the
    /// return's, rounded to the nearest whole with the Gregorian mean year of 365.2425 days: a solar return
    /// lies within a day or two of its birthday, so the rounding names the year it opens whichever side of
    /// the birthday it falls on. The birth day and the return's day are read by
    /// <see cref="FirdariaAnalyzer.BirthDayAndAge"/>, on the chart's clock.
    /// </para>
    /// <para>
    /// A return that falls before its birthday is read on the birthday, where the year it opens begins: the
    /// age counted in completed birthdays on that day is the rounded age, and a Firdaria or releasing period
    /// that begins on that birthday is the one current. A return on its birthday or after it is read on its
    /// own day, as it was until 1.7.2. Person A (born 1947-10-05 at UTC-5), solar return 2026 at 2026-10-04
    /// 17:18:50 UTC: age 79, the birthday 2026-10-05, read on 2026-10-05. Until 1.7.2 only the profection's
    /// age was the year the return opens; the Firdaria and the releasing counted completed birthdays on the
    /// return's own day, so that export said 79 in one section and 78 in the next.
    /// </para>
    /// </remarks>
    internal static (DateOnly BirthDay, int Age, DateOnly YearStart, DateOnly ReadingDay) YearOpenedBySolarReturn(
        ChartResult chart, DateTime returnIngressUtc)
    {
        // The birth day, the return's day on the chart's clock, and the refusal of a return before the birth
        // day; the age is not this method's count of completed birthdays but the rounding below.
        (DateOnly birthDay, DateOnly returnDay, _) = FirdariaAnalyzer.BirthDayAndAge(chart, returnIngressUtc);

        // Age = count of years from the birth instant rounded to the nearest natal
        // anniversary using the Gregorian mean year. For SR ingress within
        // ±2 days of the calendar birthday, this resolves unambiguously to
        // the year-of-life that the SR governs.
        double yearsSinceBirth = (returnIngressUtc - chart.UtcDateTime).TotalDays / 365.2425;
        int age = (int)Math.Round(yearsSinceBirth);

        DateOnly yearStart = birthDay.AddYears(age);
        DateOnly readingDay = returnDay > yearStart ? returnDay : yearStart;

        return (birthDay, age, yearStart, readingDay);
    }

    /// <summary>
    /// Builds the full <see cref="ProfectionYearResult"/> for a given chart,
    /// reference date, and pre-computed age. Shared between <see cref="Analyze"/>
    /// (natal semantics) and <see cref="AnalyzeForReturn"/> (return-ingress
    /// semantics): the two entry points differ only in how <paramref name="age"/>
    /// and <paramref name="referenceDay"/> are derived.
    /// </summary>
    /// <param name="chart">The natal chart result.</param>
    /// <param name="referenceDay">The reference day the age was taken on, to record on the result.</param>
    /// <param name="age">The pre-computed age in years.</param>
    /// <param name="birthDay">The birth day, as proleptic ticks, to record on the result.</param>
    /// <returns>A fully-populated <see cref="ProfectionYearResult"/>.</returns>
    private static ProfectionYearResult BuildResult(
        ChartResult chart, DateOnly referenceDay, int age, DateOnly birthDay)
    {
        DateTime birthDate = birthDay.ToDateTime(TimeOnly.MinValue);

        // Step 2 — Compute profected house
        int profectionCycleYear = age % 12;
        int profectedHouse = profectionCycleYear + 1; // House 1 at age 0

        // Step 3 — The profected sign, one sign a year from the Ascendant's sign, and its lord (T-D9)
        ZodiacSign ascendantSign = GetSignFromLongitude(chart.Houses.Ascendant);
        ZodiacSign profectedSign = SignAfter(ascendantSign, profectedHouse - 1);
        CelestialBodyId lordBody = ElementHelper.GetTraditionalRuler(profectedSign);
        string lordName = lordBody.ToString();
        string activatedThemes = GetHouseThemes(profectedHouse);

        // Step 4 — Get the lord's natal position
        PlanetPosition? lordPos = chart.Positions.FirstOrDefault(p =>
            p.Name.Equals(lordName, StringComparison.OrdinalIgnoreCase));

        string lordNatalSign = lordPos?.Sign.ToString() ?? "Unknown";
        int lordNatalHouse = lordPos?.House ?? 0;
        bool lordRetrograde = lordPos?.IsRetrograde ?? false;

        // Step 5 — Get the lord's dignity
        DignityInfo? lordDignity = chart.Dignities.FirstOrDefault(d =>
            d.PlanetName.Equals(lordName, StringComparison.OrdinalIgnoreCase));

        string lordDignityLabel = lordDignity is not null
            ? string.Create(CultureInfo.InvariantCulture, $"{lordDignity.Label} ({lordDignity.Score:+0;-0;0})")
            : "Unknown";
        int lordDignityScore = lordDignity?.Score ?? 0;

        // Step 6 — Get the lord's accidental conditions
        PlanetCondition? lordCondition = chart.PlanetConditions.FirstOrDefault(c =>
            c.PlanetName.Equals(lordName, StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<string> lordConditions = lordCondition?.Conditions
            ?? (IReadOnlyList<string>)Array.Empty<string>();
        int lordAccidentalScore = lordCondition?.AccidentalScore ?? 0;

        // Step 7 — Get sect status
        bool lordInSect = false;
        if (chart.ChartContext?.PlanetarySect is not null)
        {
            SectInfo? sectInfo = chart.ChartContext.PlanetarySect.FirstOrDefault(s =>
                s.PlanetName.Equals(lordName, StringComparison.OrdinalIgnoreCase));
            lordInSect = sectInfo?.IsInSect ?? false;
        }

        // Step 8 — Get lord's natal aspects
        List<string> lordAspects = [];
        foreach (AspectResult aspect in chart.Aspects)
        {
            bool involvesLord =
                aspect.Body1Name.Equals(lordName, StringComparison.OrdinalIgnoreCase) ||
                aspect.Body2Name.Equals(lordName, StringComparison.OrdinalIgnoreCase);

            if (!involvesLord)
                continue;

            string otherBody = aspect.Body1Name.Equals(lordName, StringComparison.OrdinalIgnoreCase)
                ? aspect.Body2Name
                : aspect.Body1Name;

            string apSp = aspect.IsApplying ? "Ap" : "Sp";
            lordAspects.Add(
                $"{lordName} {aspect.Aspect.Type} {otherBody} (orb {PositionFormatter.FormatOrb(aspect.Orb)}, {apSp})");
        }

        // Step 9 — Build 12-year profection table: row h carries the sign h − 1 after the Ascendant's
        int cycleStart = age - profectionCycleYear;
        List<ProfectionTableEntry> table = [];
        for (int h = 1; h <= 12; h++)
        {
            ZodiacSign hSign = SignAfter(ascendantSign, h - 1);
            CelestialBodyId hLord = ElementHelper.GetTraditionalRuler(hSign);
            int hCycleYear = h - 1;

            // Build ages string: list representative ages (0,12,24,... pattern) with current marked
            List<int> ageList = [];
            int startAge = hCycleYear; // first occurrence
            for (int a = startAge; a <= 120; a += 12)
                ageList.Add(a);

            // Mark the current cycle's age with an asterisk
            int currentCycleAge = cycleStart + hCycleYear;
            string agesStr = string.Join(", ", ageList.Select(a =>
                a == currentCycleAge ? string.Create(CultureInfo.InvariantCulture, $"{a}*") : a.ToString(CultureInfo.InvariantCulture)));

            bool isCurrent = h == profectedHouse;

            table.Add(new ProfectionTableEntry
            {
                House = h,
                Sign = hSign.ToString(),
                Lord = hLord.ToString(),
                Ages = agesStr,
                IsCurrent = isCurrent,
                Themes = GetHouseThemes(h),
            });
        }

        // Step 10 — Build narrative summary
        string summary = BuildSummary(
            chart.Input.Name, age, profectedHouse, profectedSign.ToString(),
            lordName, lordNatalSign, lordNatalHouse, lordDignityLabel,
            lordDignityScore, lordRetrograde, lordConditions, activatedThemes);

        return new ProfectionYearResult
        {
            Name = chart.Input.Name,
            BirthDate = birthDate,
            ReferenceDate = referenceDay.ToDateTime(TimeOnly.MinValue),
            Age = age,
            ProfectionCycleYear = profectionCycleYear,
            ProfectedHouse = profectedHouse,
            ProfectedSign = profectedSign.ToString(),
            LordOfTheYear = lordName,
            ActivatedThemes = activatedThemes,
            LordNatalSign = lordNatalSign,
            LordNatalHouse = lordNatalHouse,
            LordDignity = lordDignityLabel,
            LordDignityScore = lordDignityScore,
            LordRetrograde = lordRetrograde,
            LordConditions = lordConditions,
            LordAccidentalScore = lordAccidentalScore,
            LordInSect = lordInSect,
            LordNatalAspects = lordAspects.AsReadOnly(),
            ProfectionTable = table.AsReadOnly(),
            Summary = summary,
        };
    }

    #region Private Helpers

    /// <summary>
    /// The sign a longitude lies in.
    /// </summary>
    /// <param name="longitude">The longitude in degrees.</param>
    /// <returns>The sign.</returns>
    private static ZodiacSign GetSignFromLongitude(double longitude)
    {
        double normalized = ((longitude % 360.0) + 360.0) % 360.0;
        return (ZodiacSign)((int)(normalized / 30.0) + 1);
    }

    /// <summary>
    /// The sign a number of signs after another, in zodiacal order.
    /// </summary>
    /// <param name="sign">The sign counted from.</param>
    /// <param name="signs">How many signs on, 0 for the sign itself.</param>
    /// <returns>The sign reached.</returns>
    private static ZodiacSign SignAfter(ZodiacSign sign, int signs) =>
        (ZodiacSign)(((int)sign - 1 + signs) % 12 + 1);

    /// <summary>
    /// Builds the ordinal suffix for a house number (1st, 2nd, 3rd, 4th–12th).
    /// </summary>
    private static string OrdinalHouse(int house) => house switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => string.Create(CultureInfo.InvariantCulture, $"{house}th"),
    };

    /// <summary>
    /// The indefinite article before a house's ordinal, by its sound: "an" before 8th and 11th, "a" before
    /// the other ten (branch ExportText, 1.7.2; until then every year read "a", so "a 8th house profection
    /// year").
    /// </summary>
    /// <param name="house">The house number, 1 to 12.</param>
    /// <returns>"an" or "a".</returns>
    private static string ArticleForOrdinal(int house) => house is 8 or 11 ? "an" : "a";

    /// <summary>
    /// Builds the narrative summary paragraph for the profection year.
    /// </summary>
    private static string BuildSummary(
        string name, int age, int profectedHouse, string profectedSign,
        string lordName, string lordNatalSign, int lordNatalHouse,
        string lordDignityLabel, int lordDignityScore,
        bool lordRetrograde, IReadOnlyList<string> lordConditions,
        string activatedThemes)
    {
        string houseOrdinal = OrdinalHouse(profectedHouse);
        string lordHouseOrdinal = OrdinalHouse(lordNatalHouse);

        // Lord natal condition description
        string conditionDesc = string.Empty;
        if (lordRetrograde)
            conditionDesc += ", retrograde";
        if (lordConditions.Count > 0)
            conditionDesc += $", {string.Join(", ", lordConditions).ToLowerInvariant()}";

        // Strength assessment
        string assessment;
        if (lordDignityScore >= 4)
            assessment = $"A dignified lord of the year suggests an empowered, productive period in {activatedThemes.Split(',')[0].Trim().ToLowerInvariant()} matters.";
        else if (lordDignityScore >= 1)
            assessment = $"A moderately dignified lord suggests a manageable year with some support in {activatedThemes.Split(',')[0].Trim().ToLowerInvariant()} matters.";
        else if (lordDignityScore <= -4)
            assessment = $"A debilitated lord of the year signals significant challenges in {activatedThemes.Split(',')[0].Trim().ToLowerInvariant()} matters and areas ruled by {lordName}.";
        else
            assessment = $"A peregrine lord suggests a year where {activatedThemes.Split(',')[0].Trim().ToLowerInvariant()} themes lack a strong guiding force — outcomes depend heavily on transits and direction.";

        return string.Create(CultureInfo.InvariantCulture, $"At age {age}, {name} is in {ArticleForOrdinal(profectedHouse)} {houseOrdinal} house profection year. " +
               $"The profected house is the {houseOrdinal} ({profectedSign}), " +
               $"making {lordName} the lord of the year. " +
               $"{lordName} is natally {lordDignityLabel} in {lordNatalSign} in the {lordHouseOrdinal} house") +
               (conditionDesc.Length > 0 ? conditionDesc : string.Empty) + ". " +
               $"This year activates themes of: {activatedThemes}. " +
               assessment;
    }

    #endregion
}
