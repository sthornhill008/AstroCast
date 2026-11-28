using System.Diagnostics;
using System.Runtime.InteropServices;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Exceptions;
using AstroCast.Core.Interfaces;

namespace AstroCast.Core.Services;

/// <summary>
/// The start-up check of the ephemeris files (branch EphePath, 1.7.2; finding N4, part 2): once a program has
/// set the ephemeris path it reads one position that needs the files, and when it cannot, the user is told at
/// once, in plain words that name the folder and the cause. Both heads and the CLI run it and show its
/// message, so all three say the same.
/// </summary>
/// <remarks>
/// <para>
/// Why it exists. The Swiss Ephemeris library answers for the Sun, the Moon and the planets from a built-in
/// model when it cannot open its files, with a warning in the log and no word to the user; Chiron has no such
/// model, so it fails. Until 1.7.2 a program whose ephemeris folder could not be read started as usual, and
/// the first the user heard of it was a file name in the status bar after a chart failed. The folder cannot
/// be read when its files are missing or damaged, and, on Windows, when its name holds a letter the process's
/// code page cannot hold: the two Windows programs carry a manifest that makes that code page UTF-8, which
/// holds every name, but an earlier Windows than 10 version 1903 ignores it, and a host without the manifest
/// has the system's code page.
/// </para>
/// <para>
/// What it does. It reads Chiron's position at J2000.0 through the service the program has just made, and
/// discards it: one position, read once, a few milliseconds, the same call every chart makes. It changes no
/// chart and keeps nothing. It logs its result with the folder, pass or fail.
/// </para>
/// </remarks>
public static class EphemerisFilesCheck
{
    /// <summary>The instant the check reads: J2000.0, which every shipped ephemeris file set covers.</summary>
    private const double CheckJulianDayUt = 2451545.0;

    /// <summary>The message's opening, before the folder.</summary>
    private const string MessageStart = "AstroCast cannot read its ephemeris files in ";

    /// <summary>What follows the folder: what the user loses until the fault is put right.</summary>
    private const string MessageConsequence = ". Charts cannot be calculated until this is fixed. ";

    /// <summary>The cause and the remedy when the folder's name does not survive the process's code page.</summary>
    private const string NameCause =
        "The folder's name has letters outside your Windows language's character set: install AstroCast in a folder with a plain name.";

    /// <summary>The cause and the remedy in every other case: the files themselves.</summary>
    private const string FilesCause = "The files may be missing or damaged: install AstroCast again.";

    /// <summary>
    /// Reads one position that needs the ephemeris files, Chiron's at J2000.0, discards it, and logs the
    /// result with the folder.
    /// </summary>
    /// <param name="ephemeris">The ephemeris service the program has just made over the folder.</param>
    /// <param name="ephemerisFolder">The ephemeris folder the service was given.</param>
    /// <param name="logger">The program's logger.</param>
    /// <returns>
    /// Null when the position was read. Otherwise the message for the user
    /// (<see cref="FailureMessage(string)"/>): the program shows it as it shows its other start-up problems.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ephemeris"/> or <paramref name="logger"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ephemerisFolder"/> is null, empty or white space.</exception>
    public static string? Run(IEphemerisService ephemeris, string ephemerisFolder, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentException.ThrowIfNullOrWhiteSpace(ephemerisFolder);
        ArgumentNullException.ThrowIfNull(logger);

        string folder = Path.GetFullPath(ephemerisFolder);
        ILogger log = logger.ForContext(typeof(EphemerisFilesCheck));
        Stopwatch watch = Stopwatch.StartNew();

        try
        {
            _ = ephemeris.CalculatePosition(CheckJulianDayUt, CelestialBodyId.Chiron);
        }
        catch (EphemerisCalculationException ex)
        {
            log.Error(ex, "Ephemeris files check failed for {EphemerisFolder}", folder);
            return FailureMessage(folder);
        }

        log.Information(
            "Ephemeris files check passed for {EphemerisFolder} in {ElapsedMilliseconds} ms", folder, watch.ElapsedMilliseconds);
        return null;
    }

    /// <summary>
    /// The message for a folder the ephemeris files cannot be read from, with the cause found from the
    /// folder's name (<see cref="SurvivesTheCodePage"/>).
    /// </summary>
    /// <param name="ephemerisFolder">The ephemeris folder.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ephemerisFolder"/> is null, empty or white space.</exception>
    public static string FailureMessage(string ephemerisFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ephemerisFolder);

        return FailureMessage(ephemerisFolder, SurvivesTheCodePage(ephemerisFolder));
    }

    /// <summary>
    /// The message for a folder the ephemeris files cannot be read from: "AstroCast cannot read its ephemeris
    /// files in {folder}. Charts cannot be calculated until this is fixed." and then the cause with its
    /// remedy: "The folder's name has letters outside your Windows language's character set: install AstroCast
    /// in a folder with a plain name." when the name does not survive the process's code page, and otherwise
    /// "The files may be missing or damaged: install AstroCast again.". It names the folder and no file.
    /// </summary>
    /// <param name="ephemerisFolder">The ephemeris folder, as it is to be printed.</param>
    /// <param name="nameSurvivesTheCodePage">Whether the folder's path survives the process's code page.</param>
    /// <returns>The message.</returns>
    internal static string FailureMessage(string ephemerisFolder, bool nameSurvivesTheCodePage) =>
        MessageStart + ephemerisFolder + MessageConsequence + (nameSurvivesTheCodePage ? FilesCause : NameCause);

    /// <summary>
    /// Whether a path reaches the library as it is: it is turned into the bytes the library is handed, in the
    /// process's ANSI code page, and back, as <c>SwissEphInterop</c>'s marshalling turns it, and compared. On
    /// Windows a letter the code page cannot hold comes back as another letter or a question mark; with UTF-8
    /// as the code page, and on macOS and Linux, every path comes back as it went.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>True when the path comes back unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    internal static bool SurvivesTheCodePage(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        IntPtr bytes = Marshal.StringToHGlobalAnsi(path);
        try
        {
            return string.Equals(Marshal.PtrToStringAnsi(bytes), path, StringComparison.Ordinal);
        }
        finally
        {
            Marshal.FreeHGlobal(bytes);
        }
    }
}
