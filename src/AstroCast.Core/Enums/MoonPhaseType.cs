namespace AstroCast.Core.Enums;

/// <summary>
/// Moon phase classification based on Sun-Moon angular separation.
/// Used in electional Moon condition evaluation.
/// </summary>
public enum MoonPhaseType
{
    /// <summary>0°–45° ahead of Sun.</summary>
    NewMoon,

    /// <summary>45°–90° ahead of Sun.</summary>
    WaxingCrescent,

    /// <summary>90°–135° ahead of Sun.</summary>
    FirstQuarter,

    /// <summary>135°–180° ahead of Sun.</summary>
    WaxingGibbous,

    /// <summary>180°–225° ahead of Sun.</summary>
    FullMoon,

    /// <summary>225°–270° ahead of Sun.</summary>
    WaningGibbous,

    /// <summary>270°–315° ahead of Sun.</summary>
    LastQuarter,

    /// <summary>315°–360° ahead of Sun.</summary>
    WaningCrescent,
}
