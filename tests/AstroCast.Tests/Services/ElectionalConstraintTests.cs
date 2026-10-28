using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for the electional time-of-day / day-of-week candidate constraints
/// (T5–T7, 2026-06-10 live-run review, Defect B): candidate filtering,
/// export-header recording, the no-candidate export path, and the
/// midnight-wrap validation guard.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalConstraintTests
{
    private readonly EphemerisService _ephemeris;
    private readonly ChartEngine _chartEngine;
    private readonly ElectionalService _service;

    /// <summary>Richmond, VA latitude (Doctor Visit live-run location).</summary>
    private const double Latitude = 37.5385;

    /// <summary>Richmond, VA longitude.</summary>
    private const double Longitude = -77.4343;

    /// <summary>Richmond, VA IANA timezone.</summary>
    private const string TimeZone = "America/New_York";

    /// <summary>
    /// Cached natal chart shared across tests in this class (the chart itself
    /// is deterministic; recalculating it per test only costs time).
    /// </summary>
    private static ChartResult? s_natalChart;

    /// <summary>
    /// Cached result of the weekday-office-hours search shared by the T5
    /// filtering assertions and the T6 export-header assertions, so the
    /// expensive search runs once.
    /// </summary>
    private static ElectionalResult? s_constrainedResult;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all services.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public ElectionalConstraintTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(_ephemeris, logger);
        MidpointService midpoints = new(logger);
        _chartEngine = new ChartEngine(_ephemeris, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(_ephemeris, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(_ephemeris), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
        _service = new ElectionalService(_ephemeris, _chartEngine);
    }

    #region T5 — Time-window and day-of-week filtering

    [Fact]
    public void Calculate_WeekdayOfficeHoursConstraints_EmitsOnlyCompliantCandidates()
    {
        // Arrange / Act — 08:00–17:00, Mon–Fri over a 9-day Richmond window
        ElectionalResult result = GetConstrainedResult();

        // Assert — the search must produce candidates for the assertions to bind
        result.Candidates.Should().NotBeEmpty(
            "a 9-day window with weekday office hours must yield at least one candidate");

        TimeOnly earliest = new(8, 0);
        TimeOnly latest = new(17, 0);
        DayOfWeek[] allowedDays =
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

        foreach (CandidateWindow candidate in result.Candidates)
        {
            TimeOnly localTime = TimeOnly.FromDateTime(candidate.LocalDateTime);

            localTime.Should().BeOnOrAfter(earliest,
                $"candidate at {candidate.LocalDateTime:yyyy-MM-dd HH:mm} is before the 08:00 window start");
            localTime.Should().BeOnOrBefore(latest,
                $"candidate at {candidate.LocalDateTime:yyyy-MM-dd HH:mm} is after the 17:00 window end");
            allowedDays.Should().Contain(candidate.LocalDateTime.DayOfWeek,
                $"candidate at {candidate.LocalDateTime:yyyy-MM-dd HH:mm} falls on a disallowed "
                + $"{candidate.LocalDateTime.DayOfWeek}");
        }

        // The live-run failure mode: a 23:44 candidate must never be emitted
        result.Candidates.Should().NotContain(
            c => c.LocalDateTime.Hour == 23 && c.LocalDateTime.Minute == 44,
            "a 23:44 local moment lies outside the 08:00-17:00 window and must never be emitted");
    }

    [Fact]
    public void Calculate_MidnightWrappingTimeWindow_ThrowsArgumentException()
    {
        // Arrange — earliest after latest = midnight wrap, unsupported (B1)
        ElectionalInput input = BuildInput(
            new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 16),
            new TimeConstraint { Earliest = new TimeOnly(22, 0), Latest = new TimeOnly(6, 0) },
            allowedDays: null);

        // Act
        Action act = () => _service.Calculate(input);

        // Assert
        act.Should().Throw<ArgumentException>(
                "midnight-wrapping time windows are rejected with a validation message")
            .WithMessage("*wrap midnight*");
    }

    #endregion

    #region T6 — Constraint export round-trip

    [Fact]
    public void ToMarkdown_WeekdayOfficeHoursConstraints_RecordsExactConstraintsHeader()
    {
        // Arrange — the same search as T5
        ElectionalResult result = GetConstrainedResult();

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert — exact B4 wording, appended to the Constraints field
        markdown.Should().Contain(
            "**Constraints:** Candidates limited to 08:00\u201317:00 local; days: Mon, Tue, Wed, Thu, Fri",
            "the export header must record active constraints in the exact B4 form");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildConstraintsDisplay_NoConstraints_ReturnsNone()
    {
        // Arrange
        ElectionalInput input = BuildInput(
            new DateOnly(2026, 6, 11), new DateOnly(2026, 6, 19),
            timeConstraint: null, allowedDays: null);

        // Act
        string display = ElectionalMarkdownFormatter.BuildConstraintsDisplay(input);

        // Assert — preserves the legacy "Constraints: None" header
        display.Should().Be("None",
            $"an unrestricted search must keep the legacy 'None' value, got '{display}'");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildConstraintsDisplay_NotesPlusConstraints_JoinsWithSemicolons()
    {
        // Arrange
        ElectionalInput input = new()
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Best time to visit Doctor",
            RangeStart = new DateOnly(2026, 6, 11),
            RangeEnd = new DateOnly(2026, 6, 19),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "Richmond, VA",
            TimeZone = TimeZone,
            ConstraintNotes = "avoid the 17th",
            TimeConstraint = new TimeConstraint { Earliest = new TimeOnly(8, 0), Latest = new TimeOnly(17, 0) },
            AllowedDays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        };

        // Act
        string display = ElectionalMarkdownFormatter.BuildConstraintsDisplay(input);

        // Assert — user notes first, then the B4 constraint string, semicolon-separated
        display.Should().Be(
            "avoid the 17th; Candidates limited to 08:00\u201317:00 local; days: Mon, Tue, Wed, Thu, Fri",
            $"user-entered constraints must be joined with the generated constraint text, got '{display}'");
    }

    #endregion

    #region T7 — No-candidate export path

    [Fact]
    public void ToMarkdown_ConstraintsEliminateAllCandidates_EmitsNoCandidatesNotice()
    {
        // Arrange — Saturday-only over a Monday-Thursday range: no samplable moment
        ElectionalInput input = BuildInput(
            new DateOnly(2026, 6, 15), new DateOnly(2026, 6, 18),
            timeConstraint: null,
            allowedDays: [DayOfWeek.Saturday]);

        // Act
        ElectionalResult result = _service.Calculate(input);
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Assert — the export still generates, with the B5 notice in place of candidates
        result.Candidates.Should().BeEmpty(
            "no Saturday exists between Mon 2026-06-15 and Thu 2026-06-18");
        markdown.Should().Contain("# Electional Report",
            "the export must still generate when constraints eliminate all candidates");
        markdown.Should().Contain("## No Candidates Found",
            "the export must carry the no-candidates notice");
        markdown.Should().Contain("No viable election moments satisfied the configured constraints",
            "the notice must state the reason");
        markdown.Should().NotContain("## Candidate instant 1",
            "no candidate section may be emitted for an empty result");
        markdown.Should().Contain("## Moon Void-of-Course Calendar",
            "the remaining report sections must still be present");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Runs (once) and caches the weekday-office-hours search shared by the
    /// T5 and T6 assertions: Richmond, 2026-06-11 to 2026-06-19, candidates
    /// limited to 08:00–17:00 local on Mon–Fri.
    /// </summary>
    /// <returns>The cached electional result.</returns>
    private ElectionalResult GetConstrainedResult()
    {
        return s_constrainedResult ??= _service.Calculate(BuildInput(
            new DateOnly(2026, 6, 11), new DateOnly(2026, 6, 19),
            new TimeConstraint { Earliest = new TimeOnly(8, 0), Latest = new TimeOnly(17, 0) },
            allowedDays:
            [
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                DayOfWeek.Thursday, DayOfWeek.Friday,
            ]));
    }

    /// <summary>
    /// Builds a Doctor Visit electional input for the given range and constraints.
    /// </summary>
    /// <param name="start">First acceptable date.</param>
    /// <param name="end">Last acceptable date.</param>
    /// <param name="timeConstraint">Optional local-time window.</param>
    /// <param name="allowedDays">Optional allowed days of week.</param>
    /// <returns>A populated <see cref="ElectionalInput"/>.</returns>
    private ElectionalInput BuildInput(
        DateOnly start,
        DateOnly end,
        TimeConstraint? timeConstraint,
        IReadOnlyList<DayOfWeek>? allowedDays)
    {
        return new ElectionalInput
        {
            NatalChart = GetNatalChart(),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Best time to visit Doctor",
            RangeStart = start,
            RangeEnd = end,
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "Richmond, VA",
            TimeZone = TimeZone,
            CandidateCount = 5,
            MinimumCandidateSeparationHours = 0.0,
            TimeConstraint = timeConstraint,
            AllowedDays = allowedDays,
        };
    }

    /// <summary>
    /// Calculates (once) and caches the J2000 NYC natal chart used by every
    /// test in this class.
    /// </summary>
    /// <returns>The cached natal chart.</returns>
    private ChartResult GetNatalChart()
    {
        return s_natalChart ??= _chartEngine.Calculate(new ChartInput
        {
            Name = "Test Client",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = TimeZone,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        });
    }

    #endregion
}
