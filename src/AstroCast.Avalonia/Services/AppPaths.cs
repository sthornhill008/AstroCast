namespace AstroCast.Avalonia.Services;

/// <summary>
/// Resolves the filesystem locations the Avalonia head uses: the read-only
/// ephemeris directory beside the executable, and the writable database and log
/// directories under the current user's per-application data folder.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the writable paths differ from the WPF head.</b> The WPF head resolves
/// all three locations from <c>AppDomain.CurrentDomain.BaseDirectory</c> — the
/// directory holding the executable. That is fine for a Windows application
/// installed per-user, but it is wrong on macOS: inside a signed <c>.app</c>
/// bundle the executable's directory lives within the bundle, which is read-only
/// under Gatekeeper and is replaced wholesale on update. Writing a SQLite
/// database there fails or silently loses data.
/// </para>
/// <para>
/// <b>Only the writable locations move.</b> <see cref="EphemerisDirectory"/>
/// stays beside the executable, because the ephemeris files are a read-only build
/// artefact that <c>AstroCast.Core.csproj</c> already copies to the output
/// directory for whichever project references it. Nothing writes there.
/// </para>
/// <para>
/// <b>Deliberate divergence from the WPF head.</b> This type is used by the
/// Avalonia head only; the WPF head is untouched and keeps its
/// <c>BaseDirectory</c> paths, because moving them would strand the existing
/// database of anyone already running it. The consequence is that on Windows the
/// two heads read <i>different</i> databases. Note what the WPF head's location
/// actually is in a shipped build: the installer is a per-user package
/// (<c>Package.wxs</c>, <c>Scope="perUser"</c>) rooted at
/// <c>LocalAppDataFolder\Thornhill Computer Software\AstroCast</c>, so an
/// installed WPF build already keeps its database under <c>%LOCALAPPDATA%</c> —
/// "beside the executable" means <c>bin\Debug</c> only in a development tree.
/// The two Windows locations therefore differ by the company folder, not by
/// roaming-versus-local. A one-time migration remains a requirement before the
/// Avalonia head can replace the WPF one; it is out of scope here and tracked as
/// a later-phase decision.
/// </para>
/// <para>
/// <b>Why this branches per platform instead of calling one API.</b> An earlier
/// version resolved <see cref="Environment.SpecialFolder.ApplicationData"/> on
/// every platform and documented that as covering both targets. It does resolve
/// correctly on macOS — .NET maps both <c>ApplicationData</c> and
/// <c>LocalApplicationData</c> to <c>~/Library/Application Support</c> there —
/// but it is wrong on the other two platforms, and being accidentally right on
/// one is not a design. On Windows <c>ApplicationData</c> is the <i>roaming</i>
/// profile, which is actively hazardous for this application: roaming
/// directories are synchronised by the OS, and a SQLite database copied while it
/// is open can be captured mid-write and corrupted. On Linux it maps to
/// <c>~/.config</c>, which is for configuration, not for a database. Each
/// platform now states its own answer.
/// </para>
/// </remarks>
public static class AppPaths
{
    /// <summary>
    /// The application folder name created beneath the platform's
    /// application-data directory.
    /// </summary>
    private const string AppFolderName = "AstroCast";

    /// <summary>
    /// The publisher folder Windows convention places above
    /// <see cref="AppFolderName"/>.
    /// </summary>
    /// <remarks>
    /// Windows only, and deliberately so. This is the layout the WiX installer
    /// already creates — <c>Directories.wxs</c> nests
    /// <c>INSTALLFOLDER</c> inside a <c>CompanyFolder</c> of this name under
    /// <c>LocalAppDataFolder</c> — so an installed Avalonia build lands on the
    /// same database an installed WPF build was using, with no migration step.
    /// macOS and Linux have no publisher-folder convention to mirror, and
    /// inventing one there to match Windows would be the wrong kind of
    /// consistency.
    /// </remarks>
    private const string PublisherFolderName = "Thornhill Computer Software";

    /// <summary>
    /// Gets the writable per-user directory holding the database and log files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>%LOCALAPPDATA%\Thornhill Computer Software\AstroCast</c> on Windows,
    /// <c>~/Library/Application Support/AstroCast</c> on macOS, and
    /// <c>$XDG_DATA_HOME/AstroCast</c> (falling back to
    /// <c>~/.local/share/AstroCast</c>) on Linux and other Unix platforms.
    /// </para>
    /// <para>
    /// The path is therefore <i>not</i> the same shape on every platform, which
    /// is intended. See <see cref="PublisherFolderName"/>.
    /// </para>
    /// </remarks>
    /// <returns>The absolute path to the application data directory.</returns>
    public static string DataDirectory => OperatingSystem.IsWindows()
        ? Path.Combine(ResolveDataRoot(), PublisherFolderName, AppFolderName)
        : Path.Combine(ResolveDataRoot(), AppFolderName);

    /// <summary>
    /// Gets the absolute path of the SQLite chart database.
    /// </summary>
    public static string DatabaseFile => Path.Combine(DataDirectory, "astrocast.db");

    /// <summary>
    /// Gets the directory holding rolling log files.
    /// </summary>
    public static string LogDirectory => Path.Combine(DataDirectory, "logs");

    /// <summary>
    /// Gets the Serilog rolling-file path template for the application log.
    /// </summary>
    /// <remarks>
    /// Serilog appends the rolling-interval suffix to the stem, so the literal
    /// file name carries a trailing hyphen exactly as the WPF head's does.
    /// </remarks>
    public static string LogFileTemplate => Path.Combine(LogDirectory, "astrocast-.log");

    /// <summary>
    /// Gets the read-only Swiss Ephemeris data directory beside the executable.
    /// </summary>
    /// <remarks>
    /// Populated by <c>AstroCast.Core.csproj</c>, which copies <c>ephe/</c> to the
    /// output directory of every project referencing it. Never written to, so it
    /// is deliberately <i>not</i> platform-branched: unlike the database and the
    /// logs, a read-only build artefact belongs beside the executable on every
    /// platform, including inside a macOS <c>.app</c> bundle.
    /// </remarks>
    public static string EphemerisDirectory =>
        Path.Combine(AppContext.BaseDirectory, "ephe");

    /// <summary>
    /// Creates the writable directories if they do not already exist.
    /// </summary>
    /// <remarks>
    /// Called once during startup, before the logger or the repository is
    /// constructed, because both write into these locations immediately.
    /// <see cref="Directory.CreateDirectory(string)"/> is a no-op when the
    /// directory is already present.
    /// </remarks>
    /// <exception cref="IOException">
    /// Thrown when a directory cannot be created — for example when a file
    /// already exists at the target path.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the process lacks permission to create the directories.
    /// </exception>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }

    /// <summary>
    /// Resolves the platform's per-user data root, above the application folder.
    /// </summary>
    /// <returns>
    /// The absolute path of the directory beneath which <see cref="AppFolderName"/>
    /// is created.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>Windows</b> uses <see cref="Environment.SpecialFolder.LocalApplicationData"/>
    /// (<c>%LOCALAPPDATA%</c>) rather than
    /// <see cref="Environment.SpecialFolder.ApplicationData"/> (<c>%APPDATA%</c>).
    /// The latter is the roaming profile, which the OS synchronises between
    /// machines; a SQLite file captured mid-write by that synchronisation can be
    /// corrupted. Application databases belong in the local profile.
    /// </para>
    /// <para>
    /// <b>macOS</b> is built explicitly from the home directory rather than from
    /// a <see cref="Environment.SpecialFolder"/> value. .NET does currently map
    /// <c>ApplicationData</c> to <c>~/Library/Application Support</c> on macOS,
    /// so this resolves to the same location the previous implementation did —
    /// no existing database is stranded — but that mapping is a framework
    /// implementation detail, and the platform convention is the thing worth
    /// depending on.
    /// </para>
    /// <para>
    /// <b>Linux and other Unix platforms</b> follow the XDG Base Directory
    /// specification: <c>$XDG_DATA_HOME</c> when set, otherwise
    /// <c>~/.local/share</c>. Per that specification a relative
    /// <c>$XDG_DATA_HOME</c> is invalid and must be ignored, so a value that is
    /// not rooted falls through to the default. Note this is the <i>data</i>
    /// directory, not <c>~/.config</c> — which is where
    /// <c>SpecialFolder.ApplicationData</c> would have landed a database.
    /// </para>
    /// </remarks>
    private static string ResolveDataRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support");
        }

        string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

        return !string.IsNullOrWhiteSpace(xdgDataHome) && Path.IsPathRooted(xdgDataHome)
            ? xdgDataHome
            : Path.Combine(home, ".local", "share");
    }
}
