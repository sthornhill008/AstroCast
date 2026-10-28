namespace AstroCast.Core.Models.PrimaryDirections;

/// <summary>
/// Which point the primary motion carries to the other (branch Primary, 1.5.0, rules R2 and R3).
/// </summary>
public enum PrimaryDirectionMotion
{
    /// <summary>Direct: the promissor is carried by the diurnal motion to the significator's place.</summary>
    Direct,

    /// <summary>
    /// Converse, the traditional kind: the roles are exchanged and the significator is carried by the diurnal motion
    /// to the promissor's place; an angle as significator is carried as its ecliptic degree.
    /// </summary>
    Converse,
}
