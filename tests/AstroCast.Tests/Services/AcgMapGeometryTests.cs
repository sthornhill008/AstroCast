using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the drawing geometry both map windows draw (rule 4 of branch J, E3-8): each line as one
/// or more segments, split where it crosses 180° of longitude so that no segment runs across the
/// whole map, keeping its end points; and where its labels go.
/// </summary>
/// <remarks>
/// The synthetic lines are typed here; the real ones are person A's and the J2000 fixture's, whose
/// lines the in-mundo tests hold against Tier B. A crossing's latitude is the straight-line
/// interpolation between the two points either side of 180° (Tier A arithmetic, typed out).
/// </remarks>
[Collection("Ephemeris")]
public sealed class AcgMapGeometryTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public AcgMapGeometryTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// A line that crosses 180° going east comes back as two segments that meet the map's edge:
    /// the first ends at +180°, the second starts at -180°, at the latitude where the stroke
    /// between the points either side crosses it (11 + 2/6 = 11.333°).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Segments_LineCrossing180Eastward_ComesBackAsTwoSegmentsMeetingTheEdge()
    {
        AcgLine line = Line(AcgLineType.ASC, (10, 170), (11, 178), (12, -176), (13, -170));

        IReadOnlyList<AcgSegment> segments = AcgMapGeometry.Segments(line);

        segments.Should().HaveCount(2);
        segments[0].Points.Select(p => (p.Latitude, p.Longitude)).Should().Equal(
            [(10.0, 170.0), (11.0, 178.0), (11.0 + 2.0 / 6.0, 180.0)], Close);
        segments[1].Points.Select(p => (p.Latitude, p.Longitude)).Should().Equal(
            [(11.0 + 2.0 / 6.0, -180.0), (12.0, -176.0), (13.0, -170.0)], Close);
    }

    /// <summary>
    /// A line that crosses 180° going west comes back as two segments meeting the edge the other
    /// way: the first ends at -180°, the second starts at +180°.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Segments_LineCrossing180Westward_ComesBackAsTwoSegmentsMeetingTheEdge()
    {
        AcgLine line = Line(AcgLineType.DSC, (-20, -175), (-19, 179), (-18, 175));

        IReadOnlyList<AcgSegment> segments = AcgMapGeometry.Segments(line);

        segments.Should().HaveCount(2);
        segments[0].Points.Select(p => (p.Latitude, p.Longitude)).Should().Equal(
            [(-20.0, -175.0), (-20.0 + 5.0 / 6.0, -180.0)], Close);
        segments[1].Points.Select(p => (p.Latitude, p.Longitude)).Should().Equal(
            [(-20.0 + 5.0 / 6.0, 180.0), (-19.0, 179.0), (-18.0, 175.0)], Close);
    }

    /// <summary>
    /// A line that ends short of 85° keeps its end points: person A's Moon horizon lines start at
    /// 65.07° S and stop at 65.07° N, where the Moon stops rising and setting, and their segments
    /// start and stop there too, with every point of the line kept in order.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Segments_LineThatEnds_ReturnsItsEndPoints_AndEveryPoint()
    {
        AcgResult acg = Calculate(Hartwell());

        foreach (AcgLine line in acg.Lines.Where(l => l.Body == CelestialBodyId.Moon && l.LineType is AcgLineType.ASC or AcgLineType.DSC))
        {
            IReadOnlyList<AcgSegment> segments = AcgMapGeometry.Segments(line);

            segments[0].Points[0].Should().Be(line.Coordinates[0]);
            segments[^1].Points[^1].Should().Be(line.Coordinates[^1]);
            segments[^1].Points[^1].Latitude.Should().BeApproximately(65.07, 5e-3);
            segments.SelectMany(s => s.Points).Where(p => Math.Abs(p.Longitude) < 180.0)
                .Should().Equal(line.Coordinates.Where(p => Math.Abs(p.Longitude) < 180.0), "no point of the line is dropped or reordered");
        }
    }

    /// <summary>
    /// No segment of any line of person A or the J2000 fixture spans more than 180° of longitude,
    /// and no two neighbouring points of a segment are more than 180° apart, so no stroke runs
    /// across the whole map. Every segment has at least two points, and a meridian line is one
    /// segment from 85° S to 85° N. (A horizon line that ends at both ends spans exactly 180° from
    /// end to end, its hour angle running from 180° to 0°, so the span is allowed 1e-9° of rounding.)
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Segments_NoSegmentSpansMoreThan180DegreesOfLongitude()
    {
        foreach (ChartResult chart in new[] { Hartwell(), GoldenFixtures.J2000Nyc(_fixture) })
        {
            foreach (AcgLine line in Calculate(chart).Lines)
            {
                IReadOnlyList<AcgSegment> segments = AcgMapGeometry.Segments(line);

                foreach (AcgSegment segment in segments)
                {
                    segment.Points.Should().HaveCountGreaterThanOrEqualTo(2);
                    (segment.Points.Max(p => p.Longitude) - segment.Points.Min(p => p.Longitude)).Should().BeLessThanOrEqualTo(180.0 + 1e-9,
                        $"{chart.Input.Name} {line.BodyName} {line.LineType}");
                    for (int i = 1; i < segment.Points.Count; i++)
                    {
                        Math.Abs(segment.Points[i].Longitude - segment.Points[i - 1].Longitude).Should().BeLessThan(180.0);
                    }
                }

                if (line.LineType is AcgLineType.MC or AcgLineType.IC)
                {
                    segments.Should().ContainSingle().Which.Points.Should().Equal(line.Coordinates);
                }
            }
        }
    }

    /// <summary>
    /// A meridian line's labels sit at 66° S and 66° N on the line, where they sat when the lines
    /// stopped at 66°, clear of the horizon lines' labels at 53° to 57°; the line now runs on to 85°,
    /// where a label would sit on the map's edge.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void LabelPositions_MeridianLine_AreAt66DegreesOnTheLine()
    {
        AcgLine line = Line(AcgLineType.MC, (-85, -103.03), (85, -103.03));

        AcgMapGeometry.LabelPositions(line).Should().Equal(
            new AcgCoordinate(-66.0, -103.03), new AcgCoordinate(66.0, -103.03));
    }

    /// <summary>
    /// A horizon line's labels sit on its last points in the bands 53° to 57° north and south, as
    /// before; a line with no point in either band is labelled at its ends.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void LabelPositions_HorizonLine_AreInTheBands_OrAtItsEnds()
    {
        AcgLine banded = Line(AcgLineType.ASC, (-58, 10), (-57, 11), (-56, 12), (55, 20), (56, 21), (58, 22));
        AcgLine short_ = Line(AcgLineType.DSC, (-40.5, 30), (0, 31), (40.5, 32));

        AcgMapGeometry.LabelPositions(banded).Should().Equal(new AcgCoordinate(56, 21), new AcgCoordinate(-56, 12));
        AcgMapGeometry.LabelPositions(short_).Should().Equal(new AcgCoordinate(-40.5, 30), new AcgCoordinate(40.5, 32));
    }

    #region Helpers

    private static bool Close((double Latitude, double Longitude) a, (double Latitude, double Longitude) b) =>
        Math.Abs(a.Latitude - b.Latitude) < 1e-9 && Math.Abs(a.Longitude - b.Longitude) < 1e-9;

    private static AcgLine Line(AcgLineType type, params (double Lat, double Lon)[] points) => new()
    {
        Body = CelestialBodyId.Moon,
        BodyName = "Moon",
        LineType = type,
        Coordinates = points.Select(p => new AcgCoordinate(p.Lat, p.Lon)).ToList(),
        PlanetLongitude = 0.0,
        PlanetLatitude = 0.0,
    };

    private AcgResult Calculate(ChartResult chart) =>
        new AstrocartographyService(_fixture.EphemerisService, new LoggerConfiguration().CreateLogger()).Calculate(chart);

    /// <summary>Person A (invented): 1947-10-05 09:30 at UTC−5 (explicit), Harrisburg, Pennsylvania.</summary>
    private ChartResult Hartwell() => GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
    {
        Name = "Eleanor Hartwell",
        LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
        TimeZone = "America/New_York",
        UseExplicitOffset = true,
        UtcOffsetHours = -5.0,
        Latitude = 40.2737,
        Longitude = -76.8844,
        LocationName = "Harrisburg, Pennsylvania, United States",
        EventType = "Birth",
    });

    #endregion
}
