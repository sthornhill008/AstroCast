using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Orchestrates a complete chart calculation from input to result.
/// This is the primary entry point for all chart calculations, combining
/// ephemeris, aspect, and midpoint services into a single call.
/// </summary>
public interface IChartEngine
{
    /// <summary>
    /// Calculate a complete chart: positions, houses, aspects, and midpoints.
    /// </summary>
    /// <param name="input">Chart input parameters (name, datetime, timezone, coordinates).</param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <returns>
    /// Complete chart result with all computed data. With <see cref="ChartInput.IncludeMinorPoints"/> on and
    /// the chart not horary, <see cref="ChartResult.MinorPoints"/> holds Ceres, Pallas, Juno, Vesta and
    /// Lilith with their houses and <see cref="ChartResult.MinorPointAspects"/> their aspects at 2° (branch
    /// Asteroids, 1.6.0); otherwise both are empty, and <see cref="ChartResult.Positions"/> is the twelve
    /// bodies either way.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="input"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the latitude in <paramref name="input"/> is outside −90 to 90, its
    /// longitude outside −180 to 180, or its instant outside the years the ephemeris files
    /// cover, 1200 to 2399 (1200-01-01 04:54:46 UT on the Julian calendar to
    /// 2399-12-31 23:51:20 UT); when its local date is one of 1582-10-05 to 1582-10-14,
    /// which never occurred (the day after 1582-10-04 on the Julian calendar was 1582-10-15
    /// on the Gregorian); or when its instant falls on 29 February UT of 1300, 1400 or 1500
    /// on the Julian calendar, a date <see cref="DateTime"/> cannot hold.
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when the timezone identifier in <paramref name="input"/> is not valid.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the Swiss Ephemeris cannot calculate the chart: for example a house
    /// system that cannot be drawn at the latitude. An instant outside the ephemeris
    /// files is refused first, with <see cref="ArgumentOutOfRangeException"/>. Not where
    /// the Sun does not rise or set (the polar day or night): there the chart is returned
    /// without a planetary hour
    /// (<see cref="ChartResult.PlanetaryHour"/> is <see langword="null"/>), and
    /// <see cref="AstroCast.Core.Exceptions.SunDoesNotRiseOrSetException"/> never
    /// leaves this method.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="input"/> is a horary chart with a question
    /// category and its horary analysis reports an internal invariant breach. A
    /// valid chart never produces it; the exception reports a defect, not a
    /// condition to handle.
    /// </exception>
    ChartResult Calculate(ChartInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate a complete chart, as <see cref="Calculate"/> does, at a given Julian Day: an event
    /// chart cast at the instant of its event (a return, an ingress, a lunation, a great conjunction).
    /// </summary>
    /// <param name="input">
    /// Chart input parameters, at an explicit offset. Its reading (<see cref="ChartInput.LocalDateTime"/>
    /// and <see cref="ChartInput.UtcOffsetHours"/>) is only the label the chart prints; it is never
    /// read back into an instant.
    /// </param>
    /// <param name="julianDayUt">The Julian Day (UT) to cast at.</param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <returns>
    /// The complete chart, cast at <paramref name="julianDayUt"/> taken to the nearest whole second.
    /// Its <see cref="ChartResult.UtcDateTime"/> is that instant (<see cref="DateTimeKind.Utc"/>, on the
    /// proleptic Gregorian ticks <see cref="DateTime"/> counts), and its
    /// <see cref="ChartResult.JulianDay"/> that instant's. With <see cref="ChartInput.IncludeMinorPoints"/>
    /// on it carries the minor points as <see cref="Calculate"/> does (branch Asteroids, 1.6.0): the solar,
    /// lunar and planetary return services copy the natal chart's switch onto their return inputs, so each
    /// return chart carries its own five.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The Julian Day and the instant convert into each other by arithmetic
    /// (<see cref="Utilities.JulianDayUtility.JulianDayToInstant"/>,
    /// <see cref="Utilities.JulianDayUtility.InstantToJulianDay"/>), never through a calendar. An event
    /// chart used to be cast by reading its label back through <see cref="Calculate"/>: before
    /// 1582-10-15 the label, a proleptic Gregorian date, was read as a Julian one, so the chart landed 7 to
    /// 10 days after its event, or was refused when the label fell on 1582-10-05 to 1582-10-14.
    /// </para>
    /// <para>
    /// The second is rounded as <see cref="Utilities.JulianDayUtility.ToDateTimeUtc"/> rounds it, so on or
    /// after 1582-10-15 the chart is exactly the one <see cref="Calculate"/> gives for the event's label at
    /// offset zero: the same Julian Day, to the bit.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="input"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="input"/> is not at an explicit offset: a chart read on a zone's clock
    /// is cast at an instant by <see cref="CalculateForElectional(ChartInput, DateTime, CancellationToken)"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the latitude in <paramref name="input"/> is outside −90 to 90, its longitude outside
    /// −180 to 180, or the instant outside the years the ephemeris files cover, 1200 to 2399
    /// (1200-01-01 04:54:46 UT on the Julian calendar to 2399-12-31 23:51:20 UT), with the refusal
    /// <see cref="Calculate"/> gives; a Julian Day no instant can hold, or not a number, is refused
    /// the same way, naming the Julian Day.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the Swiss Ephemeris cannot calculate the chart, as for <see cref="Calculate"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="input"/> is a horary chart with a question category and its horary
    /// analysis reports an internal invariant breach, as for <see cref="Calculate"/>.
    /// </exception>
    ChartResult CalculateAtInstant(ChartInput input, double julianDayUt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate a natal chart relocated to another place: the same birth, the same clock, the horizon of
    /// <paramref name="target"/> (branch K2, 1.3.4; ruling S-D6).
    /// </summary>
    /// <param name="natalChart">The natal chart, as <see cref="Calculate"/> cast it.</param>
    /// <param name="target">The place to relocate to.</param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <returns>
    /// The relocated chart. Its input is the natal chart's with the place changed — the target's latitude,
    /// longitude and location name, the name "<i>natal name</i> — Relocated to <i>place</i>" — and every other
    /// member copied: the clock (<see cref="ChartInput.LocalDateTime"/>, <see cref="ChartInput.TimeZone"/>,
    /// <see cref="ChartInput.UseExplicitOffset"/>, <see cref="ChartInput.UtcOffsetHours"/>), the house system,
    /// the event type and description, the gender, the notes and keywords, the horary question and category,
    /// and the minor-points switch (<see cref="ChartInput.IncludeMinorPoints"/>, branch Asteroids, 1.6.0), so
    /// the relocated chart carries its own five, the natal longitudes with the place's houses and angles.
    /// It is cast through <see cref="Calculate"/>'s own pipeline, so the reading resolves to the natal instant,
    /// the same Julian Day bit for bit, with the natal offset label, local-time mapping and Time Note.
    /// </returns>
    /// <remarks>
    /// <para>
    /// What follows the place: the angles, cusps, houses and placements, the altitudes, the aspects to the
    /// angles, the angular planets, the Lots and the parans, and so the chart ruler, mutual receptions and the
    /// Ascendant's triplicity rulers. What stays the birth chart's: day or night, and with it the triplicity
    /// dignities and the Planetary Sect table; the planetary hour, the natal chart's own
    /// (<see langword="null"/> stays <see langword="null"/>; none is computed at the place); and the printed
    /// clock. The Lots are cast by the birth's sect at the place's Ascendant. A horary chart keeps its category
    /// and its perfection is judged at the place.
    /// </para>
    /// <para>
    /// Until 1.3.4 a relocation was a whole chart cast at the place at an explicit offset of zero: day or night
    /// flipped in 74 of 187 standard relocations and the dignities, sect table and Lots' formula with it; the
    /// planetary hour's day was the UTC date (the wrong ruler at Tokyo, Sydney and Suva); the clock printed
    /// "UTC+0 (explicit)"; and the horary category was dropped (Y3-2, Y3-3).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="natalChart"/> or <paramref name="target"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the target's latitude is outside −90 to 90 or its longitude outside −180 to 180, or when
    /// the natal input's reading is refused as <see cref="Calculate"/> refuses it.
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when the natal input's timezone identifier is not valid.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the Swiss Ephemeris cannot calculate the chart at the place: for example a house system that
    /// cannot be drawn at its latitude.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the natal chart is a horary chart with a question category and its horary analysis at the
    /// place reports an internal invariant breach, as for <see cref="Calculate"/>.
    /// </exception>
    ChartResult CalculateRelocated(ChartResult natalChart, RelocationInput target, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate the chart of an electional candidate: positions, houses, aspects, and the
    /// midpoints with their activations, as the full chart computes them. The search scores
    /// from <see cref="CalculateElectionalScoringInputs"/>; this chart is cast only for what it
    /// reports.
    /// </summary>
    /// <param name="input">Chart input parameters (name, datetime, timezone, coordinates).</param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <returns>
    /// A <see cref="ChartResult"/> with positions, houses, aspects, midpoints and midpoint
    /// activations populated. Until 1.3.3 the midpoints were skipped, so every candidate's
    /// Midpoint Activations table was empty (E2-3). <see cref="ChartResult.MinorPoints"/> and
    /// <see cref="ChartResult.MinorPointAspects"/> are empty whatever
    /// <see cref="ChartInput.IncludeMinorPoints"/> says: an electional chart never carries the minor points
    /// (branch Asteroids, 1.6.0).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="input"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the latitude in <paramref name="input"/> is outside −90 to 90, its
    /// longitude outside −180 to 180, or its instant outside the years the ephemeris files
    /// cover, 1200 to 2399 (1200-01-01 04:54:46 UT on the Julian calendar to
    /// 2399-12-31 23:51:20 UT); when its local date is one of 1582-10-05 to 1582-10-14,
    /// which never occurred (the day after 1582-10-04 on the Julian calendar was 1582-10-15
    /// on the Gregorian); or when its instant falls on 29 February UT of 1300, 1400 or 1500
    /// on the Julian calendar, a date <see cref="DateTime"/> cannot hold.
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when the timezone identifier in <paramref name="input"/> is not valid.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the Swiss Ephemeris cannot calculate the chart: for example a house
    /// system that cannot be drawn at the latitude. An instant outside the ephemeris
    /// files is refused first, with <see cref="ArgumentOutOfRangeException"/>. Not where
    /// the Sun does not rise or set (the polar day or night): there the chart is returned
    /// without a planetary hour
    /// (<see cref="ChartResult.PlanetaryHour"/> is <see langword="null"/>), and
    /// <see cref="AstroCast.Core.Exceptions.SunDoesNotRiseOrSetException"/> never
    /// leaves this method.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="input"/> is a horary chart with a question
    /// category and its horary analysis reports an internal invariant breach. A
    /// valid chart never produces it; the exception reports a defect, not a
    /// condition to handle.
    /// </exception>
    ChartResult CalculateForElectional(ChartInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate the chart of an electional candidate at a given instant: positions, houses,
    /// aspects, and the midpoints with their activations, as the full chart computes them. The
    /// search scores from <see cref="CalculateElectionalScoringInputs"/>; this chart is cast only
    /// for the instants it reports.
    /// </summary>
    /// <param name="input">
    /// Chart input parameters. Its wall clock must be the reading of
    /// <paramref name="utcDateTime"/> on its zone's clock.
    /// </param>
    /// <param name="utcDateTime">
    /// The instant to cast at. It is read as UTC whatever its <see cref="DateTime.Kind"/>,
    /// and the chart's Julian Day is built from its whole seconds by arithmetic
    /// (<see cref="Utilities.JulianDayUtility.InstantToJulianDay"/>), never through a calendar: before
    /// 1582-10-15 the date's calendar switch read the instant's proleptic Gregorian date as a Julian
    /// one, 7 to 10 days late.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <returns>
    /// A <see cref="ChartResult"/> with positions, houses, aspects, midpoints and midpoint
    /// activations populated, cast at <paramref name="utcDateTime"/> and labelled with the offset
    /// in force then. Until 1.3.3 the midpoints were skipped (E2-3). Its minor-point lists are empty
    /// whatever the switch, as for <see cref="CalculateForElectional(ChartInput, CancellationToken)"/>.
    /// </returns>
    /// <remarks>
    /// An electional scan samples instants, not wall clocks. In an hour the clocks repeat,
    /// one wall clock names two instants, and converting it back would cast the earlier
    /// sample at the later instant (TZ-1). This overload casts at the instant itself; the
    /// result's <see cref="ChartResult.LocalTimeMapping"/> still says the reading was repeated.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="input"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="input"/> is cast at an explicit offset, or its wall clock
    /// is not the reading of <paramref name="utcDateTime"/> on its zone's clock.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the latitude in <paramref name="input"/> is outside −90 to 90, its
    /// longitude outside −180 to 180, or <paramref name="utcDateTime"/> outside the years
    /// the ephemeris files cover, 1200 to 2399 (1200-01-01 04:54:46 UT on the Julian
    /// calendar to 2399-12-31 23:51:20 UT).
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when the timezone identifier in <paramref name="input"/> is not valid.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the Swiss Ephemeris cannot calculate the chart, as for
    /// <see cref="CalculateForElectional(ChartInput, CancellationToken)"/>.
    /// </exception>
    ChartResult CalculateForElectional(ChartInput input, DateTime utcDateTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates only what the electional scorer reads, at the instant
    /// <see cref="CalculateForElectional(ChartInput, DateTime, CancellationToken)"/> would cast at:
    /// the positions, the houses and their assignment, the extended dignities and the chart
    /// context. For scoring only, never for display or export.
    /// </summary>
    /// <param name="input">
    /// Chart input parameters. Its wall clock must be the reading of
    /// <paramref name="utcDateTime"/> on its zone's clock.
    /// </param>
    /// <param name="utcDateTime">
    /// The instant to cast at, read and converted exactly as
    /// <see cref="CalculateForElectional(ChartInput, DateTime, CancellationToken)"/> reads and
    /// converts it.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <returns>
    /// A <see cref="ChartResult"/> whose <see cref="ChartResult.Positions"/>,
    /// <see cref="ChartResult.Houses"/>, <see cref="ChartResult.Dignities"/> and
    /// <see cref="ChartResult.ChartContext"/> are the full chart's at that instant, as are its
    /// identity members: <see cref="ChartResult.Input"/>, <see cref="ChartResult.JulianDay"/>,
    /// <see cref="ChartResult.UtcDateTime"/>, <see cref="ChartResult.ResolvedUtcOffsetHours"/>,
    /// <see cref="ChartResult.ResolvedOffsetLabel"/>, <see cref="ChartResult.LocalTimeMapping"/>,
    /// <see cref="ChartResult.ResolvedLocalDateTime"/>, <see cref="ChartResult.EarlierUtcDateTime"/>,
    /// <see cref="ChartResult.TimeNote"/>, <see cref="ChartResult.TimeZoneRules"/>,
    /// <see cref="ChartResult.HouseSystemName"/>, <see cref="ChartResult.EngineVersion"/> and
    /// <see cref="ChartResult.CalculatedAt"/>. <see cref="ChartResult.Aspects"/>,
    /// <see cref="ChartResult.Midpoints"/>, <see cref="ChartResult.MidpointActivations"/>,
    /// <see cref="ChartResult.FixedStarConjunctions"/>,
    /// <see cref="ChartResult.DeclinationAspects"/>, <see cref="ChartResult.MinorPoints"/> and
    /// <see cref="ChartResult.MinorPointAspects"/> are empty;
    /// <see cref="ChartResult.MoonCondition"/> and <see cref="ChartResult.PlanetaryHour"/> are
    /// <see langword="null"/>; every other member has its default.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The electional search scores every eligible sample of its grid from these inputs and
    /// casts the full chart only for the instants it reports (branch O2, 1.3.3). The scorer
    /// reads the seven traditional bodies' sign, longitude, retrograde flag and house, the
    /// Ascendant, the angles Mars and Saturn sit on, Mars's and Saturn's dignity, the sect and
    /// the chart ruler; the Moon and the planetary hour reach it separately. Everything else a
    /// full chart computes, the aspects, fixed stars, declinations, the chart's own Moon
    /// condition, the planetary hour, Arabic parts, antiscia, stations and parans, it never reads.
    /// </para>
    /// <para>
    /// The members it does read come from the same calls, in the same order, as the full chart's,
    /// so they are equal by construction. Tests hold them equal, and the scores equal for every
    /// election type, at every sample of a month in three places, in an hour the clocks repeat,
    /// and at an instant outside the ephemeris files.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="input"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="input"/> is cast at an explicit offset, or its wall clock
    /// is not the reading of <paramref name="utcDateTime"/> on its zone's clock.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the latitude in <paramref name="input"/> is outside −90 to 90, its
    /// longitude outside −180 to 180, or <paramref name="utcDateTime"/> outside the years
    /// the ephemeris files cover, 1200 to 2399 (1200-01-01 04:54:46 UT on the Julian
    /// calendar to 2399-12-31 23:51:20 UT).
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when the timezone identifier in <paramref name="input"/> is not valid.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the Swiss Ephemeris cannot calculate the positions or the houses, as for
    /// <see cref="CalculateForElectional(ChartInput, CancellationToken)"/>.
    /// </exception>
    ChartResult CalculateElectionalScoringInputs(ChartInput input, DateTime utcDateTime, CancellationToken cancellationToken = default);
}
