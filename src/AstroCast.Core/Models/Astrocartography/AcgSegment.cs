namespace AstroCast.Core.Models.Astrocartography;

/// <summary>
/// One drawable stretch of an astrocartography line: consecutive points a map joins
/// with straight strokes. A line is drawn as one or more segments, split where it
/// crosses 180 degrees of longitude, so no stroke runs across the whole map.
/// </summary>
/// <param name="Points">The segment's points, in the line's order; at least two.</param>
public sealed record AcgSegment(IReadOnlyList<AcgCoordinate> Points);
