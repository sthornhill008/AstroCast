namespace AstroCast.Core.Configuration;

/// <summary>
/// Configuration options for the Swiss Ephemeris service.
/// </summary>
public sealed class EphemerisOptions
{
    /// <summary>
    /// Path to the directory containing Swiss Ephemeris data files (.se1).
    /// </summary>
    public required string EphemerisDataPath { get; init; }

    /// <summary>
    /// Swiss Ephemeris calculation flags.
    /// Default: SE_FLG_SWIEPH | SE_FLG_SPEED (Swiss Ephemeris mode with daily speed).
    /// </summary>
    /// <remarks>
    /// The value 258 corresponds to SEFLG_SWIEPH (2) | SEFLG_SPEED (256).
    /// </remarks>
    public int CalculationFlags { get; init; } = 2 | 256;

    /// <summary>
    /// A house system identifier, 'P' (Placidus) by default. Not read: each chart
    /// passes its own house system to <c>swe_houses()</c>, and nothing in the
    /// solution reads these options.
    /// </summary>
    public char HouseSystem { get; init; } = 'P';
}
