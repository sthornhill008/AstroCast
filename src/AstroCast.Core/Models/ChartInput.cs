using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Input parameters for an astrological chart calculation.
/// Stores the event data needed to compute planetary positions and house cusps.
/// </summary>
/// <remarks>
/// <para>
/// By default, the engine converts <see cref="LocalDateTime"/> to UTC using
/// the IANA <see cref="TimeZone"/> identifier. For historical dates where
/// the IANA timezone database may apply incorrect DST rules, set
/// <see cref="UseExplicitOffset"/> to <c>true</c> and provide the known
/// UTC offset in <see cref="UtcOffsetHours"/>.
/// </para>
/// <para>
/// This is essential for pre-1966 charts in the United States, where local
/// DST rules varied by municipality and the IANA database cannot reliably
/// determine whether DST was in effect for a specific locality.
/// </para>
/// </remarks>
public sealed class ChartInput
{
    /// <summary>Name of the person or event.</summary>
    public required string Name { get; init; }

    /// <summary>Local date and time of the event.</summary>
    public required DateTime LocalDateTime { get; init; }

    /// <summary>IANA timezone identifier (e.g., "America/New_York").</summary>
    public required string TimeZone { get; init; }

    /// <summary>Geographic latitude in decimal degrees (north positive).</summary>
    public required double Latitude { get; init; }

    /// <summary>Geographic longitude in decimal degrees (east positive).</summary>
    public required double Longitude { get; init; }

    /// <summary>Human-readable location name.</summary>
    public required string LocationName { get; init; }

    /// <summary>Event type: Birth, Event, Horary, etc.</summary>
    public string EventType { get; init; } = "Birth";

    /// <summary>Optional event description.</summary>
    public string? EventDescription { get; init; }

    /// <summary>Gender of the chart subject.</summary>
    public Gender Gender { get; init; } = Gender.Unspecified;

    /// <summary>Source of the birth/event data.</summary>
    public string? SourceNotes { get; init; }

    /// <summary>Optional notes.</summary>
    public string? Notes { get; init; }

    /// <summary>Comma-separated keywords for categorisation and filtering.</summary>
    public string? Keywords { get; init; }

    /// <summary>
    /// Explicit UTC offset in hours (e.g., -5 for EST, -4 for EDT).
    /// When set together with <see cref="UseExplicitOffset"/> = <c>true</c>,
    /// this overrides the IANA timezone conversion.
    /// Positive = East of Greenwich, Negative = West.
    /// </summary>
    /// <remarks>
    /// Use this for historical charts where the IANA timezone database may
    /// apply incorrect DST rules. Reference software (Solar Fire, Janus)
    /// typically allows specifying "Time zone: 5 Hrs, DST: N" — this
    /// property serves the same purpose.
    /// </remarks>
    public double? UtcOffsetHours { get; init; }

    /// <summary>
    /// Whether the user has explicitly confirmed the UTC offset.
    /// When <c>true</c>, <see cref="UtcOffsetHours"/> is used instead of
    /// IANA timezone lookup for the local-to-UTC conversion.
    /// </summary>
    public bool UseExplicitOffset { get; init; }

    /// <summary>
    /// House system to use for house cusp calculation.
    /// Defaults to Placidus. The character value is passed directly to
    /// the Swiss Ephemeris <c>swe_houses()</c> function.
    /// </summary>
    public HouseSystem HouseSystem { get; init; } = HouseSystem.Placidus;

    /// <summary>
    /// Whether the chart carries the five minor points, Ceres, Pallas, Juno, Vesta and Lilith (Black Moon
    /// Lilith, the mean lunar apogee), in <see cref="ChartResult.MinorPoints"/> with their aspects in
    /// <see cref="ChartResult.MinorPointAspects"/> (branch Asteroids, 1.6.0). Off by default, and with it off
    /// every output is as it was.
    /// </summary>
    /// <remarks>
    /// A session value, never saved: <see cref="Data.ChartMapper.ToEntity"/> does not map it, the row has no
    /// column for it, and <see cref="Data.ChartMapper.ToInput"/> takes it from its caller. The relocation
    /// (<see cref="Interfaces.IChartEngine.CalculateRelocated"/>) and the solar, lunar and planetary return
    /// recasts copy it from the natal input, so each recast chart carries its own five. A horary chart
    /// (<see cref="EventType"/> "Horary") and the electional pipelines never carry the points, whatever its
    /// value.
    /// </remarks>
    public bool IncludeMinorPoints { get; init; }

    /// <summary>
    /// The horary question being asked. Only populated when EventType is "Horary".
    /// The first line is the question itself; subsequent lines provide context.
    /// </summary>
    public string? HoraryQuestion { get; init; }

    /// <summary>
    /// The horary question category, which determines the quesited house.
    /// Only populated when EventType is "Horary".
    /// Examples: "Career", "Relationship", "Health", "Money", "Home", "Travel", "Legal", "Children", "Lost Objects".
    /// </summary>
    public string? HoraryCategory { get; init; }

    /// <summary>
    /// Returns a copy of this input with <see cref="IncludeMinorPoints"/> set to
    /// <paramref name="includeMinorPoints"/> and every other property as it is here: the input the Chart
    /// panel's chart is recast from when the Points box is ticked or unticked (branch AsteroidsPanel, 1.6.0).
    /// </summary>
    /// <remarks>
    /// <see cref="ChartInput"/> is a sealed class whose properties are init-only, so a <c>with</c> expression
    /// cannot copy it. A property added to <see cref="ChartInput"/> must be copied here too:
    /// <c>ChartInputTests.ChartInput_HasTheEighteenPropertiesTheCopyKnows</c> holds the property list to the
    /// eighteen this method copies, and fails when one is added.
    /// </remarks>
    /// <param name="includeMinorPoints">The copy's <see cref="IncludeMinorPoints"/>.</param>
    /// <returns>A new <see cref="ChartInput"/>; this one is unchanged.</returns>
    public ChartInput WithIncludeMinorPoints(bool includeMinorPoints) => new()
    {
        Name = Name,
        LocalDateTime = LocalDateTime,
        TimeZone = TimeZone,
        Latitude = Latitude,
        Longitude = Longitude,
        LocationName = LocationName,
        EventType = EventType,
        EventDescription = EventDescription,
        Gender = Gender,
        SourceNotes = SourceNotes,
        Notes = Notes,
        Keywords = Keywords,
        UtcOffsetHours = UtcOffsetHours,
        UseExplicitOffset = UseExplicitOffset,
        HouseSystem = HouseSystem,
        IncludeMinorPoints = includeMinorPoints,
        HoraryQuestion = HoraryQuestion,
        HoraryCategory = HoraryCategory,
    };
}
