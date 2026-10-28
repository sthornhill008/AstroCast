using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates antiscia (reflection across the solstice axis) and contra-antiscia
/// (reflection across the equinoctial axis) for all chart positions, then scans
/// for connections between planet pairs within the classical 2° orb.
/// </summary>
/// <remarks>
/// <para>
/// The antiscion formula: <c>(540 − longitude) % 360</c>.
/// The contra-antiscion: <c>(antiscion + 180) % 360</c>.
/// </para>
/// <para>
/// A connection is detected when planet A's antiscion (or contra-antiscion)
/// point is within <see cref="MaxOrb"/> degrees of planet B's actual position.
/// Each connection is reported once (not duplicated for A→B and B→A).
/// </para>
/// </remarks>
public sealed class AntisciaService : IAntisciaService
{
    /// <summary>
    /// Maximum orb for antiscia connection detection.
    /// Classical sources use 1°–2°; 2° is the standard maximum.
    /// </summary>
    private const double MaxOrb = 2.0;

    /// <inheritdoc />
    public (IReadOnlyList<AntisciaPoint> Points, IReadOnlyList<AntisciaConnection> Connections)
        Calculate(IReadOnlyList<PlanetPosition> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        // 1. Compute antiscia/contra-antiscia for every position
        List<AntisciaPoint> points = [];
        foreach (PlanetPosition pos in positions)
        {
            double antisciaLon = CalculateAntiscion(pos.Longitude);
            double contraLon = (antisciaLon + 180.0) % 360.0;

            points.Add(new AntisciaPoint
            {
                PlanetName = pos.Name,
                Longitude = pos.Longitude,
                AntisciaLongitude = Math.Round(antisciaLon, 4),
                ContraAntisciaLongitude = Math.Round(contraLon, 4),
            });
        }

        // 2. Scan for connections between all unique planet pairs
        List<AntisciaConnection> connections = [];

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                AntisciaPoint a = points[i];
                AntisciaPoint b = points[j];

                // Check: is A's antiscion point near B's actual position?
                double antisciaOrb = AngularSeparation(a.AntisciaLongitude, b.Longitude);
                if (antisciaOrb <= MaxOrb)
                {
                    connections.Add(new AntisciaConnection
                    {
                        Planet1 = a.PlanetName,
                        Planet2 = b.PlanetName,
                        Type = "Antiscia",
                        Orb = Math.Round(antisciaOrb, 4),
                    });
                }

                // Check: is A's contra-antiscion near B's actual position?
                double contraOrb = AngularSeparation(a.ContraAntisciaLongitude, b.Longitude);
                if (contraOrb <= MaxOrb)
                {
                    connections.Add(new AntisciaConnection
                    {
                        Planet1 = a.PlanetName,
                        Planet2 = b.PlanetName,
                        Type = "ContraAntiscia",
                        Orb = Math.Round(contraOrb, 4),
                    });
                }
            }
        }

        // Sort connections by orb (tightest first)
        connections.Sort((x, y) => x.Orb.CompareTo(y.Orb));

        return (points, connections);
    }

    /// <summary>
    /// Calculates the antiscion of a longitude: reflection across the
    /// solstice axis (0° Cancer / 0° Capricorn).
    /// Formula: (540 − longitude) % 360.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in degrees.</param>
    /// <returns>Antiscion longitude in degrees (0–360).</returns>
    internal static double CalculateAntiscion(double longitude)
    {
        double normalized = ((longitude % 360.0) + 360.0) % 360.0;
        return (540.0 - normalized) % 360.0;
    }

    /// <summary>
    /// Shortest angular separation between two longitudes (0–180°).
    /// </summary>
    private static double AngularSeparation(double lon1, double lon2)
    {
        double diff = Math.Abs(lon1 - lon2);
        if (diff > 180.0)
            diff = 360.0 - diff;
        return diff;
    }
}
