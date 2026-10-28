using System.Globalization;

using AstroCast.Tests.Fixtures;
using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// The explicit UTC offset box (O-D4, E3-1): every place that reads it reads decimal hours
/// with a point, in any culture, as the latitude and longitude boxes are read, and refuses a
/// value that is not a number or lies outside −14 to +14 hours, with the same kind of
/// message the form gives for a bad latitude.
/// </summary>
/// <remarks>
/// The box was read in the machine's culture with no range check, so a stray separator
/// multiplied the offset by ten or a hundred and the chart was cast and saved hours or weeks
/// away: "-5,5" under en-US and "-5.5" under de-DE were both −55, and "5.75" under de-DE was
/// +575 (the outputs audit's run-time probes). Real offsets lie within −12 to +14 (Tier A).
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ExplicitOffsetBoxTests
{
    private const string NotANumber = "Invalid UTC offset. Use decimal hours (e.g., -5 for EST, 5.5 for India).";
    private const string OutOfRange = "UTC offset must be between -14 and +14 hours.";

    /// <summary>The cultures the box is read under: a point, and two decimal-comma cultures.</summary>
    private static readonly string[] Cultures = ["en-US", "de-DE", "sv-SE"];

    /// <summary>Accepted texts and the offsets they are, under each culture.</summary>
    public static TheoryData<string, string, double> Accepted => Cross(
        ("-5.5", -5.5), ("5.75", 5.75), ("-12", -12.0), ("14", 14.0), ("-14", -14.0), (" +3.5 ", 3.5));

    /// <summary>Refused texts and the message for each, under each culture.</summary>
    public static TheoryData<string, string, string> Refused => Cross(
        ("-5,5", NotANumber), ("abc", NotANumber), ("", NotANumber), ("NaN", NotANumber),
        ("15", OutOfRange), ("-14.5", OutOfRange));

    /// <summary>
    /// Decimal hours with a point are read as such in any culture: "-5.5" is −5.5 under
    /// de-DE as under en-US, and −14 and +14 are the limits.
    /// </summary>
    /// <param name="culture">The culture the box is read under.</param>
    /// <param name="text">The box's text.</param>
    /// <param name="expected">The offset.</param>
    [Theory]
    [MemberData(nameof(Accepted))]
    public void TryParseUtcOffsetHours_Should_ReadDecimalHoursWithAPoint_InAnyCulture(string culture, string text, double expected)
    {
        using CultureScope scope = new(culture);

        bool ok = ChartEntryViewModel.TryParseUtcOffsetHours(text, out double hours, out string? error);

        ok.Should().BeTrue();
        hours.Should().Be(expected);
        error.Should().BeNull();
    }

    /// <summary>
    /// A comma, a word, an empty box or a value beyond ±14 hours is refused, with the
    /// message the form shows: "-5,5" is never −55.
    /// </summary>
    /// <param name="culture">The culture the box is read under.</param>
    /// <param name="text">The box's text.</param>
    /// <param name="message">The refusal.</param>
    [Theory]
    [MemberData(nameof(Refused))]
    public void TryParseUtcOffsetHours_Should_Refuse_WithTheFormsMessage(string culture, string text, string message)
    {
        using CultureScope scope = new(culture);

        bool ok = ChartEntryViewModel.TryParseUtcOffsetHours(text, out _, out string? error);

        ok.Should().BeFalse();
        error.Should().Be(message);
    }

    /// <summary>
    /// The form's live label reads the box the same way: "5.75" is "UTC+5:45 (explicit)" in
    /// any culture, and a refused text shows a dash rather than a label for −55 or +575.
    /// </summary>
    /// <param name="culture">The culture the form runs under.</param>
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    public void ResolvedOffsetDisplay_Should_ReadTheBoxInvariantly(string culture)
    {
        using CultureScope scope = new(culture);
        ChartEntryViewModel entry = MainViewModelFactory.Create().ChartEntry;
        entry.UseExplicitOffset = true;

        entry.UtcOffsetHoursText = "5.75";
        entry.ResolvedOffsetDisplay.Should().Be("UTC+5:45 (explicit)");

        entry.UtcOffsetHoursText = "-5.5";
        entry.ResolvedOffsetDisplay.Should().Be("UTC−5.5 (explicit)");

        entry.UtcOffsetHoursText = "-5,5";
        entry.ResolvedOffsetDisplay.Should().Be("—");

        entry.UtcOffsetHoursText = "15";
        entry.ResolvedOffsetDisplay.Should().Be("—");
    }

    /// <summary>
    /// Calculate &amp; Save casts the chart at the offset the box says, in any culture.
    /// </summary>
    /// <param name="culture">The culture the form runs under.</param>
    /// <param name="text">The box's text.</param>
    /// <param name="expected">The offset the chart is cast at.</param>
    [Theory]
    [MemberData(nameof(Accepted))]
    public void CalculateAndSave_Should_CastAtTheOffsetTheBoxSays(string culture, string text, double expected)
    {
        using CultureScope scope = new(culture);
        MainViewModel main = FilledForm(text);

        main.CalculateAndSaveCommand.Execute(null);
        Thread.Sleep(50);

        main.LastResult.Should().NotBeNull(main.StatusText);
        main.LastResult?.Input.UtcOffsetHours.Should().Be(expected);
    }

    /// <summary>
    /// Calculate &amp; Save refuses a bad offset as it refuses a bad latitude: a message in the
    /// status line and no chart cast or saved.
    /// </summary>
    /// <param name="culture">The culture the form runs under.</param>
    /// <param name="text">The box's text.</param>
    /// <param name="message">The refusal.</param>
    [Theory]
    [MemberData(nameof(Refused))]
    public void CalculateAndSave_Should_RefuseABadOffset_AsItRefusesABadLatitude(string culture, string text, string message)
    {
        using CultureScope scope = new(culture);
        MainViewModel main = FilledForm(text);

        main.CalculateAndSaveCommand.Execute(null);
        Thread.Sleep(50);

        main.StatusText.Should().Be(message);
        main.LastResult.Should().BeNull();
    }

    /// <summary>
    /// The electional search, falling back to the form's natal fields, reads the box the
    /// same way and refuses a bad offset with the same message.
    /// </summary>
    /// <param name="culture">The culture the form runs under.</param>
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void CalculateElectional_FromTheForm_Should_RefuseABadOffset(string culture)
    {
        using CultureScope scope = new(culture);
        MainViewModel main = FilledForm("-5,5");
        main.Electional.Goal = "Open a shop";
        main.Electional.LocationName = "New York";
        main.Electional.TimeZone = "America/New_York";

        main.CalculateElectionalCommand.Execute(null);
        Thread.Sleep(50);

        main.StatusText.Should().Be(NotANumber);
    }

    /// <summary>
    /// "Set to now" reads the box the same way: at −5.5 under de-DE, now is UT less five
    /// and a half hours, not fifty-five; a bad offset is refused and the fields left alone.
    /// </summary>
    [Fact]
    public void SetToNow_Should_ReadTheBoxInvariantly_AndRefuseABadOffset()
    {
        using CultureScope scope = new("de-DE");
        MainViewModel main = MainViewModelFactory.Create();
        main.ChartEntry.TimeZone = "America/New_York";
        main.ChartEntry.UseExplicitOffset = true;
        main.ChartEntry.UtcOffsetHoursText = "-5.5";

        DateTime before = DateTime.UtcNow.AddHours(-5.5);
        main.SetToNowCommand.Execute(null);
        DateTime after = DateTime.UtcNow.AddHours(-5.5);

        DateTime set = main.ChartEntry.EventDate.Date.Add(TimeSpan.Parse(main.ChartEntry.EventTime, CultureInfo.InvariantCulture));
        set.Should().BeOnOrAfter(DateTime.SpecifyKind(before, DateTimeKind.Unspecified).AddSeconds(-1))
            .And.BeOnOrBefore(DateTime.SpecifyKind(after, DateTimeKind.Unspecified));

        main.ChartEntry.EventDate = new DateTime(1990, 5, 17);
        main.ChartEntry.EventTime = "08:15:00";
        main.ChartEntry.UtcOffsetHoursText = "-5,5";

        main.SetToNowCommand.Execute(null);

        main.ChartEntry.EventDate.Should().Be(new DateTime(1990, 5, 17));
        main.ChartEntry.EventTime.Should().Be("08:15:00");
        main.StatusText.Should().Be(NotANumber);
    }

    /// <summary>A form ready to calculate an event chart at an explicit offset.</summary>
    /// <param name="offsetText">The offset box's text.</param>
    /// <returns>The view model.</returns>
    private static MainViewModel FilledForm(string offsetText)
    {
        MainViewModel main = MainViewModelFactory.Create();
        main.ChartEntry.Name = "Offset box";
        main.ChartEntry.EventType = "Event";
        main.ChartEntry.EventDate = new DateTime(2026, 1, 15);
        main.ChartEntry.EventTime = "12:00:00";
        main.ChartEntry.TimeZone = "America/New_York";
        main.ChartEntry.LocationName = "New York";
        main.ChartEntry.LatitudeText = "40.7128";
        main.ChartEntry.LongitudeText = "-74.006";
        main.ChartEntry.UseExplicitOffset = true;
        main.ChartEntry.UtcOffsetHoursText = offsetText;
        return main;
    }

    /// <summary>Each case under each culture.</summary>
    /// <typeparam name="T">The case's expected value.</typeparam>
    /// <param name="cases">The texts and what each must give.</param>
    /// <returns>The theory rows.</returns>
    private static TheoryData<string, string, T> Cross<T>(params (string Text, T Expected)[] cases)
    {
        TheoryData<string, string, T> data = new();
        foreach (string culture in Cultures)
        {
            foreach ((string text, T expected) in cases)
                data.Add(culture, text, expected);
        }

        return data;
    }
}
