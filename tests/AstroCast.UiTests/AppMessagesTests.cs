using Xunit;

namespace AstroCast.UiTests;

/// <summary>
/// The Avalonia head's start-up failure and unexpected-error texts are the WPF head's, word for word
/// (src/AstroCast/App.xaml.cs; O-8, H-04, H-05).
/// </summary>
public sealed class AppMessagesTests
{
    /// <summary>
    /// A missing ephemeris folder is a <see cref="DirectoryNotFoundException"/>, and the message says where the
    /// folder belongs; any other failure gives the message alone.
    /// </summary>
    [Fact]
    public void StartupFailureMessage_MissingEphemerisFolder_GivesTheHint()
    {
        Assert.Equal(
            "Failed to start AstroCast:\n\nX\n\nEnsure the 'ephe' folder with .se1 files is in the application directory.",
            global::AstroCast.Avalonia.App.StartupFailureMessage(new DirectoryNotFoundException("X")));
        Assert.Equal(
            "Failed to start AstroCast:\n\nX",
            global::AstroCast.Avalonia.App.StartupFailureMessage(new InvalidOperationException("X")));
    }

    /// <summary>An unhandled UI error says the application keeps running.</summary>
    [Fact]
    public void UnexpectedErrorMessage_SaysTheAppContinues()
    {
        Assert.Equal(
            "An unexpected error occurred:\n\nX\n\nThe application will continue running, but you may want to save your work and restart.",
            global::AstroCast.Avalonia.App.UnexpectedErrorMessage(new InvalidOperationException("X")));
    }
}
