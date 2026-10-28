using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates secondary progressions: positions, Moon info, sign changes,
/// stations, and progressed-to-natal aspects.
/// </summary>
/// <remarks>
/// <para>
/// Secondary progressions use the day-for-a-year method: planetary positions
/// N days after birth represent the progressed chart at age N years.
/// </para>
/// <para>
/// The service calculates progressed positions for Sun, Moon, Mercury, Venus and Mars (from the
/// ephemeris at the progressed instant), and the Ascendant and Midheaven by solar arc (ruling
/// T-D1, branch G, 1.3.4): the progressed Midheaven is the natal Midheaven plus the solar arc, and
/// the progressed Ascendant is the Ascendant for that Midheaven at the birth latitude with the
/// natal true obliquity. The angles' speeds per progressed day are the progressed Sun's for the
/// Midheaven and that times dASC/dMC for the Ascendant, and their sign changes are found on the
/// functions MC(t) and ASC(t) of the progressed Julian Day as the bodies' are. Until 1.3.4 the
/// angles were the quotidian ones, the chart's for the progressed instant at the birthplace, which
/// turn through the whole zodiac in a year: their aspects were always "applying" and their sign
/// changes were missed or wrongly signed (audit finding T3-1).
/// </para>
/// </remarks>
public sealed class ProgressionService : IProgressionService
{
    private readonly IEphemerisService _ephemeris;

    /// <summary>
    /// The 5 celestial bodies tracked in secondary progressions.
    /// Outer planets move too slowly in progressed time to be meaningful.
    /// </summary>
    private static readonly (CelestialBodyId Id, string Name)[] s_progressedBodies =
    [
        (CelestialBodyId.Sun, "Sun"),
        (CelestialBodyId.Moon, "Moon"),
        (CelestialBodyId.Mercury, "Mercury"),
        (CelestialBodyId.Venus, "Venus"),
        (CelestialBodyId.Mars, "Mars"),
    ];

    /// <summary>
    /// Major aspect definitions for progressed-to-natal aspect calculation.
    /// Orb: 1° for all progressed aspects.
    /// </summary>
    private static readonly (string Name, string Abbreviation, double Angle)[] s_majorAspects =
    [
        ("Conjunction", "cnj", 0.0),
        ("Opposition", "opp", 180.0),
        ("Trine", "tri", 120.0),
        ("Square", "sqr", 90.0),
        ("Sextile", "sxt", 60.0),
    ];

    /// <summary>Maximum orb for progressed-to-natal aspects in degrees.</summary>
    private const double ProgressedAspectOrb = 1.0;

    /// <summary>
    /// The real-time days after the report's midpoint over which the progressed Moon is followed
    /// for its next sign change. The transit export reads it to say how far it looked when none
    /// falls within them (E2-3).
    /// </summary>
    internal const int NextSignChangeScanDays = 90;

    /// <summary>The step of the progressed Moon's sign-change scan, in progressed days.</summary>
    private const double MoonScanStepDays = 0.01;

    /// <summary>
    /// The real-time days either side of the report's midpoint over which Mercury, Venus and Mars
    /// are followed for a progressed station.
    /// </summary>
    private const int StationScanRealDays = 365;

    /// <summary>The step of the progressed station scan, in progressed days.</summary>
    private const double StationScanStepDays = 0.05;

    /// <summary>
    /// The 8 progressed lunar phase names based on Sun-Moon angle.
    /// </summary>
    private static readonly (double MinAngle, double MaxAngle, string Name)[] s_lunarPhases =
    [
        (0.0, 45.0, "New Moon"),
        (45.0, 90.0, "Crescent"),
        (90.0, 135.0, "First Quarter"),
        (135.0, 180.0, "Gibbous"),
        (180.0, 225.0, "Full Moon"),
        (225.0, 270.0, "Disseminating"),
        (270.0, 315.0, "Last Quarter"),
        (315.0, 360.0, "Balsamic"),
    ];

    /// <summary>
    /// Initialises a new instance of <see cref="ProgressionService"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for all Swiss Ephemeris calculations.</param>
    public ProgressionService(IEphemerisService ephemeris)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
    }

    /// <inheritdoc />
    /// <summary>
    /// Calculates complete progression data for a natal chart at a reference day: the report's start
    /// since branch Modes (1.4.0), its midpoint before.
    /// </summary>
    /// <param name="natalChart">The natal <see cref="ChartResult"/>.</param>
    /// <param name="reportMidpointDate">
    /// The reference day, a proleptic day (the scan's start, read through
    /// <see cref="Formatting.CalendarDateFormatter.ProlepticDay"/>; branch R3, 1.3.4), converted by
    /// arithmetic, never a typed date. The Moon's next sign change, the sign changes and the stations are sought from it.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the calculation.</param>
    /// <returns>Complete <see cref="ProgressionResult"/>.</returns>
    public ProgressionResult Calculate(ChartResult natalChart, DateOnly reportMidpointDate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);

        // Step 1: Calculate the progressed instant, from the natal Julian Day (branch R2, 1.3.4)
        (DateTime progressedDate, double progressedJd, double ageInYears) =
            ProgressedDateCalculator.Calculate(natalChart.JulianDay, reportMidpointDate);

        // Step 2: Calculate progressed planet positions
        List<ProgressedPosition> positions = [];
        Dictionary<string, double> progressedLongitudes = [];
        Dictionary<string, PlanetPosition> progressedRawPositions = [];

        foreach ((CelestialBodyId bodyId, string name) in s_progressedBodies)
        {
            PlanetPosition natalPos = FindNatalPosition(natalChart, name);
            PlanetPosition progPos = _ephemeris.CalculatePosition(progressedJd, bodyId);
            progressedRawPositions[name] = progPos;

            double progLon = progPos.Longitude;
            progressedLongitudes[name] = progLon;

            int natalHouse = HousePlacement.HouseOf(progLon, natalChart.Houses.Cusps) ?? 1;

            positions.Add(BuildProgressedPosition(name, natalPos, progPos, natalHouse));
        }

        // Step 3: Calculate solar arc value
        double natalSunLon = FindNatalPosition(natalChart, "Sun").Longitude;
        double progSunLon = progressedLongitudes["Sun"];
        double solarArc = NormaliseAngle(progSunLon - natalSunLon);

        // Step 4: The progressed angles by solar arc (T-D1): the natal Midheaven plus the arc, and
        // the Ascendant for that Midheaven at the birth latitude with the natal true obliquity
        double natalAsc = natalChart.Houses.Ascendant;
        double natalMc = natalChart.Houses.Midheaven;
        double progMc = NormaliseAngle(natalMc + solarArc);
        double progAsc = AscendantForMidheaven(progMc, natalChart);
        progressedLongitudes["ASC"] = progAsc;
        progressedLongitudes["MC"] = progMc;

        positions.Add(BuildAnglePosition("ASC", natalAsc, progAsc));
        positions.Add(BuildAnglePosition("MC", natalMc, progMc));

        // The angles' speeds per progressed day (T3-1): the Midheaven's is the progressed Sun's,
        // the Ascendant's that times dASC/dMC at the progressed Midheaven
        double mcSpeed = progressedRawPositions["Sun"].SpeedInLongitude;
        Dictionary<string, double> angleSpeeds = new(StringComparer.Ordinal)
        {
            ["MC"] = mcSpeed,
            ["ASC"] = mcSpeed * AscendantRateAtMidheaven(progMc, natalChart),
        };

        // Step 5: Calculate progressed Moon info
        cancellationToken.ThrowIfCancellationRequested();
        ProgressedMoonInfo moonInfo = BuildMoonInfo(
            progressedLongitudes["Moon"],
            progressedLongitudes["Sun"],
            natalChart, progressedJd, reportMidpointDate);

        // Step 6: Scan for progressed sign changes (within 6 months)
        cancellationToken.ThrowIfCancellationRequested();
        List<ProgressedSignChange> signChanges = ScanSignChanges(
            natalChart, reportMidpointDate, progressedJd);

        // Step 7: Scan for progressed stations (within 1 year)
        cancellationToken.ThrowIfCancellationRequested();
        List<ProgressedStation> stations = ScanStations(
            natalChart, progressedJd, reportMidpointDate);

        // Step 8: Calculate progressed-to-natal aspects: to the 12 planets, the Ascendant and the Midheaven,
        // then to the natal Ceres, Pallas, Juno, Vesta and Lilith as targets of their own when the chart
        // carries them (R7, branch AsteroidsTiming, 1.6.0); the five are never progressed
        List<ProgressedAspect> aspects = CalculateAspects(
            positions, NatalTargets(natalChart), progressedJd, angleSpeeds);
        List<ProgressedAspect> minorPointAspects = CalculateAspects(
            positions, MinorPointTargets(natalChart), progressedJd, angleSpeeds);

        // Step 9: Assemble result
        return new ProgressionResult
        {
            ProgressedDate = progressedDate,
            ProgressedJulianDay = progressedJd,
            SolarArcValue = solarArc,
            Positions = positions.AsReadOnly(),
            MoonInfo = moonInfo,
            SignChanges = signChanges.AsReadOnly(),
            Stations = stations.AsReadOnly(),
            Aspects = aspects.AsReadOnly(),
            MinorPointAspects = minorPointAspects.AsReadOnly(),
        };
    }

    #region Angles (T-D1)

    /// <summary>
    /// The step on the Midheaven, in degrees, of the forward difference that gives dASC/dMC.
    /// </summary>
    private const double AscendantRateStepDegrees = 0.001;

    /// <summary>
    /// The Ascendant for a Midheaven at the chart's birth latitude with its natal true obliquity
    /// (<see cref="ChartResult.Obliquity"/>): the Midheaven's right ascension,
    /// RAMC = atan2(sin MC · cos ε, cos MC) in [0°, 360°), then the Ascendant the ephemeris casts
    /// for that sidereal time (<see cref="IEphemerisService.CalculateHousesFromArmc"/> with Equal
    /// houses, which no latitude refuses; the Ascendant does not depend on the house system).
    /// </summary>
    /// <param name="midheaven">The Midheaven's ecliptic longitude, in degrees.</param>
    /// <param name="natalChart">The natal chart, for its birth latitude and true obliquity.</param>
    /// <returns>The Ascendant's ecliptic longitude, in degrees.</returns>
    internal double AscendantForMidheaven(double midheaven, ChartResult natalChart)
    {
        double obliquity = natalChart.Obliquity;
        double mcRadians = midheaven * Math.PI / 180.0;
        double obliquityRadians = obliquity * Math.PI / 180.0;
        double ramc = NormaliseAngle(
            Math.Atan2(Math.Sin(mcRadians) * Math.Cos(obliquityRadians), Math.Cos(mcRadians)) * 180.0 / Math.PI);

        return _ephemeris.CalculateHousesFromArmc(ramc, natalChart.Input.Latitude, obliquity, 'E').Ascendant;
    }

    /// <summary>
    /// dASC/dMC at a Midheaven: the forward difference of <see cref="AscendantRateStepDegrees"/> on
    /// the Midheaven through <see cref="AscendantForMidheaven"/>.
    /// </summary>
    /// <param name="midheaven">The Midheaven's ecliptic longitude, in degrees.</param>
    /// <param name="natalChart">The natal chart, for its birth latitude and true obliquity.</param>
    /// <returns>The Ascendant's rate per degree of Midheaven.</returns>
    private double AscendantRateAtMidheaven(double midheaven, ChartResult natalChart)
    {
        double ascendant = AscendantForMidheaven(midheaven, natalChart);
        double ascendantAhead = AscendantForMidheaven(
            NormaliseAngle(midheaven + AscendantRateStepDegrees), natalChart);

        return WrapSigned(ascendantAhead - ascendant) / AscendantRateStepDegrees;
    }

    #endregion

    #region Position Helpers

    /// <summary>
    /// Builds a <see cref="ProgressedPosition"/> for a celestial body.
    /// </summary>
    private static ProgressedPosition BuildProgressedPosition(
        string name, PlanetPosition natalPos, PlanetPosition progPos, int natalHouse)
    {
        double movement = NormaliseAngle(progPos.Longitude - natalPos.Longitude);

        return new ProgressedPosition
        {
            Name = name,
            NatalLongitude = natalPos.Longitude,
            NatalFormattedPosition = FormatLongitude(natalPos.Longitude),
            ProgressedLongitude = progPos.Longitude,
            ProgressedSign = TransitEphemerisHelper.LongitudeToSign(progPos.Longitude),
            ProgressedDegrees = TransitEphemerisHelper.LongitudeToDegrees(progPos.Longitude),
            ProgressedMinutes = TransitEphemerisHelper.LongitudeToMinutes(progPos.Longitude),
            ProgressedFormattedPosition = FormatLongitude(progPos.Longitude),
            NatalHouse = natalHouse,
            MovementFromNatal = movement,
            IsRetrograde = progPos.IsRetrograde,
            DirectionCode = progPos.DirectionCode,
        };
    }

    /// <summary>
    /// Builds a <see cref="ProgressedPosition"/> for an angle (ASC or MC).
    /// </summary>
    private static ProgressedPosition BuildAnglePosition(
        string name, double natalLon, double progressedLon)
    {
        double movement = NormaliseAngle(progressedLon - natalLon);

        return new ProgressedPosition
        {
            Name = name,
            NatalLongitude = natalLon,
            NatalFormattedPosition = FormatLongitude(natalLon),
            ProgressedLongitude = progressedLon,
            ProgressedSign = TransitEphemerisHelper.LongitudeToSign(progressedLon),
            ProgressedDegrees = TransitEphemerisHelper.LongitudeToDegrees(progressedLon),
            ProgressedMinutes = TransitEphemerisHelper.LongitudeToMinutes(progressedLon),
            ProgressedFormattedPosition = FormatLongitude(progressedLon),
            NatalHouse = null,
            MovementFromNatal = movement,
            IsRetrograde = null,
            DirectionCode = null,
        };
    }

    /// <summary>
    /// Finds a natal planet position by name.
    /// </summary>
    private static PlanetPosition FindNatalPosition(ChartResult natalChart, string name)
    {
        foreach (PlanetPosition pos in natalChart.Positions)
        {
            if (string.Equals(pos.Name, name, StringComparison.OrdinalIgnoreCase))
                return pos;
        }

        // Handle "True Node" → "North Node" naming
        if (string.Equals(name, "North Node", StringComparison.OrdinalIgnoreCase))
        {
            foreach (PlanetPosition pos in natalChart.Positions)
            {
                if (string.Equals(pos.Name, "True Node", StringComparison.OrdinalIgnoreCase))
                    return pos;
            }
        }

        throw new InvalidOperationException($"Natal position for '{name}' not found.");
    }

    #endregion

    #region Moon Info

    /// <summary>
    /// Builds the detailed progressed Moon information.
    /// </summary>
    private ProgressedMoonInfo BuildMoonInfo(
        double moonLon, double sunLon,
        ChartResult natalChart, double progressedJd, DateOnly reportMidpointDate)
    {
        int natalHouse = HousePlacement.HouseOf(moonLon, natalChart.Houses.Cusps) ?? 1;

        // Progressed lunar phase: angle from progressed Sun to progressed Moon
        double phaseAngle = NormaliseAngle(moonLon - sunLon);
        string phaseName = GetLunarPhaseName(phaseAngle);

        // Scan for the next progressed Moon sign change (branch G, 1.3.4): 0.01 progressed day
        // (about 3.65 real days) at a step from the progressed instant to the bound,
        // NextSignChangeScanDays real days ahead, inclusive, the last step the bound itself; the
        // first change found is bisected and dated by its instant. The progressed Moon moves
        // about 13° a progressed day, so no step holds two changes.
        string currentSign = TransitEphemerisHelper.LongitudeToSign(moonLon);
        DateOnly? nextSignChangeDate = null;
        string? nextSign = null;

        double bound = ProgressedDateCalculator.RealDaysToProgressedDays(NextSignChangeScanDays);
        double previousOffset = 0.0;

        for (int i = 1; ; i++)
        {
            double offset = i * MoonScanStepDays;
            bool last = offset >= bound;
            if (last)
                offset = bound;

            PlanetPosition moonPos = _ephemeris.CalculatePosition(progressedJd + offset, CelestialBodyId.Moon);
            string scanSign = TransitEphemerisHelper.LongitudeToSign(moonPos.Longitude);

            if (scanSign != currentSign)
            {
                double crossoverJd = BinarySearchSignChange(
                    CelestialBodyId.Moon, progressedJd + previousOffset, progressedJd + offset, currentSign);
                nextSignChangeDate = DateOfProgressedOffset(reportMidpointDate, crossoverJd - progressedJd);
                nextSign = scanSign;
                break;
            }

            previousOffset = offset;
            if (last)
                break;
        }

        return new ProgressedMoonInfo
        {
            Longitude = moonLon,
            Sign = TransitEphemerisHelper.LongitudeToSign(moonLon),
            Degrees = TransitEphemerisHelper.LongitudeToDegrees(moonLon),
            Minutes = TransitEphemerisHelper.LongitudeToMinutes(moonLon),
            NatalHouse = natalHouse,
            PhaseName = phaseName,
            PhaseAngle = phaseAngle,
            NextSignChangeDate = nextSignChangeDate,
            NextSign = nextSign,
        };
    }

    /// <summary>
    /// Maps a Sun-Moon phase angle (0-360) to one of the 8 lunar phase names.
    /// </summary>
    private static string GetLunarPhaseName(double phaseAngle)
    {
        double normalised = NormaliseAngle(phaseAngle);

        foreach ((double min, double max, string name) in s_lunarPhases)
        {
            if (normalised >= min && normalised < max)
                return name;
        }

        // Edge case: exactly 360° wraps to New Moon
        return "New Moon";
    }

    #endregion

    #region Sign Changes

    /// <summary>
    /// Scans for progressed sign changes within 6 months (182 real days) either side of the report
    /// midpoint: for each body and each directed angle, the sign at the two ends of the window,
    /// then a binary search between them, each change dated by its instant (branch G, 1.3.4).
    /// </summary>
    private List<ProgressedSignChange> ScanSignChanges(
        ChartResult natalChart, DateOnly reportMidpointDate, double midpointJd)
    {
        List<ProgressedSignChange> changes = [];

        // 6 months = ~182 real days → ~0.498 progressed days
        double halfYearProgressedDays = ProgressedDateCalculator.RealDaysToProgressedDays(182);

        // Check each progressed body
        foreach ((CelestialBodyId bodyId, string name) in s_progressedBodies)
        {
            double startJd = midpointJd - halfYearProgressedDays;
            double endJd = midpointJd + halfYearProgressedDays;

            PlanetPosition startPos = _ephemeris.CalculatePosition(startJd, bodyId);
            PlanetPosition endPos = _ephemeris.CalculatePosition(endJd, bodyId);

            string startSign = TransitEphemerisHelper.LongitudeToSign(startPos.Longitude);
            string endSign = TransitEphemerisHelper.LongitudeToSign(endPos.Longitude);

            if (startSign != endSign)
            {
                // Binary search for the crossover date
                double crossoverJd = BinarySearchSignChange(bodyId, startJd, endJd, startSign);
                DateOnly changeDate = DateOfProgressedOffset(reportMidpointDate, crossoverJd - midpointJd);

                changes.Add(new ProgressedSignChange
                {
                    BodyName = name,
                    FromSign = startSign,
                    ToSign = endSign,
                    Date = changeDate,
                });
            }
        }

        // Check the directed angles (T-D1) on the functions MC(t) and ASC(t) of the progressed
        // Julian Day; both are monotonic, the arc growing about a degree a year, so one change at
        // most lies in the window
        double natalSunLon = FindNatalPosition(natalChart, "Sun").Longitude;
        double natalMc = natalChart.Houses.Midheaven;
        double MidheavenAt(double jd) => NormaliseAngle(
            natalMc + NormaliseAngle(_ephemeris.CalculatePosition(jd, CelestialBodyId.Sun).Longitude - natalSunLon));
        double AscendantAt(double jd) => AscendantForMidheaven(MidheavenAt(jd), natalChart);

        ScanAngleSignChange(changes, midpointJd, halfYearProgressedDays, reportMidpointDate, "ASC", AscendantAt);
        ScanAngleSignChange(changes, midpointJd, halfYearProgressedDays, reportMidpointDate, "MC", MidheavenAt);

        return changes;
    }

    /// <summary>
    /// Scans for a progressed angle (ASC or MC) sign change by the bodies' method: the angle's
    /// sign at the two ends of the window, then a binary search between them.
    /// </summary>
    /// <param name="changes">The list to add the change to.</param>
    /// <param name="midpointJd">The progressed Julian Day of the report midpoint.</param>
    /// <param name="halfYearProgressedDays">Half the window, in progressed days.</param>
    /// <param name="reportMidpointDate">The report midpoint date.</param>
    /// <param name="angleName">"ASC" or "MC".</param>
    /// <param name="angleAt">The angle's longitude as a function of the progressed Julian Day.</param>
    private void ScanAngleSignChange(
        List<ProgressedSignChange> changes,
        double midpointJd,
        double halfYearProgressedDays,
        DateOnly reportMidpointDate,
        string angleName,
        Func<double, double> angleAt)
    {
        double startJd = midpointJd - halfYearProgressedDays;
        double endJd = midpointJd + halfYearProgressedDays;

        string startSign = TransitEphemerisHelper.LongitudeToSign(angleAt(startJd));
        string endSign = TransitEphemerisHelper.LongitudeToSign(angleAt(endJd));

        if (startSign != endSign)
        {
            // Binary search for angle sign change
            double crossoverJd = BinarySearchAngleSignChange(angleAt, startJd, endJd, startSign);
            DateOnly changeDate = DateOfProgressedOffset(reportMidpointDate, crossoverJd - midpointJd);

            changes.Add(new ProgressedSignChange
            {
                BodyName = angleName,
                FromSign = startSign,
                ToSign = endSign,
                Date = changeDate,
            });
        }
    }

    /// <summary>
    /// Binary search for when a body changes sign between two Julian Day values.
    /// </summary>
    private double BinarySearchSignChange(
        CelestialBodyId bodyId, double startJd, double endJd, string startSign)
    {
        for (int i = 0; i < 30; i++) // ~30 iterations gives sub-minute precision
        {
            double midJd = (startJd + endJd) / 2.0;
            PlanetPosition midPos = _ephemeris.CalculatePosition(midJd, bodyId);
            string midSign = TransitEphemerisHelper.LongitudeToSign(midPos.Longitude);

            if (midSign == startSign)
                startJd = midJd;
            else
                endJd = midJd;
        }

        return (startJd + endJd) / 2.0;
    }

    /// <summary>
    /// Binary search for when a directed angle changes sign between two Julian Day values.
    /// </summary>
    /// <param name="angleAt">The angle's longitude as a function of the progressed Julian Day.</param>
    /// <param name="startJd">The window's start, in the sign <paramref name="startSign"/>.</param>
    /// <param name="endJd">The window's end, in the other sign.</param>
    /// <param name="startSign">The sign at <paramref name="startJd"/>.</param>
    /// <returns>The progressed Julian Day of the change.</returns>
    private static double BinarySearchAngleSignChange(
        Func<double, double> angleAt, double startJd, double endJd, string startSign)
    {
        for (int i = 0; i < 30; i++)
        {
            double midJd = (startJd + endJd) / 2.0;
            string midSign = TransitEphemerisHelper.LongitudeToSign(angleAt(midJd));

            if (midSign == startSign)
                startJd = midJd;
            else
                endJd = midJd;
        }

        return (startJd + endJd) / 2.0;
    }

    #endregion

    #region Stations

    /// <summary>
    /// Scans for progressed stations of Mercury, Venus and Mars, the only progressed bodies that
    /// station, within <see cref="StationScanRealDays"/> real days either side of the report's
    /// midpoint (branch G, 1.3.4): <see cref="StationScanStepDays"/> progressed day at a step from
    /// the window's start to its end inclusive, the last step the end itself; each change of the
    /// speed's sign between two samples is bisected on the speed in longitude, its type from the
    /// change's direction (positive to negative is Retrograde), and dated by its instant. Until
    /// 1.3.4 the scan stopped a step short of the end and reported the step's end, 0 to 19 days
    /// late (audit finding T3-2).
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <param name="progressedJd">The progressed Julian Day of the report midpoint.</param>
    /// <param name="reportMidpointDate">The report midpoint date.</param>
    /// <returns>The stations found, in body order then time order.</returns>
    private List<ProgressedStation> ScanStations(
        ChartResult natalChart, double progressedJd, DateOnly reportMidpointDate)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        List<ProgressedStation> stations = [];

        double bound = ProgressedDateCalculator.RealDaysToProgressedDays(StationScanRealDays);

        // Only scan Mercury, Venus, Mars for stations
        (CelestialBodyId Id, string Name)[] stationBodies =
        [
            (CelestialBodyId.Mercury, "Mercury"),
            (CelestialBodyId.Venus, "Venus"),
            (CelestialBodyId.Mars, "Mars"),
        ];

        foreach ((CelestialBodyId bodyId, string name) in stationBodies)
        {
            double previousOffset = -bound;
            double previousSpeed = _ephemeris.CalculatePosition(progressedJd + previousOffset, bodyId).SpeedInLongitude;

            for (int i = 1; ; i++)
            {
                double offset = -bound + i * StationScanStepDays;
                bool last = offset >= bound;
                if (last)
                    offset = bound;

                double speed = _ephemeris.CalculatePosition(progressedJd + offset, bodyId).SpeedInLongitude;

                // Detect speed sign change (station)
                if ((previousSpeed > 0 && speed <= 0) || (previousSpeed < 0 && speed >= 0))
                {
                    double stationJd = BinarySearchStation(
                        bodyId, progressedJd + previousOffset, progressedJd + offset, previousSpeed);

                    stations.Add(new ProgressedStation
                    {
                        BodyName = name,
                        StationType = previousSpeed > 0 ? "Retrograde" : "Direct",
                        Date = DateOfProgressedOffset(reportMidpointDate, stationJd - progressedJd),
                    });
                }

                previousSpeed = speed;
                previousOffset = offset;
                if (last)
                    break;
            }
        }

        return stations;
    }

    /// <summary>
    /// Binary search for the instant a body's speed in longitude crosses zero between two
    /// progressed Julian Day values, 30 halvings.
    /// </summary>
    /// <param name="bodyId">The body.</param>
    /// <param name="startJd">The sample before the change, whose speed has the sign of <paramref name="startSpeed"/>.</param>
    /// <param name="endJd">The sample after the change.</param>
    /// <param name="startSpeed">The speed at <paramref name="startJd"/>.</param>
    /// <returns>The progressed Julian Day of the station.</returns>
    private double BinarySearchStation(CelestialBodyId bodyId, double startJd, double endJd, double startSpeed)
    {
        int startSign = Math.Sign(startSpeed);
        for (int i = 0; i < 30; i++)
        {
            double midJd = (startJd + endJd) / 2.0;
            double midSpeed = _ephemeris.CalculatePosition(midJd, bodyId).SpeedInLongitude;

            if (Math.Sign(midSpeed) == startSign)
                startJd = midJd;
            else
                endJd = midJd;
        }

        return (startJd + endJd) / 2.0;
    }

    /// <summary>
    /// The UT date of the real instant of a progressed offset (T3-2, branch G, 1.3.4): the report's
    /// midpoint day at 12:00 UT plus the offset times 365.25 real days. The midpoint is a proleptic day
    /// derived from the scan (branch R3, 1.3.4), so its noon is its own, and the date is a
    /// <see cref="DateOnly"/> derived from an instant, printed through
    /// <see cref="Formatting.CalendarDateFormatter"/>.
    /// </summary>
    /// <param name="reportMidpointDate">The report midpoint, a proleptic day.</param>
    /// <param name="progressedDayOffset">The offset from the progressed instant, in progressed days.</param>
    /// <returns>The UT date of the instant.</returns>
    private static DateOnly DateOfProgressedOffset(DateOnly reportMidpointDate, double progressedDayOffset)
    {
        DateTime midpointNoonUtc = reportMidpointDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        double realDays = ProgressedDateCalculator.ProgressedDaysToRealDays(progressedDayOffset);
        return DateOnly.FromDateTime(midpointNoonUtc.AddDays(realDays));
    }

    #endregion

    #region Aspects

    /// <summary>
    /// The natal targets of the progressed-to-natal aspects: the 12 planets, the Ascendant and the Midheaven.
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <returns>The 14 targets' names and longitudes.</returns>
    private static List<(string Name, double Longitude)> NatalTargets(ChartResult natalChart)
    {
        List<(string Name, double Longitude)> natalTargets = [];
        foreach (PlanetPosition pos in natalChart.Positions)
        {
            natalTargets.Add((pos.Name, pos.Longitude));
        }
        natalTargets.Add(("ASC", natalChart.Houses.Ascendant));
        natalTargets.Add(("MC", natalChart.Houses.Midheaven));

        return natalTargets;
    }

    /// <summary>
    /// The natal Ceres, Pallas, Juno, Vesta and Lilith as targets (R7, branch AsteroidsTiming, 1.6.0), in
    /// <see cref="ChartResult.MinorPoints"/>' order; none when the chart does not carry them.
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <returns>The five's names and longitudes, or none.</returns>
    private static List<(string Name, double Longitude)> MinorPointTargets(ChartResult natalChart) =>
        natalChart.MinorPoints.Select(p => (p.Name, p.Longitude)).ToList();

    /// <summary>
    /// Calculates progressed-to-natal aspects (1° orb, major aspects only) from the progressed points to a
    /// list of natal targets: the chart's 14 (<see cref="NatalTargets"/>), or the five minor points
    /// (<see cref="MinorPointTargets"/>).
    /// </summary>
    /// <param name="progressedPositions">The seven progressed positions.</param>
    /// <param name="natalTargets">The natal targets' names and longitudes.</param>
    /// <param name="progressedJd">The progressed Julian Day.</param>
    /// <param name="angleSpeeds">The Ascendant's and Midheaven's speeds per progressed day, by name.</param>
    /// <returns>The aspects within orb.</returns>
    private List<ProgressedAspect> CalculateAspects(
        List<ProgressedPosition> progressedPositions,
        IReadOnlyList<(string Name, double Longitude)> natalTargets,
        double progressedJd,
        IReadOnlyDictionary<string, double> angleSpeeds)
    {
        List<ProgressedAspect> aspects = [];

        // Check each progressed body against each natal target
        foreach (ProgressedPosition progPos in progressedPositions)
        {
            foreach ((string natalName, double natalLon) in natalTargets)
            {
                // A body may aspect its own natal place (the progressed Moon's return to the natal Moon;
                // branch Modes, 1.4.0, as the dated hits list it); the angles skip theirs, the progressed
                // Midheaven being the solar arc Midheaven, whose aspect to itself is the arc. Until 1.4.0
                // every point skipped its own place.
                if (progPos.IsRetrograde is null && string.Equals(progPos.Name, natalName, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach ((string aspectName, string abbr, double aspectAngle) in s_majorAspects)
                {
                    double separation = Math.Abs(
                        NormaliseAngle180(progPos.ProgressedLongitude - natalLon) - aspectAngle);

                    // Also check supplementary angle for non-conjunction aspects
                    if (aspectAngle > 0)
                    {
                        double altSeparation = Math.Abs(
                            NormaliseAngle180(progPos.ProgressedLongitude - natalLon) + aspectAngle);
                        separation = Math.Min(separation, altSeparation);
                    }

                    // Calculate actual orb more precisely
                    double angleDiff = NormaliseAngle(progPos.ProgressedLongitude - natalLon);
                    double orb = CalculateAspectOrb(angleDiff, aspectAngle);

                    if (orb <= ProgressedAspectOrb)
                    {
                        // Determine direction (applying/separating)
                        string direction = DetermineAspectDirection(
                            progPos, progressedJd, natalLon, aspectAngle, angleSpeeds);

                        aspects.Add(new ProgressedAspect
                        {
                            ProgressedBody = $"P {progPos.Name}",
                            AspectName = aspectName,
                            AspectAbbreviation = abbr,
                            NatalTarget = natalName,
                            OrbDegrees = Math.Round(orb, 4),
                            Direction = direction,
                        });
                    }
                }
            }
        }

        return aspects;
    }

    /// <summary>
    /// Calculates the orb between two points for a given aspect angle.
    /// </summary>
    private static double CalculateAspectOrb(double angleDifference, double aspectAngle)
    {
        // The angular difference between the two bodies
        double diff = angleDifference;

        // Check both forward and backward
        double orb1 = Math.Abs(diff - aspectAngle);
        double orb2 = Math.Abs(diff - (360.0 - aspectAngle));

        // For conjunction (0°), only check orb1 and the wraparound
        if (aspectAngle == 0.0)
        {
            double orbWrap = Math.Abs(360.0 - diff);
            return Math.Min(orb1, orbWrap);
        }

        return Math.Min(orb1, orb2);
    }

    /// <summary>
    /// Determines whether a progressed aspect is applying or separating: applying when
    /// the orb is decreasing at the progressed instant, by the progressed point's speed
    /// there (per progressed day) with the natal point held still
    /// (<see cref="AspectDirection"/>). A body's speed is its speed in longitude at the
    /// progressed instant; an angle's is the Midheaven's, the progressed Sun's, or the
    /// Ascendant's, that times dASC/dMC (T3-1, branch G, 1.3.4).
    /// </summary>
    /// <remarks>
    /// Until 1.3.3 this compared the orb now with the orb 0.01 progressed day later, so
    /// an aspect closer to exact than half that step was called separating while still
    /// applying (finding T3-3); until 1.3.4 an angle's aspect was always "applying".
    /// </remarks>
    private string DetermineAspectDirection(
        ProgressedPosition progPos, double progressedJd,
        double natalLon, double aspectAngle, IReadOnlyDictionary<string, double> angleSpeeds)
    {
        double speed;
        if (progPos.IsRetrograde is null)
        {
            // An angle: its speed per progressed day, from the solar arc's
            speed = angleSpeeds[progPos.Name];
        }
        else
        {
            // Look up the body to get its CelestialBodyId
            CelestialBodyId? bodyId = GetBodyIdByName(progPos.Name);
            if (bodyId is null)
                return "applying";

            // The progressed body's speed at the progressed instant
            speed = _ephemeris.CalculatePosition(progressedJd, bodyId.Value).SpeedInLongitude;
        }

        // The natal point does not move
        return AspectDirection.IsApplying(progPos.ProgressedLongitude, speed, natalLon, 0.0, aspectAngle)
            ? "applying" : "separating";
    }

    /// <summary>
    /// Maps a body name to its CelestialBodyId.
    /// </summary>
    private static CelestialBodyId? GetBodyIdByName(string name) => name switch
    {
        "Sun" => CelestialBodyId.Sun,
        "Moon" => CelestialBodyId.Moon,
        "Mercury" => CelestialBodyId.Mercury,
        "Venus" => CelestialBodyId.Venus,
        "Mars" => CelestialBodyId.Mars,
        _ => null,
    };

    #endregion

    #region Utility

    /// <summary>
    /// Normalises an angle to the range [0, 360).
    /// </summary>
    private static double NormaliseAngle(double angle)
    {
        double result = angle % 360.0;
        if (result < 0) result += 360.0;
        return result;
    }

    /// <summary>
    /// Wraps an angle difference to the range [−180, 180).
    /// </summary>
    private static double WrapSigned(double angle) => NormaliseAngle(angle + 180.0) - 180.0;

    /// <summary>
    /// Normalises an angle to the range [0, 360) for aspect orb calculation.
    /// Returns the shortest arc (0 to 180).
    /// </summary>
    private static double NormaliseAngle180(double angle)
    {
        double result = angle % 360.0;
        if (result < 0) result += 360.0;
        if (result > 180.0) result = 360.0 - result;
        return result;
    }

    /// <summary>
    /// Formats a longitude as "DD°MM' Sign" (e.g., "26°19' Taurus").
    /// </summary>
    private static string FormatLongitude(double longitude)
    {
        int degrees = TransitEphemerisHelper.LongitudeToDegrees(longitude);
        int minutes = TransitEphemerisHelper.LongitudeToMinutes(longitude);
        string sign = TransitEphemerisHelper.LongitudeToSign(longitude);
        return string.Create(CultureInfo.InvariantCulture, $"{degrees:D2}\u00b0{minutes:D2}' {sign}");
    }

    #endregion
}
