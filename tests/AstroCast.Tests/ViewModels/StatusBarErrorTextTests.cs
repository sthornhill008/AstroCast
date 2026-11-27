using AstroCast.Tests.Fixtures;
using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// The one place an exception becomes status-bar text (branch InputChecks, 1.7.2, finding N6):
/// <see cref="DisplayText.ErrorMessage"/>.
/// </summary>
/// <remarks>
/// The framework ends an <see cref="ArgumentException"/>'s message with " (Parameter 'name')" and adds an
/// "Actual value was ..." line to an <see cref="ArgumentOutOfRangeException"/>'s; both name the code, not the
/// user's input. Until 1.7.2 only the electional search dropped the first, and no panel the second: reversed
/// transit dates read "The period's end is before its start. (Parameter 'end')" and, on a second line, "Actual
/// value was 12/1/2026."
/// </remarks>
[Trait("Category", "Unit")]
public sealed class StatusBarErrorTextTests
{
    private const string Refusal = "The period's end is before its start.";

    /// <summary>An argument exception naming its parameter reads without the framework's suffix.</summary>
    [Fact]
    public void ErrorMessage_ArgumentException_DropsTheParameterSuffix()
    {
        ArgumentException exception = new(Refusal, "end");
        exception.Message.Should().EndWith("(Parameter 'end')", "the framework adds the suffix");

        DisplayText.ErrorMessage(exception).Should().Be(Refusal);
    }

    /// <summary>
    /// An out-of-range exception carrying the value reads without the suffix and without the "Actual value was"
    /// line.
    /// </summary>
    [Fact]
    public void ErrorMessage_ArgumentOutOfRange_DropsTheSuffixAndTheActualValueLine()
    {
        ArgumentOutOfRangeException exception = new("end", new DateOnly(2026, 12, 1), Refusal);
        exception.Message.Should().Contain("Actual value was", "the framework adds the line");

        DisplayText.ErrorMessage(exception).Should().Be(Refusal);
    }

    /// <summary>The "Actual value was" line goes whether or not the exception names a parameter.</summary>
    [Fact]
    public void ErrorMessage_ArgumentOutOfRangeWithoutAParameter_DropsTheActualValueLine()
    {
        ArgumentOutOfRangeException exception = new(null, 2400, Refusal);
        exception.Message.Should().Contain("Actual value was");

        DisplayText.ErrorMessage(exception).Should().Be(Refusal);
    }

    /// <summary>
    /// The value's text follows the culture, a date and a decimal comma included, and the line goes all the same.
    /// </summary>
    /// <param name="culture">The culture the form runs under.</param>
    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    [InlineData("th-TH")]
    public void ErrorMessage_ActualValueLine_GoesUnderEveryCulture(string culture)
    {
        using CultureScope scope = new(culture);

        DisplayText.ErrorMessage(new ArgumentOutOfRangeException("end", new DateTime(2026, 12, 1), Refusal))
            .Should().Be(Refusal);
        DisplayText.ErrorMessage(new ArgumentOutOfRangeException("latitude", -91.5, "Latitude is out of range."))
            .Should().Be("Latitude is out of range.");
    }

    /// <summary>The framework's own guard helpers read without their suffix and line too.</summary>
    [Fact]
    public void ErrorMessage_FrameworkGuard_DropsTheSuffixAndTheActualValueLine()
    {
        int count = -1;
        Action guard = () => ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException exception = guard.Should().Throw<ArgumentOutOfRangeException>().Which;

        string text = DisplayText.ErrorMessage(exception);

        text.Should().NotContain("(Parameter").And.NotContain("Actual value was").And.NotContain("\n");
        exception.Message.Should().StartWith(text);
    }

    /// <summary>
    /// A message is otherwise unchanged: an argument exception naming no parameter keeps its text, even text that
    /// itself ends as the suffix does, and any other exception keeps its own.
    /// </summary>
    [Fact]
    public void ErrorMessage_NothingToDrop_IsTheMessage()
    {
        const string endsLikeASuffix = "No houses at that latitude. (Parameter 'input')";

        DisplayText.ErrorMessage(new ArgumentException(endsLikeASuffix)).Should().Be(endsLikeASuffix);
        DisplayText.ErrorMessage(new InvalidOperationException(endsLikeASuffix)).Should().Be(endsLikeASuffix);
        DisplayText.ErrorMessage(new IOException("The disk is full.")).Should().Be("The disk is full.");
    }

    /// <summary>A missing exception is refused.</summary>
    [Fact]
    public void ErrorMessage_Null_Throws()
    {
        Action act = () => DisplayText.ErrorMessage(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
