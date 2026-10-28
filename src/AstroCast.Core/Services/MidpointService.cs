using Serilog;

using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates Ebertin cosmobiology midpoints and detects activations on the
/// 45° dial (8th harmonic). A midpoint activation occurs when a chart point
/// lies at 0°, 45°, 90°, 135°, or 180° from the midpoint axis, within orb.
/// </summary>
/// <remarks>
/// <para>
/// Orbs: ±1.5° for conjunction/opposition (0°/180°), ±1.0° for 45°/90°/135°.
/// </para>
/// <para>
/// No dependency on <see cref="IEphemerisService"/> — midpoints are purely
/// geometric calculations on longitudes already computed.
/// </para>
/// </remarks>
public sealed class MidpointService : IMidpointService
{
    /// <summary>Orb allowance for direct contact (0°) and opposition (180°).</summary>
    private const double DirectOrb = 1.5;

    /// <summary>Orb allowance for 45°, 90°, and 135° harmonics.</summary>
    private const double HarmonicOrb = 1.0;

    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="MidpointService"/>.
    /// </summary>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    public MidpointService(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger.ForContext<MidpointService>();
    }

    /// <inheritdoc />
    public IReadOnlyList<Midpoint> CalculateMidpoints(
        IReadOnlyList<(string Name, double Longitude)> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        List<Midpoint> midpoints = [];

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                double midLon = CalculateNearMidpoint(
                    points[i].Longitude, points[j].Longitude);

                midpoints.Add(new Midpoint
                {
                    Body1Name = points[i].Name,
                    Body1Longitude = points[i].Longitude,
                    Body2Name = points[j].Name,
                    Body2Longitude = points[j].Longitude,
                    Longitude = midLon,
                });
            }
        }

        _logger.Debug("Calculated {Count} midpoints from {PointCount} chart points",
            midpoints.Count, points.Count);

        return midpoints.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<MidpointActivation> CalculateActivations(
        IReadOnlyList<Midpoint> midpoints,
        IReadOnlyList<(string Name, double Longitude)> points)
    {
        ArgumentNullException.ThrowIfNull(midpoints);
        ArgumentNullException.ThrowIfNull(points);

        List<MidpointActivation> activations = [];

        foreach (Midpoint midpoint in midpoints)
        {
            foreach ((string pointName, double pointLongitude) in points)
            {
                // Skip if the point is one of the bodies forming this midpoint
                if (pointName == midpoint.Body1Name || pointName == midpoint.Body2Name)
                    continue;

                (double orb, int harmonicAngle) = Check45DegreeHarmonic(
                    pointLongitude, midpoint.Longitude);

                // Apply the correct orb limit for the harmonic
                double maxOrb = (harmonicAngle == 0 || harmonicAngle == 180)
                    ? DirectOrb
                    : HarmonicOrb;

                if (orb <= maxOrb)
                {
                    activations.Add(new MidpointActivation
                    {
                        Midpoint = midpoint,
                        ActivatorName = pointName,
                        ActivatorLongitude = pointLongitude,
                        HarmonicAngle = harmonicAngle,
                        Orb = orb,
                    });
                }
            }
        }

        activations.Sort((a, b) => a.Orb.CompareTo(b.Orb));

        _logger.Debug("Found {Count} midpoint activations from {MidpointCount} midpoints",
            activations.Count, midpoints.Count);

        return activations.AsReadOnly();
    }

    /// <inheritdoc />
    public (IReadOnlyList<Midpoint> Midpoints, IReadOnlyList<MidpointActivation> Activations)
        CalculateAll(IReadOnlyList<(string Name, double Longitude)> points)
    {
        IReadOnlyList<Midpoint> midpoints = CalculateMidpoints(points);
        IReadOnlyList<MidpointActivation> activations = CalculateActivations(midpoints, points);
        return (midpoints, activations);
    }

    /// <summary>
    /// Calculate the near midpoint between two ecliptic longitudes.
    /// Uses the shorter arc between the two points.
    /// </summary>
    /// <param name="lon1">First longitude in degrees (0–360).</param>
    /// <param name="lon2">Second longitude in degrees (0–360).</param>
    /// <returns>Near midpoint longitude in degrees (0–360).</returns>
    internal static double CalculateNearMidpoint(double lon1, double lon2)
    {
        double diff = Math.Abs(lon1 - lon2);
        double sum = lon1 + lon2;

        if (diff > 180.0)
            sum += 360.0;

        double mid = (sum / 2.0) % 360.0;

        if (mid < 0)
            mid += 360.0;

        return mid;
    }

    /// <summary>
    /// Check whether a point activates a midpoint on the 45° dial.
    /// Returns the orb and the nearest harmonic angle (0, 45, 90, 135, or 180).
    /// </summary>
    /// <param name="pointLongitude">Ecliptic longitude of the potential activator (degrees).</param>
    /// <param name="midpointLongitude">Ecliptic longitude of the midpoint (degrees).</param>
    /// <returns>
    /// A tuple of (orb, harmonicAngle) where orb is the distance from the nearest
    /// harmonic and harmonicAngle is 0, 45, 90, 135, or 180.
    /// </returns>
    internal static (double Orb, int HarmonicAngle) Check45DegreeHarmonic(
        double pointLongitude, double midpointLongitude)
    {
        double separation = Math.Abs(pointLongitude - midpointLongitude);

        if (separation > 180.0)
            separation = 360.0 - separation;

        // Determine which harmonic (0, 45, 90, 135, 180) is closest
        int nearestHarmonic = (int)Math.Round(separation / 45.0) * 45;

        if (nearestHarmonic > 180)
            nearestHarmonic = 180;

        double orb = Math.Abs(separation - nearestHarmonic);
        return (orb, nearestHarmonic);
    }

    /// <summary>
    /// Build a point list combining planet positions and chart angles (ASC, MC)
    /// into the tuple format expected by the midpoint calculation methods.
    /// </summary>
    /// <param name="positions">Calculated planet positions.</param>
    /// <param name="houseData">House data containing ASC and MC longitudes.</param>
    /// <returns>List of named chart points with longitudes.</returns>
    public static IReadOnlyList<(string Name, double Longitude)> BuildPointList(
        IReadOnlyList<PlanetPosition> positions, HouseData houseData)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(houseData);

        List<(string Name, double Longitude)> points = new(positions.Count + 2);

        foreach (PlanetPosition p in positions)
            points.Add((p.Name, p.Longitude));

        points.Add(("ASC", houseData.Ascendant));
        points.Add(("MC", houseData.Midheaven));

        return points.AsReadOnly();
    }
}
