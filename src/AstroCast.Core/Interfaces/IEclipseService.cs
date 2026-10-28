using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service for finding solar and lunar eclipses with definitive type classification
/// and sub-second precision timing using Swiss Ephemeris eclipse functions.
/// </summary>
/// <remarks>
/// Uses <c>swe_sol_eclipse_when_glob</c> for solar eclipses and
/// <c>swe_lun_eclipse_when</c> for lunar eclipses. Provides exact eclipse type
/// (Total, Annular, Hybrid, Partial, Penumbral), precise phase timing, and
/// positional data. Does not cast charts — provides timing only.
/// </remarks>
public interface IEclipseService
{
    /// <summary>
    /// Find all solar and lunar eclipses within a date range.
    /// </summary>
    /// <param name="startYear">Start year.</param>
    /// <param name="endYear">End year.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="EclipseSeries"/> with all eclipses found chronologically.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="startYear"/> is greater than <paramref name="endYear"/>.
    /// </exception>
    EclipseSeries FindEclipses(
        int startYear, int endYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find only solar eclipses within a date range.
    /// </summary>
    /// <param name="startYear">Start year.</param>
    /// <param name="endYear">End year.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="EclipseSeries"/> containing only solar eclipses.</returns>
    EclipseSeries FindSolarEclipses(
        int startYear, int endYear,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find only lunar eclipses within a date range.
    /// </summary>
    /// <param name="startYear">Start year.</param>
    /// <param name="endYear">End year.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="EclipseSeries"/> containing only lunar eclipses.</returns>
    EclipseSeries FindLunarEclipses(
        int startYear, int endYear,
        CancellationToken cancellationToken = default);
}
