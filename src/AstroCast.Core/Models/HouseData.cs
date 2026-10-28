using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Contains all house cusp positions and chart angles calculated for a specific
/// time and location in the chart's house system.
/// </summary>
/// <remarks>
/// Cusps are ecliptic longitudes (0–360°) where each house begins.
/// The Descendant and IC are computed properties, derived as the points
/// directly opposite the Ascendant and Midheaven respectively.
/// </remarks>
public sealed class HouseData
{
    /// <summary>Ecliptic longitudes of house cusps 1–12 (decimal degrees, 0–360).</summary>
    public required IReadOnlyList<double> Cusps { get; init; }

    /// <summary>Ascendant ecliptic longitude (decimal degrees).</summary>
    public required double Ascendant { get; init; }

    /// <summary>Midheaven (MC) ecliptic longitude (decimal degrees).</summary>
    public required double Midheaven { get; init; }

    /// <summary>ARMC (sidereal time in degrees) used for house position calculations.</summary>
    public required double Armc { get; init; }

    /// <summary>Vertex ecliptic longitude (decimal degrees).</summary>
    public required double Vertex { get; init; }

    /// <summary>
    /// True obliquity of the ecliptic (decimal degrees) the houses were cast with.
    /// </summary>
    /// <remarks>
    /// With <see cref="GeographicLatitude"/>, it tells day from night inside the polar circles, where the
    /// ecliptic's upper-meridian point can lie below the horizon (<c>ChartSect.IsDayChart</c>).
    /// <see cref="double.NaN"/> when the houses were not cast by the ephemeris service, as in a test fixture.
    /// </remarks>
    public double Obliquity { get; init; } = double.NaN;

    /// <summary>
    /// Geographic latitude (decimal degrees, north positive) the houses were cast for.
    /// </summary>
    /// <remarks>
    /// With <see cref="Obliquity"/>, it tells day from night inside the polar circles (<c>ChartSect.IsDayChart</c>).
    /// <see cref="double.NaN"/> when the houses were not cast by the ephemeris service, as in a test fixture.
    /// </remarks>
    public double GeographicLatitude { get; init; } = double.NaN;

    /// <summary>
    /// Descendant ecliptic longitude (decimal degrees).
    /// Computed as (Ascendant + 180) mod 360.
    /// </summary>
    public double Descendant => (Ascendant + 180.0) % 360.0;

    /// <summary>
    /// Imum Coeli (IC) ecliptic longitude (decimal degrees).
    /// Computed as (Midheaven + 180) mod 360.
    /// </summary>
    public double ImumCoeli => (Midheaven + 180.0) % 360.0;
}
