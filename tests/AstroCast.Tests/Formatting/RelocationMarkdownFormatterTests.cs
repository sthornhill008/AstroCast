using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// The relocation export keeps the birth's clock, planetary hour, sect and time lords while the angles,
/// houses and Lots follow the place (branch K2, 1.3.4; ruling S-D6): J2000 NYC, a night chart born
/// 2000-01-01 07:00 America/New_York, relocated to London, where 12:00 UT is noon. Until 1.3.4 the
/// relocated chart was a whole chart cast at the place at an explicit offset of zero, so the export
/// printed the UTC clock, an hour on the UTC calendar, the place's sect and time lords counted from the
/// relocated Ascendant and Lots.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class RelocationMarkdownFormatterTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Keeps the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public RelocationMarkdownFormatterTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The London export's relocated body prints the birth clock, the natal planetary hour, the natal sect and
    /// the natal time-lord sections, and never the explicit UTC label.
    /// </summary>
    [Fact]
    public void ToMarkdown_J2000ToLondon_Should_PrintTheBirthClock_Hour_Sect_AndTimeLords()
    {
        ChartResult natal = GoldenFixtures.J2000Nyc(_fixture);
        RelocationService service = new(GoldenFixtures.CreateChartEngine(_fixture), new LoggerConfiguration().CreateLogger());
        RelocationResult result = service.Calculate(natal, new RelocationInput
        {
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London",
        });

        string md = RelocationMarkdownFormatter.ToMarkdown(result, GoldenFixtures.GoldenClock());

        md.Should().Contain("**Date/Time:** 2000-01-01 07:00:00 (America/New_York)");
        md.Should().Contain("**UTC Offset:** EST (UTC−5)");
        md.Should().Contain("**Hour Ruler:** Moon (Night hour #12)");
        md.Should().Contain("**Hour Period:** 06:07:57 – 07:21:44 (America/New_York)");
        md.Should().Contain("| Sect | Night chart |");
        md.Should().Contain("**Profected House:** 3rd (Pisces)");
        md.Should().Contain("**Lot of Spirit:** Scorpio");
        md.Should().Contain("**Chart Sect:** Night");
        md.Should().NotContain("UTC+0 (explicit)");
    }

    /// <summary>
    /// An Event chart's header names the event's place and moment, never a birth's (1.6.1, branch StaleResult).
    /// Until 1.6.1 every relocated chart's header read Birth Location and Birth Date/Time.
    /// </summary>
    [Fact]
    public void ToMarkdown_EventChart_LabelsTheEventPlaceAndMoment()
    {
        string md = RelocateToLondon(J2000Input("Event"));

        Line(md, "**Event Location:**").Should().Be("**Event Location:** New York City (40.7128, -74.0060)  ");
        Line(md, "**Event Date/Time:**").Should().Be("**Event Date/Time:** 2000-01-01 07:00:00 (America/New_York)  ");
        md.Should().NotContain("**Birth ");
    }

    /// <summary>A Horary chart's header names the question's place and moment.</summary>
    [Fact]
    public void ToMarkdown_HoraryChart_LabelsTheQuestionPlaceAndMoment()
    {
        string md = RelocateToLondon(J2000Input("Horary", "Will the move go well?"));

        Line(md, "**Question Location:**").Should().Be("**Question Location:** New York City (40.7128, -74.0060)  ");
        Line(md, "**Question Date/Time:**").Should().Be("**Question Date/Time:** 2000-01-01 07:00:00 (America/New_York)  ");
        md.Should().NotContain("**Birth ");
    }

    /// <summary>A Birth chart's header keeps Birth Location and Birth Date/Time.</summary>
    [Fact]
    public void ToMarkdown_BirthChart_KeepsTheBirthLabels()
    {
        string md = RelocateToLondon(J2000Input("Birth"));

        Line(md, "**Birth Location:**").Should().Be("**Birth Location:** New York City (40.7128, -74.0060)  ");
        Line(md, "**Birth Date/Time:**").Should().Be("**Birth Date/Time:** 2000-01-01 07:00:00 (America/New_York)  ");
    }

    /// <summary>J2000 NYC's input with the given type, and a question for a horary chart.</summary>
    /// <param name="eventType">The event type as stored.</param>
    /// <param name="question">The horary question, if any.</param>
    /// <returns>The input.</returns>
    private static ChartInput J2000Input(string eventType, string? question = null) => new()
    {
        Name = "J2000 NYC Fixture",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = eventType,
        HouseSystem = HouseSystem.Placidus,
        HoraryQuestion = question,
    };

    /// <summary>Casts the input and relocates it to London, as the relocation export prints it under the golden clock.</summary>
    /// <param name="input">The chart's input.</param>
    /// <returns>The export.</returns>
    private string RelocateToLondon(ChartInput input)
    {
        ChartResult natal = GoldenFixtures.CreateChartEngine(_fixture).Calculate(input);
        RelocationService service = new(GoldenFixtures.CreateChartEngine(_fixture), new LoggerConfiguration().CreateLogger());
        RelocationResult result = service.Calculate(natal, new RelocationInput
        {
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London",
        });
        return RelocationMarkdownFormatter.ToMarkdown(result, GoldenFixtures.GoldenClock());
    }

    /// <summary>The one line of <paramref name="md"/> that starts with <paramref name="prefix"/>.</summary>
    /// <param name="md">The export.</param>
    /// <param name="prefix">The line's start.</param>
    /// <returns>The line.</returns>
    private static string Line(string md, string prefix) =>
        md.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith(prefix, StringComparison.Ordinal))
            .Should().ContainSingle().Subject;
}
