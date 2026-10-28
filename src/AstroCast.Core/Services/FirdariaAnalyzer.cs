using System.Globalization;

using AstroCast.Core.Formatting;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Computes the firdaria timeline for a natal chart.
/// Pure static function — no ephemeris calls, no engine changes.
/// </summary>
/// <remarks>
/// <para>
/// The firdaria divide life into nine major planetary periods of 75 years in all, in descending Chaldean
/// order from the sect light, the nodes last (branch H, 1.3.4; ruling T-D8 option a). The sources: Abu
/// Ma'shar, <i>On the Revolutions of the Years of Nativities</i>; al-Biruni, <i>The Book of Instruction in the
/// Elements of the Art of Astrology</i>, on the firdaria; Bonatti, <i>Liber Astronomiae</i>, on the firdaria
/// (timing-investigation.md §T6). The lengths are the same by day and by night:
/// </para>
/// <para>
/// <b>Day:</b> Sun (10) → Venus (8) → Mercury (13) → Moon (9) → Saturn (11) → Jupiter (12) → Mars (7) →
/// North Node (3) → South Node (2).
/// </para>
/// <para>
/// <b>Night</b> (Abu Ma'shar; Bonatti, as usually read, puts the nodes after Mars): Moon (9) → Saturn (11) →
/// Jupiter (12) → Mars (7) → Sun (10) → Venus (8) → Mercury (13) → North Node (3) → South Node (2).
/// </para>
/// <para>
/// Period k runs from one anniversary of the birth day to another (R-D6, tropical anniversaries): the birth
/// day is the local birth reading's day as proleptic ticks, anniversary N is the birth day plus N years
/// (29 February falls on 28 February in a common year), and the age at a reference date is the greatest N
/// whose anniversary is on or before the reference date's day (<see cref="BirthDayAndAge"/>, the one rule
/// the three time-lord analyzers share). The timeline holds (age ÷ 75) + 1 cycles of the nine. A planet's
/// period is divided into seven sub-periods of a seventh of its span in days, in Chaldean order from its lord
/// (Saturn, Jupiter, Mars, Sun, Venus, Mercury, Moon), the seventh ending on the period's end; a node's period
/// is not divided. A reference instant is read on the chart's clock, and a reference day before the birth
/// day is refused (T6-6).
/// </para>
/// <para>
/// Until 1.3.4 the order was no source's (day: Sun, Moon, North Node, Jupiter, Mercury, Saturn, South Node,
/// Mars, Venus; night: Moon, Sun, then as by day), the nodes' periods were divided from Saturn, and every
/// period ran years × 365.25 days from local midnight of the birth date, so a boundary fell up to a day from
/// the birthday and a boundary before 1582-10-15 printed as proleptic ticks (T6-1, T6-5).
/// </para>
/// </remarks>
public static class FirdariaAnalyzer
{
    #region Period Sequences

    /// <summary>Invariant culture used for every digit.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Day chart firdaria sequence: ruler and duration in years, the sources' order.</summary>
    private static readonly (string Ruler, int Years)[] DaySequence =
    [
        ("Sun", 10), ("Venus", 8), ("Mercury", 13), ("Moon", 9), ("Saturn", 11),
        ("Jupiter", 12), ("Mars", 7), ("North Node", 3), ("South Node", 2)
    ];

    /// <summary>Night chart firdaria sequence: ruler and duration in years, Abu Ma'shar's order.</summary>
    private static readonly (string Ruler, int Years)[] NightSequence =
    [
        ("Moon", 9), ("Saturn", 11), ("Jupiter", 12), ("Mars", 7), ("Sun", 10),
        ("Venus", 8), ("Mercury", 13), ("North Node", 3), ("South Node", 2)
    ];

    /// <summary>
    /// Chaldean order of the seven traditional planets, used for sub-period
    /// ruler assignment within each major period.
    /// </summary>
    private static readonly string[] ChaldeanOrder =
        ["Saturn", "Jupiter", "Mars", "Sun", "Venus", "Mercury", "Moon"];

    /// <summary>Total firdaria cycle length in years.</summary>
    private const int CycleYears = 75;

    /// <summary>Number of sub-periods per planet's major period (7 traditional planets).</summary>
    private const int SubPeriodCount = 7;

    #endregion

    #region Planet Keywords

    /// <summary>
    /// Returns interpretive keywords for a firdaria planet ruler.
    /// </summary>
    /// <param name="ruler">The planet name.</param>
    /// <returns>Comma-separated keyword string.</returns>
    private static string GetPlanetKeywords(string ruler) => ruler switch
    {
        "Sun" => "authority, vitality, leadership, public recognition",
        "Moon" => "emotions, home, family, nurturing, memory",
        "Mercury" => "communication, learning, commerce, travel, intellect",
        "Venus" => "relationships, pleasure, beauty, finances, harmony",
        "Mars" => "action, conflict, energy, ambition, courage",
        "Jupiter" => "expansion, wisdom, fortune, faith, generosity",
        "Saturn" => "discipline, structure, boundaries, responsibility, limitation",
        "North Node" => "destiny, growth, new direction, karmic development",
        "South Node" => "release, past patterns, letting go, karmic completion",
        _ => string.Empty
    };

    #endregion

    #region The birth day and the age

    /// <summary>
    /// The birth day, the reference day and the age at it, the rule every time-lord analyzer counts years by
    /// (branch H, 1.3.4; R-D6). The birth day is the local birth reading's day as proleptic ticks
    /// (<see cref="CalendarDateFormatter.ProlepticDay"/>: a reading before 1582-10-15 is read on the Julian
    /// calendar); anniversary N is the birth day plus N years, <see cref="DateOnly.AddYears"/>, so 29 February
    /// falls on 28 February in a common year; and the age is the greatest N whose anniversary is on or before
    /// the reference day. A reference of <see cref="DateTimeKind.Utc"/> is an instant (a return's), read on
    /// the natal chart's clock before its day is taken — at an explicit offset the instant plus
    /// <see cref="ChartResult.ResolvedUtcOffsetHours"/>, on a zone the reading the chart's rules give
    /// (<see cref="ChartResult.TimeZoneRules"/>, or the app's data), the two readings
    /// <c>ChartTimeFormatter.FormatLocalTime</c> makes; a reference of any other kind is a date as given
    /// (the natal export's date of today). A boundary printed through <see cref="CalendarDateFormatter"/>
    /// shows its Julian label before the switch. Until commit 4 of branch H an instant's UT date was taken,
    /// so a return instant that read the day before in the natal zone counted a year less, and a chart born
    /// after local midnight east of Greenwich had its birth-year solar return refused (T6-5, T6-6).
    /// </summary>
    /// <param name="chart">The natal chart.</param>
    /// <param name="referenceDate">
    /// The reference: an instant of <see cref="DateTimeKind.Utc"/>, read on the chart's clock, or a date as
    /// given; its day is read, the time of day is not.
    /// </param>
    /// <returns>The birth day, the reference day the age was taken on, and the age in completed anniversaries.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the reference day is before the birth day (T6-6).
    /// </exception>
    internal static (DateOnly BirthDay, DateOnly ReferenceDay, int Age) BirthDayAndAge(ChartResult chart, DateTime referenceDate)
    {
        DateOnly birthDay = CalendarDateFormatter.ProlepticDay(DateOnly.FromDateTime(chart.Input.LocalDateTime));
        DateOnly referenceDay = DateOnly.FromDateTime(ReferenceReading(chart, referenceDate));
        if (referenceDay < birthDay)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceDate),
                string.Create(Inv,
                    $"The reference date {referenceDay:yyyy-MM-dd} is before the birth day {CalendarDateFormatter.Format(birthDay, "yyyy-MM-dd")}."));
        }

        int age = referenceDay.Year - birthDay.Year;
        if (referenceDay < birthDay.AddYears(age))
            age--;

        return (birthDay, referenceDay, age);
    }

    /// <summary>
    /// The reading a reference denotes on the natal chart's clock: an instant of <see cref="DateTimeKind.Utc"/>
    /// at the chart's explicit offset, or on its zone through the rules it was cast with; any other kind as
    /// given.
    /// </summary>
    /// <param name="chart">The natal chart.</param>
    /// <param name="referenceDate">The reference.</param>
    /// <returns>The reading whose day the age is taken on.</returns>
    private static DateTime ReferenceReading(ChartResult chart, DateTime referenceDate)
    {
        if (referenceDate.Kind != DateTimeKind.Utc)
            return referenceDate;

        if (chart.Input.UseExplicitOffset && chart.Input.UtcOffsetHours.HasValue)
            return referenceDate.AddHours(chart.ResolvedUtcOffsetHours);

        return (chart.TimeZoneRules ?? new TzdbTimeZoneRules())
            .ToLocal(referenceDate, chart.Input.TimeZone).LocalDateTime;
    }

    #endregion

    /// <summary>
    /// Compute the firdaria timeline for a natal chart at a given reference date.
    /// </summary>
    /// <param name="chart">The natal chart result.</param>
    /// <param name="referenceDate">The date to compute firdaria for (typically today).</param>
    /// <returns>A <see cref="FirdariaResult"/> with the full firdaria timeline.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chart"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="referenceDate"/> is before the birth day (T6-6).
    /// </exception>
    public static FirdariaResult Analyze(ChartResult chart, DateTime referenceDate)
    {
        ArgumentNullException.ThrowIfNull(chart);

        (DateOnly birthDay, DateOnly referenceDay, int age) = BirthDayAndAge(chart, referenceDate);
        DateTime birthDate = birthDay.ToDateTime(TimeOnly.MinValue);
        bool isDayChart = chart.ChartContext?.IsDayChart ?? true;
        (string Ruler, int Years)[] sequence = isDayChart ? DaySequence : NightSequence;

        // Build (age ÷ 75) + 1 cycles of the nine, each period between two anniversaries of the birth day.
        int cyclesNeeded = (age / CycleYears) + 1;
        List<FirdariaPeriod> periods = [];
        int yearsElapsed = 0;

        string currentMajorRuler = string.Empty;
        DateTime currentMajorStart = birthDate;
        DateTime currentMajorEnd = birthDate;
        int currentMajorYears = 0;
        string currentSubRuler = string.Empty;
        DateTime currentSubStart = birthDate;
        DateTime currentSubEnd = birthDate;

        for (int cycle = 0; cycle < cyclesNeeded; cycle++)
        {
            foreach ((string ruler, int years) in sequence)
            {
                DateOnly start = birthDay.AddYears(yearsElapsed);
                DateOnly end = birthDay.AddYears(yearsElapsed + years);
                DateTime periodStart = start.ToDateTime(TimeOnly.MinValue);
                DateTime periodEnd = end.ToDateTime(TimeOnly.MinValue);

                bool isCurrent = referenceDay >= start && referenceDay < end;

                // A planet's period is divided into sevenths; a node's is not.
                List<FirdariaSubPeriod> subPeriods = BuildSubPeriods(ruler, periodStart, periodEnd, referenceDay);

                periods.Add(new FirdariaPeriod
                {
                    Ruler = ruler,
                    Years = years,
                    StartDate = periodStart,
                    EndDate = periodEnd,
                    IsCurrent = isCurrent,
                    SubPeriods = subPeriods,
                });

                if (isCurrent)
                {
                    currentMajorRuler = ruler;
                    currentMajorStart = periodStart;
                    currentMajorEnd = periodEnd;
                    currentMajorYears = years;

                    FirdariaSubPeriod? activeSub = subPeriods.FirstOrDefault(sp => sp.IsCurrent);
                    if (activeSub is not null)
                    {
                        currentSubRuler = activeSub.Ruler;
                        currentSubStart = activeSub.StartDate;
                        currentSubEnd = activeSub.EndDate;
                    }
                    else
                    {
                        // A node's period: no sub-ruler, the sub dates the major's.
                        currentSubRuler = string.Empty;
                        currentSubStart = periodStart;
                        currentSubEnd = periodEnd;
                    }
                }

                yearsElapsed += years;
            }
        }

        string summary = BuildSummary(
            age, currentMajorRuler, currentMajorStart, currentMajorEnd,
            currentSubRuler, currentSubStart, currentSubEnd);

        return new FirdariaResult
        {
            IsDayChart = isDayChart,
            BirthDate = birthDate,
            ReferenceDate = referenceDay.ToDateTime(TimeOnly.MinValue),
            Age = age,
            CurrentMajorRuler = currentMajorRuler,
            CurrentMajorStart = currentMajorStart,
            CurrentMajorEnd = currentMajorEnd,
            CurrentMajorYears = currentMajorYears,
            CurrentSubRuler = currentSubRuler,
            CurrentSubStart = currentSubStart,
            CurrentSubEnd = currentSubEnd,
            Periods = periods,
            Summary = summary,
        };
    }

    #region Sub-Period Computation

    /// <summary>
    /// Builds the sub-period timeline for a single major period: seven sub-periods of a seventh of the
    /// period's span in days, measured from its start, in Chaldean order from the major ruler, the seventh
    /// ending on the period's end. A node's period is not divided: the list is empty.
    /// </summary>
    /// <param name="majorRuler">The major period ruler.</param>
    /// <param name="periodStart">Start date of the major period.</param>
    /// <param name="periodEnd">End date of the major period.</param>
    /// <param name="referenceDay">Reference day for current sub-period detection.</param>
    /// <returns>The seven sub-periods of a planet's period; none for a node's.</returns>
    private static List<FirdariaSubPeriod> BuildSubPeriods(
        string majorRuler,
        DateTime periodStart,
        DateTime periodEnd,
        DateOnly referenceDay)
    {
        int startIndex = Array.IndexOf(ChaldeanOrder, majorRuler);
        if (startIndex < 0)
        {
            // The nodes are not in the Chaldean order: their periods are left whole (T-D8).
            return [];
        }

        double totalDays = (periodEnd - periodStart).TotalDays;
        List<FirdariaSubPeriod> subPeriods = [];
        DateTime subCursor = periodStart;

        for (int i = 0; i < SubPeriodCount; i++)
        {
            DateTime subStart = subCursor;
            DateTime subEnd = (i == SubPeriodCount - 1)
                ? periodEnd  // The seventh ends on the period's end
                : periodStart.AddDays(totalDays * (i + 1) / SubPeriodCount);

            bool isCurrent = referenceDay >= DateOnly.FromDateTime(subStart) &&
                             referenceDay < DateOnly.FromDateTime(subEnd);

            subPeriods.Add(new FirdariaSubPeriod
            {
                Ruler = ChaldeanOrder[(startIndex + i) % SubPeriodCount],
                StartDate = subStart,
                EndDate = subEnd,
                IsCurrent = isCurrent,
            });

            subCursor = subEnd;
        }

        return subPeriods;
    }

    #endregion

    #region Summary

    /// <summary>
    /// Builds the narrative summary for the current firdaria period, its dates on the calendar in force.
    /// A node's period, undivided, has no sub-period sentence.
    /// </summary>
    private static string BuildSummary(
        int age,
        string majorRuler,
        DateTime majorStart,
        DateTime majorEnd,
        string subRuler,
        DateTime subStart,
        DateTime subEnd)
    {
        string majorKeywords = GetPlanetKeywords(majorRuler);
        string majorYears = CalendarDateFormatter.Format(majorStart, "yyyy") + "–" + CalendarDateFormatter.Format(majorEnd, "yyyy");

        if (subRuler.Length == 0)
        {
            return string.Create(Inv, $"Age {age}: {majorRuler} major period ({majorYears}), undivided. " +
                   $"{majorRuler} themes of {majorKeywords} are the year’s background.");
        }

        string subKeywords = GetPlanetKeywords(subRuler);

        return string.Create(Inv, $"Age {age}: {majorRuler} major period ({majorYears}), " +
               $"{subRuler} sub-period " +
               $"({CalendarDateFormatter.Date(subStart)}–{CalendarDateFormatter.Date(subEnd)}). " +
               $"{majorRuler} themes of {majorKeywords} are the year’s background; " +
               $"{subRuler} themes of {subKeywords} are the current focus.");
    }

    #endregion
}
