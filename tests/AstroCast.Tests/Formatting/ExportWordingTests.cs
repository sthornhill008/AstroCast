using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// The wording of export lines that branch ExportText corrected (1.7.2), for the formatters that have no test
/// file of their own: each test names the corrected text outright, since a golden comparison masks some lines
/// and normalises line endings.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExportWordingTests
{
    private const string Engine = "AstroCast 0.0.0 (SwissEph 0.00.00, tzdb 0000a)";

    private static readonly DateTime CalcTime = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    #region Counts and their nouns (finding N12)

    /// <summary>
    /// The lunation series' counts take the singular for one: "1 New Moon, 1 Full Moon", and the plural
    /// otherwise. Until 1.7.2: "1 New Moons, 1 Full Moons".
    /// </summary>
    /// <param name="newMoons">How many New Moons the series holds.</param>
    /// <param name="fullMoons">How many Full Moons.</param>
    /// <param name="expected">The counts as the Lunations Found line prints them.</param>
    [Theory]
    [InlineData(1, 1, "(1 New Moon, 1 Full Moon)")]
    [InlineData(2, 1, "(2 New Moons, 1 Full Moon)")]
    [InlineData(1, 0, "(1 New Moon, 0 Full Moons)")]
    public void LunationSeries_Counts_FollowTheirNumbers(int newMoons, int fullMoons, string expected)
    {
        List<LunationResult> lunations = [];
        for (int i = 0; i < newMoons; i++)
            lunations.Add(Lunation(isNewMoon: true, day: 1 + i));
        for (int i = 0; i < fullMoons; i++)
            lunations.Add(Lunation(isNewMoon: false, day: 15 + i));

        LunationSeries series = new()
        {
            Lunations = lunations.AsReadOnly(),
            ScanStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ScanEnd = new DateTime(2024, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            Count = lunations.Count,
            EngineVersion = Engine,
            CalculatedAt = CalcTime,
        };

        string md = LunationFormatter.SeriesToMarkdown(series);

        md.Should().Contain($"**Lunations Found:** {lunations.Count} {expected}");
    }

    /// <summary>
    /// The return and ingress timeline's counts take the singular for one: "1 return, 1 ingress", and the
    /// plural otherwise. Until 1.7.2: "1 returns, 1 ingresses".
    /// </summary>
    /// <param name="returns">How many returns the timeline holds.</param>
    /// <param name="ingresses">How many ingresses.</param>
    /// <param name="expected">The counts as the Events Found line prints them.</param>
    [Theory]
    [InlineData(1, 1, "(1 return, 1 ingress)")]
    [InlineData(2, 0, "(2 returns, 0 ingresses)")]
    [InlineData(0, 2, "(0 returns, 2 ingresses)")]
    public void ReturnIngressTimeline_Counts_FollowTheirNumbers(int returns, int ingresses, string expected)
    {
        List<ReturnIngressTimelineEntry> entries = [];
        for (int i = 0; i < returns; i++)
            entries.Add(TimelineEntry("Return", "Return #1", day: 1 + i));
        for (int i = 0; i < ingresses; i++)
            entries.Add(TimelineEntry("Ingress", "Enters Taurus", day: 15 + i));

        string md = ReturnIngressTimelineFormatter.ToMarkdown(Timeline(entries));

        md.Should().Contain($"**Events Found:** {entries.Count} {expected}");
    }

    #endregion

    #region Helpers

    /// <summary>A lunation of March 2024 with invented values.</summary>
    /// <param name="isNewMoon">Whether it is a New Moon; a Full Moon otherwise.</param>
    /// <param name="day">Its day of the month.</param>
    /// <returns>The lunation.</returns>
    private static LunationResult Lunation(bool isNewMoon, int day) => new()
    {
        Type = isNewMoon ? "New Moon" : "Full Moon",
        IsNewMoon = isNewMoon,
        ExactUtc = new DateTime(2024, 3, day, 9, 0, 0, DateTimeKind.Utc),
        SunLongitude = 350.0,
        MoonLongitude = isNewMoon ? 350.0 : 170.0,
        Sign = isNewMoon ? "Pisces" : "Virgo",
        Degree = 20.0,
        PositionFormatted = isNewMoon ? "20\u00B000' Pisces" : "20\u00B000' Virgo",
        Element = isNewMoon ? "Water" : "Earth",
        Modality = "Mutable",
        PrecisionArcSeconds = 0.1,
        IsNearNode = false,
        NodeDistance = 60.0,
        EclipseIndicator = null,
        EngineVersion = Engine,
        CalculatedAt = CalcTime,
    };

    /// <summary>A timeline entry of January 2026 with invented values.</summary>
    /// <param name="eventType">"Return" or "Ingress".</param>
    /// <param name="label">The entry's label.</param>
    /// <param name="day">Its day of the month.</param>
    /// <returns>The entry.</returns>
    private static ReturnIngressTimelineEntry TimelineEntry(string eventType, string label, int day) => new()
    {
        EventType = eventType,
        Planet = CelestialBodyId.Mars,
        PlanetName = "Mars",
        ExactUtc = new DateTime(2026, 1, day, 12, 0, 0, DateTimeKind.Utc),
        Longitude = 30.0,
        PositionFormatted = "00\u00B000\u203200\u2033 Taurus",
        Sign = "Taurus",
        Label = label,
        PrecisionArcSeconds = 0.1,
    };

    /// <summary>A timeline of 2026 holding the given entries, Mars searched for returns and for ingresses.</summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The timeline.</returns>
    private static ReturnIngressTimeline Timeline(List<ReturnIngressTimelineEntry> entries) => new()
    {
        NativeName = "Eleanor Hartwell",
        Entries = entries.AsReadOnly(),
        ScanStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ScanEnd = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Count = entries.Count,
        ReturnPlanets = ["Mars"],
        IngressPlanets = ["Mars"],
        EngineVersion = Engine,
        CalculatedAt = CalcTime,
    };

    #endregion
}
