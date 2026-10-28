using Serilog;

using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Detects fixed star conjunctions with chart points using the Swiss Ephemeris.
/// For each star in the 55-star catalog, calculates its ecliptic longitude for the
/// chart date, then checks all chart points (planets, angles, nodes, Chiron) for
/// conjunctions within the 1 degree 00 arc-minute orb.
/// </summary>
/// <remarks>
/// Depends on <see cref="IEphemerisService.CalculateFixedStarLongitude"/> for the
/// underlying Swiss Ephemeris <c>swe_fixstar2_ut()</c> calls, which ensures proper
/// thread-safety through the shared ephemeris lock.
/// </remarks>
public sealed class FixedStarService : IFixedStarService
{
    private readonly IEphemerisService _ephemeris;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="FixedStarService"/>.
    /// </summary>
    /// <param name="ephemeris">
    /// Ephemeris service for calculating fixed star positions via Swiss Ephemeris.
    /// </param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ephemeris"/> or <paramref name="logger"/> is null.
    /// </exception>
    public FixedStarService(IEphemerisService ephemeris, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _logger = logger.ForContext<FixedStarService>();
    }

    /// <inheritdoc />
    public IReadOnlyList<FixedStarConjunction> FindConjunctions(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses,
        double julianDayUt)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(houses);

        // Build the list of chart points to check (16 total)
        List<(string Name, double Longitude)> chartPoints = BuildChartPoints(positions, houses);
        List<FixedStarConjunction> conjunctions = [];

        foreach (FixedStarEntry star in FixedStarCatalog.Stars)
        {
            double starLongitude;
            try
            {
                starLongitude = _ephemeris.CalculateFixedStarLongitude(
                    star.SwissEphName, julianDayUt);
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    "Skipping star '{StarName}' (SwissEph: '{SwissEphName}'): {Error}",
                    star.DisplayName, star.SwissEphName, ex.Message);
                continue;
            }

            foreach ((string pointName, double pointLongitude) in chartPoints)
            {
                double orb = CalculateAngularDistance(starLongitude, pointLongitude);

                if (orb <= FixedStarCatalog.DefaultOrb)
                {
                    conjunctions.Add(new FixedStarConjunction
                    {
                        StarName = star.DisplayName,
                        Constellation = star.Constellation,
                        Magnitude = star.Magnitude,
                        StarLongitude = starLongitude,
                        ChartPointName = pointName,
                        ChartPointLongitude = pointLongitude,
                        Orb = orb,
                        Nature = star.Nature,
                        Keywords = star.Keywords,
                        IsRoyalStar = star.IsRoyalStar,
                        IsBehenianStar = star.IsBehenianStar,
                        ChartPointSortOrder = FixedStarCatalog.GetChartPointSortIndex(pointName),
                    });
                }
            }
        }

        // Sort by chart point priority, then by orb (tightest first)
        conjunctions.Sort((a, b) =>
        {
            int cmp = a.ChartPointSortOrder.CompareTo(b.ChartPointSortOrder);
            return cmp != 0 ? cmp : a.Orb.CompareTo(b.Orb);
        });

        _logger.Information(
            "Found {Count} fixed star conjunction(s) at JD {JulianDay:F6}",
            conjunctions.Count, julianDayUt);

        return conjunctions.AsReadOnly();
    }

    /// <summary>
    /// Builds the list of chart points to check for fixed star conjunctions.
    /// Includes the 4 angles, 10 planets, True Node, and Chiron (16 points total).
    /// </summary>
    /// <param name="positions">All planetary positions.</param>
    /// <param name="houses">House data with angles.</param>
    /// <returns>List of (name, longitude) tuples for all chart points.</returns>
    private static List<(string Name, double Longitude)> BuildChartPoints(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses)
    {
        List<(string, double)> points = new(16);

        // Angles first (highest priority in output)
        points.Add(("ASC", houses.Ascendant));
        points.Add(("MC", houses.Midheaven));
        points.Add(("DSC", houses.Descendant));
        points.Add(("IC", houses.ImumCoeli));

        // Planets, True Node, and Chiron
        foreach (PlanetPosition pos in positions)
        {
            points.Add((pos.Name, pos.Longitude));
        }

        return points;
    }

    /// <summary>
    /// Calculates the shortest angular distance between two ecliptic longitudes.
    /// Handles the 360 degree/0 degree wraparound correctly.
    /// </summary>
    /// <param name="longitude1">First ecliptic longitude in degrees.</param>
    /// <param name="longitude2">Second ecliptic longitude in degrees.</param>
    /// <returns>Angular distance in degrees (always positive, 0-180).</returns>
    private static double CalculateAngularDistance(double longitude1, double longitude2)
    {
        double diff = Math.Abs(longitude1 - longitude2);
        if (diff > 180.0)
        {
            diff = 360.0 - diff;
        }
        return diff;
    }
}
