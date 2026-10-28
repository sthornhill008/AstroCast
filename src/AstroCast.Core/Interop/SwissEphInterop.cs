using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace AstroCast.Core.Interop;

/// <summary>
/// Low-level P/Invoke declarations for the Swiss Ephemeris native library
/// (swedll64.dll on Windows, libswe.dylib on macOS, libswe.so on Linux — see
/// <see cref="ResolveNativeLibrary"/>).
/// These are direct mappings of the C functions defined in swephexp.h.
/// Do not call directly from application code — use <see cref="AstroCast.Core.Interfaces.IEphemerisService"/> instead.
/// </summary>
/// <remarks>
/// Calling convention is __cdecl (no PASCAL/stdcall defined during compilation).
/// String parameters use ANSI encoding (C char*).
/// The error buffer (serr) must be pre-allocated to at least 256 characters (AS_MAXCH).
/// </remarks>
internal static class SwissEphInterop
{
    /// <summary>Name of the native Swiss Ephemeris DLL (without extension).</summary>
    private const string DllName = "swedll64";

    /// <summary>Calling convention used by all Swiss Ephemeris functions.</summary>
    private const CallingConvention CC = CallingConvention.Cdecl;

    /// <summary>Maximum size of the Swiss Ephemeris error string buffer (AS_MAXCH).</summary>
    internal const int MaxErrorLength = 256;

    /// <summary>
    /// Special body constant for obliquity and nutation.
    /// Not a real body — used with swe_calc_ut to retrieve ecliptic obliquity.
    /// xx[0] = true obliquity, xx[1] = mean obliquity, xx[2] = nutation in longitude,
    /// xx[3] = nutation in obliquity.
    /// </summary>
    internal const int SE_ECL_NUT = -1;

    #region Native Library Resolution

    /// <summary>
    /// Registers <see cref="ResolveNativeLibrary"/> as this assembly's DllImport
    /// resolver. Declared as an explicit static constructor — not a field
    /// initialiser — so the compiler does not emit <c>beforefieldinit</c>: the CLR
    /// then guarantees the resolver is registered before the first P/Invoke on this
    /// type, and exactly once. That single registration matters because
    /// <see cref="NativeLibrary.SetDllImportResolver"/> throws
    /// <see cref="InvalidOperationException"/> if called twice for the same assembly,
    /// so this constructor must remain the only registration site.
    /// </summary>
    static SwissEphInterop()
    {
        NativeLibrary.SetDllImportResolver(
            typeof(SwissEphInterop).Assembly, ResolveNativeLibrary);
    }

    /// <summary>
    /// Maps the platform-neutral <see cref="DllName"/> ("swedll64") used by every
    /// P/Invoke in this class to the platform's actual Swiss Ephemeris binary:
    /// <c>swedll64.dll</c> on Windows, <c>libswe.dylib</c> on macOS,
    /// <c>libswe.so</c> on Linux.
    /// </summary>
    /// <remarks>
    /// This resolver exists because the runtime's default probing decorates the
    /// bare name per platform (e.g. <c>libswedll64.dylib</c> on macOS,
    /// <c>libswedll64.so</c> on Linux), so the real binaries — which follow the
    /// library's own naming convention — would never be found outside Windows.
    /// Resolving here keeps the P/Invoke declarations platform-neutral instead of
    /// duplicating them per OS.
    /// </remarks>
    /// <param name="libraryName">Library name the runtime is attempting to load.</param>
    /// <param name="assembly">Assembly whose P/Invoke triggered the load.</param>
    /// <param name="searchPath">Search-path hint from the P/Invoke declaration, if any.</param>
    /// <returns>
    /// A handle to the loaded native library, or <see cref="IntPtr.Zero"/> for any
    /// unrecognised library name (or unsupported OS) so the runtime's default
    /// probing still applies.
    /// </returns>
    private static IntPtr ResolveNativeLibrary(
        string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != DllName)
        {
            return IntPtr.Zero;
        }

        string platformLibraryName;

        if (OperatingSystem.IsWindows())
        {
            platformLibraryName = "swedll64.dll";
        }
        else if (OperatingSystem.IsMacOS())
        {
            platformLibraryName = "libswe.dylib";
        }
        else if (OperatingSystem.IsLinux())
        {
            platformLibraryName = "libswe.so";
        }
        else
        {
            return IntPtr.Zero;
        }

        return NativeLibrary.TryLoad(platformLibraryName, assembly, searchPath, out IntPtr handle)
            ? handle
            : IntPtr.Zero;
    }

    #endregion

    #region Initialization and Cleanup

    /// <summary>
    /// Set the directory path for ephemeris data files (.se1).
    /// Must be called before any calculation functions.
    /// </summary>
    /// <param name="path">Path to the directory containing .se1 files, or null for default.</param>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern void swe_set_ephe_path(string? path);

    /// <summary>
    /// Close Swiss Ephemeris and release all allocated resources.
    /// Should be called when the ephemeris service is no longer needed.
    /// </summary>
    [DllImport(DllName, CallingConvention = CC)]
    internal static extern void swe_close();

    /// <summary>
    /// Get the Swiss Ephemeris library version string.
    /// </summary>
    /// <param name="svers">Pre-allocated buffer (at least 256 chars) to receive the version string.</param>
    /// <returns>Pointer to the version string (same as svers buffer).</returns>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern IntPtr swe_version(StringBuilder svers);

    #endregion

    #region Julian Day Conversion

    /// <summary>
    /// Convert a calendar date and time to Julian Day number.
    /// </summary>
    /// <param name="year">Calendar year.</param>
    /// <param name="month">Month (1–12).</param>
    /// <param name="day">Day of month (1–31).</param>
    /// <param name="hour">Hour as decimal (e.g., 13.5 = 13:30).</param>
    /// <param name="gregflag">Calendar type: 0 = Julian, 1 = Gregorian.</param>
    /// <returns>The Julian Day number.</returns>
    [DllImport(DllName, CallingConvention = CC)]
    internal static extern double swe_julday(
        int year, int month, int day, double hour, int gregflag);

    /// <summary>
    /// Convert a Julian Day number back to a calendar date and time.
    /// </summary>
    /// <param name="jd">Julian Day number.</param>
    /// <param name="gregflag">Calendar type: 0 = Julian, 1 = Gregorian.</param>
    /// <param name="year">Output: calendar year.</param>
    /// <param name="month">Output: month (1–12).</param>
    /// <param name="day">Output: day of month (1–31).</param>
    /// <param name="hour">Output: hour as decimal.</param>
    [DllImport(DllName, CallingConvention = CC)]
    internal static extern void swe_revjul(
        double jd, int gregflag,
        out int year, out int month, out int day, out double hour);

    /// <summary>
    /// Convert UTC date/time to Julian Day numbers (both ET and UT).
    /// </summary>
    /// <param name="year">Year.</param>
    /// <param name="month">Month (1–12).</param>
    /// <param name="day">Day (1–31).</param>
    /// <param name="hour">Hour (0–23).</param>
    /// <param name="min">Minute (0–59).</param>
    /// <param name="sec">Second (0–59.999...).</param>
    /// <param name="gregflag">Calendar type: 0 = Julian, 1 = Gregorian.</param>
    /// <param name="dret">Output array of 2 doubles: dret[0] = JD(ET), dret[1] = JD(UT).</param>
    /// <param name="serr">Error string buffer (at least 256 chars).</param>
    /// <returns>0 on success, negative on error.</returns>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern int swe_utc_to_jd(
        int year, int month, int day,
        int hour, int min, double sec,
        int gregflag,
        [Out] double[] dret,
        StringBuilder serr);

    #endregion

    #region Planetary Calculation

    /// <summary>
    /// Calculate the position of a celestial body for a given Julian Day UT.
    /// </summary>
    /// <param name="tjd_ut">Julian Day number in Universal Time.</param>
    /// <param name="ipl">Planet/body number (SE_SUN=0, SE_MOON=1, etc.).</param>
    /// <param name="iflag">Calculation flags (combine SEFLG_* constants with bitwise OR).</param>
    /// <param name="xx">Output array of 6 doubles:
    /// xx[0]=longitude, xx[1]=latitude, xx[2]=distance,
    /// xx[3]=speed in longitude, xx[4]=speed in latitude, xx[5]=speed in distance.
    /// With SEFLG_EQUATORIAL: xx[0]=RA, xx[1]=declination.</param>
    /// <param name="serr">Error string buffer (at least 256 chars).</param>
    /// <returns>Flags actually used on success (>= 0), or negative on error.</returns>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern int swe_calc_ut(
        double tjd_ut, int ipl, int iflag,
        [Out] double[] xx,
        StringBuilder serr);

    /// <summary>
    /// Get the name of a planet or celestial body by its constant number.
    /// </summary>
    /// <param name="ipl">Planet/body number.</param>
    /// <param name="snam">Pre-allocated buffer (at least 256 chars) to receive the name.</param>
    /// <returns>Pointer to the name string (same as snam buffer).</returns>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern IntPtr swe_get_planet_name(
        int ipl, StringBuilder snam);

    #endregion

    #region House Calculation

    /// <summary>
    /// Calculate house cusps and angles for a given time and location.
    /// </summary>
    /// <param name="tjd_ut">Julian Day number in Universal Time.</param>
    /// <param name="geolat">Geographic latitude (north positive).</param>
    /// <param name="geolon">Geographic longitude (east positive).</param>
    /// <param name="hsys">House system character: 'P' for Placidus, 'K' for Koch, etc.</param>
    /// <param name="cusps">Output array of 13 doubles: cusps[1..12] are house cusps, cusps[0] is unused.</param>
    /// <param name="ascmc">Output array of 10 doubles:
    /// ascmc[0]=ASC, ascmc[1]=MC, ascmc[2]=ARMC, ascmc[3]=Vertex,
    /// ascmc[4]=equatorial ASC, ascmc[5]=co-ASC (Koch),
    /// ascmc[6]=co-ASC (Munkasey), ascmc[7]=polar ASC.</param>
    /// <returns>0 on success.</returns>
    [DllImport(DllName, CallingConvention = CC)]
    internal static extern int swe_houses(
        double tjd_ut, double geolat, double geolon, int hsys,
        [Out] double[] cusps,
        [Out] double[] ascmc);

    /// <summary>
    /// Get the house position (1.0–12.999) for a body at given ecliptic coordinates.
    /// </summary>
    /// <param name="armc">ARMC (sidereal time in degrees).</param>
    /// <param name="geolat">Geographic latitude.</param>
    /// <param name="eps">Obliquity of the ecliptic.</param>
    /// <param name="hsys">House system character.</param>
    /// <param name="xpin">Input array: xpin[0]=ecliptic longitude, xpin[1]=ecliptic latitude.</param>
    /// <param name="serr">Error string buffer.</param>
    /// <returns>House position (1.0–12.999), or negative on error.</returns>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern double swe_house_pos(
        double armc, double geolat, double eps, int hsys,
        double[] xpin,
        StringBuilder serr);

    /// <summary>
    /// Calculate house cusps and angles from a given ARMC (sidereal time in degrees),
    /// geographic latitude, and obliquity. Unlike <see cref="swe_houses"/> which
    /// requires a Julian Day, this accepts pre-computed ARMC directly — essential
    /// for composite chart house cusps derived from the composite MC, and for the
    /// progressed Ascendant, cast for the progressed Midheaven's sidereal time.
    /// </summary>
    /// <param name="armc">ARMC (sidereal time) in degrees (0–360).</param>
    /// <param name="geolat">Geographic latitude (north positive).</param>
    /// <param name="eps">True obliquity of the ecliptic in degrees.</param>
    /// <param name="hsys">House system character: 'P' for Placidus, 'K' for Koch, etc.</param>
    /// <param name="cusps">Output array of 13 doubles: cusps[1..12] are house cusps, cusps[0] is unused.</param>
    /// <param name="ascmc">Output array of 10 doubles:
    /// ascmc[0]=ASC, ascmc[1]=MC, ascmc[2]=ARMC, ascmc[3]=Vertex, etc.</param>
    /// <returns>0 on success.</returns>
    [DllImport(DllName, CallingConvention = CC)]
    internal static extern int swe_houses_armc(
        double armc, double geolat, double eps, int hsys,
        [Out] double[] cusps,
        [Out] double[] ascmc);

    #endregion

    #region Coordinate Transformation

    /// <summary>
    /// Transform between ecliptic and equatorial coordinates.
    /// </summary>
    /// <param name="xpo">Input array: xpo[0]=longitude/RA, xpo[1]=latitude/declination, xpo[2]=distance.</param>
    /// <param name="xpn">Output array: same structure as xpo but in the target coordinate system.</param>
    /// <param name="eps">Obliquity of the ecliptic. Use negative value for equatorial-to-ecliptic.</param>
    [DllImport(DllName, CallingConvention = CC)]
    internal static extern void swe_cotrans(
        double[] xpo,
        [Out] double[] xpn,
        double eps);

    #endregion

    #region Sidereal Time

    /// <summary>
    /// Calculate sidereal time at Greenwich for a given Julian Day UT.
    /// </summary>
    /// <param name="tjd_ut">Julian Day number in Universal Time.</param>
    /// <returns>Sidereal time in hours (0–24).</returns>
    [DllImport(DllName, CallingConvention = CC)]
    internal static extern double swe_sidtime(double tjd_ut);

    #endregion

    #region Rise and Transit

    /// <summary>
    /// Calculate rise, set, or transit of a celestial body.
    /// Used for sunrise/sunset calculation required by the planetary hour system.
    /// </summary>
    /// <param name="tjd_ut">Julian Day number in Universal Time.</param>
    /// <param name="ipl">Planet/body number (SE_SUN=0, SE_MOON=1, etc.).</param>
    /// <param name="starname">Star name (null for planets).</param>
    /// <param name="epheflag">Ephemeris flag (SEFLG_SWIEPH = 2).</param>
    /// <param name="rsmi">Rise/set/transit flag (SE_CALC_RISE, SE_CALC_SET, etc.).</param>
    /// <param name="geopos">Geographic position: [longitude, latitude, altitude].</param>
    /// <param name="atpress">Atmospheric pressure in mbar (1013.25 for standard).</param>
    /// <param name="attemp">Atmospheric temperature in °C (15.0 for standard).</param>
    /// <param name="tret">Output: Julian Day number of the event.</param>
    /// <param name="serr">Error string buffer (at least 256 chars).</param>
    /// <returns>0 on success, negative on error, 1 if event not found.</returns>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern int swe_rise_trans(
        double tjd_ut, int ipl, StringBuilder? starname,
        int epheflag, int rsmi, double[] geopos,
        double atpress, double attemp, ref double tret,
        StringBuilder serr);

    /// <summary>Rise flag for swe_rise_trans.</summary>
    internal const int SE_CALC_RISE = 1;

    /// <summary>Set flag for swe_rise_trans.</summary>
    internal const int SE_CALC_SET = 2;

    /// <summary>Use disc center (not upper limb) for rise/set calculation.</summary>
    internal const int SE_BIT_DISC_CENTER = 256;

    /// <summary>
    /// Upper meridian transit flag for swe_rise_trans.
    /// Finds the next moment the body's local hour angle reaches 0° (body on MC).
    /// This is a purely geometric calculation — no atmospheric refraction is applied.
    /// </summary>
    internal const int SE_CALC_MTRANSIT = 4;

    /// <summary>
    /// Lower meridian transit flag for swe_rise_trans.
    /// Finds the next moment the body's local hour angle reaches 180° (body on IC).
    /// This is a purely geometric calculation — no atmospheric refraction is applied.
    /// </summary>
    internal const int SE_CALC_ITRANSIT = 8;

    /// <summary>Sun body ID for swe_rise_trans.</summary>
    internal const int SE_SUN = 0;

    #endregion

    #region Fixed Stars

    /// <summary>
    /// Swiss Ephemeris flag for using Swiss Ephemeris data files (SEFLG_SWIEPH = 2).
    /// Required by <see cref="swe_fixstar2_ut"/> to use .se1 ephemeris files.
    /// </summary>
    internal const int SEFLG_SWIEPH = 2;

    /// <summary>
    /// Calculate the position of a fixed star for a given Julian Day UT.
    /// Uses the improved swe_fixstar2 interface which searches by star name
    /// without requiring the traditional ";"-prefixed notation.
    /// </summary>
    /// <param name="star">
    /// Star name to look up (e.g., "Regulus", "Algol", "Spica").
    /// The buffer is modified in place to contain the full star record from sefstars.txt.
    /// Must be pre-allocated to at least 41 characters (SE_MAX_STNAME * 2 + 1).
    /// </param>
    /// <param name="tjd_ut">Julian Day number in Universal Time.</param>
    /// <param name="iflag">Calculation flags (typically SEFLG_SWIEPH = 2).</param>
    /// <param name="xx">Output array of 6 doubles:
    /// xx[0]=ecliptic longitude, xx[1]=ecliptic latitude, xx[2]=distance (AU),
    /// xx[3]=speed in longitude, xx[4]=speed in latitude, xx[5]=speed in distance.</param>
    /// <param name="serr">Error string buffer (at least 256 chars).</param>
    /// <returns>Flags actually used on success (&gt;= 0), or negative on error.</returns>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern int swe_fixstar2_ut(
        StringBuilder star,
        double tjd_ut,
        int iflag,
        [Out] double[] xx,
        StringBuilder serr);

    #endregion

    #region Delta T

    /// <summary>
    /// Get Delta T (difference between Ephemeris Time and Universal Time) for a Julian Day.
    /// </summary>
    /// <param name="tjd">Julian Day number.</param>
    /// <param name="iflag">Ephemeris flag (SEFLG_SWIEPH, SEFLG_JPLEPH, or SEFLG_MOSEPH).</param>
    /// <param name="serr">Error string buffer.</param>
    /// <returns>Delta T in days.</returns>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern double swe_deltat_ex(
        double tjd, int iflag, StringBuilder serr);

    #endregion

    #region Eclipse Calculation

    /// <summary>Eclipse flag: Central eclipse.</summary>
    internal const int SE_ECL_CENTRAL = 1;

    /// <summary>Eclipse flag: Non-central eclipse.</summary>
    internal const int SE_ECL_NONCENTRAL = 2;

    /// <summary>Eclipse type flag: Total.</summary>
    internal const int SE_ECL_TOTAL = 4;

    /// <summary>Eclipse type flag: Annular.</summary>
    internal const int SE_ECL_ANNULAR = 8;

    /// <summary>Eclipse type flag: Partial.</summary>
    internal const int SE_ECL_PARTIAL = 16;

    /// <summary>Eclipse type flag: Annular-Total (Hybrid).</summary>
    internal const int SE_ECL_ANNULAR_TOTAL = 32;

    /// <summary>Eclipse type flag: Penumbral (lunar only).</summary>
    internal const int SE_ECL_PENUMBRAL = 64;

    /// <summary>
    /// Find the next solar eclipse globally after a given Julian Day.
    /// Returns eclipse type flags and fills tret with timing data.
    /// </summary>
    /// <param name="tjd_start">Julian Day to start searching from.</param>
    /// <param name="ifl">Ephemeris flag (SEFLG_SWIEPH = 2).</param>
    /// <param name="ifltype">Eclipse type filter: 0 = any type.</param>
    /// <param name="tret">Output array of 10 doubles with eclipse phase times.</param>
    /// <param name="backward">0 = forward, 1 = backward.</param>
    /// <param name="serr">Error string buffer (at least 256 chars).</param>
    /// <returns>Eclipse type flags (SE_ECL_TOTAL etc.), or negative on error.</returns>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern int swe_sol_eclipse_when_glob(
        double tjd_start, int ifl, int ifltype,
        [Out] double[] tret,
        int backward,
        StringBuilder serr);

    /// <summary>
    /// Find the next lunar eclipse after a given Julian Day.
    /// Returns eclipse type flags and fills tret with timing data.
    /// </summary>
    /// <param name="tjd_start">Julian Day to start searching from.</param>
    /// <param name="ifl">Ephemeris flag (SEFLG_SWIEPH = 2).</param>
    /// <param name="ifltype">Eclipse type filter: 0 = any type.</param>
    /// <param name="tret">Output array of 10 doubles with eclipse phase times.</param>
    /// <param name="backward">0 = forward, 1 = backward.</param>
    /// <param name="serr">Error string buffer (at least 256 chars).</param>
    /// <returns>Eclipse type flags (SE_ECL_TOTAL etc.), or negative on error.</returns>
    [DllImport(DllName, CallingConvention = CC, CharSet = CharSet.Ansi)]
    internal static extern int swe_lun_eclipse_when(
        double tjd_start, int ifl, int ifltype,
        [Out] double[] tret,
        int backward,
        StringBuilder serr);

    #endregion
}
