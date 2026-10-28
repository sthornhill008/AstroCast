using System.Globalization;
using System.Runtime.CompilerServices;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services.MinorPoints;

/// <summary>
/// The Tier B lists of the minor points (branch Asteroids, 1.6.0): ast_truth.py, an independent pyswisseph script
/// over the shipped ephemeris files that holds the plan's rules R1, R4 and R5 as the plan states them, its fifteen
/// files copied byte for byte into <c>TierB/</c> beside this file. One reader and one comparison for every test
/// that holds a chart to them: <see cref="MinorPointTierBTests"/>, <see cref="MinorPointTierATests"/> and the
/// relocation case in <c>ChartEngineTests</c>.
/// </summary>
/// <remarks>
/// A points list is tab-separated with one header row, <c>body lon lat speed ra dec house dir oob</c> (dir D or R;
/// oob "OOB" or empty), the five in R1's order; an aspects list <c>point aspect target orb apsp</c> (the aspect's
/// name; the target a body's name, ASC or MC; apsp Ap or Sp), sorted by orb; <c>lilith_meeus.tsv</c> is Tier A,
/// <c>fixture lon lat</c>, Lilith by Meeus ch. 47 with the reduction to the ecliptic. Every number is printed to
/// nine decimals, the lists' own precision. Branch AsteroidsExport (1.6.0) added ast_truth_export.py's three lists,
/// J2000 NYC with the Secondary fixture: <c>synastry_j2000_secondary.tsv</c> (<c>body1 aspect body2 orb note</c>, the
/// names prefixed "A " and "B ", the note "exact" or empty), <c>composite_j2000_secondary_points.tsv</c>
/// (<c>body lon lat speed house dir</c>) and <c>composite_j2000_secondary_aspects.tsv</c> (<c>point aspect target orb</c>).
/// Branch AsteroidsTiming (1.6.0) added ast_truth_timing.py's two lists, J2000 NYC's natal five as timing targets (R7):
/// <c>timing_transits.tsv</c> (<c>scan body aspect target point entry exit orb_start apsp passes stations</c>; the passes
/// and stations ";"-joined, a station <c>Kind@time</c>; the transit golden's scan and the timeline's, UT, entry, exit
/// and stations to the minute and passes to the second) and <c>timing_prog.tsv</c> (<c>report kind point aspect target
/// orb direction instant</c>; the solar arc golden's 2026 and the transit golden's 2026-04-12, an aspect row's orb to
/// six decimals and a hit row's instant to the minute).
/// </remarks>
internal static class MinorPointTierB
{
    /// <summary>Tolerance on a longitude, latitude, right ascension and declination, in degrees, and on a speed, in degrees a day.</summary>
    public const double Tolerance = 1e-6;

    /// <summary>Tolerance on an orb, in degrees.</summary>
    public const double OrbTolerance = 1e-6;

    /// <summary>The five points in the order every list holds them (R1).</summary>
    public static readonly string[] Names = ["Ceres", "Pallas", "Juno", "Vesta", "Lilith"];

    /// <summary>One row of a points list.</summary>
    /// <param name="Body">The point's name.</param>
    /// <param name="Longitude">The ecliptic longitude, in degrees.</param>
    /// <param name="Latitude">The ecliptic latitude, in degrees.</param>
    /// <param name="Speed">The speed in longitude, in degrees a day.</param>
    /// <param name="RightAscension">The right ascension, in degrees.</param>
    /// <param name="Declination">The declination, in degrees.</param>
    /// <param name="House">The house, by longitude between the cusps.</param>
    /// <param name="Retrograde">Whether the speed is negative.</param>
    /// <param name="OutOfBounds">Whether the declination's magnitude exceeds the true obliquity.</param>
    public sealed record PointRow(
        string Body, double Longitude, double Latitude, double Speed, double RightAscension, double Declination,
        int House, bool Retrograde, bool OutOfBounds);

    /// <summary>One row of an aspects list.</summary>
    /// <param name="Point">The minor point's name.</param>
    /// <param name="Aspect">The aspect.</param>
    /// <param name="Target">The target: a body's name, ASC or MC.</param>
    /// <param name="Orb">The orb, in degrees.</param>
    /// <param name="Applying">Whether the aspect is applying.</param>
    public sealed record AspectRow(string Point, AspectType Aspect, string Target, double Orb, bool Applying);

    /// <summary>One row of the Tier A list, Lilith by Meeus.</summary>
    /// <param name="Fixture">The fixture's stem.</param>
    /// <param name="Longitude">The ecliptic longitude, in degrees.</param>
    /// <param name="Latitude">The ecliptic latitude, in degrees.</param>
    public sealed record LilithRow(string Fixture, double Longitude, double Latitude);

    /// <summary>One row of the synastry list, <c>synastry_j2000_secondary.tsv</c> (branch AsteroidsExport, 1.6.0).</summary>
    /// <param name="Body1">The minor point, prefixed by its chart ("A Ceres", "B Vesta").</param>
    /// <param name="Aspect">The aspect.</param>
    /// <param name="Body2">The target, prefixed by the other chart ("B Uranus", "A MC").</param>
    /// <param name="Orb">The orb, in degrees.</param>
    /// <param name="Note">"exact" below ten minutes of orb, else empty.</param>
    public sealed record SynastryRow(string Body1, AspectType Aspect, string Body2, double Orb, string Note);

    /// <summary>One row of the composite points list, <c>composite_j2000_secondary_points.tsv</c> (branch AsteroidsExport, 1.6.0).</summary>
    /// <param name="Body">The point's name.</param>
    /// <param name="Longitude">The short-arc midpoint of the two charts' longitudes, in degrees.</param>
    /// <param name="Latitude">The mean of the two latitudes, in degrees.</param>
    /// <param name="Speed">The mean of the two speeds in longitude, in degrees a day.</param>
    /// <param name="House">The house, by longitude between the composite cusps.</param>
    /// <param name="Retrograde">Whether the mean speed is negative.</param>
    public sealed record CompositePointRow(string Body, double Longitude, double Latitude, double Speed, int House, bool Retrograde);

    /// <summary>One row of the composite aspects list, <c>composite_j2000_secondary_aspects.tsv</c> (branch AsteroidsExport, 1.6.0).</summary>
    /// <param name="Point">The minor point's name.</param>
    /// <param name="Aspect">The aspect.</param>
    /// <param name="Target">The target: a composite body's name, ASC or MC.</param>
    /// <param name="Orb">The orb, in degrees.</param>
    public sealed record CompositeAspectRow(string Point, AspectType Aspect, string Target, double Orb);

    /// <summary>A station in a transit's lifecycle, as the timing list holds it (branch AsteroidsTiming, 1.6.0).</summary>
    /// <param name="Kind">"Retrograde" or "Direct".</param>
    /// <param name="InstantUtc">The station's instant, UT, to the minute.</param>
    public sealed record TimingStation(string Kind, DateTime InstantUtc);

    /// <summary>
    /// One row of the timing transits list, <c>timing_transits.tsv</c> (branch AsteroidsTiming, 1.6.0): one passage of a
    /// transiting body over one aspect point of one of the natal five, in one of the two scans.
    /// </summary>
    /// <param name="Scan">The scan: <c>transit_20260412_18</c> or <c>timeline_202604</c>.</param>
    /// <param name="Body">The transiting body's symbol: JU, SA, UR, NE, PL, CH, NN or MA.</param>
    /// <param name="Aspect">The aspect's abbreviation: cnj, opp, tri, sqr or sxt.</param>
    /// <param name="Target">The natal point's name: Ceres, Pallas, Juno, Vesta or Lilith.</param>
    /// <param name="Point">The aspect point's longitude, in degrees.</param>
    /// <param name="EntryUtc">The entry instant, UT, to the minute.</param>
    /// <param name="ExitUtc">The exit instant, UT, to the minute.</param>
    /// <param name="OrbAtStart">The orb at the scan's start, in degrees, to its own point.</param>
    /// <param name="Applying">Whether the orb is closing at the scan's start.</param>
    /// <param name="Passes">The exact passes' instants, UT, to the second, in time order.</param>
    /// <param name="Stations">The stations between entry and exit, in time order; the list gives the node's too.</param>
    public sealed record TimingTransitRow(
        string Scan, string Body, string Aspect, string Target, double Point, DateTime EntryUtc, DateTime ExitUtc,
        double OrbAtStart, bool Applying, IReadOnlyList<DateTime> Passes, IReadOnlyList<TimingStation> Stations);

    /// <summary>
    /// One row of the timing progressions list, <c>timing_prog.tsv</c> (branch AsteroidsTiming, 1.6.0): an aspect at a
    /// report's start or a dated hit, by kind.
    /// </summary>
    /// <param name="Report">The report: <c>solararc_2026</c> (2026, at 2026-01-01) or <c>transit_20260412_18</c> (at 2026-04-12).</param>
    /// <param name="Kind">"P-to-natal", "P hit", "SA-to-natal" or "SA hit".</param>
    /// <param name="Point">The progressed or directed point's name: a body, ASC or MC.</param>
    /// <param name="Aspect">The aspect's abbreviation: cnj, opp, tri, sqr, sxt, ssq or ses.</param>
    /// <param name="Target">The natal point's name: Ceres, Pallas, Juno, Vesta or Lilith.</param>
    /// <param name="Orb">The orb at the report's start, in degrees; null for a hit.</param>
    /// <param name="Direction">"Applying" or "Separating"; null for a hit.</param>
    /// <param name="InstantUtc">The hit's instant, UT, to the minute; null for an aspect at the start.</param>
    public sealed record TimingProgressionRow(
        string Report, string Kind, string Point, string Aspect, string Target, double? Orb, string? Direction, DateTime? InstantUtc);

    /// <summary>The input of a fixture, by its lists' stem, with the switch on.</summary>
    /// <param name="fixture">j2000nyc, secondary, hartwell, m35, n1 or tromso.</param>
    /// <returns>The input.</returns>
    public static ChartInput Input(string fixture) => fixture switch
    {
        "j2000nyc" => GoldenFixtures.J2000NycInput(includeMinorPoints: true),
        "secondary" => GoldenFixtures.SecondaryNatalInput(includeMinorPoints: true),
        "hartwell" => GoldenFixtures.HartwellInput(includeMinorPoints: true),
        "m35" => GoldenFixtures.M35Input(includeMinorPoints: true),
        "n1" => GoldenFixtures.N1Input(includeMinorPoints: true),
        "tromso" => GoldenFixtures.TromsoInput(includeMinorPoints: true),
        _ => throw new ArgumentOutOfRangeException(nameof(fixture), fixture, "Unknown fixture."),
    };

    /// <summary>Casts a fixture chart with the switch on, by its lists' stem.</summary>
    /// <param name="ephemeris">The shared ephemeris fixture.</param>
    /// <param name="fixture">j2000nyc, secondary, hartwell, m35, n1 or tromso.</param>
    /// <returns>The chart.</returns>
    public static ChartResult Cast(EphemerisFixture ephemeris, string fixture) =>
        GoldenFixtures.CreateChartEngine(ephemeris).Calculate(Input(fixture));

    /// <summary>Reads a points list.</summary>
    /// <param name="fixture">The list's stem; the file is <c>{fixture}_points.tsv</c>.</param>
    /// <returns>The rows, in the list's order.</returns>
    public static List<PointRow> LoadPoints(string fixture)
    {
        List<PointRow> rows = [];
        foreach (string[] cells in Rows(fixture + "_points.tsv", 9))
        {
            cells[7].Should().BeOneOf(new[] { "D", "R" }, "the dir column is D or R");
            cells[8].Should().BeOneOf(new[] { "", "OOB" }, "the oob column is OOB or empty");
            rows.Add(new PointRow(
                cells[0],
                Parse(cells[1]), Parse(cells[2]), Parse(cells[3]), Parse(cells[4]), Parse(cells[5]),
                int.Parse(cells[6], CultureInfo.InvariantCulture),
                cells[7] == "R",
                cells[8] == "OOB"));
        }

        return rows;
    }

    /// <summary>Reads an aspects list.</summary>
    /// <param name="fixture">The list's stem; the file is <c>{fixture}_aspects.tsv</c>.</param>
    /// <returns>The rows, in the list's order.</returns>
    public static List<AspectRow> LoadAspects(string fixture)
    {
        List<AspectRow> rows = [];
        foreach (string[] cells in Rows(fixture + "_aspects.tsv", 5))
        {
            cells[4].Should().BeOneOf(new[] { "Ap", "Sp" }, "the apsp column is Ap or Sp");
            rows.Add(new AspectRow(
                cells[0], Enum.Parse<AspectType>(cells[1]), cells[2], Parse(cells[3]), cells[4] == "Ap"));
        }

        return rows;
    }

    /// <summary>Reads a fixture's row of the Tier A list, <c>lilith_meeus.tsv</c>.</summary>
    /// <param name="fixture">The fixture's stem.</param>
    /// <returns>The row.</returns>
    public static LilithRow LoadLilith(string fixture)
    {
        foreach (string[] cells in Rows("lilith_meeus.tsv", 3))
        {
            if (cells[0] == fixture)
            {
                return new LilithRow(cells[0], Parse(cells[1]), Parse(cells[2]));
            }
        }

        throw new InvalidOperationException($"The Tier A list has no row for '{fixture}'.");
    }

    /// <summary>Reads the synastry list, <c>synastry_j2000_secondary.tsv</c>: J2000 NYC's five to the Secondary fixture's targets and the reverse, sorted by orb.</summary>
    /// <returns>The rows, in the list's order.</returns>
    public static List<SynastryRow> LoadSynastry()
    {
        List<SynastryRow> rows = [];
        foreach (string[] cells in Rows("synastry_j2000_secondary.tsv", 5))
        {
            cells[4].Should().BeOneOf(new[] { "", "exact" }, "the note column is exact or empty");
            rows.Add(new SynastryRow(cells[0], Enum.Parse<AspectType>(cells[1]), cells[2], Parse(cells[3]), cells[4]));
        }

        return rows;
    }

    /// <summary>Reads the composite points list, <c>composite_j2000_secondary_points.tsv</c>: the five composite midpoints in R1's order.</summary>
    /// <returns>The rows, in the list's order.</returns>
    public static List<CompositePointRow> LoadCompositePoints()
    {
        List<CompositePointRow> rows = [];
        foreach (string[] cells in Rows("composite_j2000_secondary_points.tsv", 6))
        {
            cells[5].Should().BeOneOf(new[] { "D", "R" }, "the dir column is D or R");
            rows.Add(new CompositePointRow(
                cells[0], Parse(cells[1]), Parse(cells[2]), Parse(cells[3]),
                int.Parse(cells[4], CultureInfo.InvariantCulture), cells[5] == "R"));
        }

        return rows;
    }

    /// <summary>Reads the composite aspects list, <c>composite_j2000_secondary_aspects.tsv</c>: the composite five's aspects at 2°, sorted by orb.</summary>
    /// <returns>The rows, in the list's order.</returns>
    public static List<CompositeAspectRow> LoadCompositeAspects()
    {
        List<CompositeAspectRow> rows = [];
        foreach (string[] cells in Rows("composite_j2000_secondary_aspects.tsv", 4))
        {
            rows.Add(new CompositeAspectRow(cells[0], Enum.Parse<AspectType>(cells[1]), cells[2], Parse(cells[3])));
        }

        return rows;
    }

    /// <summary>
    /// Reads the rows of one scan of the timing transits list, <c>timing_transits.tsv</c> (branch AsteroidsTiming, 1.6.0),
    /// in the list's order: every transiting body's passages over the five, the Mars rows the report's gates drop included.
    /// </summary>
    /// <param name="scan">The scan: <c>transit_20260412_18</c> or <c>timeline_202604</c>.</param>
    /// <returns>The scan's rows.</returns>
    public static List<TimingTransitRow> LoadTimingTransits(string scan)
    {
        List<TimingTransitRow> rows = [];
        foreach (string[] cells in Rows("timing_transits.tsv", 11))
        {
            if (cells[0] != scan)
            {
                continue;
            }

            cells[8].Should().BeOneOf(new[] { "applying", "separating" }, "the apsp column is applying or separating");
            List<DateTime> passes = cells[9].Length == 0
                ? []
                : cells[9].Split(';').Select(p => ParseUtc(p, "yyyy-MM-dd HH:mm:ss")).ToList();
            List<TimingStation> stations = [];
            if (cells[10].Length > 0)
            {
                foreach (string station in cells[10].Split(';'))
                {
                    string[] parts = station.Split('@');
                    parts.Should().HaveCount(2, $"of the station '{station}'");
                    parts[0].Should().BeOneOf(new[] { "Retrograde", "Direct" }, $"of the station '{station}'");
                    stations.Add(new TimingStation(parts[0], ParseUtc(parts[1], "yyyy-MM-dd HH:mm")));
                }
            }

            rows.Add(new TimingTransitRow(
                cells[0], cells[1], cells[2], cells[3], Parse(cells[4]),
                ParseUtc(cells[5], "yyyy-MM-dd HH:mm"), ParseUtc(cells[6], "yyyy-MM-dd HH:mm"),
                Parse(cells[7]), cells[8] == "applying", passes, stations));
        }

        rows.Should().NotBeEmpty($"the timing transits list holds the scan '{scan}'");
        return rows;
    }

    /// <summary>
    /// Reads the rows of one report and kind of the timing progressions list, <c>timing_prog.tsv</c> (branch
    /// AsteroidsTiming, 1.6.0), in the list's order: the aspects at the report's start sorted by orb, or the hits in
    /// time order.
    /// </summary>
    /// <param name="report">The report: <c>solararc_2026</c> or <c>transit_20260412_18</c>.</param>
    /// <param name="kind">"P-to-natal", "P hit", "SA-to-natal" or "SA hit".</param>
    /// <returns>The rows, none when the report has no row of the kind (the transit golden's hits).</returns>
    public static List<TimingProgressionRow> LoadTimingProgressions(string report, string kind)
    {
        List<TimingProgressionRow> rows = [];
        foreach (string[] cells in Rows("timing_prog.tsv", 8))
        {
            if (cells[0] != report || cells[1] != kind)
            {
                continue;
            }

            bool isHit = kind.EndsWith(" hit", StringComparison.Ordinal);
            if (isHit)
            {
                cells[5].Should().BeEmpty("a hit row has no orb");
                cells[6].Should().BeEmpty("a hit row has no direction");
                rows.Add(new TimingProgressionRow(
                    cells[0], cells[1], cells[2], cells[3], cells[4], null, null, ParseUtc(cells[7], "yyyy-MM-dd HH:mm")));
            }
            else
            {
                cells[6].Should().BeOneOf(new[] { "Applying", "Separating" }, "the direction column is Applying or Separating");
                cells[7].Should().BeEmpty("an aspect row has no instant");
                rows.Add(new TimingProgressionRow(
                    cells[0], cells[1], cells[2], cells[3], cells[4], Parse(cells[5]), cells[6], null));
            }
        }

        return rows;
    }

    /// <summary>
    /// Holds a chart's minor points to a points list: the five names in R1's order, every longitude, latitude, right
    /// ascension and declination within <see cref="Tolerance"/>, the speed within it, the house equal, the direction
    /// equal and out of bounds, read against the chart's obliquity as the export reads it, equal; and the chart's
    /// positions still the twelve.
    /// </summary>
    /// <param name="chart">The chart, cast with the switch on.</param>
    /// <param name="rows">The list.</param>
    /// <returns>The largest deviation in position (longitude, latitude, right ascension or declination) and in speed.</returns>
    public static (double Position, double Speed) AssertPoints(ChartResult chart, IReadOnlyList<PointRow> rows)
    {
        rows.Select(r => r.Body).Should().Equal(Names, "the list holds the five in R1's order");
        chart.Positions.Should().HaveCount(12, "the five never join the positions (R3)");
        chart.MinorPoints.Select(p => p.Name).Should().Equal(Names, "the five are named in R1's order");

        double worstPosition = 0.0;
        double worstSpeed = 0.0;
        for (int i = 0; i < rows.Count; i++)
        {
            PointRow row = rows[i];
            PlanetPosition point = chart.MinorPoints[i];
            string because = $"of {row.Body}";

            point.RightAscension.Should().NotBeNull(because);
            point.Declination.Should().NotBeNull(because);
            if (point.RightAscension is double rightAscension && point.Declination is double declination)
            {
                worstPosition = Math.Max(worstPosition, WrappedDifference(rightAscension, row.RightAscension));
                worstPosition = Math.Max(worstPosition, Math.Abs(declination - row.Declination));
                WrappedDifference(rightAscension, row.RightAscension).Should().BeLessThanOrEqualTo(Tolerance, because);
                declination.Should().BeApproximately(row.Declination, Tolerance, because);
            }

            worstPosition = Math.Max(worstPosition, WrappedDifference(point.Longitude, row.Longitude));
            worstPosition = Math.Max(worstPosition, Math.Abs(point.Latitude - row.Latitude));
            worstSpeed = Math.Max(worstSpeed, Math.Abs(point.SpeedInLongitude - row.Speed));
            WrappedDifference(point.Longitude, row.Longitude).Should().BeLessThanOrEqualTo(Tolerance, because);
            point.Latitude.Should().BeApproximately(row.Latitude, Tolerance, because);
            point.SpeedInLongitude.Should().BeApproximately(row.Speed, Tolerance, because);

            point.House.Should().Be(row.House, because);
            point.IsRetrograde.Should().Be(row.Retrograde, because);
            PositionFormatter.IsOutOfBounds(point, chart.Obliquity).Should().Be(row.OutOfBounds, because);
        }

        return (worstPosition, worstSpeed);
    }

    /// <summary>
    /// Holds a chart's minor-point aspects to an aspects list, row for row in the list's order: point, aspect,
    /// target and applying or separating equal, the orb within <see cref="OrbTolerance"/>.
    /// </summary>
    /// <param name="chart">The chart, cast with the switch on.</param>
    /// <param name="rows">The list.</param>
    /// <returns>The largest deviation in orb.</returns>
    public static double AssertAspects(ChartResult chart, IReadOnlyList<AspectRow> rows)
    {
        chart.MinorPointAspects.Should().HaveCount(rows.Count, "the list holds every aspect at 2° (R5)");

        double worstOrb = 0.0;
        for (int i = 0; i < rows.Count; i++)
        {
            AspectRow row = rows[i];
            AspectResult aspect = chart.MinorPointAspects[i];
            string because = $"of row {i + 1}, {row.Point} {row.Aspect} {row.Target}";

            aspect.Body1Name.Should().Be(row.Point, because);
            aspect.Aspect.Type.Should().Be(row.Aspect, because);
            aspect.Body2Name.Should().Be(row.Target, because);
            aspect.IsApplying.Should().Be(row.Applying, because);
            worstOrb = Math.Max(worstOrb, Math.Abs(aspect.Orb - row.Orb));
            aspect.Orb.Should().BeApproximately(row.Orb, OrbTolerance, because);
        }

        return worstOrb;
    }

    /// <summary>The difference of two angles in degrees, the shorter way round, 0 to 180.</summary>
    /// <param name="a">The first angle.</param>
    /// <param name="b">The second angle.</param>
    /// <returns>The difference, in degrees.</returns>
    public static double WrappedDifference(double a, double b)
    {
        double difference = Math.Abs(a - b) % 360.0;
        return difference > 180.0 ? 360.0 - difference : difference;
    }

    /// <summary>The data rows of a list: every line after the header, split at its tabs.</summary>
    /// <param name="fileName">The list's file name.</param>
    /// <param name="cellCount">The number of cells every row holds.</param>
    /// <returns>The rows' cells.</returns>
    private static IEnumerable<string[]> Rows(string fileName, int cellCount)
    {
        string path = TierBPath(fileName);
        File.Exists(path).Should().BeTrue($"the Tier B list '{path}' should exist");

        foreach (string line in File.ReadAllLines(path).Skip(1))
        {
            if (line.Length == 0)
            {
                continue;
            }

            string[] cells = line.Split('\t');
            cells.Should().HaveCount(cellCount, $"of the row '{line}'");
            yield return cells;
        }
    }

    /// <summary>A list's number, printed with a point.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The number.</returns>
    private static double Parse(string cell) => double.Parse(cell, CultureInfo.InvariantCulture);

    /// <summary>A list's instant, UT, in the format given.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="format">The cell's format: to the minute or to the second.</param>
    /// <returns>The instant, <see cref="DateTimeKind.Utc"/>.</returns>
    private static DateTime ParseUtc(string cell, string format) =>
        DateTime.SpecifyKind(DateTime.ParseExact(cell, format, CultureInfo.InvariantCulture), DateTimeKind.Utc);

    /// <summary>The path of a list, in <c>TierB/</c> beside this file, as the goldens are found beside theirs.</summary>
    /// <param name="fileName">The list's file name.</param>
    /// <param name="callerFilePath">This file's path, supplied by the compiler.</param>
    /// <returns>The path.</returns>
    private static string TierBPath(string fileName, [CallerFilePath] string callerFilePath = "")
    {
        string directory = Path.GetDirectoryName(callerFilePath)
            ?? throw new InvalidOperationException("Unable to determine the test's source directory.");
        return Path.Combine(directory, "TierB", fileName);
    }
}
