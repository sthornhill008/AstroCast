using AstroCast.Tests.Fixtures;
using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// The one check the Chart panel's Time box goes through (branch InputChecks, 1.7.2, finding N1):
/// <see cref="ChartEntryViewModel.TryParseTimeOfDay"/>.
/// </summary>
/// <remarks>
/// A time of day only: hours and minutes with a colon, seconds optional, 00:00:00 to 23:59:59, in any culture.
/// Until 1.7.2 the box was read with <c>TimeSpan.TryParse</c>, which reads a number with no colon as days: "9",
/// meant as nine o'clock, cast and saved the chart for midnight nine days later, and "-5:00" five hours before
/// midnight on the day before.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class TimeOfDayEntryTests
{
    private const string Invalid = "Invalid time format. Use HH:mm:ss.";
    private const string InvalidNatal = "Invalid natal time format. Use HH:mm:ss.";

    /// <summary>Hours and minutes, with seconds optional, are a time of day; blanks around them are dropped.</summary>
    /// <param name="text">The box's text.</param>
    /// <param name="hours">The hours read.</param>
    /// <param name="minutes">The minutes read.</param>
    /// <param name="seconds">The seconds read.</param>
    [Theory]
    [InlineData("9:30", 9, 30, 0)]
    [InlineData("09:30", 9, 30, 0)]
    [InlineData("9:30:15", 9, 30, 15)]
    [InlineData("09:30:00", 9, 30, 0)]
    [InlineData("00:00", 0, 0, 0)]
    [InlineData("23:59:59", 23, 59, 59)]
    [InlineData("  14:05  ", 14, 5, 0)]
    public void TryParseTimeOfDay_AcceptsHoursAndMinutesWithSecondsOptional(string text, int hours, int minutes, int seconds)
    {
        bool ok = ChartEntryViewModel.TryParseTimeOfDay(text, out TimeSpan time, out string? error);

        ok.Should().BeTrue();
        time.Should().Be(new TimeSpan(hours, minutes, seconds));
        error.Should().BeNull();
    }

    /// <summary>
    /// Everything else is refused with the form's message: a bare number, a sign, a day part, a dot or a comma, a
    /// fraction of a second, 24:00, a value past 59, an empty box.
    /// </summary>
    /// <param name="text">The box's text.</param>
    [Theory]
    [InlineData("9")]
    [InlineData("14")]
    [InlineData("1430")]
    [InlineData("-5:00")]
    [InlineData("+5:00")]
    [InlineData("9.30")]
    [InlineData("9,30")]
    [InlineData("24:00")]
    [InlineData("1.09:30:00")]
    [InlineData("09:30:00.5")]
    [InlineData("09:60")]
    [InlineData("09:30:60")]
    [InlineData("9:5")]
    [InlineData("9:30 PM")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParseTimeOfDay_RefusesEverythingElse(string text)
    {
        bool ok = ChartEntryViewModel.TryParseTimeOfDay(text, out TimeSpan time, out string? error);

        ok.Should().BeFalse();
        time.Should().Be(TimeSpan.Zero);
        error.Should().Be(Invalid);
    }

    /// <summary>A missing text is refused as an empty box is.</summary>
    [Fact]
    public void TryParseTimeOfDay_RefusesNull()
    {
        ChartEntryViewModel.TryParseTimeOfDay(null, out _, out string? error).Should().BeFalse();
        error.Should().Be(Invalid);
    }

    /// <summary>
    /// The machine's culture changes nothing: a colon is the separator under cultures that write a time with a
    /// dot (fi-FI, da-DK), and a dot is refused there too.
    /// </summary>
    /// <param name="culture">The culture the form runs under.</param>
    [Theory]
    [InlineData("fi-FI")]
    [InlineData("da-DK")]
    [InlineData("de-DE")]
    [InlineData("th-TH")]
    public void TryParseTimeOfDay_IgnoresTheMachineCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ChartEntryViewModel.TryParseTimeOfDay("14:30:15", out TimeSpan time, out _).Should().BeTrue();
        time.Should().Be(new TimeSpan(14, 30, 15));
        ChartEntryViewModel.TryParseTimeOfDay("14.30.15", out _, out string? error).Should().BeFalse();
        error.Should().Be(Invalid);
    }

    /// <summary>
    /// Calculate &amp; Save refuses a bare hour: the status line has the message, no chart is cast and nothing is
    /// saved. Until 1.7.2 "9" was nine days, and the chart was saved for midnight nine days on.
    /// </summary>
    /// <param name="text">The Time box's text.</param>
    [Theory]
    [InlineData("9")]
    [InlineData("-5:00")]
    [InlineData("1.09:30:00")]
    public void CalculateAndSave_RefusesATimeThatIsNotATimeOfDay_AndSavesNothing(string text)
    {
        SavedChartsRepository repository = new();
        MainViewModel main = FilledForm(repository);
        main.ChartEntry.EventTime = text;

        main.CalculateAndSaveCommand.Execute(null);
        Thread.Sleep(50);

        main.StatusText.Should().Be(Invalid);
        main.LastResult.Should().BeNull();
        repository.Count.Should().Be(0);
    }

    /// <summary>Calculate &amp; Save casts hours and minutes without seconds at that time on the form's date.</summary>
    [Fact]
    public void CalculateAndSave_CastsHoursAndMinutesOnTheFormsDate()
    {
        SavedChartsRepository repository = new();
        MainViewModel main = FilledForm(repository);
        main.ChartEntry.EventTime = "9:30";

        main.CalculateAndSaveCommand.Execute(null);
        Thread.Sleep(50);

        main.LastResult.Should().NotBeNull(main.StatusText);
        main.LastResult?.Input.LocalDateTime.Should().Be(new DateTime(1947, 10, 5, 9, 30, 0));
        repository.Count.Should().Be(1);
    }

    /// <summary>
    /// The electional search, casting the natal chart from the form, refuses the same texts in its own words.
    /// </summary>
    [Fact]
    public void CalculateElectional_FromTheForm_RefusesABareHour()
    {
        MainViewModel main = FilledForm(new SavedChartsRepository());
        main.ChartEntry.EventTime = "9";
        main.Electional.Goal = "Open a shop";
        main.Electional.LocationName = "Harrisburg";
        main.Electional.TimeZone = "America/New_York";

        main.CalculateElectionalCommand.Execute(null);
        Thread.Sleep(50);

        main.StatusText.Should().Be(InvalidNatal);
        main.LastResult.Should().BeNull();
    }

    /// <summary>
    /// The form's live offset label reads the box through the same check and falls back to noon when the text is
    /// refused: a bare "9" on a summer date shows the offset in force at noon that day, not nine days later.
    /// </summary>
    [Fact]
    public void ResolvedOffsetDisplay_ReadsARefusedTimeAsNoonOfTheFormsDate()
    {
        ChartEntryViewModel entry = MainViewModelFactory.Create().ChartEntry;
        entry.TimeZone = "America/New_York";
        // Clocks went back at 02:00 on 1947-09-28: the 25th is on daylight time, nine days on is not.
        entry.EventDate = new DateTime(1947, 9, 25);

        entry.EventTime = "9";

        entry.ResolvedOffsetDisplay.Should().Contain("EDT");
    }

    /// <summary>A chart loaded from the database fills the box with text the check accepts.</summary>
    [Fact]
    public void PopulateFrom_FillsTheBoxWithATimeTheCheckAccepts()
    {
        ChartEntryViewModel entry = MainViewModelFactory.Create().ChartEntry;

        entry.PopulateFrom(SavedChartsRepository.NewChart(1, "Eleanor Hartwell"));

        entry.EventTime.Should().Be("09:30:00");
        ChartEntryViewModel.TryParseTimeOfDay(entry.EventTime, out TimeSpan time, out _).Should().BeTrue();
        time.Should().Be(new TimeSpan(9, 30, 0));
    }

    /// <summary>A form ready to calculate the invented birth chart, person A, saving into the given repository.</summary>
    /// <param name="repository">The repository the view model saves into.</param>
    /// <returns>The view model.</returns>
    private static MainViewModel FilledForm(SavedChartsRepository repository)
    {
        MainViewModel main = MainViewModelFactory.Create(repository: repository);
        main.ChartEntry.Name = "Eleanor Hartwell";
        main.ChartEntry.EventType = "Birth";
        main.ChartEntry.EventDate = new DateTime(1947, 10, 5);
        main.ChartEntry.EventTime = "09:30:00";
        main.ChartEntry.TimeZone = "America/New_York";
        main.ChartEntry.LocationName = "Harrisburg, Pennsylvania, United States";
        main.ChartEntry.LatitudeText = "40.2737";
        main.ChartEntry.LongitudeText = "-76.8844";
        return main;
    }
}
