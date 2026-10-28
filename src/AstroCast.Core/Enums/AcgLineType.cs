namespace AstroCast.Core.Enums;

/// <summary>
/// The type of angular line in an astrocartography map.
/// </summary>
public enum AcgLineType
{
    /// <summary>Midheaven - the planet culminates (on the meridian above the horizon).</summary>
    MC,

    /// <summary>Imum Coeli - the planet anti-culminates (on the meridian below the horizon).</summary>
    IC,

    /// <summary>Ascendant - the planet rises (on the eastern horizon).</summary>
    ASC,

    /// <summary>Descendant - the planet sets (on the western horizon).</summary>
    DSC
}
