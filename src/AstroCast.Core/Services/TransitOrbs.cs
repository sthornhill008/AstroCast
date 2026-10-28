using AstroCast.Core.Enums;

namespace AstroCast.Core.Services;

/// <summary>
/// Transit orb definitions. Transit orbs are tighter than natal orbs
/// because transits are time-bound events.
/// </summary>
public static class TransitOrbs
{
    /// <summary>
    /// Gets the orb in degrees for a transit aspect.
    /// </summary>
    /// <param name="transitBody">The transiting planet.</param>
    /// <param name="aspectAngle">The aspect angle (0, 60, 90, 120, 180 for major;
    /// 30, 45, 108, 135, 150 for minor).</param>
    /// <param name="isMinorAspect">True if this is a minor aspect.</param>
    /// <returns>Orb in degrees.</returns>
    public static double GetOrb(TransitBody transitBody, double aspectAngle, bool isMinorAspect)
    {
        if (isMinorAspect)
            return GetMinorOrb(transitBody);

        return GetMajorOrb(transitBody, aspectAngle);
    }

    /// <summary>Major aspect orbs by transit body and aspect type.</summary>
    private static double GetMajorOrb(TransitBody body, double aspectAngle)
    {
        // Conjunction (0°) and Opposition (180°) get wider orbs
        bool isConjunctionOrOpposition = aspectAngle is 0.0 or 180.0;

        return body switch
        {
            TransitBody.Jupiter or TransitBody.Saturn =>
                isConjunctionOrOpposition ? 2.0 : 1.5,

            TransitBody.Uranus or TransitBody.Neptune or TransitBody.Pluto
                or TransitBody.Chiron or TransitBody.NorthNode or TransitBody.Mars =>
                isConjunctionOrOpposition ? 1.5 : 1.0,

            _ => 1.0
        };
    }

    /// <summary>Minor aspect orbs by transit body.</summary>
    private static double GetMinorOrb(TransitBody body)
    {
        return body switch
        {
            TransitBody.Jupiter or TransitBody.Saturn => 1.0,
            _ => 0.75 // 0°45' = 0.75 degrees
        };
    }

    /// <summary>
    /// The midpoint activation orb (10 arc-minutes = 0.1667°).
    /// Used for all transiting bodies on the 45° dial.
    /// </summary>
    public const double MidpointOrb = 10.0 / 60.0; // 0.1667°

    /// <summary>
    /// Mars daily motion threshold. Mars is only included in transit
    /// reports when it moves slower than this (near station).
    /// </summary>
    public const double MarsSlowMotionThreshold = 0.3; // degrees per day
}
