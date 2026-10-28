namespace AstroCast.ViewModels;

/// <summary>
/// Mode for planetary return calculation: find by target year or by ordinal return number.
/// </summary>
public enum ReturnFindMode
{
    /// <summary>Find the return nearest to a specified year.</summary>
    ByYear,

    /// <summary>Find the Nth return in the native's life.</summary>
    ByReturnNumber,
}
