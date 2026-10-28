using AstroCast.Core.Utilities;

using FluentAssertions;

namespace AstroCast.Tests.Utilities;

/// <summary>
/// Tests for <see cref="FileNameSanitiser"/> verifying path traversal removal,
/// invalid character handling, and edge case behaviour.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FileNameSanitiserTests
{
    [Fact]
    public void Sanitise_Should_RemovePathTraversal_When_InputContainsDotDot()
    {
        // Arrange
        string input = "../../../etc/passwd";

        // Act
        string result = FileNameSanitiser.Sanitise(input);

        // Assert
        result.Should().NotContain("..", "path traversal sequences must be removed");
        result.Should().NotContain("/", "forward slashes must be removed");
        result.Should().NotContain("\\", "backslashes must be removed");
    }

    [Fact]
    public void Sanitise_Should_RemoveInvalidChars_When_InputContainsSpecialCharacters()
    {
        // Arrange
        string input = "Chart: John <Doe> 2024";

        // Act
        string result = FileNameSanitiser.Sanitise(input);

        // Assert — result should not contain invalid file name characters
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidChars)
        {
            result.Should().NotContain(c.ToString(),
                $"character '{c}' (U+{(int)c:X4}) is invalid in file names");
        }

        // Should still contain the meaningful parts
        result.Should().Contain("Chart");
        result.Should().Contain("John");
        result.Should().Contain("Doe");
        result.Should().Contain("2024");
    }

    [Fact]
    public void Sanitise_Should_ThrowArgumentException_When_InputIsEmpty()
    {
        // Act & Assert
        Action act = () => FileNameSanitiser.Sanitise("");
        act.Should().Throw<ArgumentException>(
            "empty string is not a valid file name");
    }

    [Fact]
    public void Sanitise_Should_ReplaceSpaces_When_InputContainsSpaces()
    {
        // Arrange
        string input = "John Doe Birth Chart";

        // Act
        string result = FileNameSanitiser.Sanitise(input);

        // Assert
        result.Should().NotContain(" ", "spaces should be replaced with underscores");
        result.Should().Be("John_Doe_Birth_Chart");
    }

    /// <summary>
    /// The sanitiser removes the same characters on every platform: <c>&lt; &gt; : " / \ | ? *</c> and the control
    /// characters 0 to 31 (E3-5). Until 1.3.3 it removed <see cref="Path.GetInvalidFileNameChars"/>, which on macOS
    /// and Linux is only NUL and '/', so a name exported there kept characters Windows refuses.
    /// </summary>
    [Fact]
    public void Sanitise_Should_RemoveTheSameCharacters_On_EveryPlatform()
    {
        FileNameSanitiser.Sanitise("A<B>C:D\"E|F?G*H").Should().Be("ABCDEFGH");
        FileNameSanitiser.Sanitise("A\u0001B\u001FC\tD").Should().Be("ABCD");
    }
}
