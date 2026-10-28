namespace AstroCast.Core.Models;

/// <summary>
/// A single eclipse with definitive type, precise timing, and positional data
/// calculated from the Swiss Ephemeris eclipse functions.
/// </summary>
/// <remarks>
/// Solar eclipses use <c>swe_sol_eclipse_when_glob</c>; lunar eclipses use
/// <c>swe_lun_eclipse_when</c>. Both provide sub-second precision for all
/// phase times, enabling precise Event chart casting at any eclipse phase.
/// </remarks>
public sealed class EclipseResult
{
    /// <summary>"Solar" or "Lunar".</summary>
    public required string Category { get; init; }

    /// <summary>True for solar eclipse, false for lunar.</summary>
    public required bool IsSolar { get; init; }

    /// <summary>Eclipse type: "Total", "Annular", "Partial", "Hybrid", "Penumbral".</summary>
    public required string Type { get; init; }

    /// <summary>Full label: "Total Solar Eclipse", "Penumbral Lunar Eclipse", etc.</summary>
    public required string FullLabel { get; init; }

    /// <summary>Exact UTC date/time of maximum eclipse.</summary>
    public required DateTime MaximumUtc { get; init; }

    /// <summary>Julian Day of maximum eclipse (for internal use/verification).</summary>
    public required double MaximumJd { get; init; }

    // --- Solar eclipse phase times ---

    /// <summary>First contact (partial phase begins). Solar eclipses only.</summary>
    public DateTime? FirstContactUtc { get; init; }

    /// <summary>Last contact (partial phase ends). Solar eclipses only.</summary>
    public DateTime? LastContactUtc { get; init; }

    /// <summary>Totality/annularity begins: for a solar eclipse, the central phase over the whole Earth; for a lunar eclipse, totality. Null for partial eclipses.</summary>
    public DateTime? TotalityBeginUtc { get; init; }

    /// <summary>Totality/annularity ends: for a solar eclipse, the central phase over the whole Earth; for a lunar eclipse, totality. Null for partial eclipses.</summary>
    public DateTime? TotalityEndUtc { get; init; }

    // --- Lunar eclipse additional phase times ---

    /// <summary>Penumbral phase begins (lunar eclipses only).</summary>
    public DateTime? PenumbralBeginUtc { get; init; }

    /// <summary>Penumbral phase ends (lunar eclipses only).</summary>
    public DateTime? PenumbralEndUtc { get; init; }

    /// <summary>
    /// The eclipse's duration figure in minutes, which is a different quantity for each kind.
    /// </summary>
    /// <remarks>
    /// For a solar eclipse: the duration of the central eclipse over the whole Earth, from the first
    /// moment the total or annular phase is seen anywhere on Earth to the last (the library's global
    /// search, from <see cref="TotalityBeginUtc"/> to <see cref="TotalityEndUtc"/>); 7 to 239 minutes,
    /// against at most about 7½ minutes of totality at any one place. Null for a partial eclipse,
    /// which has no central phase. For a lunar eclipse: the duration of totality; null when the
    /// eclipse is not total. The name is kept for compatibility.
    /// </remarks>
    public double? TotalityDurationMinutes { get; init; }

    // --- Positional data ---

    /// <summary>Sun longitude at maximum eclipse.</summary>
    public required double SunLongitude { get; init; }

    /// <summary>Moon longitude at maximum eclipse.</summary>
    public required double MoonLongitude { get; init; }

    /// <summary>Zodiac sign of the eclipse (Sun's sign for solar, Moon's sign for lunar).</summary>
    public required string Sign { get; init; }

    /// <summary>Degree within the sign.</summary>
    public required double Degree { get; init; }

    /// <summary>Formatted position (e.g., "28°49' Aquarius").</summary>
    public required string PositionFormatted { get; init; }

    /// <summary>Element of the eclipse sign.</summary>
    public required string Element { get; init; }

    /// <summary>Modality of the eclipse sign.</summary>
    public required string Modality { get; init; }

    /// <summary>True Node longitude at the eclipse moment.</summary>
    public required double NodeLongitude { get; init; }

    /// <summary>Distance from Moon to nearest node in degrees.</summary>
    public required double NodeDistance { get; init; }

    /// <summary>The Swiss Ephemeris raw eclipse type flags.</summary>
    public required int RawFlags { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}

/// <summary>
/// A series of solar and lunar eclipses within a date range.
/// </summary>
public sealed class EclipseSeries
{
    /// <summary>All eclipses found, chronologically.</summary>
    public required IReadOnlyList<EclipseResult> Eclipses { get; init; }

    /// <summary>Solar eclipses only.</summary>
    public IReadOnlyList<EclipseResult> SolarEclipses =>
        Eclipses.Where(e => e.IsSolar).ToList();

    /// <summary>Lunar eclipses only.</summary>
    public IReadOnlyList<EclipseResult> LunarEclipses =>
        Eclipses.Where(e => !e.IsSolar).ToList();

    /// <summary>Scan start.</summary>
    public required DateTime ScanStart { get; init; }

    /// <summary>Scan end.</summary>
    public required DateTime ScanEnd { get; init; }

    /// <summary>Total count.</summary>
    public required int Count { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}
