namespace AstroCast.Core.Models.PrimaryDirections;

/// <summary>
/// The settings of a primary-direction run (branch Primary, 1.5.0): the key, whether the planets are significators,
/// the age the list runs to, and the birth-time uncertainty the range is given for.
/// </summary>
/// <remarks>
/// The method itself is fixed: Placidus semi-arcs in zodiaco, direct and traditional converse, with the promissors
/// and significators of rules R4 and R5. The limits are enforced as the settings are built, so a result never carries
/// a setting outside them.
/// </remarks>
public sealed class PrimaryDirectionSettings
{
    /// <summary>The lowest age limit, in years.</summary>
    public const int MinAgeLimitYears = 1;

    /// <summary>The highest age limit, in years.</summary>
    public const int MaxAgeLimitYears = 120;

    /// <summary>The widest birth-time uncertainty, in minutes.</summary>
    public const int MaxBirthTimeUncertaintyMinutes = 120;

    /// <summary>The default settings: Naibod's key, the angles, the lights and the Part of Fortune, to age 100, no uncertainty.</summary>
    public static PrimaryDirectionSettings Default { get; } = new();

    /// <summary>The key that turns an arc into years. Naibod by default.</summary>
    public PrimaryDirectionKey Key { get; init; } = PrimaryDirectionKey.Naibod;

    /// <summary>
    /// Whether Mercury to Pluto are significators beside the angles, the Sun, the Moon and the Part of Fortune.
    /// False by default.
    /// </summary>
    public bool PlanetsAsSignificators { get; init; }

    /// <summary>
    /// The age, in years, the life list runs to, both ends included: a direction whose years exceed it is not listed.
    /// 100 by default, from <see cref="MinAgeLimitYears"/> to <see cref="MaxAgeLimitYears"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the limits.</exception>
    public int AgeLimitYears
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, MinAgeLimitYears, nameof(AgeLimitYears));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxAgeLimitYears, nameof(AgeLimitYears));
            field = value;
        }
    } = 100;

    /// <summary>
    /// The birth-time uncertainty, in minutes either way, each hit's date range is given for; 0, the default, gives no
    /// range. From 0 to <see cref="MaxBirthTimeUncertaintyMinutes"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the limits.</exception>
    public int BirthTimeUncertaintyMinutes
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(BirthTimeUncertaintyMinutes));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, MaxBirthTimeUncertaintyMinutes, nameof(BirthTimeUncertaintyMinutes));
            field = value;
        }
    }
}
