using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services.PrimaryDirections;

/// <summary>
/// Primary directions of a natal chart (branch Primary, 1.5.0): Placidus semi-arcs in zodiaco, direct and
/// traditional converse, dated by the key, with each hit's sensitivity to the birth time.
/// </summary>
/// <remarks>
/// <para>
/// The speculum of rule R1 is built from the chart (<see cref="Speculum"/>); every significator of rule R5 is paired
/// with every promissor point of rule R4, the Descendant and IC only with the conjunction of a body other than the
/// node (at its real place, with latitude) and with the stars, and a body never with its own points; each pair gives
/// a direct arc and a converse arc (<see cref="PrimaryDirectionArcs"/>), or is
/// counted as left out when a semi-arc it uses does not exist; the key of rule R6 turns an arc into years, and a hit
/// is kept when its years do not exceed the age limit (rule R7). The date is the natal Julian Day plus the years at
/// 365.2422 days a year.
/// </para>
/// <para>
/// For rule R8 the same list is found at the birth instant a minute earlier and a minute later, the speculum rebuilt
/// from the ephemeris with Equal houses and the Part of Fortune by that instant's day or night; a hit matches when
/// significator, promissor, aspect, aspect angle and motion match, and its months per minute are half the
/// difference of the two dates in months of 365.2422 / 12 days. With an uncertainty of N minutes the same is done at
/// N minutes either way, and the two dates, earlier first, are the hit's range. A hit absent at either instant has
/// no figure, and nor has a Part of Fortune hit when the sect differs at either instant from the birth's, the Lot
/// having changed its formula.
/// </para>
/// <para>
/// A hit is marked as turning (<see cref="PrimaryDirectionHit.IsTurning"/>; branch DirectionNotes, 1.7.2, finding
/// A5) when its own date is not between the dates a minute either side of the birth, or not between the dates at the
/// two ends of the uncertainty, both ends included (<see cref="IsBetween"/>): its date turns there, so its range does
/// not bound its date and its months per minute are not a rate. Only dates already in hand are compared; no date,
/// arc or figure is changed.
/// </para>
/// </remarks>
public sealed class PrimaryDirectionService : IPrimaryDirectionService
{
    #region Constants

    /// <summary>The length of a year the keys and the dates use, in days (rule R6).</summary>
    internal const double DaysPerYear = 365.2422;

    /// <summary>Naibod's key: the Sun's mean motion, in degrees of arc a year.</summary>
    internal const double NaibodDegreesPerYear = 360.0 / DaysPerYear;

    /// <summary>The days in a month of 365.2422 / 12 days, the unit of <see cref="PrimaryDirectionHit.MonthsPerMinute"/>.</summary>
    private const double DaysPerMonth = DaysPerYear / 12.0;

    /// <summary>The true node's position name: its conjunction does not direct to the Descendant or IC (rule R5).</summary>
    private const string TrueNodeName = "True Node";

    /// <summary>A minute as a fraction of a day.</summary>
    private const double MinuteInDays = 1.0 / 1440.0;

    /// <summary>
    /// The days beyond the age limit the solar-arc key's bracket runs to: the Sun's right ascension advances about a
    /// degree a day, so an arc of A degrees is reached within about A days, and none past the bracket is dated.
    /// </summary>
    private const double SolarArcBracketExtraDays = 20.0;

    /// <summary>The solar-arc key's bisection stops when its bracket, in days, is under this: a tenth of a microsecond of life.</summary>
    private const double SolarArcToleranceDays = 1e-9;

    /// <summary>The end of the ephemeris span as the refusal prints it, <see cref="EphemerisSpan.LastJulianDay"/> to the second.</summary>
    private const string SpanEndText = "2399-12-31 23:51:20 UT";

    /// <summary>
    /// The tolerance within which two hits' instants count as one for the order of the list, in days: one second.
    /// <see cref="CompareHits"/> applies it as a quantised key, the instant rounded to the second, never as a
    /// tolerance comparer, which is not transitive and which a sort may reject (1.6.1, item G, branch TiesAndGuards).
    /// </summary>
    internal const double TieTolerance = 1.0 / SecondsPerDay;

    /// <summary>The seconds in a day, the quantum of <see cref="CompareHits"/>'s first key.</summary>
    private const double SecondsPerDay = 86400.0;

    #endregion

    private readonly IEphemerisService _ephemeris;

    /// <summary>
    /// Initialises a new instance of <see cref="PrimaryDirectionService"/>.
    /// </summary>
    /// <param name="ephemeris">The ephemeris, for the stars, the rotation, the recasts and the Sun's right ascension.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ephemeris"/> is null.</exception>
    public PrimaryDirectionService(IEphemerisService ephemeris)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
    }

    /// <inheritdoc />
    public PrimaryDirectionResult Calculate(
        ChartResult natalChart, PrimaryDirectionSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentNullException.ThrowIfNull(settings);

        // The solar-arc key reads the Sun to the end of its bracket; a birth whose bracket passes the span's end
        // is refused here, before the speculum reads anything (1.6.1, item F, branch TiesAndGuards).
        if (settings.Key == PrimaryDirectionKey.SolarArcRightAscension
            && natalChart.JulianDay + SolarArcBracketDays(settings.AgeLimitYears) > EphemerisSpan.LastJulianDay)
        {
            throw new ArgumentException(SolarArcKeyBeyondSpanMessage(settings.AgeLimitYears));
        }

        Speculum natal = Speculum.FromChart(natalChart, _ephemeris, settings.PlanetsAsSignificators);
        (Dictionary<HitKey, Direction> birth, int leftOut) = HitsAt(natal, settings, cancellationToken);

        double latitude = natalChart.Input.Latitude;
        double longitude = natalChart.Input.Longitude;
        (Dictionary<HitKey, Direction> minuteBefore, bool sectBefore) = HitsAtShift(natal.JulianDayUt - MinuteInDays, latitude, longitude, settings, cancellationToken);
        (Dictionary<HitKey, Direction> minuteAfter, bool sectAfter) = HitsAtShift(natal.JulianDayUt + MinuteInDays, latitude, longitude, settings, cancellationToken);
        bool sectFlipsWithinMinute = sectBefore != natal.IsDayChart || sectAfter != natal.IsDayChart;

        Dictionary<HitKey, Direction>? earlier = null;
        Dictionary<HitKey, Direction>? later = null;
        bool sectFlipsWithinRange = false;
        if (settings.BirthTimeUncertaintyMinutes > 0)
        {
            double shift = settings.BirthTimeUncertaintyMinutes * MinuteInDays;
            (earlier, bool sectEarlier) = HitsAtShift(natal.JulianDayUt - shift, latitude, longitude, settings, cancellationToken);
            (later, bool sectLater) = HitsAtShift(natal.JulianDayUt + shift, latitude, longitude, settings, cancellationToken);
            sectFlipsWithinRange = sectEarlier != natal.IsDayChart || sectLater != natal.IsDayChart;
        }

        Dictionary<string, int> significatorOrder = natal.Significators
            .Select((s, i) => (s.Name, i))
            .ToDictionary(x => x.Name, x => x.i, StringComparer.Ordinal);

        List<PrimaryDirectionHit> hits = new(birth.Count);
        foreach ((HitKey key, Direction direction) in birth)
        {
            bool isFortune = string.Equals(key.Significator, Speculum.PartOfFortuneName, StringComparison.Ordinal);

            // A hit is turning when its own date is not between the two dates found either side of the birth
            // (branch DirectionNotes, 1.7.2; finding A5): only the dates already in hand are compared, so nothing
            // here reads the ephemeris or says how an arc becomes a date
            bool isTurning = false;

            double? monthsPerMinute = null;
            if (minuteBefore.TryGetValue(key, out Direction before)
                && minuteAfter.TryGetValue(key, out Direction after)
                && !(isFortune && sectFlipsWithinMinute))
            {
                monthsPerMinute = (after.JulianDayUt - before.JulianDayUt) / 2.0 / DaysPerMonth;
                isTurning = !IsBetween(direction.JulianDayUt, before.JulianDayUt, after.JulianDayUt);
            }

            DateTime? rangeStart = null;
            DateTime? rangeEnd = null;
            if (earlier is not null && later is not null
                && earlier.TryGetValue(key, out Direction atEarlier)
                && later.TryGetValue(key, out Direction atLater)
                && !(isFortune && sectFlipsWithinRange))
            {
                double first = Math.Min(atEarlier.JulianDayUt, atLater.JulianDayUt);
                double second = Math.Max(atEarlier.JulianDayUt, atLater.JulianDayUt);
                rangeStart = JulianDayUtility.JulianDayToInstant(first);
                rangeEnd = JulianDayUtility.JulianDayToInstant(second);
                isTurning |= !IsBetween(direction.JulianDayUt, first, second);
            }

            hits.Add(new PrimaryDirectionHit
            {
                Significator = key.Significator,
                Promissor = key.Promissor,
                PromissorKind = direction.Kind,
                Aspect = key.Aspect,
                AspectAngle = key.Angle,
                Motion = key.Motion,
                Arc = direction.Arc,
                Years = direction.Years,
                JulianDayUt = direction.JulianDayUt,
                InstantUtc = JulianDayUtility.JulianDayToInstant(direction.JulianDayUt),
                MonthsPerMinute = monthsPerMinute,
                RangeStartUtc = rangeStart,
                RangeEndUtc = rangeEnd,
                IsTurning = isTurning,
            });
        }

        hits.Sort((a, b) => CompareHits(a, b, significatorOrder));

        return new PrimaryDirectionResult
        {
            NatalChart = natalChart,
            Settings = settings,
            Hits = hits.AsReadOnly(),
            LeftOutCount = leftOut,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<PrimaryDirectionHit> FindHits(
        ChartResult natalChart, PrimaryDirectionSettings settings, DateOnly start, DateOnly end,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentNullException.ThrowIfNull(settings);
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), end, "The period's end is before its start.");

        PrimaryDirectionResult result = Calculate(natalChart, settings, cancellationToken);
        return result.Hits
            .Where(hit =>
            {
                DateOnly date = DateOnly.FromDateTime(hit.InstantUtc);
                return date >= start && date <= end;
            })
            .ToList()
            .AsReadOnly();
    }

    #region The turning row (branch DirectionNotes, 1.7.2)

    /// <summary>
    /// Whether a date lies between two others, both included, whichever of the two is the earlier: the test of
    /// <see cref="PrimaryDirectionHit.IsTurning"/>, which is true when a direction's own date fails it against the
    /// dates a minute either side of the birth, or against the dates at the two ends of the birth-time uncertainty.
    /// </summary>
    /// <param name="julianDayUt">The date, as a Julian Day (UT).</param>
    /// <param name="one">One of the two dates.</param>
    /// <param name="other">The other.</param>
    /// <returns>True when the date is at or after the earlier of the two and at or before the later.</returns>
    internal static bool IsBetween(double julianDayUt, double one, double other) =>
        julianDayUt >= Math.Min(one, other) && julianDayUt <= Math.Max(one, other);

    #endregion

    #region The order of the hits

    /// <summary>
    /// The order of a result's hits: by the instant to the second (<c>Math.Round(JulianDayUt × 86400)</c>, so two hits
    /// within <see cref="TieTolerance"/> of each other are one instant), then the significator's place in the
    /// speculum's order (Asc, MC, Dsc, IC, Sun, Moon, Mercury to Pluto, Fortune), then the promissor, ordinal, the
    /// aspect, the angle and the motion (1.6.1, item G, branch TiesAndGuards). A direct conjunction of P to S and the
    /// converse conjunction of S to P are one Placidian distance and land ulps apart; until 1.6.1 the exact instant
    /// decided, so such a pair was ordered by rounding.
    /// </summary>
    /// <param name="a">The first hit.</param>
    /// <param name="b">The second hit.</param>
    /// <param name="significatorOrder">Each significator's place in the speculum's order.</param>
    /// <returns>Negative when <paramref name="a"/> sorts first, positive when <paramref name="b"/> does, zero when they are one.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a hit or the order is null.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a hit's significator is not in the order.</exception>
    internal static int CompareHits(PrimaryDirectionHit a, PrimaryDirectionHit b, IReadOnlyDictionary<string, int> significatorOrder)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        ArgumentNullException.ThrowIfNull(significatorOrder);

        int byInstant = Math.Round(a.JulianDayUt * SecondsPerDay).CompareTo(Math.Round(b.JulianDayUt * SecondsPerDay));
        if (byInstant != 0)
            return byInstant;
        int bySignificator = significatorOrder[a.Significator].CompareTo(significatorOrder[b.Significator]);
        if (bySignificator != 0)
            return bySignificator;
        int byPromissor = string.CompareOrdinal(a.Promissor, b.Promissor);
        if (byPromissor != 0)
            return byPromissor;
        int byAspect = a.Aspect.CompareTo(b.Aspect);
        if (byAspect != 0)
            return byAspect;
        int byAngle = a.AspectAngle.CompareTo(b.AspectAngle);
        return byAngle != 0 ? byAngle : a.Motion.CompareTo(b.Motion);
    }

    #endregion

    #region Keys and dates (rule R6)

    /// <summary>
    /// The years an arc stands for under a key: the arc over 360 / 365.2422 for Naibod, the arc itself for Ptolemy,
    /// and for the solar arc in right ascension the days after the birth at which the Sun's right ascension has
    /// advanced by the arc, read as years.
    /// </summary>
    /// <param name="arc">The arc, in degrees.</param>
    /// <param name="key">The key.</param>
    /// <param name="birthJulianDayUt">The birth instant, as a Julian Day (UT): the solar-arc key reads the Sun from it.</param>
    /// <param name="ageLimitYears">The age limit, which bounds the solar-arc key's bracket at the limit plus 20 days.</param>
    /// <returns>The years, or null when the solar-arc key finds no solution within its bracket.</returns>
    internal double? YearsFor(double arc, PrimaryDirectionKey key, double birthJulianDayUt, int ageLimitYears) =>
        YearsFor(arc, key, new SolarArcInverter(_ephemeris, birthJulianDayUt, ageLimitYears));

    /// <summary>The instant a direction is exact as a Julian Day (UT): the birth's plus the years at 365.2422 days a year.</summary>
    /// <param name="birthJulianDayUt">The birth instant, as a Julian Day (UT).</param>
    /// <param name="years">The years of life.</param>
    /// <returns>The Julian Day (UT).</returns>
    internal static double JulianDayAt(double birthJulianDayUt, double years) => birthJulianDayUt + years * DaysPerYear;

    /// <summary>The solar-arc key's bracket for an age limit, in days: the limit plus <see cref="SolarArcBracketExtraDays"/>.</summary>
    /// <param name="ageLimitYears">The age limit, in years.</param>
    /// <returns>The bracket, in days.</returns>
    private static double SolarArcBracketDays(int ageLimitYears) => ageLimitYears + SolarArcBracketExtraDays;

    /// <summary>
    /// The one message of the refusal of the solar-arc key for a birth whose bracket passes the end of the ephemeris
    /// files (1.6.1, item F, branch TiesAndGuards): "The solar-arc key reads the Sun's right ascension to {bracket}
    /// days after the birth, past the end of the ephemeris files (2399-12-31 23:51:20 UT); for a birth after {date}
    /// use the Naibod or Ptolemy key.", the bracket in whole days and the date the last birth whose bracket stays
    /// inside the span, on the calendar in force (age limit 100: 2399-09-02). Until 1.6.1 the key read the Sun past
    /// the span, on the Moshier model with a log line only.
    /// </summary>
    /// <param name="ageLimitYears">The age limit, in years.</param>
    /// <returns>The message, culture-independent.</returns>
    internal static string SolarArcKeyBeyondSpanMessage(int ageLimitYears)
    {
        double bracket = SolarArcBracketDays(ageLimitYears);
        DateTime lastBirth = JulianDayUtility.JulianDayToInstant(EphemerisSpan.LastJulianDay - bracket);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"The solar-arc key reads the Sun's right ascension to {bracket:0} days after the birth, past the end of the ephemeris files ({SpanEndText}); for a birth after {CalendarDateFormatter.Date(lastBirth)} use the Naibod or Ptolemy key.");
    }

    /// <summary>The years for an arc under a key, the solar-arc key through an inverter built for the birth instant.</summary>
    private static double? YearsFor(double arc, PrimaryDirectionKey key, SolarArcInverter inverter) => key switch
    {
        PrimaryDirectionKey.Naibod => arc / NaibodDegreesPerYear,
        PrimaryDirectionKey.Ptolemy => arc,
        PrimaryDirectionKey.SolarArcRightAscension => inverter.YearsFor(arc),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown primary direction key."),
    };

    /// <summary>
    /// Inverts the Sun's right ascension from a birth instant: the days at which it has advanced by an arc, found by
    /// bisection on a bracket from the birth to the age limit plus 20 days, clamped to the end of the ephemeris span
    /// so nothing is read past it (1.6.1, item F, branch TiesAndGuards). <see cref="Calculate"/>'s guard refuses a
    /// birth the clamp would cut, so from there the clamp is unreachable; the internal <see cref="YearsFor(double, PrimaryDirectionKey, double, int)"/> reaches it.
    /// </summary>
    private sealed class SolarArcInverter
    {
        private readonly IEphemerisService _ephemeris;
        private readonly double _birthJulianDayUt;
        private readonly double _bracketDays;
        private double? _rightAscensionAtBirth;
        private double? _advanceAtBracketEnd;

        /// <summary>Builds the inverter; nothing is read from the ephemeris until the first arc.</summary>
        /// <param name="ephemeris">The ephemeris.</param>
        /// <param name="birthJulianDayUt">The birth instant, as a Julian Day (UT).</param>
        /// <param name="ageLimitYears">The age limit, in years.</param>
        internal SolarArcInverter(IEphemerisService ephemeris, double birthJulianDayUt, int ageLimitYears)
        {
            _ephemeris = ephemeris;
            _birthJulianDayUt = birthJulianDayUt;
            _bracketDays = Math.Min(SolarArcBracketDays(ageLimitYears), EphemerisSpan.LastJulianDay - birthJulianDayUt);
        }

        /// <summary>The days, read as years, at which the Sun's right ascension has advanced by the arc; null beyond the bracket.</summary>
        /// <param name="arc">The arc, in degrees.</param>
        /// <returns>The years, or null.</returns>
        internal double? YearsFor(double arc)
        {
            _rightAscensionAtBirth ??= SunRightAscension(_birthJulianDayUt);
            _advanceAtBracketEnd ??= Advance(_bracketDays);

            if (_advanceAtBracketEnd.Value < arc)
                return null;

            return Bisection.Solve(Advance, arc, 0.0, _bracketDays, SolarArcToleranceDays);
        }

        /// <summary>The Sun's advance in right ascension from the birth after some days, in [0°, 360°).</summary>
        private double Advance(double days) =>
            SpeculumPoint.Normalize360(SunRightAscension(_birthJulianDayUt + days) - (_rightAscensionAtBirth ?? 0.0));

        /// <summary>The Sun's right ascension at an instant.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the ephemeris gives the Sun no right ascension.</exception>
        private double SunRightAscension(double julianDayUt) =>
            _ephemeris.CalculatePosition(julianDayUt, CelestialBodyId.Sun).RightAscension
            ?? throw new InvalidOperationException("The ephemeris gave the Sun no right ascension.");
    }

    #endregion

    #region The hits of one speculum

    /// <summary>What names a direction: its significator, promissor source, aspect, signed angle and motion.</summary>
    private readonly record struct HitKey(
        string Significator, string Promissor, PrimaryDirectionAspect Aspect, double Angle, PrimaryDirectionMotion Motion);

    /// <summary>A direction found: its arc, its years and its instant, and what its promissor is.</summary>
    private readonly record struct Direction(double Arc, double Years, double JulianDayUt, PrimaryDirectionPromissorKind Kind);

    /// <summary>The hits of a birth at a shifted instant, and whether that instant is a day chart.</summary>
    private (Dictionary<HitKey, Direction> Hits, bool IsDayChart) HitsAtShift(
        double julianDayUt, double latitude, double longitude, PrimaryDirectionSettings settings,
        CancellationToken cancellationToken)
    {
        Speculum speculum = Speculum.AtInstant(julianDayUt, latitude, longitude, _ephemeris, settings.PlanetsAsSignificators);
        (Dictionary<HitKey, Direction> hits, _) = HitsAt(speculum, settings, cancellationToken);
        return (hits, speculum.IsDayChart);
    }

    /// <summary>
    /// Every direction of a speculum within the age limit, by key, and the count of pairs left out for want of a
    /// semi-arc (rules R1 to R7).
    /// </summary>
    private (Dictionary<HitKey, Direction> Hits, int LeftOut) HitsAt(
        Speculum speculum, PrimaryDirectionSettings settings, CancellationToken cancellationToken)
    {
        Dictionary<HitKey, Direction> hits = [];
        int leftOut = 0;
        SolarArcInverter inverter = new(_ephemeris, speculum.JulianDayUt, settings.AgeLimitYears);
        double ageLimit = settings.AgeLimitYears;

        foreach (SpeculumSignificator significator in speculum.Significators)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The Descendant and IC take only the conjunction of a body other than the node, at its real place
            // with latitude, and the stars (rule R5 as amended on 2026-10-18): a point without latitude and its
            // exact opposite give the same arc, so an antiscion to the IC is already its contra-antiscion to the
            // Midheaven, and the node to the IC its opposition to the Midheaven
            bool lowerAngle = significator.Name is "Descendant" or "IC";
            foreach (SpeculumPromissor promissor in speculum.Promissors)
            {
                if (lowerAngle
                    && (promissor.Aspect != PrimaryDirectionAspect.Conjunction
                        || string.Equals(promissor.Source, TrueNodeName, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (significator.Kind == SignificatorKind.Body
                    && string.Equals(promissor.Source, significator.Name, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (PrimaryDirectionMotion motion in (ReadOnlySpan<PrimaryDirectionMotion>)[PrimaryDirectionMotion.Direct, PrimaryDirectionMotion.Converse])
                {
                    double? arc = motion == PrimaryDirectionMotion.Direct
                        ? PrimaryDirectionArcs.Direct(significator, promissor.Point)
                        : PrimaryDirectionArcs.Converse(significator.Point, promissor.Point);
                    if (arc is not double arcValue)
                    {
                        leftOut++;
                        continue;
                    }

                    double? years = YearsFor(arcValue, settings.Key, inverter);
                    if (years is not double yearsValue || yearsValue > ageLimit)
                        continue;

                    HitKey key = new(significator.Name, promissor.Source, promissor.Aspect, promissor.Angle, motion);
                    hits[key] = new Direction(arcValue, yearsValue, JulianDayAt(speculum.JulianDayUt, yearsValue), promissor.Kind);
                }
            }
        }

        return (hits, leftOut);
    }

    #endregion
}
