using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;

namespace AstroCast.Core.Services;

/// <summary>
/// Computes astrocartography (ACG) lines for a natal chart, determining the
/// geographic locations where each natal planet is angular (on the ASC, DSC,
/// MC, or IC).
/// </summary>
/// <remarks>
/// <para>
/// The lines are <b>in mundo</b>: they mark where the body itself is on the meridian
/// or the horizon, not where its zodiac degree is. Each is computed in closed form,
/// with no search, from the body's geocentric apparent right ascension α and
/// declination δ on the true equator of date and the Greenwich apparent sidereal
/// time θ at the chart's instant. The positions are geocentric (no parallax, the
/// Moon included) and the horizon is the geometric one (no refraction).
/// </para>
/// <para>
/// <b>MC/IC lines</b> are meridians: the body culminates where the local apparent
/// sidereal time equals its right ascension, at geographic longitude <c>α - θ</c>,
/// and is on the IC 180 degrees from there. Each runs from 85° S to 85° N.
/// </para>
/// <para>
/// <b>ASC/DSC lines</b> are curves. At latitude φ the body is on the horizon at hour
/// angle ±H₀, where <c>cos H₀ = -tan φ · tan δ</c>: it rises at <c>α - H₀ - θ</c> and
/// sets at <c>α + H₀ - θ</c>. Where |tan φ · tan δ| exceeds 1 it neither rises nor
/// sets, so the two lines end at |φ| = 90° - |δ|, where they meet. Points are given
/// at every whole degree from 85° S to 85° N at which the body rises and sets, and
/// at each end.
/// </para>
/// <para>
/// All Swiss Ephemeris access is delegated to <see cref="IEphemerisService"/>,
/// which serialises native calls through a <see cref="System.Threading.SemaphoreSlim"/>
/// to ensure thread safety.
/// </para>
/// </remarks>
public sealed class AstrocartographyService : IAstrocartographyService
{
    /// <summary>
    /// Radius of the sphere crossing distances are measured on (kilometres): the
    /// mean Earth radius of IUGG, 6,371.0088 km.
    /// </summary>
    public const double EarthRadiusKilometres = 6371.0088;

    /// <summary>
    /// Default crossing orb (kilometres): a line is listed for a place when its
    /// nearest point is within 334 km, about 3 degrees of great circle.
    /// </summary>
    public const double DefaultCrossingOrbKilometres = 334.0;

    /// <summary>
    /// Furthest latitude, north or south, at which any line is given (degrees).
    /// Web Mercator, the projection both map windows draw in, ends at about
    /// 85.05°, so a line beyond 85° could not be drawn.
    /// </summary>
    private const double LatitudeLimitDegrees = 85.0;

    /// <summary>
    /// Southernmost latitude of every line (degrees).
    /// Derived from <see cref="LatitudeLimitDegrees"/>.
    /// </summary>
    private const double LatitudeMin = -LatitudeLimitDegrees;

    /// <summary>
    /// Northernmost latitude of every line (degrees).
    /// Derived from <see cref="LatitudeLimitDegrees"/>.
    /// </summary>
    private const double LatitudeMax = LatitudeLimitDegrees;

    /// <summary>Latitude step of the ASC/DSC points between their ends (degrees).</summary>
    private const double LatitudeStep = 1.0;

    private readonly IEphemerisService _ephemeris;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="AstrocartographyService"/> class.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for coordinate transformations and house calculations.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ephemeris"/> or <paramref name="logger"/> is null.
    /// </exception>
    public AstrocartographyService(IEphemerisService ephemeris, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _logger = logger.ForContext<AstrocartographyService>();
    }

    /// <inheritdoc />
    public AcgResult Calculate(ChartResult natalChart, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        cancellationToken.ThrowIfCancellationRequested();

        _logger.Debug(
            "Calculating astrocartography for '{Name}' at JD {JulianDay}",
            natalChart.Input.Name, natalChart.JulianDay);

        double obliquity = _ephemeris.GetObliquity(natalChart.JulianDay);
        double gastDegrees = _ephemeris.GetSiderealTimeDegrees(natalChart.JulianDay);

        List<AcgLine> lines = new(natalChart.Positions.Count * 4);

        foreach (PlanetPosition planet in natalChart.Positions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string bodyName = GetBodyName(planet.Body);
            (double rightAscension, double declination) = GetEquatorialPosition(planet, obliquity);

            AcgLine mcLine = ComputeMcLine(planet, bodyName, rightAscension, gastDegrees);
            AcgLine icLine = ComputeIcLine(planet, bodyName, mcLine);
            AcgLine ascLine = ComputeAscLine(planet, bodyName, rightAscension, declination,
                gastDegrees, AcgLineType.ASC);
            AcgLine dscLine = ComputeAscLine(planet, bodyName, rightAscension, declination,
                gastDegrees, AcgLineType.DSC);

            lines.Add(mcLine);
            lines.Add(icLine);
            lines.Add(ascLine);
            lines.Add(dscLine);
        }

        // Information-level stays PII-free (M-6); the subject name is Debug-only.
        _logger.Information(
            "Computed {LineCount} ACG lines for natal chart",
            lines.Count);
        _logger.Debug(
            "Computed {LineCount} ACG lines for '{Name}'",
            lines.Count, natalChart.Input.Name);

        return new AcgResult
        {
            NatalChart = natalChart,
            Lines = lines.AsReadOnly(),
            Obliquity = obliquity,
            GastDegrees = gastDegrees,
            LatitudeMin = LatitudeMin,
            LatitudeMax = LatitudeMax,
            LatitudeStep = LatitudeStep,
        };
    }

    /// <inheritdoc />
    public AcgCrossingReport GetCrossings(
        AcgResult acgResult,
        double latitude, double longitude,
        string locationName,
        double orbKilometres = DefaultCrossingOrbKilometres)
    {
        ArgumentNullException.ThrowIfNull(acgResult);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);

        if (orbKilometres <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(orbKilometres),
                orbKilometres, "Orb must be positive.");
        }

        List<AcgCrossing> nearby = new();

        foreach (AcgLine line in acgResult.Lines)
        {
            double? distance = ComputeDistanceKilometres(line, latitude, longitude);

            if (distance.HasValue && distance.Value <= orbKilometres)
            {
                nearby.Add(new AcgCrossing
                {
                    Body = line.Body,
                    BodyName = line.BodyName,
                    LineType = line.LineType,
                    DistanceKilometres = distance.Value,
                });
            }
        }

        nearby.Sort((a, b) => a.DistanceKilometres.CompareTo(b.DistanceKilometres));

        _logger.Debug(
            "Found {Count} ACG line crossings near {LocationName} ({Lat:F4}, {Lon:F4}) within {Orb} km",
            nearby.Count, locationName, latitude, longitude, orbKilometres);

        return new AcgCrossingReport
        {
            LocationName = locationName,
            Latitude = latitude,
            Longitude = longitude,
            OrbKilometres = orbKilometres,
            Crossings = nearby.AsReadOnly(),
        };
    }

    #region MC / IC line computation

    /// <summary>
    /// The body's geocentric apparent right ascension and declination on the true
    /// equator of date: the chart's own, from <c>swe_calc_ut</c> with
    /// <c>SEFLG_EQUATORIAL</c>, or, for a position that carries none, its ecliptic
    /// longitude and latitude rotated by the true obliquity, which is the same position.
    /// </summary>
    private (double RightAscension, double Declination) GetEquatorialPosition(
        PlanetPosition planet, double obliquity)
    {
        if (planet.RightAscension is double ra && planet.Declination is double dec)
        {
            return (ra, dec);
        }

        return _ephemeris.EclipticToEquatorial(planet.Longitude, planet.Latitude, obliquity);
    }

    /// <summary>
    /// Compute the MC line for a planet: the meridian at the geographic longitude
    /// where the body itself culminates.
    /// </summary>
    private static AcgLine ComputeMcLine(PlanetPosition planet, string bodyName,
        double rightAscension, double gastDegrees)
    {
        // The body is on the MC where the local apparent sidereal time equals its RA.
        // Local sidereal time = GAST + geographic longitude (east positive, all in degrees).
        // So for LST = RA, geographic longitude = RA - GAST.
        double geoLon = NormalizeLongitude(rightAscension - gastDegrees);

        IReadOnlyList<AcgCoordinate> coords =
        [
            new AcgCoordinate(LatitudeMin, geoLon),
            new AcgCoordinate(LatitudeMax, geoLon),
        ];

        return new AcgLine
        {
            Body = planet.Body,
            BodyName = bodyName,
            LineType = AcgLineType.MC,
            Coordinates = coords,
            PlanetLongitude = planet.Longitude,
            PlanetLatitude = planet.Latitude,
        };
    }

    /// <summary>
    /// Compute the IC line for a planet: vertical meridian 180 degrees opposite
    /// the MC line in geographic longitude.
    /// </summary>
    private static AcgLine ComputeIcLine(PlanetPosition planet, string bodyName, AcgLine mcLine)
    {
        // IC geographic longitude is 180 degrees from MC geographic longitude,
        // normalised back to (-180, 180].
        double mcGeoLon = mcLine.Coordinates[0].Longitude;
        double icGeoLon = NormalizeLongitude(mcGeoLon + 180.0);

        IReadOnlyList<AcgCoordinate> coords =
        [
            new AcgCoordinate(LatitudeMin, icGeoLon),
            new AcgCoordinate(LatitudeMax, icGeoLon),
        ];

        return new AcgLine
        {
            Body = planet.Body,
            BodyName = bodyName,
            LineType = AcgLineType.IC,
            Coordinates = coords,
            PlanetLongitude = planet.Longitude,
            PlanetLatitude = planet.Latitude,
        };
    }

    #endregion

    #region ASC / DSC line computation

    /// <summary>
    /// Compute the ASC (rising) or DSC (setting) line of a body in closed form.
    /// At latitude φ the body is on the geometric horizon at hour angle ±H₀, where
    /// <c>cos H₀ = -tan φ · tan δ</c>; it rises at longitude <c>α - H₀ - GAST</c> and
    /// sets at <c>α + H₀ - GAST</c>.
    /// </summary>
    /// <remarks>
    /// Points are given at every whole degree from 85° S to 85° N at which the body
    /// rises and sets, ordered by latitude. Where |tan φ · tan δ| exceeds 1 the body
    /// neither rises nor sets: the line ends at |φ| = 90° - |δ|, where H₀ is 0° or
    /// 180° and the rising and setting lines meet, and both ends are points of the
    /// line. A body within 5° of the equator rises and sets at every latitude in
    /// range, and its line runs from 85° S to 85° N.
    /// </remarks>
    private static AcgLine ComputeAscLine(PlanetPosition planet, string bodyName,
        double rightAscension, double declination, double gastDegrees, AcgLineType lineType)
    {
        double endLatitude = 90.0 - Math.Abs(declination);
        bool ends = endLatitude < LatitudeMax;

        List<double> latitudes = new(2 * (int)LatitudeLimitDegrees + 3);
        if (ends)
        {
            latitudes.Add(-endLatitude);
        }

        for (double lat = LatitudeMin; lat <= LatitudeMax + 1e-9; lat += LatitudeStep)
        {
            if (!ends || Math.Abs(lat) < endLatitude)
            {
                latitudes.Add(lat);
            }
        }

        if (ends)
        {
            latitudes.Add(endLatitude);
        }

        double tanDec = Math.Tan(declination * Math.PI / 180.0);
        List<AcgCoordinate> coords = new(latitudes.Count);

        foreach (double lat in latitudes)
        {
            // At an end |tan φ · tan δ| is 1 to rounding; the clamp keeps acos defined there.
            double cosH0 = Math.Clamp(-Math.Tan(lat * Math.PI / 180.0) * tanDec, -1.0, 1.0);
            double h0 = Math.Acos(cosH0) * 180.0 / Math.PI;

            double geoLon = lineType == AcgLineType.ASC
                ? NormalizeLongitude(rightAscension - h0 - gastDegrees)
                : NormalizeLongitude(rightAscension + h0 - gastDegrees);
            coords.Add(new AcgCoordinate(lat, geoLon));
        }

        return new AcgLine
        {
            Body = planet.Body,
            BodyName = bodyName,
            LineType = lineType,
            Coordinates = coords.AsReadOnly(),
            PlanetLongitude = planet.Longitude,
            PlanetLatitude = planet.Latitude,
        };
    }

    #endregion

    #region Crossing distance calculations

    /// <summary>
    /// The great-circle distance in kilometres from a place to the nearest point of a
    /// line, on a sphere of radius <see cref="EarthRadiusKilometres"/>; null for a line
    /// with no points.
    /// </summary>
    /// <remarks>
    /// Every line lies on one great circle: an MC or IC line on a meridian, an ASC or DSC
    /// line on the circle 90 degrees from the point beneath the body, where the body is on
    /// the horizon. The stretch of a line between two neighbouring points is therefore the
    /// shorter great-circle arc between them, and the distance to the line is the least
    /// distance to any of its arcs: exact, with no interpolation, at any latitude.
    /// </remarks>
    private static double? ComputeDistanceKilometres(AcgLine line, double queryLat, double queryLon)
    {
        IReadOnlyList<AcgCoordinate> coords = line.Coordinates;
        if (coords.Count == 0)
        {
            return null;
        }

        (double X, double Y, double Z) place = ToUnitVector(queryLat, queryLon);
        (double X, double Y, double Z) previous = ToUnitVector(coords[0].Latitude, coords[0].Longitude);
        double least = AngleBetween(place, previous);

        for (int i = 1; i < coords.Count; i++)
        {
            (double X, double Y, double Z) next = ToUnitVector(coords[i].Latitude, coords[i].Longitude);
            least = Math.Min(least, AngleToArc(place, previous, next));
            previous = next;
        }

        return least * EarthRadiusKilometres;
    }

    /// <summary>
    /// The angle in radians from point <paramref name="p"/> to the shorter great-circle arc
    /// from <paramref name="a"/> to <paramref name="b"/> (all unit vectors).
    /// </summary>
    /// <remarks>
    /// When the foot of the perpendicular from <paramref name="p"/> to the arc's great circle
    /// lies on the arc, the distance is the angle to the circle; otherwise it is the angle to
    /// the nearer end.
    /// </remarks>
    private static double AngleToArc(
        (double X, double Y, double Z) p, (double X, double Y, double Z) a, (double X, double Y, double Z) b)
    {
        double toEnds = Math.Min(AngleBetween(p, a), AngleBetween(p, b));

        (double X, double Y, double Z) pole = Cross(a, b);
        double poleLength = Math.Sqrt(Dot(pole, pole));
        if (poleLength < 1e-15)
        {
            return toEnds;
        }

        pole = (pole.X / poleLength, pole.Y / poleLength, pole.Z / poleLength);
        double height = Dot(p, pole);
        (double X, double Y, double Z) foot = (p.X - height * pole.X, p.Y - height * pole.Y, p.Z - height * pole.Z);

        bool onArc = Dot(Cross(a, foot), pole) >= 0.0 && Dot(Cross(foot, b), pole) >= 0.0;
        return onArc
            ? Math.Atan2(Math.Abs(height), Math.Sqrt(Dot(foot, foot)))
            : toEnds;
    }

    /// <summary>The unit vector of a geographic position (degrees).</summary>
    private static (double X, double Y, double Z) ToUnitVector(double latitude, double longitude)
    {
        double phi = latitude * Math.PI / 180.0;
        double lambda = longitude * Math.PI / 180.0;
        return (Math.Cos(phi) * Math.Cos(lambda), Math.Cos(phi) * Math.Sin(lambda), Math.Sin(phi));
    }

    /// <summary>The angle in radians between two unit vectors, accurate at any size.</summary>
    private static double AngleBetween((double X, double Y, double Z) u, (double X, double Y, double Z) v)
    {
        (double X, double Y, double Z) c = Cross(u, v);
        return Math.Atan2(Math.Sqrt(Dot(c, c)), Dot(u, v));
    }

    /// <summary>The cross product of two vectors.</summary>
    private static (double X, double Y, double Z) Cross((double X, double Y, double Z) u, (double X, double Y, double Z) v) =>
        (u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X);

    /// <summary>The dot product of two vectors.</summary>
    private static double Dot((double X, double Y, double Z) u, (double X, double Y, double Z) v) =>
        u.X * v.X + u.Y * v.Y + u.Z * v.Z;

    #endregion

    #region Helpers

    /// <summary>
    /// Normalises a geographic longitude to the range (-180, +180].
    /// </summary>
    private static double NormalizeLongitude(double lon)
    {
        lon = ((lon % 360.0) + 360.0) % 360.0;
        return lon > 180.0 ? lon - 360.0 : lon;
    }

    /// <summary>
    /// Displayable name for a celestial body. Uses "North Node" for TrueNode
    /// to match the astrocartography convention.
    /// </summary>
    private static string GetBodyName(CelestialBodyId body) => body switch
    {
        CelestialBodyId.Sun => "Sun",
        CelestialBodyId.Moon => "Moon",
        CelestialBodyId.Mercury => "Mercury",
        CelestialBodyId.Venus => "Venus",
        CelestialBodyId.Mars => "Mars",
        CelestialBodyId.Jupiter => "Jupiter",
        CelestialBodyId.Saturn => "Saturn",
        CelestialBodyId.Uranus => "Uranus",
        CelestialBodyId.Neptune => "Neptune",
        CelestialBodyId.Pluto => "Pluto",
        CelestialBodyId.TrueNode => "North Node",
        CelestialBodyId.Chiron => "Chiron",
        _ => body.ToString(),
    };

    #endregion
}
