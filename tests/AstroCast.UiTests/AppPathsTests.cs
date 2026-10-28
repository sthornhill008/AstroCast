using Xunit;

using AstroCast.Avalonia.Services;

namespace AstroCast.UiTests;

/// <summary>
/// Verifies that <see cref="AppPaths"/> resolves the idiomatic per-user data
/// location for the platform it is running on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why <c>[Fact]</c> and not <c>[AvaloniaFact]</c> here.</b> Every other test
/// in this assembly drives Avalonia controls and needs the headless session's UI
/// thread. These do not touch a control at all — <see cref="AppPaths"/> is plain
/// path arithmetic that happens to live in the Avalonia head. Using the plain
/// attribute keeps that distinction visible.
/// </para>
/// <para>
/// <b>These tests are platform-conditional by design.</b> Each branch of
/// <see cref="AppPaths"/> can only be observed on the platform it serves, so the
/// assertions fork the same way the implementation does. On this Mac only the
/// macOS branch is exercised; the Windows branch is exercised when the suite runs
/// on Windows, which is the point — the Windows change is the one that fixes a
/// real hazard (a roaming-profile SQLite file) and it cannot be verified here.
/// </para>
/// </remarks>
public sealed class AppPathsTests
{
    /// <summary>
    /// The data directory sits under the platform's per-user data root and is
    /// named for the application.
    /// </summary>
    [Fact]
    public void DataDirectory_UsesThePlatformConvention()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string actual = AppPaths.DataDirectory;

        if (OperatingSystem.IsWindows())
        {
            string localAppData =
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Publisher-scoped, matching what the WiX installer creates:
            // Directories.wxs nests INSTALLFOLDER inside a CompanyFolder of this
            // name under LocalAppDataFolder. An installed Avalonia build
            // therefore finds the database an installed WPF build was already
            // using. This asymmetry with the Unix branches is deliberate --
            // neither macOS nor Linux has a publisher-folder convention.
            Assert.Equal(
                Path.Combine(localAppData, "Thornhill Computer Software", "AstroCast"),
                actual);

            // The roaming profile is the specific thing being avoided: it is
            // OS-synchronised, and a SQLite file copied mid-write can corrupt.
            string roaming =
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            Assert.False(
                actual.StartsWith(roaming, StringComparison.OrdinalIgnoreCase),
                "The data directory must not sit under the roaming profile.");
        }
        else if (OperatingSystem.IsMacOS())
        {
            Assert.Equal(
                Path.Combine(home, "Library", "Application Support", "AstroCast"),
                actual);
        }
        else
        {
            string? xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            string expectedRoot = !string.IsNullOrWhiteSpace(xdg) && Path.IsPathRooted(xdg)
                ? xdg
                : Path.Combine(home, ".local", "share");

            Assert.Equal(Path.Combine(expectedRoot, "AstroCast"), actual);
        }
    }

    /// <summary>
    /// The database and log locations hang off the data directory.
    /// </summary>
    [Fact]
    public void DatabaseAndLogs_LiveUnderTheDataDirectory()
    {
        Assert.Equal(Path.Combine(AppPaths.DataDirectory, "astrocast.db"), AppPaths.DatabaseFile);
        Assert.Equal(Path.Combine(AppPaths.DataDirectory, "logs"), AppPaths.LogDirectory);
        Assert.Equal(Path.Combine(AppPaths.LogDirectory, "astrocast-.log"), AppPaths.LogFileTemplate);
    }

    /// <summary>
    /// The ephemeris directory stays beside the executable and is not moved into
    /// the per-user data directory.
    /// </summary>
    /// <remarks>
    /// This is the invariant most at risk from a well-meaning tidy-up that
    /// "unifies" all three paths. <c>ephe/</c> is a read-only build artefact
    /// copied to the output directory; relocating it would break the ephemeris
    /// load on every platform while looking like a consistency improvement.
    /// </remarks>
    [Fact]
    public void EphemerisDirectory_StaysBesideTheExecutable()
    {
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "ephe"), AppPaths.EphemerisDirectory);
        Assert.DoesNotContain(AppPaths.DataDirectory, AppPaths.EphemerisDirectory);
    }
}
