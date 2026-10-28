using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using Microsoft.Extensions.Time.Testing;

using Serilog;

namespace AstroCast.Tests.Formatting.Golden;

/// <summary>
/// Deterministic fixture helpers shared across golden-file formatter tests.
/// All inputs are hardcoded so the same <see cref="ChartResult"/> is produced
/// on any machine at any time; no dependency on <c>DateTime.Now</c> or locale.
/// </summary>
/// <remarks>
/// <para>
/// <b>Primary natal — J2000 NYC.</b> 2000-01-01 07:00 America/New_York,
/// Placidus houses, 40.7128°N, 74.006°W. Same reference moment used by
/// <c>ChartFormatterGoldenTests</c> and by the rest of the AstroCast test
/// suite; reused here so every formatter's golden is anchored to the same
/// well-known chart.
/// </para>
/// <para>
/// <b>Secondary natal.</b> 1970-06-15 12:00 America/Los_Angeles, Placidus,
/// 34.0522°N, 118.2437°W. Chosen to be clearly distinct from J2000 NYC in
/// epoch, hemisphere, and longitude so that inter-chart aspect logic in
/// Synastry and Composite formatters has real structure to exercise — two
/// charts at the same moment and location would collapse every inter-aspect
/// to 0°.
/// </para>
/// <para>
/// <b>Engine construction.</b> <see cref="CreateChartEngine"/> replicates the
/// full <see cref="ChartEngine"/> dependency graph using the shared
/// <see cref="EphemerisFixture"/>. Kept here (rather than promoted to a
/// cross-project helper) per the Session 3.5 scope — one solo developer,
/// premature abstraction costs more than the DRY benefit returns.
/// </para>
/// </remarks>
internal static class GoldenFixtures
{
    #region Golden Clock (F-GR-2)

    /// <summary>
    /// The single reference "today" that every date-sensitive golden is pinned
    /// to: 2026-06-10, the date the current profection / firdaria / zodiacal
    /// releasing goldens were generated at commit <c>dad3c56</c>. Injecting a
    /// clock fixed to this day makes the natal time-lord sections deterministic
    /// regardless of the real calendar, closing the F-GR-2 golden-drift bug
    /// (before this, <c>ChartMarkdownFormatter</c> read <see cref="DateTime.Today"/>
    /// and the goldens expired on every profection-year boundary). Changing this
    /// value re-dates every profection section and will require regenerating the
    /// affected goldens.
    /// </summary>
    public static readonly DateOnly GoldenClockDate = new(2026, 6, 10);

    /// <summary>
    /// Builds a <see cref="FakeTimeProvider"/> fixed to
    /// <see cref="GoldenClockDate"/> at noon UTC, with the local time zone
    /// pinned to UTC. The noon anchor plus an explicit UTC local zone guarantee
    /// that <c>GetLocalNow().Date</c> — the value
    /// <c>ChartMarkdownFormatter.ToMarkdown</c> derives "today" from — resolves
    /// to <see cref="GoldenClockDate"/> on any host. <see cref="FakeTimeProvider"/>
    /// already defaults its local zone to UTC, but pinning it explicitly guards
    /// against a day-boundary slip should that default ever change.
    /// </summary>
    /// <returns>
    /// A fresh clock the date-sensitive formatter goldens pass as the
    /// <c>timeProvider</c> argument so their profection / firdaria / zodiacal
    /// releasing sections render identically on every machine and every day.
    /// </returns>
    public static FakeTimeProvider GoldenClock()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(
            GoldenClockDate.Year, GoldenClockDate.Month, GoldenClockDate.Day,
            12, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.Utc);
        return clock;
    }

    #endregion

    #region Canonical Chart Inputs

    /// <summary>
    /// Builds the canonical J2000 NYC natal <see cref="ChartInput"/>.
    /// </summary>
    /// <param name="includeMinorPoints">Whether the chart carries the minor points (branch Asteroids, 1.6.0); off by default, as every golden casts it.</param>
    /// <returns>A new <see cref="ChartInput"/> for 2000-01-01 07:00 America/New_York at NYC.</returns>
    public static ChartInput J2000NycInput(bool includeMinorPoints = false) => new()
    {
        Name = "J2000 NYC Fixture",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
        IncludeMinorPoints = includeMinorPoints,
    };

    /// <summary>
    /// Builds the secondary natal <see cref="ChartInput"/> used by
    /// pair-formatter tests (Synastry, Composite).
    /// </summary>
    /// <param name="includeMinorPoints">Whether the chart carries the minor points (branch Asteroids, 1.6.0); off by default.</param>
    /// <returns>A new <see cref="ChartInput"/> for 1970-06-15 12:00 America/Los_Angeles.</returns>
    public static ChartInput SecondaryNatalInput(bool includeMinorPoints = false) => new()
    {
        Name = "Secondary Fixture",
        LocalDateTime = new DateTime(1970, 6, 15, 12, 0, 0),
        TimeZone = "America/Los_Angeles",
        Latitude = 34.0522,
        Longitude = -118.2437,
        LocationName = "Los Angeles",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
        IncludeMinorPoints = includeMinorPoints,
    };

    /// <summary>
    /// Person A's input (invented, branch Fixtures, 1.7.1): 1947-10-05 09:30:00 at an explicit UTC−5,
    /// Harrisburg, Pennsylvania, Placidus. A day chart with a Scorpio Ascendant whose Placidus cusps skip
    /// and repeat a sign, an out-of-bounds Moon and an evening-star Mercury (fixtures/truth/persons.py).
    /// </summary>
    /// <param name="sourceNotes">The birth time's source, "Birth Certificate" for person A; none by default.</param>
    /// <param name="includeMinorPoints">Whether the chart carries the minor points; off by default.</param>
    /// <returns>A new <see cref="ChartInput"/> for person A.</returns>
    public static ChartInput HartwellInput(string? sourceNotes = null, bool includeMinorPoints = false) => new()
    {
        Name = "Eleanor Hartwell",
        SourceNotes = sourceNotes,
        LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
        TimeZone = "America/New_York",
        UtcOffsetHours = -5.0,
        UseExplicitOffset = true,
        Latitude = 40.2737,
        Longitude = -76.8844,
        LocationName = "Harrisburg, Pennsylvania",
        EventType = "Birth",
        HouseSystem = HouseSystem.Placidus,
        IncludeMinorPoints = includeMinorPoints,
    };

    /// <summary>
    /// M35's input, as <c>ProgressedHitFinderTests</c> builds it: 1960-02-05 12:00 UT at Greenwich, Etc/UTC at an
    /// explicit offset of 0 (branch Primary, 1.5.0).
    /// </summary>
    /// <param name="includeMinorPoints">Whether the chart carries the minor points (branch Asteroids, 1.6.0); off by default.</param>
    /// <returns>A new <see cref="ChartInput"/> for M35.</returns>
    public static ChartInput M35Input(bool includeMinorPoints = false) =>
        GreenwichInput("M35", new DateTime(1960, 2, 5, 12, 0, 0), includeMinorPoints);

    /// <summary>
    /// N1's input, as <c>ProgressedHitFinderTests</c> builds it: Julian 1300-03-15 12:00 UT at Greenwich, Etc/UTC at
    /// an explicit offset of 0, the pre-1583 fixture (branch Primary, 1.5.0).
    /// </summary>
    /// <param name="includeMinorPoints">Whether the chart carries the minor points (branch Asteroids, 1.6.0); off by default.</param>
    /// <returns>A new <see cref="ChartInput"/> for N1.</returns>
    public static ChartInput N1Input(bool includeMinorPoints = false) =>
        GreenwichInput("N1", new DateTime(1300, 3, 15, 12, 0, 0), includeMinorPoints);

    /// <summary>
    /// The polar fixture: 1990-06-21 12:00 Europe/Oslo at Tromsø, 69.6492° N 18.9553° E, in Equal houses, which the
    /// polar circle does not refuse; the Sun never sets at the birth (branch Primary, 1.5.0).
    /// </summary>
    /// <param name="includeMinorPoints">Whether the chart carries the minor points (branch Asteroids, 1.6.0); off by default.</param>
    /// <returns>A new <see cref="ChartInput"/> for Tromsø.</returns>
    public static ChartInput TromsoInput(bool includeMinorPoints = false) => new()
    {
        Name = "Tromso",
        LocalDateTime = new DateTime(1990, 6, 21, 12, 0, 0),
        TimeZone = "Europe/Oslo",
        Latitude = 69.6492,
        Longitude = 18.9553,
        LocationName = "Tromsø",
        EventType = "Birth",
        HouseSystem = HouseSystem.Equal,
        IncludeMinorPoints = includeMinorPoints,
    };

    /// <summary>A chart at Greenwich read at an explicit offset of 0, as <c>ProgressedHitFinderTests</c> builds one.</summary>
    /// <param name="name">The chart's name.</param>
    /// <param name="localDateTime">The reading, UT.</param>
    /// <param name="includeMinorPoints">Whether the chart carries the minor points (branch Asteroids, 1.6.0).</param>
    /// <returns>The input.</returns>
    private static ChartInput GreenwichInput(string name, DateTime localDateTime, bool includeMinorPoints = false) => new()
    {
        Name = name,
        LocalDateTime = localDateTime,
        TimeZone = "Etc/UTC",
        UseExplicitOffset = true,
        UtcOffsetHours = 0.0,
        Latitude = 51.4779,
        Longitude = -0.0015,
        LocationName = "Greenwich",
        EventType = "Birth",
        IncludeMinorPoints = includeMinorPoints,
    };

    #endregion

    #region Engine Construction

    /// <summary>
    /// Builds a fully-wired <see cref="ChartEngine"/> for golden-test use,
    /// sharing the provided <paramref name="fixture"/>'s ephemeris service.
    /// </summary>
    /// <param name="fixture">Shared <see cref="EphemerisFixture"/> from xUnit collection.</param>
    /// <returns>
    /// A new <see cref="ChartEngine"/> with every service dependency
    /// instantiated. The returned engine carries no state between
    /// <c>Calculate</c> calls and is safe to reuse within a single test.
    /// </returns>
    public static ChartEngine CreateChartEngine(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        ILogger logger = new LoggerConfiguration().CreateLogger();
        DignityService dignity = new();
        SectService sect = new();
        AspectService aspects = new(fixture.EphemerisService, logger);

        return new ChartEngine(
            fixture.EphemerisService,
            aspects,
            new MidpointService(logger),
            dignity,
            new MoonConditionService(),
            new ChartContextService(dignity, sect),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(),
            new PlanetConditionService(),
            new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(dignity),
            new AntisciaService(),
            new StationService(),
            new ParanService(fixture.EphemerisService, logger),
            logger);
    }

    #endregion

    #region Ready-Made Chart Results

    /// <summary>
    /// Produces the canonical J2000 NYC natal chart using a freshly built engine.
    /// </summary>
    /// <param name="fixture">Shared ephemeris fixture.</param>
    /// <returns>The J2000 NYC <see cref="ChartResult"/>.</returns>
    public static ChartResult J2000Nyc(EphemerisFixture fixture) =>
        CreateChartEngine(fixture).Calculate(J2000NycInput());

    /// <summary>
    /// Produces the secondary natal chart using a freshly built engine.
    /// </summary>
    /// <param name="fixture">Shared ephemeris fixture.</param>
    /// <returns>The secondary <see cref="ChartResult"/>.</returns>
    public static ChartResult SecondaryNatal(EphemerisFixture fixture) =>
        CreateChartEngine(fixture).Calculate(SecondaryNatalInput());

    #endregion
}
