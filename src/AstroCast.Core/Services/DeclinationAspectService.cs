using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Detects declination aspects (parallels and contraparallels) between
/// celestial bodies, and identifies declination clusters — groups of 3+
/// chart points whose absolute declinations fall within a span of 2° or less.
/// A parallel joins two bodies on the same side of the celestial equator whose
/// declinations differ by 1° or less (equivalent to conjunction). A contraparallel
/// joins two bodies on opposite sides whose declinations are equal and opposite
/// within 1° (equivalent to opposition). North is a declination of 0° or more and
/// south below 0°, so a body at exactly 0° counts as north, and each pair forms at
/// most one of the two.
/// </summary>
public sealed class DeclinationAspectService : IDeclinationAspectService
{
    /// <summary>
    /// Default orb for declination aspects in degrees.
    /// Traditional standard is ±1° for both parallels and contraparallels.
    /// </summary>
    internal const double DefaultOrb = 1.0;

    /// <summary>
    /// The widest span of a declination cluster, in degrees. A window starts at the
    /// absolute declination s of a point not yet in a cluster and holds those points
    /// from s to s + this, both ends included; the window with the most members is
    /// chosen first (see <see cref="IdentifyClusters"/>).
    /// </summary>
    internal const double ClusterWindowDegrees = 2.0;

    /// <summary>
    /// Minimum number of points required to form a declination cluster.
    /// </summary>
    internal const int MinimumClusterSize = 3;

    /// <inheritdoc />
    public IReadOnlyList<DeclinationAspect> Calculate(IReadOnlyList<PlanetPosition> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);

        List<DeclinationAspect> aspects = [];

        for (int i = 0; i < positions.Count; i++)
        {
            if (!positions[i].Declination.HasValue) continue;

            for (int j = i + 1; j < positions.Count; j++)
            {
                if (!positions[j].Declination.HasValue) continue;

                double dec1 = positions[i].Declination!.Value;
                double dec2 = positions[j].Declination!.Value;

                // One aspect at most, by side of the equator
                if (FindDeclinationAspect(dec1, dec2) is (bool isParallel, double orb))
                {
                    aspects.Add(new DeclinationAspect
                    {
                        Body1Name = positions[i].Name,
                        Body1Declination = dec1,
                        Body2Name = positions[j].Name,
                        Body2Declination = dec2,
                        IsParallel = isParallel,
                        Orb = orb,
                    });
                }
            }
        }

        // Sort by orb ascending (tightest first)
        aspects.Sort((a, b) => a.Orb.CompareTo(b.Orb));
        return aspects;
    }

    /// <summary>
    /// Finds cross-chart declination aspects (parallels and contraparallels)
    /// between two position lists from different charts. Each body in
    /// <paramref name="chartA"/> is compared to every body in
    /// <paramref name="chartB"/>, by the same side rule as <see cref="Calculate"/>:
    /// a parallel needs the two bodies on the same side of the celestial equator,
    /// a contraparallel on opposite sides (a declination of exactly 0° counts as
    /// north), so each pair forms at most one aspect. This is the shared computation
    /// used by synastry, solar return, lunar return, event, and electional exports.
    /// </summary>
    /// <param name="chartA">Positions from the first chart (e.g., Person A, return, event, candidate).</param>
    /// <param name="chartB">Positions from the second chart (e.g., Person B, natal).</param>
    /// <returns>
    /// Cross-chart declination aspects sorted by orb ascending (tightest first).
    /// Empty list if none found.
    /// </returns>
    public static IReadOnlyList<DeclinationAspect> FindCrossChartDeclinationAspects(
        IReadOnlyList<PlanetPosition> chartA,
        IReadOnlyList<PlanetPosition> chartB)
    {
        ArgumentNullException.ThrowIfNull(chartA);
        ArgumentNullException.ThrowIfNull(chartB);

        List<DeclinationAspect> aspects = [];

        foreach (PlanetPosition a in chartA)
        {
            if (!a.Declination.HasValue) continue;
            double decA = a.Declination.Value;

            foreach (PlanetPosition b in chartB)
            {
                if (!b.Declination.HasValue) continue;
                double decB = b.Declination.Value;

                // One aspect at most, by side of the equator
                if (FindDeclinationAspect(decA, decB) is (bool isParallel, double orb))
                {
                    aspects.Add(new DeclinationAspect
                    {
                        Body1Name = a.Name,
                        Body1Declination = decA,
                        Body2Name = b.Name,
                        Body2Declination = decB,
                        IsParallel = isParallel,
                        Orb = orb,
                    });
                }
            }
        }

        // Sort by orb ascending (tightest first)
        aspects.Sort((a, b) => a.Orb.CompareTo(b.Orb));
        return aspects;
    }

    /// <summary>
    /// The one declination aspect two declinations can form, by side of the celestial
    /// equator, within a chart, across two charts and between a transiting body and a
    /// natal one (<see cref="TransitService"/>, branch D). North is a declination of 0° or
    /// more and south below 0°, so a body at
    /// exactly 0° counts as north. A parallel needs both on the same side and
    /// |<paramref name="declination1"/> − <paramref name="declination2"/>| ≤ <see cref="DefaultOrb"/>,
    /// which is its orb; a contraparallel needs them on opposite sides and
    /// |<paramref name="declination1"/> + <paramref name="declination2"/>| ≤ <see cref="DefaultOrb"/>,
    /// which is its orb.
    /// </summary>
    /// <param name="declination1">The first body's declination, in degrees.</param>
    /// <param name="declination2">The second body's declination, in degrees.</param>
    /// <returns>
    /// Whether the aspect is a parallel, and its orb in degrees; or <see langword="null"/>
    /// when the pair forms neither.
    /// </returns>
    internal static (bool IsParallel, double Orb)? FindDeclinationAspect(double declination1, double declination2)
    {
        bool sameSide = (declination1 >= 0.0) == (declination2 >= 0.0);
        double orb = sameSide
            ? Math.Abs(declination1 - declination2)
            : Math.Abs(declination1 + declination2);

        if (orb > DefaultOrb)
            return null;

        return (sameSide, orb);
    }

    /// <summary>
    /// Identifies declination clusters — groups of 3 or more chart points
    /// whose absolute declinations fall within a span of 2° or less. The pool is
    /// every point with a declination, at its absolute declination. A window starts
    /// at the absolute declination s of a point in the pool and holds the pool's
    /// points from s to s + <see cref="ClusterWindowDegrees"/>, both ends included;
    /// its span is its highest minus its lowest absolute declination. The window
    /// with the most members is chosen, on a tie the narrower span, then the lower
    /// start. If it holds <see cref="MinimumClusterSize"/> or more, its points form
    /// a cluster and leave the pool, and the choice is repeated until no such window
    /// is left.
    /// </summary>
    /// <param name="positions">All planetary positions with declination values.</param>
    /// <param name="declinationAspects">
    /// The pre-computed declination aspects, used to count intra-cluster aspects.
    /// </param>
    /// <returns>
    /// List of clusters in the order chosen, which is by member count, largest first.
    /// Empty list if no cluster of 3+ points exists.
    /// </returns>
    public static IReadOnlyList<DeclinationCluster> IdentifyClusters(
        IReadOnlyList<PlanetPosition> positions,
        IReadOnlyList<DeclinationAspect> declinationAspects)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(declinationAspects);

        // 1. Collect all chart points with their absolute declinations
        List<(string Name, double SignedDec, double AbsDec)> points = [];
        foreach (PlanetPosition p in positions)
        {
            if (p.Declination.HasValue)
                points.Add((p.Name, p.Declination.Value, Math.Abs(p.Declination.Value)));
        }

        if (points.Count < MinimumClusterSize)
            return [];

        // 2. Sort by absolute declination ascending
        points.Sort((a, b) => a.AbsDec.CompareTo(b.AbsDec));

        // 3. Choose the window with the most members: from each point left in the pool,
        //    the pool's points from its absolute declination to 2° above it, both ends
        //    included; on a tie the narrower span wins, then the lower start
        // 4. A window of 3+ points becomes a cluster and its points leave the pool;
        //    repeat until none is left, keeping the clusters in the order chosen
        List<int> pool = [.. Enumerable.Range(0, points.Count)];
        List<List<int>> chosen = [];
        while (true)
        {
            List<int> best = [];
            double bestSpan = double.MaxValue;
            for (int i = 0; i < pool.Count; i++)
            {
                double windowStart = points[pool[i]].AbsDec;
                double windowEnd = windowStart + ClusterWindowDegrees;

                List<int> window = [];
                for (int j = i; j < pool.Count && points[pool[j]].AbsDec <= windowEnd; j++)
                {
                    window.Add(pool[j]);
                }

                double span = points[window[^1]].AbsDec - windowStart;
                if (window.Count > best.Count || (window.Count == best.Count && span < bestSpan))
                {
                    best = window;
                    bestSpan = span;
                }
            }

            if (best.Count < MinimumClusterSize)
                break;

            chosen.Add(best);
            pool.RemoveAll(best.Contains);
        }

        // 5. Build final cluster objects
        List<DeclinationCluster> clusters = [];
        foreach (List<int> memberIndices in chosen)
        {
            List<DeclinationClusterMember> members = [];
            double minAbs = double.MaxValue;
            double maxAbs = double.MinValue;

            foreach (int idx in memberIndices.OrderBy(i => points[i].AbsDec))
            {
                (string name, double signedDec, double absDec) = points[idx];
                members.Add(new DeclinationClusterMember
                {
                    Name = name,
                    Declination = signedDec,
                });

                if (absDec < minAbs) minAbs = absDec;
                if (absDec > maxAbs) maxAbs = absDec;
            }

            // Count intra-cluster aspects
            HashSet<string> memberNames = new(members.Select(m => m.Name), StringComparer.Ordinal);
            int aspectCount = CountIntraClusterAspects(memberNames, declinationAspects);

            clusters.Add(new DeclinationCluster
            {
                ZoneMinAbsDeclination = minAbs,
                ZoneMaxAbsDeclination = maxAbs,
                Members = members,
                IntraClusterAspectCount = aspectCount,
            });
        }

        return clusters;
    }

    /// <summary>
    /// Counts the number of declination aspects that bind cluster members to each other.
    /// </summary>
    /// <param name="memberNames">Names of the cluster member chart points.</param>
    /// <param name="aspects">All declination aspects.</param>
    /// <returns>Count of aspects where both bodies are cluster members.</returns>
    private static int CountIntraClusterAspects(
        HashSet<string> memberNames, IReadOnlyList<DeclinationAspect> aspects)
    {
        int count = 0;
        foreach (DeclinationAspect aspect in aspects)
        {
            if (memberNames.Contains(aspect.Body1Name) && memberNames.Contains(aspect.Body2Name))
                count++;
        }

        return count;
    }
}
