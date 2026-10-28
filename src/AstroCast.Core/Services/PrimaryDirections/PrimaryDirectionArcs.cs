namespace AstroCast.Core.Services.PrimaryDirections;

/// <summary>
/// The arcs of direction (rules R2 and R3): the degrees of right ascension the diurnal motion turns through to carry
/// one point to the other's place under the Placidus proportion of semi-arcs.
/// </summary>
internal static class PrimaryDirectionArcs
{
    /// <summary>
    /// The direct arc, carrying promissor P to significator S (rule R2): (target − H_P) mod 360, the target 0 for the
    /// Midheaven, 180 for the IC, −DSA_P for the Ascendant, +DSA_P for the Descendant, and for a body or the Part of
    /// Fortune the hour angle in S's hemisphere h_S · SA_P / SA_S, plus 180 when that hemisphere is the lower.
    /// </summary>
    /// <param name="significator">The significator.</param>
    /// <param name="promissor">The promissor point.</param>
    /// <returns>The arc in [0°, 360°), or null when a semi-arc it uses does not exist.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    internal static double? Direct(SpeculumSignificator significator, SpeculumPoint promissor)
    {
        ArgumentNullException.ThrowIfNull(significator);
        ArgumentNullException.ThrowIfNull(promissor);

        double target;
        switch (significator.Name)
        {
            case "Midheaven":
                target = 0.0;
                break;
            case "IC":
                target = 180.0;
                break;
            case "Ascendant":
            case "Descendant":
                if (promissor.DiurnalSemiArc is not double promissorDsa)
                    return null;
                target = significator.Name == "Ascendant" ? -promissorDsa : promissorDsa;
                break;
            default:
                SpeculumPoint s = significator.Point;
                if (!promissor.HasSemiArcs || !s.HasSemiArcs)
                    return null;
                (double hourAngle, double significatorSemiArc) = s.InHemisphere(s.IsUpper);
                (_, double promissorSemiArc) = promissor.InHemisphere(s.IsUpper);
                double proportional = hourAngle * promissorSemiArc / significatorSemiArc;
                target = s.IsUpper ? proportional : proportional + 180.0;
                break;
        }

        return SpeculumPoint.Normalize360(target - promissor.HourAngle);
    }

    /// <summary>
    /// The converse arc, carrying significator S to promissor P's place (rule R3): (target − H_S) mod 360, the target
    /// the hour angle in P's hemisphere h_P · SA_S / SA_P, plus 180 when that hemisphere is the lower. An angle is
    /// carried as its ecliptic degree, which its point holds.
    /// </summary>
    /// <param name="significator">The significator's point.</param>
    /// <param name="promissor">The promissor point.</param>
    /// <returns>The arc in [0°, 360°), or null when either point lacks its semi-arcs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
    internal static double? Converse(SpeculumPoint significator, SpeculumPoint promissor)
    {
        ArgumentNullException.ThrowIfNull(significator);
        ArgumentNullException.ThrowIfNull(promissor);

        if (!promissor.HasSemiArcs || !significator.HasSemiArcs)
            return null;

        (double hourAngle, double promissorSemiArc) = promissor.InHemisphere(promissor.IsUpper);
        (_, double significatorSemiArc) = significator.InHemisphere(promissor.IsUpper);
        double proportional = hourAngle * significatorSemiArc / promissorSemiArc;
        double target = promissor.IsUpper ? proportional : proportional + 180.0;

        return SpeculumPoint.Normalize360(target - significator.HourAngle);
    }
}
