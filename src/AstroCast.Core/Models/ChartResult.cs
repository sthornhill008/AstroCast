using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;

namespace AstroCast.Core.Models;

/// <summary>
/// Complete chart calculation result containing all computed data.
/// This is the universal output object consumed by all presentation layers
/// (the CLI, the WPF and Avalonia heads, and the Markdown, JSON and PDF exports).
/// </summary>
/// <remarks>
/// Produced by <see cref="AstroCast.Core.Interfaces.IChartEngine.Calculate"/>
/// which orchestrates calls to the ephemeris, aspect, and midpoint services.
/// Positions, aspects, and midpoints are computed on demand — never stored.
/// </remarks>
public sealed class ChartResult
{
    /// <summary>The input parameters used for this calculation.</summary>
    public required ChartInput Input { get; init; }

    /// <summary>Julian Day (Universal Time) used for the calculation.</summary>
    public required double JulianDay { get; init; }

    /// <summary>
    /// The true obliquity of the ecliptic (nutation included) at <see cref="JulianDay"/>, in degrees, as
    /// <see cref="IEphemerisService.GetObliquity"/> returns it. For a composite it is taken at the midpoint
    /// Julian Day its houses are cast for. Out-of-bounds is read against it.
    /// </summary>
    public required double Obliquity { get; init; }

    /// <summary>
    /// UTC date/time of the chart: the instant <see cref="JulianDay"/> denotes, to the whole
    /// second, always with <see cref="DateTimeKind.Utc"/>, whether the chart was cast through
    /// its zone or at an explicit offset, on every chart (R-D4, branch R2, 1.3.4). It is
    /// proleptic ticks, as .NET counts them; a label on the calendar in force is
    /// <see cref="Formatting.CalendarDateFormatter"/>'s, and a reading on it
    /// <see cref="Formatting.CalendarDateFormatter.Reading"/>'s. Until 1.3.4 a natal chart's was
    /// held as its Julian label before 1582-10-15, 7 to 10 days from the instant as ticks.
    /// </summary>
    public required DateTime UtcDateTime { get; init; }

    /// <summary>All planetary and point positions.</summary>
    public required IReadOnlyList<PlanetPosition> Positions { get; init; }

    /// <summary>House cusps and angles.</summary>
    public required HouseData Houses { get; init; }

    /// <summary>All detected aspects (planet-to-planet and planet-to-angle).</summary>
    public required IReadOnlyList<AspectResult> Aspects { get; init; }

    /// <summary>
    /// The five minor points, in this order: Ceres, Pallas, Juno, Vesta and Lilith (Black Moon Lilith, the mean
    /// lunar apogee, <see cref="CelestialBodyId.MeanApogee"/>), each computed as the twelve bodies are, with its
    /// house by longitude between the chart's cusps (<see cref="PlanetPosition.House"/>), its direction by its
    /// speed (<see cref="PlanetPosition.IsRetrograde"/>; the mean Lilith is never retrograde) and its right
    /// ascension and declination, out of bounds being read against <see cref="Obliquity"/> as every body's is
    /// (branch Asteroids, 1.6.0). Empty unless <see cref="ChartInput.IncludeMinorPoints"/> is on, and always
    /// empty on a horary chart, an electional chart and the electional scoring inputs.
    /// </summary>
    /// <remarks>
    /// Never in <see cref="Positions"/>, which stays the twelve: no midpoint, fixed star, declination aspect,
    /// dignity, condition, Lot, antiscion, station or paran reads the five, and they take no altitude.
    /// </remarks>
    public IReadOnlyList<PlanetPosition> MinorPoints { get; init; } = [];

    /// <summary>
    /// The minor points' aspects (<see cref="IAspectService.CalculateMinorPointAspects"/>): from each of the five
    /// to the Sun to Pluto, the Ascendant and the Midheaven only, the five Ptolemaic aspects at an orb of 2°,
    /// inclusive, applying or separating by the speeds with the angles held still; never between the five, nor
    /// to the True Node, Chiron or the Part of Fortune. <see cref="AspectResult.Body1Name"/> is the point and
    /// <see cref="AspectResult.Body2Name"/> the target. Sorted by orb, tightest first; empty whenever
    /// <see cref="MinorPoints"/> is.
    /// </summary>
    public IReadOnlyList<AspectResult> MinorPointAspects { get; init; } = [];

    /// <summary>All midpoint pair calculations.</summary>
    public required IReadOnlyList<Midpoint> Midpoints { get; init; }

    /// <summary>All midpoint activations on the 45° dial.</summary>
    public required IReadOnlyList<MidpointActivation> MidpointActivations { get; init; }

    /// <summary>
    /// Essential dignity information for each planet in the chart.
    /// Describes domicile, exaltation, detriment, fall, and peregrine status.
    /// </summary>
    public required IReadOnlyList<DignityInfo> Dignities { get; init; }

    /// <summary>
    /// Moon condition analysis: phase, Void of Course, first applying aspect,
    /// and Via Combusta status. Null if the chart has no Moon or no Sun
    /// position, and on the electional scoring inputs, which leave it unset.
    /// </summary>
    public required MoonConditionInfo? MoonCondition { get; init; }

    /// <summary>
    /// Interpretive chart context including chart ruler, sect, event significator,
    /// angular planets, and mutual receptions.
    /// </summary>
    public required ChartContextInfo? ChartContext { get; init; }

    /// <summary>
    /// Fixed star conjunctions with chart points (planets, angles, nodes, Chiron)
    /// within the 1 degree 00 arc-minute orb. Sorted by chart point priority
    /// then by orb (tightest first). Empty list if no conjunctions found.
    /// </summary>
    public required IReadOnlyList<FixedStarConjunction> FixedStarConjunctions { get; init; }

    /// <summary>
    /// Declination aspects (parallels and contraparallels) between planetary
    /// positions, detected within a ±1° orb. Parallels are equivalent in
    /// strength to conjunctions; contraparallels to oppositions.
    /// Sorted by orb (tightest first). Empty list if none found.
    /// </summary>
    public IReadOnlyList<DeclinationAspect> DeclinationAspects { get; init; } = [];

    /// <summary>
    /// Accidental conditions for each planet: combustion (cazimi, combust,
    /// under the beams), besiegement, and benefic/malefic influence.
    /// Only planets with at least one active condition are included.
    /// </summary>
    public IReadOnlyList<PlanetCondition> PlanetConditions { get; init; } = [];

    /// <summary>
    /// The Moon's lunar mansion at the chart moment. One of 28 equal
    /// divisions of the ecliptic, each with a traditional nature and
    /// interpretive keywords. Null if the chart has no Moon position.
    /// </summary>
    public MoonMansionInfo? MoonMansion { get; init; }

    /// <summary>
    /// The planetary hour at the chart moment, with radicality analysis
    /// for horary charts. Null if planetary hour could not be computed
    /// (e.g., extreme polar latitudes with no sunrise/sunset). A relocated
    /// chart carries the natal chart's own hour, none computed at the place
    /// (S-D6, branch K2, 1.3.4), so it is null only when the natal chart's is.
    /// </summary>
    public PlanetaryHourInfo? PlanetaryHour { get; init; }

    /// <summary>
    /// Calculated Arabic Parts (Lots) and Hellenistic Lots. Contains 15 entries:
    /// 8 traditional Arabic Parts and 7 Hermetic Lots. Each Part has a longitude,
    /// sign, house, ruler, and domain description. Empty list if not computed.
    /// </summary>
    public IReadOnlyList<ArabicPart> ArabicParts { get; init; } = [];

    /// <summary>
    /// Antiscia points for each planet — mirror reflections across the solstice
    /// axis (0° Cancer / 0° Capricorn). Each entry includes the antiscion and
    /// contra-antiscion longitudes with their zodiac signs.
    /// </summary>
    public IReadOnlyList<AntisciaPoint> AntisciaPoints { get; init; } = [];

    /// <summary>
    /// Detected antiscia and contra-antiscia connections between planet pairs
    /// within the classical 2° orb. Antiscia connections act as hidden conjunctions;
    /// contra-antiscia connections act as hidden oppositions.
    /// Sorted by orb (tightest first).
    /// </summary>
    public IReadOnlyList<AntisciaConnection> AntisciaConnections { get; init; } = [];

    /// <summary>
    /// Planets currently stationing (speed below the station threshold).
    /// Only stationing planets are included; non-stationing planets are
    /// omitted. Empty list if no planets are stationing.
    /// </summary>
    public IReadOnlyList<StationInfo> Stations { get; init; } = [];

    /// <summary>
    /// Horary perfection analysis, present only for a chart whose
    /// <see cref="ChartInput.EventType"/> is "Horary" and whose
    /// <see cref="ChartInput.HoraryCategory"/> is set. Null for every other
    /// chart. Settable rather than init-only because the analysis consumes the
    /// assembled result (positions, aspects, houses, context, conditions), so
    /// <c>ChartEngine</c> computes it after construction and stores it here;
    /// the formatters only render it.
    /// </summary>
    public HoraryPerfectionResult? HoraryPerfection { get; set; }

    /// <summary>
    /// Paran (paranatellonta) analysis for the chart moment and location.
    /// Contains angular crossings and detected paran events within the
    /// configured time orb. Null if paran calculation failed (e.g., extreme
    /// polar latitudes where rise/set events do not occur).
    /// </summary>
    public ParanResult? Parans { get; init; }

    /// <summary>
    /// The UTC offset (in hours) that was actually applied during the
    /// local-to-UTC conversion. Positive = east of Greenwich, negative = west.
    /// For a chart cast at an explicit offset it is the difference between
    /// <see cref="ChartInput.LocalDateTime"/> and <see cref="UtcDateTime"/>; for a
    /// chart cast through its zone it is the zone's offset at <see cref="UtcDateTime"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see cref="ChartInput.UseExplicitOffset"/> is true, this equals
    /// <see cref="ChartInput.UtcOffsetHours"/>. When false, this is the offset
    /// the app's own IANA time-zone data (<see cref="TimeZoneRules"/>) gives at the
    /// instant, to the second: −4.9339 for New York's local mean time (−4:56:02), and
    /// −4 (EDT) for a skipped 02:30 in New York cast at 03:00 EDT, where the difference
    /// between the entered reading and the instant would be −4.5.
    /// </para>
    /// <para>
    /// This field exists for verification — the user should always be able to
    /// confirm that the correct offset was applied, especially for historical
    /// charts where the IANA database may disagree with local records.
    /// </para>
    /// </remarks>
    public required double ResolvedUtcOffsetHours { get; init; }

    /// <summary>
    /// Human-readable label for the resolved UTC offset, including the
    /// standard timezone abbreviation when identifiable.
    /// Examples: "EST (UTC−5)", "EDT (UTC−4)", "UTC+1 (explicit)", "UTC+0".
    /// </summary>
    /// <remarks>
    /// Built by <see cref="Formatting.OffsetLabelFormatter"/>: the zone record's name
    /// when it is a word, then the offset, as hours when it is a whole or half hour and
    /// as h:mm or h:mm:ss otherwise ("LMT (UTC−4:56:02)").
    /// </remarks>
    public required string ResolvedOffsetLabel { get; init; }

    /// <summary>
    /// How the chart's wall clock mapped to an instant in its zone: once, twice (a
    /// repeated hour, cast at the later instant), or not at all (a skipped hour, cast at
    /// the first instant after the gap). <see cref="LocalTimeMapping.Unique"/> for a chart
    /// cast at an explicit offset.
    /// </summary>
    public LocalTimeMapping LocalTimeMapping { get; init; } = LocalTimeMapping.Unique;

    /// <summary>
    /// The wall clock the chart was cast at, on its own clock: the entered reading, except
    /// for a skipped hour, when it is the reading at the end of the gap (03:00 for a
    /// skipped 02:30 in New York). <see langword="null"/> on a result the chart engine did
    /// not cast.
    /// </summary>
    public DateTime? ResolvedLocalDateTime { get; init; }

    /// <summary>
    /// For a repeated reading, the earlier of the two instants its wall clock names, from the
    /// zone's own record (05:30 UT for 01:30 on 2026-11-01 in New York, cast at 06:30 UT).
    /// <see langword="null"/> for any other chart.
    /// </summary>
    public DateTime? EarlierUtcDateTime { get; init; }

    /// <summary>
    /// The Time Note: one or two plain sentences saying how the chart's wall clock was read
    /// when it was not a single instant on its zone's standard time — a reading the clocks
    /// repeated or skipped, or one on local mean time — built by
    /// <see cref="Formatting.TimeNoteFormatter"/> and culture-independent.
    /// <see langword="null"/> for an ordinary reading and for any chart cast at an explicit
    /// offset.
    /// </summary>
    /// <remarks>
    /// The chart exports print it under its own label, <c>**Time Note:**</c>, beneath the
    /// UTC Offset line, and the chart JSON as <c>timeNote</c>; never inside "Notes", which
    /// holds the user's own text (and a horary chart's question).
    /// </remarks>
    public string? TimeNote { get; init; }

    /// <summary>
    /// The time-zone rules the chart was cast with. Exports convert the chart's other
    /// instants (stations, perfections) on the same rules, and can name their data version.
    /// <see langword="null"/> on a result the chart engine did not cast; its consumers then
    /// use the data the app carries.
    /// </summary>
    public ITimeZoneRules? TimeZoneRules { get; init; }

    /// <summary>
    /// Human-readable name of the house system used for this chart
    /// (e.g., "Placidus", "Equal (from ASC)", "Whole Sign").
    /// Defaults to "Placidus" for backward compatibility with existing chart data.
    /// </summary>
    public string HouseSystemName { get; init; } = "Placidus";

    /// <summary>Calculation engine version info.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>Timestamp when this calculation was performed.</summary>
    public required DateTime CalculatedAt { get; init; }
}
