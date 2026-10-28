namespace AstroCast.Core.Utilities;

/// <summary>
/// The span of time the shipped ephemeris files cover for every body a chart casts: from
/// 1200-01-01 04:54:46 UT (Julian calendar) to 2399-12-31 23:51:20 UT. The chart engine refuses
/// an instant outside it, and every year-based series clamps its window to it, so nothing asks the
/// library for the sky outside it.
/// </summary>
/// <remarks>
/// Established (F0-6) from the files and from direct <c>swe_calc_ut</c> calls with the app's flags,
/// each from a freshly opened library. Outside the span the library falls back silently to the
/// Moshier model for the planets and fails for Chiron, or succeeds or fails by which file a
/// previous call left open.
/// </remarks>
public static class EphemerisSpan
{
    /// <summary>
    /// The first instant at which the shipped ephemeris files give every body a chart casts,
    /// JD 2159357.7046912 (UT): 1200-01-01 04:54:45.32 UT on the Julian calendar.
    /// </summary>
    /// <remarks>
    /// sepl_12 and seas_12 begin at 1200-01-01 00:00 TT, and Pluto, whose light time reaches back
    /// about five hours, is the last body to come from them; before this instant the library falls
    /// back silently to the Moshier model for the planets. The value is the edge rounded inward to
    /// 1e-7 day.
    /// </remarks>
    public const double FirstJulianDay = 2159357.7046912;

    /// <summary>
    /// The last instant at which the shipped ephemeris files give every body a chart casts,
    /// JD 2597641.4939863 (UT): 2399-12-31 23:51:20.42 UT.
    /// </summary>
    /// <remarks>
    /// After it Terrestrial Time reaches the year 2400, whose asteroid file (seas_24) is not
    /// shipped, so Chiron fails; the planets would follow from 2400-01-10, when sepl_18 ends, on the
    /// Moshier model. A call made while seas_18 is still open reaches further, so a request there
    /// succeeded or failed by what had been asked before it. The value is the edge rounded inward
    /// to 1e-7 day.
    /// </remarks>
    public const double LastJulianDay = 2597641.4939863;

    /// <summary>
    /// The end of a scan that steps whole days from <paramref name="start"/> and must reach
    /// <paramref name="end"/>: the first whole number of days from the start at or after the end,
    /// but never after <see cref="LastJulianDay"/>.
    /// </summary>
    /// <param name="start">The scan's first instant (Julian Day, UT).</param>
    /// <param name="end">The instant the scan must reach (Julian Day, UT).</param>
    /// <returns>The scan's last instant, at or after <paramref name="end"/> unless the span ends first.</returns>
    /// <remarks>
    /// A window clamped to the span's first instant, 04:54:46 UT, steps at 04:54:46, so its last whole
    /// step falls short of 1 January 00:00 UT by 19 hours; the scan runs on to the next step, and the
    /// series drops anything found at or after <paramref name="end"/>. A window that begins and ends at
    /// 00:00 UT is a whole number of days long and is unchanged.
    /// </remarks>
    public static double WholeDayScanEnd(double start, double end) =>
        Math.Min(start + Math.Ceiling(end - start), LastJulianDay);
}
