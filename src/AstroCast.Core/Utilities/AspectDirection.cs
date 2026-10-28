namespace AstroCast.Core.Utilities;

/// <summary>
/// Decides whether an aspect is applying or separating: applying when its orb is
/// decreasing at the instant, separating otherwise. The one rule every caller uses
/// — chart aspects, transits, progressions, solar arcs and composites.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rule.</b> With Δ the signed difference of the two longitudes,
/// <c>longitude1 − longitude2</c>, wrapped to (−180°, 180°], the separation is |Δ| and
/// its rate is <c>sign(Δ) · (speed1 − speed2)</c>. The orb is
/// <c>|separation − aspectAngle|</c>, so its rate is the separation's rate times
/// <c>sign(separation − aspectAngle)</c>. The aspect is applying when that rate is
/// negative.
/// </para>
/// <para>
/// <b>The rate comes from the speeds at the instant, not from a look-ahead.</b> Until
/// 1.3.3 each caller compared the orb now with the orb a step later — 0.01 day for
/// chart aspects, 24 hours for transits, 0.01 progressed day for progressions, 0.01°
/// of arc for solar arcs. An aspect that perfects inside the step has a larger orb
/// at the end of it than now, so it was called separating while it was still
/// applying: every Sun–Moon major aspect of 2026 read "separating" for the last
/// seven minutes before it perfected, and 392 of 254,628 planet-to-planet labels in a
/// 9,979-chart sweep were wrong. A pair whose relative motion reverses inside the step
/// was wrong the same way. The speeds at the instant have neither failure.
/// </para>
/// <para>
/// <b>A point that does not move has speed 0.</b> That is a natal point under a
/// transit, progression or solar arc, and the Ascendant and Midheaven of a chart,
/// which are held still (ruling D5): an aspect to an angle is applying when the
/// planet moves toward the angle's degree, whatever the angle's own motion. A
/// progressed angle does move: the Midheaven at the progressed Sun's speed, the
/// Ascendant at that times dASC/dMC (branch G, 1.3.4). A
/// solar-arc point moves at the arc's rate, which is never negative. A composite
/// position moves at the average of the two natal speeds, so a composite aspect is
/// judged by the composite's own motion.
/// </para>
/// <para>
/// <b>An orb of exactly 0 is separating.</b> The separation equals the aspect angle,
/// <c>sign(separation − aspectAngle)</c> is 0, and so is the orb's rate: the aspect is
/// perfect and its orb is not decreasing. The same holds for a conjunction with Δ
/// exactly 0 and an opposition with Δ exactly 180°.
/// </para>
/// <para>
/// <b>A rate of exactly 0 is separating.</b> Two points moving at the same speed, or a
/// still point and an angle, keep their orb: it is not decreasing, so the aspect is not
/// applying.
/// </para>
/// <para>
/// The speeds may be in any unit of time — degrees per day, per progressed day, per
/// degree of arc — so long as both are in the same one. Only the sign of the result
/// decides the label.
/// </para>
/// </remarks>
public static class AspectDirection
{
    /// <summary>
    /// Returns the rate at which the orb of an aspect between two points changes at the
    /// instant their longitudes and speeds describe.
    /// </summary>
    /// <param name="longitude1">Ecliptic longitude of the first point, in degrees.</param>
    /// <param name="speed1">Speed in longitude of the first point; 0 for a point that does not move.</param>
    /// <param name="longitude2">Ecliptic longitude of the second point, in degrees.</param>
    /// <param name="speed2">Speed in longitude of the second point, in the same unit as <paramref name="speed1"/>; 0 for a point that does not move.</param>
    /// <param name="aspectAngle">The aspect's exact angle, in degrees (0–180).</param>
    /// <returns>
    /// The orb's rate in degrees per unit of the speeds' time: negative while the orb is
    /// decreasing, positive while it is increasing, 0 when the orb is exactly 0 or the
    /// separation is not changing.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any argument is NaN or infinite, or when <paramref name="aspectAngle"/>
    /// is outside 0–180°.
    /// </exception>
    /// <remarks>See <see cref="AspectDirection"/> for the rule and its edge cases.</remarks>
    public static double OrbRate(
        double longitude1, double speed1,
        double longitude2, double speed2,
        double aspectAngle)
    {
        RequireFinite(longitude1, nameof(longitude1));
        RequireFinite(speed1, nameof(speed1));
        RequireFinite(longitude2, nameof(longitude2));
        RequireFinite(speed2, nameof(speed2));
        RequireFinite(aspectAngle, nameof(aspectAngle));
        if (aspectAngle < 0.0 || aspectAngle > 180.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(aspectAngle), aspectAngle, "An aspect angle must lie between 0° and 180°.");
        }

        double delta = (longitude1 - longitude2) % 360.0;
        if (delta > 180.0)
        {
            delta -= 360.0;
        }
        else if (delta <= -180.0)
        {
            delta += 360.0;
        }

        double separation = Math.Abs(delta);
        double separationRate = Math.Sign(delta) * (speed1 - speed2);

        return separationRate * Math.Sign(separation - aspectAngle);
    }

    /// <summary>
    /// Determines whether an aspect between two points is applying: whether its orb is
    /// decreasing at the instant their longitudes and speeds describe.
    /// </summary>
    /// <param name="longitude1">Ecliptic longitude of the first point, in degrees.</param>
    /// <param name="speed1">Speed in longitude of the first point; 0 for a point that does not move.</param>
    /// <param name="longitude2">Ecliptic longitude of the second point, in degrees.</param>
    /// <param name="speed2">Speed in longitude of the second point, in the same unit as <paramref name="speed1"/>; 0 for a point that does not move.</param>
    /// <param name="aspectAngle">The aspect's exact angle, in degrees (0–180).</param>
    /// <returns>
    /// <c>true</c> when the orb's rate (<see cref="OrbRate"/>) is negative — applying;
    /// <c>false</c> otherwise — separating, including an orb of exactly 0 and a rate of
    /// exactly 0.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any argument is NaN or infinite, or when <paramref name="aspectAngle"/>
    /// is outside 0–180°.
    /// </exception>
    /// <remarks>See <see cref="AspectDirection"/> for the rule and its edge cases.</remarks>
    public static bool IsApplying(
        double longitude1, double speed1,
        double longitude2, double speed2,
        double aspectAngle)
    {
        return OrbRate(longitude1, speed1, longitude2, speed2, aspectAngle) < 0.0;
    }

    /// <summary>
    /// Throws when a value is NaN or infinite.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="paramName">The parameter's name, for the exception.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is not finite.</exception>
    private static void RequireFinite(double value, string paramName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, "The value must be a finite number.");
        }
    }
}
