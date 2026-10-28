using Serilog;

using AstroCast.Core.Configuration;
using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Interop;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates paran (paranatellonta) events by finding angular crossings for
/// celestial bodies and detecting simultaneities within a configurable time orb.
/// </summary>
/// <remarks>
/// <para>
/// A paran occurs when Planet A crosses one of the four angles (rising, culminating,
/// setting, anti-culminating) at the same moment Planet B crosses a different angle,
/// as seen from a specific geographic location. Unlike ecliptic aspects, parans are
/// inherently place-dependent.
/// </para>
/// <para>
/// The algorithm has two phases:
/// <list type="number">
/// <item><description>
/// Phase 1 (Angular Crossings): For each body, call <c>swe_rise_trans()</c> four
/// times (once per angle type) via <see cref="IEphemerisService.CalculateRiseTransit"/>
/// to find the next rise, culminate, set, and anti-culminate events within one
/// sidereal day. For each crossing, fetch the body's ecliptic longitude at that moment.
/// </description></item>
/// <item><description>
/// Phase 2 (Paran Detection): Sort crossings chronologically. Compare every pair of
/// crossings from different bodies. Two crossings within the orb constitute a paran
/// event. Early exit when sorted time gap exceeds the orb. Deduplicate A-B / B-A by
/// emitting only pairs where BodyAName &lt; BodyBName lexicographically.
/// </description></item>
/// </list>
/// </para>
/// <para>
/// All Swiss Ephemeris access is serialised through <see cref="IEphemerisService"/>
/// which owns the single <see cref="System.Threading.SemaphoreSlim"/> guarding the
/// library's global internal state.
/// </para>
/// </remarks>
public sealed class ParanService : IParanService
{
    /// <summary>One sidereal day in Julian Days (~23h 56m 4s).</summary>
    private const double SiderealDay = 0.99726957;

    /// <summary>Standard atmospheric pressure in mbar.</summary>
    private const double StandardPressure = 1013.25;

    /// <summary>Standard atmospheric temperature in degrees Celsius.</summary>
    private const double StandardTemp = 15.0;

    private readonly IEphemerisService _ephemeris;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="ParanService"/> class.
    /// </summary>
    /// <param name="ephemeris">
    /// Ephemeris service for Swiss Ephemeris calculations. All <c>swe_rise_trans()</c>
    /// calls are delegated to this service to ensure proper thread-safety serialisation.
    /// </param>
    /// <param name="logger">Serilog logger instance.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ephemeris"/> or <paramref name="logger"/> is null.
    /// </exception>
    public ParanService(IEphemerisService ephemeris, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _logger = logger.ForContext<ParanService>();
    }

    /// <inheritdoc />
    public ParanResult Calculate(double julianDayUt, double latitude, double longitude)
    {
        return Calculate(julianDayUt, latitude, longitude, ParanOptions.Default);
    }

    /// <inheritdoc />
    public ParanResult Calculate(double julianDayUt, double latitude, double longitude,
                                 ParanOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _logger.Debug(
            "Calculating parans at JD {JulianDay}, lat={Latitude}, lon={Longitude}, " +
            "orb={Orb} min, bodies={BodyCount}",
            julianDayUt, latitude, longitude, options.OrbMinutes, options.Bodies.Count);

        double endJd = julianDayUt + SiderealDay;

        // Phase 1: Calculate angular crossings for all bodies
        List<AngularCrossing> crossings = CalculateAllCrossings(
            julianDayUt, latitude, longitude, options);

        // Sort crossings chronologically
        crossings.Sort((a, b) => a.JulianDayUt.CompareTo(b.JulianDayUt));

        // Phase 2: Detect paran simultaneities
        List<ParanEvent> parans = DetectParans(crossings, options);

        // Sort parans by orb (tightest first)
        parans.Sort((a, b) => a.OrbMinutes.CompareTo(b.OrbMinutes));

        _logger.Information(
            "Paran calculation complete: {CrossingCount} crossings, {ParanCount} parans detected",
            crossings.Count, parans.Count);

        return new ParanResult
        {
            Latitude = latitude,
            Longitude = longitude,
            StartJulianDay = julianDayUt,
            EndJulianDay = endJd,
            OrbMinutes = options.OrbMinutes,
            Crossings = crossings.AsReadOnly(),
            Parans = parans.AsReadOnly(),
            BodyCount = options.Bodies.Count + options.FixedStars.Count,
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Phase 1: Calculate all angular crossings for all configured bodies.
    /// Each body produces up to 4 crossings (one per angle type).
    /// </summary>
    private List<AngularCrossing> CalculateAllCrossings(
        double julianDayUt, double latitude, double longitude, ParanOptions options)
    {
        List<AngularCrossing> crossings = new(options.Bodies.Count * 4);

        // Planetary body crossings
        foreach (CelestialBodyId body in options.Bodies)
        {
            string bodyName = body.ToString();
            // Normalise "TrueNode" to "True Node"
            if (body == CelestialBodyId.TrueNode)
            {
                bodyName = "True Node";
            }

            CalculateFourCrossings(julianDayUt, body, bodyName, latitude, longitude,
                                   null, crossings);
        }

        // Fixed star crossings
        foreach (string starName in options.FixedStars)
        {
            CalculateFourCrossings(julianDayUt, CelestialBodyId.Sun, starName,
                                   latitude, longitude, starName, crossings);
        }

        return crossings;
    }

    /// <summary>
    /// Calculate the four angular crossings (rise, culminate, set, anti-culminate)
    /// for a single body or fixed star.
    /// </summary>
    private void CalculateFourCrossings(
        double julianDayUt, CelestialBodyId body, string bodyName,
        double latitude, double longitude, string? starName,
        List<AngularCrossing> crossings)
    {
        // Map AngleType to swe_rise_trans rsmi flags
        ReadOnlySpan<(AngleType Angle, int Flag)> angleFlags =
        [
            (AngleType.Rising, SwissEphInterop.SE_CALC_RISE | SwissEphInterop.SE_BIT_DISC_CENTER),
            (AngleType.Setting, SwissEphInterop.SE_CALC_SET | SwissEphInterop.SE_BIT_DISC_CENTER),
            (AngleType.Culminating, SwissEphInterop.SE_CALC_MTRANSIT),
            (AngleType.AntiCulminating, SwissEphInterop.SE_CALC_ITRANSIT),
        ];

        foreach ((AngleType angle, int flag) in angleFlags)
        {
            try
            {
                double? eventJd = _ephemeris.CalculateRiseTransit(
                    julianDayUt, body, flag, latitude, longitude, starName);

                if (eventJd is null)
                {
                    _logger.Debug(
                        "{Body} {Angle} does not occur at lat={Latitude} (circumpolar)",
                        bodyName, angle, latitude);
                    continue;
                }

                // Get the body's ecliptic longitude at the crossing moment
                double crossingLongitude;
                if (starName is not null)
                {
                    crossingLongitude = _ephemeris.CalculateFixedStarLongitude(
                        starName, eventJd.Value);
                }
                else
                {
                    PlanetPosition pos = _ephemeris.CalculatePositionLongitudeOnly(
                        eventJd.Value, body);
                    crossingLongitude = pos.Longitude;
                }

                crossings.Add(new AngularCrossing
                {
                    Body = body,
                    BodyName = bodyName,
                    Angle = angle,
                    JulianDayUt = eventJd.Value,
                    Longitude = crossingLongitude,
                });
            }
            catch (Exception ex)
            {
                _logger.Warning(ex,
                    "Failed to calculate {Angle} for {Body} at JD {JulianDay}: {Message}",
                    angle, bodyName, julianDayUt, ex.Message);
            }
        }
    }

    /// <summary>
    /// Phase 2: Detect paran events by finding pairs of crossings from different
    /// bodies that occur within the configured time orb.
    /// </summary>
    /// <remarks>
    /// Uses a sorted sliding-window approach for efficiency. Since crossings are
    /// sorted chronologically, once the time gap between two crossings exceeds the
    /// orb, all subsequent pairs with the earlier crossing can be skipped.
    /// Deduplication: only emit pairs where BodyAName &lt; BodyBName lexicographically.
    /// </remarks>
    private static List<ParanEvent> DetectParans(
        List<AngularCrossing> crossings, ParanOptions options)
    {
        List<ParanEvent> parans = [];
        double orbJd = options.OrbMinutes / 1440.0; // Convert minutes to Julian Day fraction

        for (int i = 0; i < crossings.Count; i++)
        {
            AngularCrossing a = crossings[i];

            for (int j = i + 1; j < crossings.Count; j++)
            {
                AngularCrossing b = crossings[j];

                double timeDiff = b.JulianDayUt - a.JulianDayUt;

                // Early exit: since crossings are sorted, all subsequent gaps are >= this one
                if (timeDiff > orbJd)
                {
                    break;
                }

                // Skip same-body pairings
                if (a.Body == b.Body && a.BodyName == b.BodyName)
                {
                    continue;
                }

                // Optionally skip same-angle pairings
                if (options.ExcludeSameAngle && a.Angle == b.Angle)
                {
                    continue;
                }

                // Deduplicate: only emit A-B where A.Name < B.Name lexicographically
                int nameOrder = string.Compare(a.BodyName, b.BodyName, StringComparison.Ordinal);
                AngularCrossing first = nameOrder <= 0 ? a : b;
                AngularCrossing second = nameOrder <= 0 ? b : a;

                double orbMinutes = Math.Abs(timeDiff) * 1440.0;

                parans.Add(new ParanEvent
                {
                    BodyA = first.Body,
                    BodyAName = first.BodyName,
                    AngleA = first.Angle,
                    JulianDayA = first.JulianDayUt,
                    LongitudeA = first.Longitude,
                    BodyB = second.Body,
                    BodyBName = second.BodyName,
                    AngleB = second.Angle,
                    JulianDayB = second.JulianDayUt,
                    LongitudeB = second.Longitude,
                    OrbMinutes = orbMinutes,
                });
            }
        }

        return parans;
    }
}
