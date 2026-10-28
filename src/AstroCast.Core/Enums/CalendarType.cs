namespace AstroCast.Core.Enums;

/// <summary>
/// Calendar system for date conversions.
/// Values map to SE_JUL_CAL and SE_GREG_CAL in swephexp.h.
/// </summary>
public enum CalendarType
{
    /// <summary>Julian calendar (SE_JUL_CAL = 0).</summary>
    Julian = 0,

    /// <summary>Gregorian calendar (SE_GREG_CAL = 1).</summary>
    Gregorian = 1
}
