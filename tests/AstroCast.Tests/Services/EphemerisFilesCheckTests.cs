using System.Runtime.InteropServices;

using AstroCast.Core.Enums;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AstroCast.Tests.Services;

/// <summary>
/// The start-up check of the ephemeris files (branch EphePath, 1.7.2; finding N4, part 2): when a program has
/// set the ephemeris path it reads one position that needs the files, Chiron's, and when it cannot the user is
/// told at once, in plain words that name the folder and the cause, in place of a file name in the status bar
/// after a failed chart. <see cref="EphemerisFilesCheck"/> is the one check and the one message for both heads
/// and the CLI.
/// </summary>
/// <remarks>
/// <para>
/// The Swiss Ephemeris library keeps its ephemeris path for the whole process, so a test that points it at
/// another folder is in the "Ephemeris" collection, where no other ephemeris test runs beside it, and puts the
/// suite's own folder back before it ends (<see cref="RestoreTheSuiteFolder"/>), then reads Chiron through the
/// shared fixture to show the suite is as it was. No service made here is disposed: the library is closed once
/// for the whole run, by the fixture.
/// </para>
/// <para>
/// What no test here can show is Chiron being read from a folder whose name is outside the system's code
/// page: that needs the program's own manifest (UTF-8 as the process's code page), and the test host has
/// none. <see cref="Run_FolderOutsideTheCodePage_InAHostWithoutTheManifest_FailsAndSaysWhy"/> shows the other
/// half, the fault itself and the message it now gets; the read is proved on the built programs.
/// </para>
/// </remarks>
/// <param name="fixture">The shared ephemeris fixture.</param>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class EphemerisFilesCheckTests(EphemerisFixture fixture)
{
    /// <summary>J2000.0, the instant the check reads.</summary>
    private const double J2000 = 2451545.0;

    /// <summary>The suite's own ephemeris folder, as the fixture gives it to the library.</summary>
    private static readonly string SuiteFolder = Path.Combine(AppContext.BaseDirectory, "ephe");

    /// <summary>The check passes for the suite's own ephemeris folder, says nothing to the user, and logs that it passed, with the folder.</summary>
    [Fact]
    public void Run_SuiteFolder_PassesAndLogsTheFolder()
    {
        RecordingSink sink = new();

        string? message = EphemerisFilesCheck.Run(fixture.EphemerisService, SuiteFolder, LoggerOver(sink));

        message.Should().BeNull();
        LogEvent logged = sink.Events.Should().ContainSingle().Which;
        logged.Level.Should().Be(LogEventLevel.Information);
        logged.RenderMessage().Should().Contain("passed").And.Contain(Path.GetFullPath(SuiteFolder));
    }

    /// <summary>
    /// For a folder that has no files the check fails with the plain message: it names the folder and the
    /// cause, and no file name; and the failure is logged with the folder.
    /// </summary>
    [Fact]
    public void Run_FolderWithNoFiles_FailsWithThePlainMessage()
    {
        string empty = Path.Combine(Path.GetTempPath(), "astrocast-ephecheck-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        RecordingSink sink = new();
        try
        {
            EphemerisService pointedAtTheEmptyFolder = new(empty, new LoggerConfiguration().CreateLogger());

            string? message = EphemerisFilesCheck.Run(pointedAtTheEmptyFolder, empty, LoggerOver(sink));

            message.Should().Be(
                $"AstroCast cannot read its ephemeris files in {empty}. Charts cannot be calculated until this is fixed. "
                + "The files may be missing or damaged: install AstroCast again.");
            message.Should().NotContain(".se1", "the message names the folder, not a file");
            sink.Events.Should().ContainSingle(e => e.Level == LogEventLevel.Error)
                .Which.RenderMessage().Should().Contain("failed").And.Contain(empty);
        }
        finally
        {
            RestoreTheSuiteFolder();
            Directory.Delete(empty, recursive: true);
        }

        TheSuiteReadsChironAgain();
    }

    /// <summary>
    /// The fault itself, in a host without the manifest (Windows only): the suite's ephemeris files, copied
    /// under the temporary folder into a folder named with Polish, Greek and Japanese letters. Where the
    /// process's code page cannot hold the name, as in the test host on any system whose code page is not
    /// UTF-8, the library cannot open the files, and the check fails with the message that says the name is
    /// the cause. Where the process's code page is UTF-8 the name survives, and the check passes: that is the
    /// state the two Windows programs are in by their manifest.
    /// </summary>
    [Fact]
    public void Run_FolderOutsideTheCodePage_InAHostWithoutTheManifest_FailsAndSaysWhy()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // L-stroke, Greek epsilon lambda, and two Japanese characters: no one legacy code page holds all three
        string name = "astrocast-\u0141ukasz-\u0395\u03bb-\u592a\u90ce-" + Guid.NewGuid().ToString("N");
        string folder = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(folder);
        try
        {
            foreach (string file in Directory.GetFiles(SuiteFolder))
            {
                File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
            }

            bool nameSurvives = GetACP() == Utf8CodePage;
            EphemerisFilesCheck.SurvivesTheCodePage(folder).Should().Be(nameSurvives);

            EphemerisService pointedAtTheFolder = new(folder, new LoggerConfiguration().CreateLogger());
            string? message = EphemerisFilesCheck.Run(pointedAtTheFolder, folder, new LoggerConfiguration().CreateLogger());

            if (nameSurvives)
            {
                message.Should().BeNull("with UTF-8 as the process's code page the name reaches the library whole");
            }
            else
            {
                message.Should().Be(
                    $"AstroCast cannot read its ephemeris files in {folder}. Charts cannot be calculated until this is fixed. "
                    + "The folder's name has letters outside your Windows language's character set: install AstroCast in a folder with a plain name.");
            }
        }
        finally
        {
            RestoreTheSuiteFolder();
            Directory.Delete(folder, recursive: true);
        }

        TheSuiteReadsChironAgain();
    }

    /// <summary>
    /// The message names the cause: a folder whose name the process's code page cannot hold is told to move to a
    /// plain name; any other folder that the files cannot be read from is told to install again. Both name the
    /// folder as given, and neither names a file.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void FailureMessage_NamesTheFolderAndTheCause()
    {
        const string folder = @"C:\Users\Example\AppData\Local\Thornhill Computer Software\AstroCast\ephe";

        EphemerisFilesCheck.FailureMessage(folder, nameSurvivesTheCodePage: true).Should().Be(
            @"AstroCast cannot read its ephemeris files in C:\Users\Example\AppData\Local\Thornhill Computer Software\AstroCast\ephe. "
            + "Charts cannot be calculated until this is fixed. The files may be missing or damaged: install AstroCast again.");
        EphemerisFilesCheck.FailureMessage(folder, nameSurvivesTheCodePage: false).Should().Be(
            @"AstroCast cannot read its ephemeris files in C:\Users\Example\AppData\Local\Thornhill Computer Software\AstroCast\ephe. "
            + "Charts cannot be calculated until this is fixed. The folder's name has letters outside your Windows language's character set: "
            + "install AstroCast in a folder with a plain name.");
    }

    /// <summary>A plain folder name survives every code page, so its failure is never put down to its name.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void SurvivesTheCodePage_PlainName_IsTrue()
    {
        EphemerisFilesCheck.SurvivesTheCodePage(@"C:\Program Files\AstroCast\ephe").Should().BeTrue();
        EphemerisFilesCheck.SurvivesTheCodePage("/Applications/AstroCast.app/Contents/MacOS/ephe").Should().BeTrue();
    }

    /// <summary>
    /// Both heads and the CLI run the one check directly after they set the ephemeris path, and none builds a
    /// message of its own. The WPF head and the CLI have no test project, so all three are read from their
    /// source.
    /// </summary>
    /// <param name="relativePath">The program's start-up file, from the repository root.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("src/AstroCast/App.xaml.cs")]
    [InlineData("src/AstroCast.Avalonia/App.axaml.cs")]
    [InlineData("src/AstroCast.Cli/Program.cs")]
    public void EveryProgram_RunsTheOneCheckAtStartUp(string relativePath)
    {
        string source = File.ReadAllText(Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar)));

        int created = source.IndexOf("new EphemerisService(", StringComparison.Ordinal);
        if (created < 0)
        {
            created = source.IndexOf("EphemerisService ephemeris = new(", StringComparison.Ordinal);
        }

        int check = source.IndexOf("EphemerisFilesCheck.Run(", StringComparison.Ordinal);
        created.Should().BePositive("the program makes its ephemeris service");
        check.Should().BeGreaterThan(created, "the check runs once the ephemeris path is set");
        source.Should().NotContain("cannot read its ephemeris files", "the message is the shared one");
    }

    #region Helpers

    /// <summary>The code page number of UTF-8.</summary>
    private const uint Utf8CodePage = 65001;

    /// <summary>The process's ANSI code page (Windows).</summary>
    /// <returns>The code page number.</returns>
    [DllImport("kernel32.dll")]
    private static extern uint GetACP();

    /// <summary>Points the library back at the suite's own ephemeris folder, as the fixture set it.</summary>
    private static void RestoreTheSuiteFolder() => _ = new EphemerisService(SuiteFolder, new LoggerConfiguration().CreateLogger());

    /// <summary>The shared fixture reads Chiron at J2000.0 at its known place, 11 degrees 37 minutes of Sagittarius: the suite's folder is back.</summary>
    private void TheSuiteReadsChironAgain() =>
        fixture.EphemerisService.CalculatePosition(J2000, CelestialBodyId.Chiron).Longitude.Should().BeApproximately(251.6176, 1e-3);

    /// <summary>A logger that writes every event to a sink.</summary>
    /// <param name="sink">The sink.</param>
    /// <returns>The logger.</returns>
    private static ILogger LoggerOver(RecordingSink sink) =>
        new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

    /// <summary>The repository root: the nearest folder above the test's output that holds <c>AstroCast.slnx</c>.</summary>
    /// <returns>The repository root's full path.</returns>
    /// <exception cref="DirectoryNotFoundException">No folder above the output holds <c>AstroCast.slnx</c>.</exception>
    private static string RepositoryRoot()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "AstroCast.slnx")))
            {
                return folder.FullName;
            }
        }

        throw new DirectoryNotFoundException($"No folder above {AppContext.BaseDirectory} holds AstroCast.slnx.");
    }

    /// <summary>Keeps every log event written to it.</summary>
    private sealed class RecordingSink : ILogEventSink
    {
        /// <summary>The events, in the order written.</summary>
        public List<LogEvent> Events { get; } = [];

        /// <inheritdoc />
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    #endregion
}
