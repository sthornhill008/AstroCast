namespace AstroCast.Core.Enums;

/// <summary>
/// Whether a stationing planet is moving toward its nearest station or away
/// from it. Decided from the station's time relative to the chart instant,
/// never from the sign of the current speed.
/// </summary>
public enum StationPhase
{
    /// <summary>
    /// The station lies ahead of the chart instant: the planet is still
    /// decelerating and its |speed| is falling toward zero.
    /// </summary>
    Approaching,

    /// <summary>
    /// The station lies at or behind the chart instant: the planet has
    /// already turned and its |speed| is rising away from zero.
    /// </summary>
    Separating,
}
