using System.Globalization;
using System.Text.Json;

using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// The Time Note (D17, Q5, TZ card 4): when a chart's wall clock was repeated or skipped in
/// its zone, or read on local mean time, the chart export says so under its own label,
/// <c>**Time Note:**</c>, beneath the UTC Offset line, and the chart JSON carries the same
/// text as <c>timeNote</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every instant, offset and zone name in the expected notes is Python <c>zoneinfo</c>'s over
/// <c>tzdata</c> 2026.5 (IANA 2026e): both candidates of each repeated reading (fold 0 and
/// fold 1), the clock either side of the New York gap, and New York's local mean time
/// (<c>f5-time/truth/note_truth.py</c>). The labels follow card 6.
/// </para>
/// <para>
/// The Note never goes into "Notes": seven prompts read Notes as supplied biography, and
/// the horary prompt reads the question from it (Q5).
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TimeNoteTests
{
    private const string RepeatedNewYork =
        "01:30 occurred twice on 2026-11-01 in America/New_York: at 05:30 UT on EDT (UTC−4), " +
        "and again at 06:30 UT on EST (UTC−5). The chart is cast for the later; an explicit offset " +
        "of UTC−4 casts it for the earlier.";

    private const string RepeatedLondon =
        "02:30 occurred twice on 1941-08-10 in Europe/London: at 00:30 UT on BDST (UTC+2), " +
        "and again at 01:30 UT on BST (UTC+1). The chart is cast for the later; an explicit offset " +
        "of UTC+2 casts it for the earlier.";

    private const string RepeatedAlmaty =
        "02:30 occurred twice on 2003-10-26 in Asia/Almaty: at 2003-10-25 19:30 UT on UTC+7, " +
        "and again at 2003-10-25 20:30 UT on UTC+6. The chart is cast for the later; an explicit " +
        "offset of UTC+7 casts it for the earlier.";

    private const string SkippedNewYork =
        "02:30 did not occur on 2026-03-08 in America/New_York: the clocks went from 02:00 to 03:00. " +
        "The chart is cast for the first instant after the gap, 03:00 EDT (UTC−4), which is 07:00 UT.";

    private const string LocalMeanTimeNewYork =
        "Before standard time, the time in America/New_York is the local mean time of New York, " +
        "UTC−4:56:02; the chart is cast for 16:56:02 UT. An explicit offset gives the birthplace's " +
        "own mean time.";

    private readonly ChartEngine _engine;

    /// <summary>Receives the shared ephemeris fixture and builds a fully wired engine.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public TimeNoteTests(EphemerisFixture fixture)
    {
        _engine = GoldenFixtures.CreateChartEngine(fixture);
    }

    /// <summary>The five inputs of the time-zone design, and the Note each must carry.</summary>
    public static TheoryData<string, string, string> FiveInputs => new()
    {
        { "America/New_York", "2026-11-01T01:30:00", RepeatedNewYork },
        { "Europe/London", "1941-08-10T02:30:00", RepeatedLondon },
        { "Asia/Almaty", "2003-10-26T02:30:00", RepeatedAlmaty },
        { "America/New_York", "2026-03-08T02:30:00", SkippedNewYork },
        { "America/New_York", "1880-06-15T12:00:00", LocalMeanTimeNewYork },
    };

    /// <summary>
    /// A repeated, a skipped and a local-mean-time reading each carry their Note: on the
    /// result, in the export directly beneath the UTC Offset line under its own label, and in
    /// the chart JSON as <c>timeNote</c>.
    /// </summary>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="local">The wall-clock reading.</param>
    /// <param name="expected">The Note.</param>
    [Theory]
    [MemberData(nameof(FiveInputs))]
    public void Export_Should_CarryTheTimeNote_BeneathTheUtcOffsetLine(string zone, string local, string expected)
    {
        ChartResult result = _engine.Calculate(Input(zone, local));

        result.TimeNote.Should().Be(expected);

        string[] lines = ChartMarkdownFormatter.ToMarkdown(result).Split('\n');
        int offsetLine = Array.FindIndex(lines, l => l.StartsWith("**UTC Offset:**", StringComparison.Ordinal));
        offsetLine.Should().BeGreaterThanOrEqualTo(0);
        lines[offsetLine + 1].TrimEnd('\r').Should().Be($"**Time Note:** {expected}  ");
        lines.Where(l => l.StartsWith("**Notes:**", StringComparison.Ordinal)).Should().BeEmpty(
            "the Note has its own label and never goes into Notes");

        using JsonDocument json = JsonDocument.Parse(ChartJsonFormatter.ToJson(result));
        json.RootElement.GetProperty("chart").GetProperty("timeNote").GetString().Should().Be(expected);
    }

    /// <summary>
    /// The Note is the same text under every culture: its dates, times and offsets are
    /// written culture-independently (th-TH would otherwise date 2026 as 2569).
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void TimeNote_Should_NotDependOnTheCulture(string culture)
    {
        using CultureScope scope = new(culture);

        foreach (object[] row in FiveInputs)
        {
            string expected = (string)row[2];
            ChartResult result = _engine.Calculate(Input((string)row[0], (string)row[1]));

            result.TimeNote.Should().Be(expected);
            ChartMarkdownFormatter.ToMarkdown(result).Should().Contain($"**Time Note:** {expected}  ");
        }
    }

    /// <summary>
    /// A repeated reading's result names its earlier instant, from the zone's own record
    /// (zoneinfo fold 0), as well as the later one it is cast at.
    /// </summary>
    [Fact]
    public void Calculate_Should_RecordTheEarlierInstant_OfARepeatedReading()
    {
        ChartResult result = _engine.Calculate(Input("Europe/London", "1941-08-10T02:30:00"));

        result.EarlierUtcDateTime.Should().Be(new DateTime(1941, 8, 10, 0, 30, 0, DateTimeKind.Utc));
        result.UtcDateTime.Should().Be(new DateTime(1941, 8, 10, 1, 30, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// An electional candidate is cast at the instant it was sampled at (TZ-1), which in a
    /// repeated hour may be either one, and its Note says which: the later as D17 would
    /// choose, or the earlier, never claiming the later for a chart cast at the earlier.
    /// </summary>
    /// <param name="castUtc">The sampled instant (zoneinfo's fold 0 or fold 1).</param>
    /// <param name="expected">The Note.</param>
    [Theory]
    [InlineData("2026-11-01T06:30:00", RepeatedNewYork)]
    [InlineData("2026-11-01T05:30:00",
        "01:30 occurred twice on 2026-11-01 in America/New_York: at 05:30 UT on EDT (UTC−4), " +
        "and again at 06:30 UT on EST (UTC−5). The chart is cast for the earlier.")]
    public void ElectionalCandidate_Should_SayWhichInstantItIsCastFor(string castUtc, string expected)
    {
        DateTime at = DateTime.SpecifyKind(DateTime.Parse(castUtc, CultureInfo.InvariantCulture), DateTimeKind.Utc);

        ChartResult result = _engine.CalculateForElectional(Input("America/New_York", "2026-11-01T01:30:00"), at);

        result.UtcDateTime.Should().Be(at);
        result.TimeNote.Should().Be(expected);
    }

    /// <summary>
    /// An ordinary reading, which occurs once and on standard time, carries no Note: no line
    /// in the export, no <c>timeNote</c> in the JSON, and no earlier instant.
    /// </summary>
    [Fact]
    public void OrdinaryReading_Should_CarryNoTimeNote()
    {
        ChartResult result = _engine.Calculate(Input("America/New_York", "2026-06-15T12:00:00"));

        result.TimeNote.Should().BeNull();
        result.EarlierUtcDateTime.Should().BeNull();
        ChartMarkdownFormatter.ToMarkdown(result).Should().NotContain("**Time Note:**");
        ChartJsonFormatter.ToJson(result).Should().NotContain("timeNote");
    }

    /// <summary>
    /// A chart at an explicit offset is read on that offset alone, so it carries no Note,
    /// even at a reading its zone repeats or skips, or before its zone's standard time.
    /// </summary>
    /// <param name="local">The wall-clock reading, in New York's repeated hour, its gap, or 1880.</param>
    /// <param name="offsetHours">The explicit offset.</param>
    [Theory]
    [InlineData("2026-11-01T01:30:00", -4.0)]
    [InlineData("2026-03-08T02:30:00", -5.0)]
    [InlineData("1880-06-15T12:00:00", -5.0)]
    public void ExplicitOffset_Should_CarryNoTimeNote(string local, double offsetHours)
    {
        ChartResult result = _engine.Calculate(Input("America/New_York", local, explicitOffset: offsetHours));

        result.TimeNote.Should().BeNull();
        ChartMarkdownFormatter.ToMarkdown(result).Should().NotContain("**Time Note:**");
        ChartJsonFormatter.ToJson(result).Should().NotContain("timeNote");
    }

    /// <summary>
    /// A horary chart in a repeated hour keeps its question in Notes, exactly as entered, and
    /// carries the Time Note on its own line: the horary prompt reads the question from Notes.
    /// </summary>
    [Fact]
    public void HoraryChart_Should_KeepItsQuestionInNotes_AndTheTimeNoteApart()
    {
        ChartResult result = _engine.Calculate(
            Input("America/New_York", "2026-11-01T01:30:00", horaryQuestion: "Will the letter arrive?"));
        string[] lines = ChartMarkdownFormatter.ToMarkdown(result).Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

        lines.Should().Contain("**Notes:** Will the letter arrive?  ");
        lines.Should().Contain($"**Time Note:** {RepeatedNewYork}  ");
        lines.Count(l => l.Contains("occurred twice", StringComparison.Ordinal)).Should().Be(1);
        result.Input.Notes.Should().BeNull();
    }

    /// <summary>Builds a chart input at New York's coordinates.</summary>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="local">The wall-clock reading, as <c>yyyy-MM-ddTHH:mm:ss</c>.</param>
    /// <param name="explicitOffset">An explicit offset in hours, or <see langword="null"/> to cast through the zone.</param>
    /// <param name="horaryQuestion">A horary question, or <see langword="null"/> for an event chart.</param>
    /// <returns>The input.</returns>
    private static ChartInput Input(string zone, string local, double? explicitOffset = null, string? horaryQuestion = null) => new()
    {
        Name = "Clock reading",
        LocalDateTime = DateTime.Parse(local, CultureInfo.InvariantCulture),
        TimeZone = zone,
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "Test",
        EventType = horaryQuestion is null ? "Event" : "Horary",
        HoraryQuestion = horaryQuestion,
        UseExplicitOffset = explicitOffset.HasValue,
        UtcOffsetHours = explicitOffset,
    };
}
