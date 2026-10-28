using AstroCast.Core.Enums;
using AstroCast.Core.Models.Astrocartography;

namespace AstroCast.Core.Services;

/// <summary>
/// The drawing geometry of astrocartography lines, shared by both map windows: the
/// segments each line is drawn as, and where its labels go. The windows only draw
/// what this returns, so the two heads cannot drift apart in what they draw.
/// </summary>
/// <remarks>
/// A map in longitude and latitude has an edge at 180 degrees, and a line that crosses
/// it, joined point to point, would be drawn as a stroke back across the whole map. So a
/// line is split there: one segment ends on the edge at +180 degrees and the next starts
/// on the edge at -180 degrees (or the other way about), at the latitude where the stroke
/// between the points either side crosses it. A line is never joined past its own first or
/// last point, so a line that ends, where its body stops rising and setting, is drawn to
/// its end points and no further.
/// </remarks>
public static class AcgMapGeometry
{
    /// <summary>
    /// Latitude, north and south, of a meridian (MC or IC) line's labels (degrees). It is
    /// where they sat when every line stopped at 66 degrees: clear of the ASC and DSC labels
    /// in the 53 to 57 degree bands, and off the map's edge, where a meridian line, now
    /// drawn to 85 degrees, ends.
    /// </summary>
    private const double MeridianLabelLatitude = 66.0;

    /// <summary>
    /// The segments a line is drawn as: its points in order, split wherever the line
    /// crosses 180 degrees of longitude, each segment ending or starting on that edge.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>
    /// The line's segments, in order, each of at least two points; none for a line with
    /// fewer than two points. No segment spans more than 180 degrees of longitude.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="line"/> is null.</exception>
    public static IReadOnlyList<AcgSegment> Segments(AcgLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        IReadOnlyList<AcgCoordinate> coords = line.Coordinates;
        List<AcgSegment> segments = [];
        if (coords.Count < 2)
        {
            return segments;
        }

        List<AcgCoordinate> current = [coords[0]];
        for (int i = 1; i < coords.Count; i++)
        {
            AcgCoordinate a = coords[i - 1];
            AcgCoordinate b = coords[i];
            double step = b.Longitude - a.Longitude;

            if (Math.Abs(step) > 180.0)
            {
                // The shorter way from a to b crosses 180 degrees: eastward across +180
                // when b's longitude is the smaller, westward across -180 otherwise.
                double edge = step < 0.0 ? 180.0 : -180.0;
                double shorterStep = step < 0.0 ? step + 360.0 : step - 360.0;
                double t = (edge - a.Longitude) / shorterStep;
                double latitude = a.Latitude + t * (b.Latitude - a.Latitude);

                AddPoint(current, new AcgCoordinate(latitude, edge));
                if (current.Count >= 2)
                {
                    segments.Add(new AcgSegment(current.AsReadOnly()));
                }

                current = [new AcgCoordinate(latitude, -edge)];
            }

            AddPoint(current, b);
        }

        if (current.Count >= 2)
        {
            segments.Add(new AcgSegment(current.AsReadOnly()));
        }

        return segments;
    }

    /// <summary>
    /// Where a line's labels go: up to two positions. A meridian (MC or IC) line is
    /// labelled at 66 degrees south and north on the line. An ASC or DSC line is labelled at
    /// its last point in each of the bands 53 to 57 degrees north and south, or, with no point
    /// in either band, at its two ends.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The label positions; none for a line with fewer than two points.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="line"/> is null.</exception>
    public static IReadOnlyList<AcgCoordinate> LabelPositions(AcgLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        if (line.Coordinates.Count < 2)
        {
            return [];
        }

        if (line.LineType is AcgLineType.MC or AcgLineType.IC)
        {
            double south = line.Coordinates[0].Latitude;
            double north = line.Coordinates[^1].Latitude;
            double longitude = line.Coordinates[0].Longitude;
            return
            [
                new AcgCoordinate(Math.Clamp(-MeridianLabelLatitude, south, north), longitude),
                new AcgCoordinate(Math.Clamp(MeridianLabelLatitude, south, north), longitude),
            ];
        }

        AcgCoordinate? northLabel = null;
        AcgCoordinate? southLabel = null;

        foreach (AcgCoordinate coord in line.Coordinates)
        {
            if (coord.Latitude >= 53.0 && coord.Latitude <= 57.0)
            {
                northLabel = coord;
            }
            if (coord.Latitude >= -57.0 && coord.Latitude <= -53.0)
            {
                southLabel = coord;
            }
        }

        List<AcgCoordinate> result = new(2);
        if (northLabel is not null) result.Add(northLabel);
        if (southLabel is not null) result.Add(southLabel);

        // Fallback: if no points landed in the preferred bands (an ASC/DSC line that
        // ends short of them), label the line's ends.
        if (result.Count == 0)
        {
            result.Add(line.Coordinates[0]);
            result.Add(line.Coordinates[^1]);
        }

        return result;
    }

    /// <summary>
    /// Appends a point to a segment unless it repeats the segment's last point, as the
    /// edge point does when a line's own point lies on 180 degrees.
    /// </summary>
    private static void AddPoint(List<AcgCoordinate> segment, AcgCoordinate point)
    {
        if (segment.Count == 0 || segment[^1] != point)
        {
            segment.Add(point);
        }
    }
}
