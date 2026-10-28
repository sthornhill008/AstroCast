using System.Globalization;
using System.Reflection;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates planetary return charts for any traditional planet (Mercury, Venus,
/// Mars, Jupiter, Saturn) by finding the exact UTC moment the transiting planet
/// returns to its natal ecliptic longitude, then casting a full chart at that
/// moment for the specified location.
/// </summary>
/// <remarks>
/// <para>
/// A return is a passage of the planet over its natal longitude, found by
/// <see cref="ReturnPassageFinder"/> from birth, and its instant is the passage's first pass
/// (ruling T-D6, branch F, 1.3.4); the chart is cast there and the passage's other passes, when a
/// retrograde loop lies over the degree, are carried on <see cref="PlanetaryReturnResult.Passes"/>.
/// Return #N is the Nth passage after birth, a count of returns for every planet; by year, the
/// return is the first passage with a pass whose UT date lies in the year, and a year with none
/// throws. Until 1.3.4 the number centred a search on birth + N × the sidereal period (so Mercury's
/// and Venus's numbers counted periods, not returns: T2-2), the chart was cast at the last pass
/// for Jupiter and Saturn (T2-4), and a year's return was the pass nearest 1 July in a window from
/// the July before to the June after (T2-3).
/// </para>
/// <para>
/// The return time is determined by ephemeris computation to sub-second precision.
/// </para>
/// </remarks>
public sealed class PlanetaryReturnService : IPlanetaryReturnService
{
    private readonly IEphemerisService _ephemeris;
    private readonly IChartEngine _chartEngine;
    private readonly ILogger _logger;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// The set of planets for which planetary returns are supported.
    /// </summary>
    private static readonly HashSet<CelestialBodyId> SupportedPlanets =
    [
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
    ];

    /// <summary>
    /// Initialises a new instance of <see cref="PlanetaryReturnService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for planet longitude lookups and JD conversions.</param>
    /// <param name="chartEngine">Chart engine for casting the full return chart.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules whose data version the engine version names. Optional: when
    /// omitted, the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public PlanetaryReturnService(
        IEphemerisService ephemeris,
        IChartEngine chartEngine,
        ILogger logger,
        ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(chartEngine);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemeris = ephemeris;
        _chartEngine = chartEngine;
        _logger = logger.ForContext<PlanetaryReturnService>();
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);
    }

    /// <inheritdoc />
    public PlanetaryReturnResult Calculate(
        ChartResult natalChart,
        CelestialBodyId planet,
        int returnNumber,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);
        ValidatePlanet(planet);

        if (returnNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(returnNumber),
                "Return number must be >= 1.");

        // Step 1: Extract natal planet longitude
        PlanetPosition natalPos = GetNatalPosition(natalChart, planet);
        double natalLon = natalPos.Longitude;

        _logger.Debug(
            "Searching for {Planet} return #{N}: natal {Lon:F4}°, the Nth passage from birth",
            planet, returnNumber, natalLon);

        // Step 2: The Nth passage of the planet over its natal longitude after birth (R1)
        cancellationToken.ThrowIfCancellationRequested();
        ReturnPassage? passage = null;
        ReturnPassage? last = null;
        foreach (ReturnPassage candidate in ReturnPassageFinder.EnumeratePassages(
            _ephemeris, natalChart, planet, cancellationToken))
        {
            last = candidate;
            if (candidate.Number == returnNumber)
            {
                passage = candidate;
                break;
            }
        }

        if (passage is null)
        {
            string lastFound = last is null
                ? "no passage is found before the ephemeris files end"
                : string.Create(CultureInfo.InvariantCulture,
                    $"the last passage before the ephemeris files end is #{last.Number}, at {CalendarDateFormatter.DateTime(JulianDayUtility.ToDateTimeUtc(_ephemeris, last.FirstPass))} UTC");
            throw new InvalidOperationException(
                $"{planet} return #{returnNumber} does not occur before the ephemeris files end: {lastFound}.");
        }

        return BuildResult(natalChart, planet, natalLon, passage,
            latitude, longitude, locationName, timeZone, cancellationToken);
    }

    /// <inheritdoc />
    public PlanetaryReturnResult CalculateForYear(
        ChartResult natalChart,
        CelestialBodyId planet,
        int year,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZone);
        ValidatePlanet(planet);

        PlanetPosition natalPos = GetNatalPosition(natalChart, planet);
        double natalLon = natalPos.Longitude;

        // The first passage with a pass whose UT date lies in the year (R3); the passages before
        // and after it are kept to name them when there is none
        cancellationToken.ThrowIfCancellationRequested();
        ReturnPassage? found = null;
        ReturnPassage? previous = null;
        ReturnPassage? next = null;
        foreach (ReturnPassage candidate in ReturnPassageFinder.EnumeratePassages(
            _ephemeris, natalChart, planet, cancellationToken))
        {
            if (candidate.PassJulianDays.Any(jd => JulianDayUtility.ToDateTimeUtc(_ephemeris, jd).Year == year))
            {
                found = candidate;
                break;
            }

            if (JulianDayUtility.ToDateTimeUtc(_ephemeris, candidate.FirstPass).Year < year)
            {
                previous = candidate;
            }
            else
            {
                next = candidate;
                break;
            }
        }

        if (found is null)
        {
            string Describe(ReturnPassage? passage, string none) => passage is null
                ? none
                : string.Create(CultureInfo.InvariantCulture,
                    $"#{passage.Number} at {CalendarDateFormatter.DateTime(JulianDayUtility.ToDateTimeUtc(_ephemeris, passage.FirstPass))} UTC");
            throw new InvalidOperationException(
                $"No {planet} return has a pass in {year}: the previous passage is " +
                $"{Describe(previous, "none after birth")} and the next is " +
                $"{Describe(next, "none before the ephemeris files end")}.");
        }

        return BuildResult(natalChart, planet, natalLon, found,
            latitude, longitude, locationName, timeZone, cancellationToken);
    }

    /// <summary>
    /// Build the complete <see cref="PlanetaryReturnResult"/> from a passage: the chart is cast at
    /// its first pass and its passes are carried as UTC instants.
    /// </summary>
    private PlanetaryReturnResult BuildResult(
        ChartResult natalChart, CelestialBodyId planet, double natalLon,
        ReturnPassage passage,
        double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken)
    {
        double jdExact = passage.FirstPass;
        int returnNumber = passage.Number;
        DateTime returnUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, jdExact);
        List<DateTime> passes = passage.PassJulianDays
            .Select(jd => JulianDayUtility.ToDateTimeUtc(_ephemeris, jd))
            .ToList();
        string planetName = planet.ToString();

        _logger.Debug(
            "{Planet} return #{N} found at {Utc:yyyy-MM-dd HH:mm:ss} UTC",
            planetName, returnNumber, returnUtc);

        // Cast full chart at the exact return moment
        cancellationToken.ThrowIfCancellationRequested();

        ChartInput returnInput = new()
        {
            Name = string.Create(CultureInfo.InvariantCulture, $"{planetName} Return {natalChart.Input.Name} #{returnNumber}"),
            LocalDateTime = returnUtc,
            TimeZone = timeZone,
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = latitude,
            Longitude = longitude,
            LocationName = locationName,
            EventType = $"{planetName} Return",
            // The return chart carries its own five when the natal chart does (branch Asteroids, 1.6.0).
            IncludeMinorPoints = natalChart.Input.IncludeMinorPoints,
        };

        // At the return's Julian Day: returnUtc is only the label the chart prints (D4).
        ChartResult returnChart = _chartEngine.CalculateAtInstant(returnInput, jdExact, cancellationToken);

        // Verify precision
        PlanetPosition? returnPos = null;
        foreach (PlanetPosition p in returnChart.Positions)
        {
            if (p.Body == planet)
            {
                returnPos = p;
                break;
            }
        }

        double returnLon = returnPos?.Longitude ?? 0.0;
        double precisionDegrees = Math.Abs(
            ReturnChartFinder.SignedLongitudeDelta(returnLon, natalLon));
        double precisionArcSeconds = precisionDegrees * 3600.0;

        // The age at the return: whole years from the birth instant to the return's, both UTC
        // (R4; until 1.3.4 the local birth date was compared with the UT return date, T2-7)
        DateTime birthUtc = natalChart.UtcDateTime;
        int ageAtReturn = returnUtc.Year - birthUtc.Year;
        if ((returnUtc.Month, returnUtc.Day, returnUtc.TimeOfDay)
            .CompareTo((birthUtc.Month, birthUtc.Day, birthUtc.TimeOfDay)) < 0)
        {
            ageAtReturn--;
        }

        // Information-level stays PII-free (M-6); the subject name is Debug-only.
        _logger.Information(
            "{Planet} return #{N}: natal {NatLon:F4}°, return {RetLon:F4}°, " +
            "precision {Prec:F4}\" at {Utc:yyyy-MM-dd HH:mm:ss} UTC (age {Age})",
            planetName, returnNumber, natalLon, returnLon,
            precisionArcSeconds, returnUtc, ageAtReturn);
        _logger.Debug(
            "{Planet} return #{N} for '{Name}' computed",
            planetName, returnNumber, natalChart.Input.Name);

        string engineVersion = BuildVersionString();

        return new PlanetaryReturnResult
        {
            NatalChart = natalChart,
            ReturnChart = returnChart,
            Planet = planet,
            PlanetName = planetName,
            NatalLongitude = natalLon,
            ReturnLongitude = returnLon,
            ReturnNumber = returnNumber,
            AgeAtReturn = ageAtReturn,
            Passes = passes.AsReadOnly(),
            ReturnLocationName = locationName,
            ReturnLatitude = latitude,
            ReturnLongitude_Geo = longitude,
            PrecisionArcSeconds = precisionArcSeconds,
            EngineVersion = engineVersion,
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Validates that the planet is one of the supported traditional planets.
    /// </summary>
    /// <param name="planet">The planet to validate.</param>
    /// <exception cref="ArgumentException">Thrown if the planet is not supported.</exception>
    private static void ValidatePlanet(CelestialBodyId planet)
    {
        if (!SupportedPlanets.Contains(planet))
            throw new ArgumentException(
                $"Planetary returns are only supported for Mercury, Venus, Mars, Jupiter, " +
                $"and Saturn. Got: {planet}",
                nameof(planet));
    }

    /// <summary>
    /// Extracts the natal position for the specified planet from the chart result.
    /// </summary>
    /// <param name="chart">The natal chart result.</param>
    /// <param name="planet">The planet to find.</param>
    /// <returns>The natal planet position.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the planet is not in the chart.</exception>
    private static PlanetPosition GetNatalPosition(ChartResult chart, CelestialBodyId planet)
    {
        foreach (PlanetPosition p in chart.Positions)
        {
            if (p.Body == planet)
                return p;
        }

        throw new InvalidOperationException(
            $"Natal chart does not contain a {planet} position.");
    }

    /// <summary>
    /// Build the engine version string from assembly metadata.
    /// </summary>
    private string BuildVersionString()
    {
        string asmVersion = typeof(PlanetaryReturnService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0)
            asmVersion = asmVersion[..plusIndex];

        string sweVersion = _ephemeris.Version;
        return $"AstroCast {asmVersion} (SwissEph {sweVersion}, tzdb {_timeZoneRules.DataVersion})";
    }
}
