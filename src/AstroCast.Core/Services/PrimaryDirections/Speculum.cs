using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;

namespace AstroCast.Core.Services.PrimaryDirections;

/// <summary>
/// One point of the speculum (rule R1): its place on the ecliptic and the equator, its hour angle and its semi-arcs
/// at the place.
/// </summary>
internal sealed class SpeculumPoint
{
    /// <summary>Degrees per radian.</summary>
    private const double DegreesPerRadian = 180.0 / Math.PI;

    /// <summary>The point's name: a body, a star, an angle or the Part of Fortune.</summary>
    internal required string Name { get; init; }

    /// <summary>Ecliptic longitude, in degrees, 0 to 360.</summary>
    internal required double Longitude { get; init; }

    /// <summary>Ecliptic latitude, in degrees: a body's or a star's own, 0 for every other point.</summary>
    internal required double EclipticLatitude { get; init; }

    /// <summary>Right ascension of date, in degrees, 0 to 360.</summary>
    internal required double RightAscension { get; init; }

    /// <summary>Declination of date, in degrees.</summary>
    internal required double Declination { get; init; }

    /// <summary>The hour angle H = RAMC − α, in degrees, in (−180, 180]: negative east of the upper meridian.</summary>
    internal required double HourAngle { get; init; }

    /// <summary>The diurnal semi-arc, 90° + AD, in degrees; null when the point never rises or sets at the place.</summary>
    internal required double? DiurnalSemiArc { get; init; }

    /// <summary>The nocturnal semi-arc, 90° − AD, in degrees; null when the point never rises or sets at the place.</summary>
    internal required double? NocturnalSemiArc { get; init; }

    /// <summary>
    /// Whether the point is above the horizon: |H| &lt; DSA when it has semi-arcs; otherwise whether its declination
    /// has the latitude's name, which keeps it always above.
    /// </summary>
    internal required bool IsUpper { get; init; }

    /// <summary>Whether the point rises and sets at the place, so that both semi-arcs exist.</summary>
    internal bool HasSemiArcs => DiurnalSemiArc.HasValue;

    /// <summary>
    /// The hour angle from a hemisphere's meridian, and that hemisphere's semi-arc: H and the DSA from the upper
    /// meridian, H − 180° normalised and the NSA from the lower.
    /// </summary>
    /// <param name="upper">The hemisphere.</param>
    /// <returns>The hour angle and the semi-arc.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the point has no semi-arcs.</exception>
    internal (double HourAngle, double SemiArc) InHemisphere(bool upper)
    {
        if (DiurnalSemiArc is not double dsa || NocturnalSemiArc is not double nsa)
            throw new InvalidOperationException($"{Name} never rises or sets at the place: it has no semi-arcs.");

        return upper
            ? (HourAngle, dsa)
            : (Wrap180(HourAngle - 180.0), nsa);
    }

    /// <summary>
    /// Builds a point from its coordinates at a place: AD = asin(tan φ · tan δ), DSA = 90° + AD, NSA = 90° − AD,
    /// H = RAMC − α; when |tan φ · tan δ| ≥ 1 the point has no semi-arcs.
    /// </summary>
    /// <param name="name">The point's name.</param>
    /// <param name="longitude">Ecliptic longitude, in degrees.</param>
    /// <param name="eclipticLatitude">Ecliptic latitude, in degrees.</param>
    /// <param name="rightAscension">Right ascension of date, in degrees.</param>
    /// <param name="declination">Declination of date, in degrees.</param>
    /// <param name="geographicLatitude">The place's latitude φ, in degrees, north positive.</param>
    /// <param name="armc">The right ascension of the upper meridian, in degrees.</param>
    /// <returns>The point.</returns>
    internal static SpeculumPoint Build(
        string name, double longitude, double eclipticLatitude, double rightAscension, double declination,
        double geographicLatitude, double armc)
    {
        double product = Math.Tan(geographicLatitude / DegreesPerRadian) * Math.Tan(declination / DegreesPerRadian);
        double hourAngle = Wrap180(armc - rightAscension);
        double? dsa = null;
        double? nsa = null;
        bool upper;

        if (Math.Abs(product) >= 1.0)
        {
            upper = product > 0.0;
        }
        else
        {
            double ascensionalDifference = Math.Asin(product) * DegreesPerRadian;
            dsa = 90.0 + ascensionalDifference;
            nsa = 90.0 - ascensionalDifference;
            upper = Math.Abs(hourAngle) < dsa.Value;
        }

        return new SpeculumPoint
        {
            Name = name,
            Longitude = Normalize360(longitude),
            EclipticLatitude = eclipticLatitude,
            RightAscension = rightAscension,
            Declination = declination,
            HourAngle = hourAngle,
            DiurnalSemiArc = dsa,
            NocturnalSemiArc = nsa,
            IsUpper = upper,
        };
    }

    /// <summary>An angle brought into [0°, 360°).</summary>
    /// <param name="angle">The angle, in degrees.</param>
    /// <returns>The angle in [0°, 360°).</returns>
    internal static double Normalize360(double angle) => ((angle % 360.0) + 360.0) % 360.0;

    /// <summary>An angle brought into (−180°, 180°].</summary>
    /// <param name="angle">The angle, in degrees.</param>
    /// <returns>The angle in (−180°, 180°].</returns>
    internal static double Wrap180(double angle)
    {
        double wrapped = angle % 360.0;
        if (wrapped <= -180.0)
            wrapped += 360.0;
        if (wrapped > 180.0)
            wrapped -= 360.0;
        return wrapped;
    }
}

/// <summary>A promissor point (rule R4): the body or star it is taken from, its relation to it, and its point.</summary>
/// <param name="Source">The body's position name or the star's catalog display name.</param>
/// <param name="Aspect">The point's relation to its body; a star's is the conjunction.</param>
/// <param name="Angle">The aspect point's signed angle from the body's longitude; 0 for a conjunction, an antiscion or a contra-antiscion.</param>
/// <param name="Kind">A body or a star.</param>
/// <param name="Point">The point.</param>
internal sealed record SpeculumPromissor(
    string Source,
    PrimaryDirectionAspect Aspect,
    double Angle,
    PrimaryDirectionPromissorKind Kind,
    SpeculumPoint Point);

/// <summary>What a significator is (rule R5).</summary>
internal enum SignificatorKind
{
    /// <summary>The Ascendant, Midheaven, Descendant or IC: carried as its ecliptic degree when converse.</summary>
    Angle,

    /// <summary>The Sun, the Moon, or Mercury to Pluto when the setting adds them: never directed to its own points.</summary>
    Body,

    /// <summary>The Part of Fortune.</summary>
    Lot,
}

/// <summary>A significator (rule R5): its name, its kind and its point.</summary>
/// <param name="Name">"Ascendant", "Midheaven", "Descendant", "IC", a body's name or "Part of Fortune".</param>
/// <param name="Kind">An angle, a body or the Lot.</param>
/// <param name="Point">The point: an angle's is its ecliptic degree at latitude 0.</param>
internal sealed record SpeculumSignificator(string Name, SignificatorKind Kind, SpeculumPoint Point);

/// <summary>
/// The speculum of a birth at an instant and a place (rule R1): every promissor point of rule R4 and every
/// significator of rule R5, each with its hour angle and semi-arcs.
/// </summary>
/// <remarks>
/// <para>
/// Built from a cast chart (<see cref="FromChart"/>) at the birth instant, and from the ephemeris alone
/// (<see cref="AtInstant"/>) at the birth instant moved by minutes for the sensitivity of rule R8. The bodies'
/// right ascensions and declinations are the ephemeris's own (apparent, true equator of date); every other point's
/// are its ecliptic coordinates rotated with the chart's true obliquity (<see cref="IEphemerisService.EclipticToEquatorial"/>),
/// the rotation the ephemeris applied to the bodies.
/// </para>
/// <para>
/// The promissors, in order: for each of the twelve bodies its place with its latitude, its aspect points (the
/// true node only its opposition) and its antiscion and contra-antiscion at latitude 0; then the 55 catalog stars at
/// their own places. The significators: the Ascendant, Midheaven, Descendant and IC, the Sun and the Moon, Mercury to
/// Pluto when asked, then the Part of Fortune, Asc + Moon − Sun by day and Asc + Sun − Moon by night.
/// </para>
/// </remarks>
internal sealed class Speculum
{
    /// <summary>The aspect points a body's promissors include, with their signed angles (rule R4).</summary>
    private static readonly (PrimaryDirectionAspect Aspect, double Angle)[] AspectPoints =
    [
        (PrimaryDirectionAspect.Sextile, 60.0),
        (PrimaryDirectionAspect.Sextile, -60.0),
        (PrimaryDirectionAspect.Square, 90.0),
        (PrimaryDirectionAspect.Square, -90.0),
        (PrimaryDirectionAspect.Trine, 120.0),
        (PrimaryDirectionAspect.Trine, -120.0),
        (PrimaryDirectionAspect.Opposition, 180.0),
    ];

    /// <summary>The planets the setting adds as significators, in order.</summary>
    private static readonly string[] PlanetSignificators =
        ["Mercury", "Venus", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune", "Pluto"];

    /// <summary>The name of the Lot among a chart's Arabic Parts.</summary>
    internal const string PartOfFortuneName = "Part of Fortune";

    /// <summary>The instant the speculum is built for, as a Julian Day (UT).</summary>
    internal required double JulianDayUt { get; init; }

    /// <summary>The place's latitude, in degrees, north positive.</summary>
    internal required double Latitude { get; init; }

    /// <summary>The true obliquity of the ecliptic at the instant, in degrees.</summary>
    internal required double Obliquity { get; init; }

    /// <summary>The right ascension of the upper meridian, in degrees.</summary>
    internal required double Armc { get; init; }

    /// <summary>Whether the Sun's longitude is on the half of the ecliptic above the horizon.</summary>
    internal required bool IsDayChart { get; init; }

    /// <summary>The Part of Fortune's longitude, in degrees.</summary>
    internal required double PartOfFortune { get; init; }

    /// <summary>The promissor points, in the order described on the class.</summary>
    internal required IReadOnlyList<SpeculumPromissor> Promissors { get; init; }

    /// <summary>The significators, in the order described on the class.</summary>
    internal required IReadOnlyList<SpeculumSignificator> Significators { get; init; }

    /// <summary>
    /// Builds the speculum of a cast chart at its own instant: the positions' right ascensions and declinations, the
    /// houses' ARMC and angles, the chart's obliquity and its Part of Fortune, the stars from the ephemeris.
    /// </summary>
    /// <param name="chart">The natal chart.</param>
    /// <param name="ephemeris">The ephemeris, for the stars and the rotation.</param>
    /// <param name="planetsAsSignificators">Whether Mercury to Pluto are significators.</param>
    /// <returns>The speculum.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chart"/> or <paramref name="ephemeris"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a position carries no right ascension or declination, the chart has no houses or obliquity, or its
    /// Arabic Parts hold no Part of Fortune.
    /// </exception>
    internal static Speculum FromChart(ChartResult chart, IEphemerisService ephemeris, bool planetsAsSignificators)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(ephemeris);

        if (chart.Houses is null)
            throw new ArgumentException("The natal chart has no houses.", nameof(chart));
        if (double.IsNaN(chart.Obliquity))
            throw new ArgumentException("The natal chart has no obliquity.", nameof(chart));
        if (chart.Positions.Count == 0 || chart.Positions.Any(p => p.RightAscension is null || p.Declination is null))
            throw new ArgumentException("The natal chart's positions carry no right ascension and declination.", nameof(chart));

        ArabicPart fortune = chart.ArabicParts.FirstOrDefault(p => string.Equals(p.Name, PartOfFortuneName, StringComparison.Ordinal))
            ?? throw new ArgumentException("The natal chart has no Part of Fortune.", nameof(chart));

        PlanetPosition sun = chart.Positions.First(p => p.Body == Enums.CelestialBodyId.Sun);

        return Build(
            chart.JulianDay,
            chart.Input.Latitude,
            chart.Obliquity,
            chart.Houses,
            chart.Positions,
            fortune.Longitude,
            ChartSect.IsDayChart(sun.Longitude, chart.Houses),
            ephemeris,
            planetsAsSignificators);
    }

    /// <summary>
    /// Builds the speculum of a birth at an instant and a place from the ephemeris alone: the positions, the houses
    /// in Equal houses (which no latitude refuses and whose angles are every system's), the obliquity, and the Part of
    /// Fortune by day or night.
    /// </summary>
    /// <param name="julianDayUt">The instant, as a Julian Day (UT).</param>
    /// <param name="latitude">The place's latitude, in degrees, north positive.</param>
    /// <param name="longitude">The place's longitude, in degrees, east positive.</param>
    /// <param name="ephemeris">The ephemeris.</param>
    /// <param name="planetsAsSignificators">Whether Mercury to Pluto are significators.</param>
    /// <returns>The speculum.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ephemeris"/> is null.</exception>
    internal static Speculum AtInstant(
        double julianDayUt, double latitude, double longitude, IEphemerisService ephemeris, bool planetsAsSignificators)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);

        IReadOnlyList<PlanetPosition> positions = ephemeris.CalculateAllPositions(julianDayUt);
        HouseData houses = ephemeris.CalculateHouses(julianDayUt, latitude, longitude, 'E');
        double obliquity = double.IsNaN(houses.Obliquity) ? ephemeris.GetObliquity(julianDayUt) : houses.Obliquity;

        PlanetPosition sun = positions.First(p => p.Body == Enums.CelestialBodyId.Sun);
        PlanetPosition moon = positions.First(p => p.Body == Enums.CelestialBodyId.Moon);
        bool isDay = ChartSect.IsDayChart(sun.Longitude, houses);
        double fortune = SpeculumPoint.Normalize360(isDay
            ? houses.Ascendant + moon.Longitude - sun.Longitude
            : houses.Ascendant + sun.Longitude - moon.Longitude);

        return Build(julianDayUt, latitude, obliquity, houses, positions, fortune, isDay, ephemeris, planetsAsSignificators);
    }

    /// <summary>The antiscion of a longitude, 180° less the longitude, unrounded.</summary>
    /// <param name="longitude">The longitude, in degrees.</param>
    /// <returns>The antiscion, in [0°, 360°).</returns>
    internal static double Antiscion(double longitude) => AntisciaService.CalculateAntiscion(longitude);

    /// <summary>The contra-antiscion of a longitude, 360° less the longitude, unrounded.</summary>
    /// <param name="longitude">The longitude, in degrees.</param>
    /// <returns>The contra-antiscion, in [0°, 360°).</returns>
    internal static double ContraAntiscion(double longitude) => SpeculumPoint.Normalize360(Antiscion(longitude) + 180.0);

    /// <summary>Builds the speculum from the inputs the two entry points gather.</summary>
    private static Speculum Build(
        double julianDayUt, double latitude, double obliquity, HouseData houses,
        IReadOnlyList<PlanetPosition> positions, double fortune, bool isDay,
        IEphemerisService ephemeris, bool planetsAsSignificators)
    {
        double armc = houses.Armc;

        SpeculumPoint AtLatitudeZero(string name, double longitude)
        {
            (double ra, double dec) = ephemeris.EclipticToEquatorial(SpeculumPoint.Normalize360(longitude), 0.0, obliquity);
            return SpeculumPoint.Build(name, longitude, 0.0, ra, dec, latitude, armc);
        }

        List<SpeculumPromissor> promissors = [];
        foreach (PlanetPosition position in positions)
        {
            if (position.RightAscension is not double ra || position.Declination is not double dec)
                throw new ArgumentException($"{position.Name} carries no right ascension and declination.", nameof(positions));

            string source = position.Name;
            bool isNode = position.Body == Enums.CelestialBodyId.TrueNode;

            promissors.Add(new SpeculumPromissor(
                source, PrimaryDirectionAspect.Conjunction, 0.0, PrimaryDirectionPromissorKind.Body,
                SpeculumPoint.Build(source, position.Longitude, position.Latitude, ra, dec, latitude, armc)));

            foreach ((PrimaryDirectionAspect aspect, double angle) in AspectPoints)
            {
                if (isNode && aspect != PrimaryDirectionAspect.Opposition)
                    continue;

                promissors.Add(new SpeculumPromissor(
                    source, aspect, angle, PrimaryDirectionPromissorKind.Body,
                    AtLatitudeZero(source, position.Longitude + angle)));
            }

            promissors.Add(new SpeculumPromissor(
                source, PrimaryDirectionAspect.Antiscion, 0.0, PrimaryDirectionPromissorKind.Body,
                AtLatitudeZero(source, Antiscion(position.Longitude))));
            promissors.Add(new SpeculumPromissor(
                source, PrimaryDirectionAspect.ContraAntiscion, 0.0, PrimaryDirectionPromissorKind.Body,
                AtLatitudeZero(source, ContraAntiscion(position.Longitude))));
        }

        foreach (FixedStarEntry star in FixedStarCatalog.Stars)
        {
            (double starLongitude, double starLatitude) = ephemeris.CalculateFixedStarPosition(star.SwissEphName, julianDayUt);
            (double ra, double dec) = ephemeris.EclipticToEquatorial(starLongitude, starLatitude, obliquity);
            promissors.Add(new SpeculumPromissor(
                star.DisplayName, PrimaryDirectionAspect.Conjunction, 0.0, PrimaryDirectionPromissorKind.FixedStar,
                SpeculumPoint.Build(star.DisplayName, starLongitude, starLatitude, ra, dec, latitude, armc)));
        }

        List<SpeculumSignificator> significators =
        [
            new("Ascendant", SignificatorKind.Angle, AtLatitudeZero("Ascendant", houses.Ascendant)),
            new("Midheaven", SignificatorKind.Angle, AtLatitudeZero("Midheaven", houses.Midheaven)),
            new("Descendant", SignificatorKind.Angle, AtLatitudeZero("Descendant", houses.Ascendant + 180.0)),
            new("IC", SignificatorKind.Angle, AtLatitudeZero("IC", houses.Midheaven + 180.0)),
        ];

        IEnumerable<string> bodyNames = planetsAsSignificators
            ? ["Sun", "Moon", .. PlanetSignificators]
            : ["Sun", "Moon"];
        foreach (string bodyName in bodyNames)
        {
            PlanetPosition position = positions.First(p => string.Equals(p.Name, bodyName, StringComparison.Ordinal));
            if (position.RightAscension is not double ra || position.Declination is not double dec)
                throw new ArgumentException($"{position.Name} carries no right ascension and declination.", nameof(positions));

            significators.Add(new SpeculumSignificator(
                bodyName, SignificatorKind.Body,
                SpeculumPoint.Build(bodyName, position.Longitude, position.Latitude, ra, dec, latitude, armc)));
        }

        significators.Add(new SpeculumSignificator(PartOfFortuneName, SignificatorKind.Lot, AtLatitudeZero(PartOfFortuneName, fortune)));

        return new Speculum
        {
            JulianDayUt = julianDayUt,
            Latitude = latitude,
            Obliquity = obliquity,
            Armc = armc,
            IsDayChart = isDay,
            PartOfFortune = fortune,
            Promissors = promissors.AsReadOnly(),
            Significators = significators.AsReadOnly(),
        };
    }
}
