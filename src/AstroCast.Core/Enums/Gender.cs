namespace AstroCast.Core.Enums;

/// <summary>
/// Gender of the chart subject (for natal charts).
/// </summary>
public enum Gender
{
    /// <summary>Not specified or not applicable (e.g., event charts).</summary>
    Unspecified = 0,

    /// <summary>Male.</summary>
    Male = 1,

    /// <summary>Female.</summary>
    Female = 2,

    /// <summary>Other or non-binary.</summary>
    Other = 3
}
