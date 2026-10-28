namespace AstroCast.Core.Enums;

/// <summary>
/// Identifies the four local-space angles used in paran (paranatellonta) calculations.
/// A paran occurs when two celestial bodies simultaneously occupy different angles
/// as seen from a specific geographic location.
/// </summary>
/// <remarks>
/// Rising and Setting are horizon events affected by atmospheric refraction,
/// semi-diameter, and true altitude. Culminating and Anti-Culminating are purely
/// geometric meridian crossings (local hour angle = 0° and 180° respectively).
/// </remarks>
public enum AngleType
{
    /// <summary>
    /// Rising — the body crosses the eastern horizon (Ascendant axis).
    /// Calculated via <c>swe_rise_trans()</c> with <c>SE_CALC_RISE</c>,
    /// which accounts for atmospheric refraction and disc centre.
    /// </summary>
    Rising = 0,

    /// <summary>
    /// Culminating — the body crosses the upper meridian (MC axis).
    /// The body's local hour angle reaches 0°. This is the moment of
    /// highest altitude above the horizon (upper transit).
    /// Calculated via <c>swe_rise_trans()</c> with <c>SE_CALC_MTRANSIT</c>.
    /// </summary>
    Culminating = 1,

    /// <summary>
    /// Setting — the body crosses the western horizon (Descendant axis).
    /// Calculated via <c>swe_rise_trans()</c> with <c>SE_CALC_SET</c>,
    /// which accounts for atmospheric refraction and disc centre.
    /// </summary>
    Setting = 2,

    /// <summary>
    /// Anti-Culminating — the body crosses the lower meridian (IC axis).
    /// The body's local hour angle reaches 180°. This is the moment of
    /// lowest altitude (lower transit, below the horizon for most bodies).
    /// Calculated via <c>swe_rise_trans()</c> with <c>SE_CALC_ITRANSIT</c>.
    /// </summary>
    AntiCulminating = 3,
}
