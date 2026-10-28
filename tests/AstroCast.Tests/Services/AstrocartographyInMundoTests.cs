using System.Runtime.InteropServices;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Interop;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the astrocartography lines in mundo (ruling S-D7, branch J): each line marks where
/// the body itself is on the meridian or the geometric horizon, from its geocentric apparent
/// right ascension and declination and the Greenwich apparent sidereal time, in closed form;
/// MC and IC lines run from 85° S to 85° N, and ASC and DSC lines have a point at every whole
/// degree in that range at which the body rises and sets, and end where it stops.
/// </summary>
/// <remarks>
/// <para>
/// Every expected value comes from outside the code under test. The person A figures were
/// computed from direct <c>swe_calc_ut</c> calls (equatorial, apparent, true equator of date)
/// and <c>swe_sidtime</c> on the shipped library, with the closed form applied by hand. The
/// per-point checks call the library directly here, through <see cref="TierB"/>:
/// <c>swe_azalt</c> for the body's true altitude (no refraction) and azimuth at each ASC and
/// DSC point, and <c>swe_calc_ut</c> with <c>swe_sidtime</c> for its hour angle at each MC and
/// IC point.
/// </para>
/// <para>
/// Person A (invented) is the fixed birth chart these figures were computed for: 1947-10-05
/// 09:30 at UTC−5 (explicit), Harrisburg, Pennsylvania (JD 2432464.1041666665 UT;
/// fixtures/truth/acg_hartwell.py). Its Sun has declination −4.55° and so rises and sets at
/// every latitude to 85°; its Moon (+24.93°) and Mars (+20.63°) stop short of 85°.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
public sealed class AstrocartographyInMundoTests
{
    /// <summary>Tolerance for a value given to four decimals (degrees).</summary>
    private const double FourDecimals = 5e-5;

    /// <summary>Tolerance for a value given to two decimals (degrees).</summary>
    private const double TwoDecimals = 5e-3;

    /// <summary>Tolerance on the body's altitude or hour angle at a line point (degrees).</summary>
    private const double OnTheLine = 1e-6;

    private readonly EphemerisFixture _fixture;
    private readonly AstrocartographyService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public AstrocartographyInMundoTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _service = new AstrocartographyService(fixture.EphemerisService, new LoggerConfiguration().CreateLogger());
    }

    /// <summary>
    /// Person A's Sun crosses the equator on its rising line at 130.3494° W and on its setting
    /// line at 49.6506° E: the two horizon lines are different curves. (Until branch J the DSC
    /// line was a copy of the ASC line.)
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Hartwell_SunHorizonLines_CrossTheEquator_WhereTheSunRisesAndSets()
    {
        AcgResult acg = _service.Calculate(Hartwell());

        LongitudeAt(acg, CelestialBodyId.Sun, AcgLineType.ASC, 0.0).Should().BeApproximately(-130.3494, FourDecimals);
        LongitudeAt(acg, CelestialBodyId.Sun, AcgLineType.DSC, 0.0).Should().BeApproximately(49.6506, FourDecimals);
    }

    /// <summary>
    /// Person A's Pluto MC line is where Pluto itself culminates, 91.7112° W; the zodiac
    /// degree's MC line would lie at 93.91° W.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Hartwell_PlutoMcLine_IsWherePlutoItselfCulminates()
    {
        AcgResult acg = _service.Calculate(Hartwell());

        AcgLine mc = Line(acg, CelestialBodyId.Pluto, AcgLineType.MC);
        mc.Coordinates.Should().OnlyContain(c => Math.Abs(c.Longitude - -91.7112) < FourDecimals);
    }

    /// <summary>
    /// A body whose declination is more than 5° from the equator stops rising and setting
    /// short of 85°, at |latitude| = 90° − |declination|, and both its horizon lines end there:
    /// person A's Moon at ±65.07°, its Mars at ±69.37°.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="end">Where its ASC and DSC lines end (degrees of latitude, north and south).</param>
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(CelestialBodyId.Moon, 65.07)]
    [InlineData(CelestialBodyId.Mars, 69.37)]
    public void Hartwell_HorizonLines_EndWhereTheBodyStopsRisingAndSetting(CelestialBodyId body, double end)
    {
        AcgResult acg = _service.Calculate(Hartwell());

        foreach (AcgLineType type in new[] { AcgLineType.ASC, AcgLineType.DSC })
        {
            AcgLine line = Line(acg, body, type);
            line.Coordinates[0].Latitude.Should().BeApproximately(-end, TwoDecimals, $"{body} {type} starts at its southern end");
            line.Coordinates[^1].Latitude.Should().BeApproximately(end, TwoDecimals, $"{body} {type} stops at its northern end");
        }
    }

    /// <summary>
    /// At every point of every line of person A and of the J2000 fixture, the body is where the
    /// line says, by Tier B: on an ASC or DSC point its true altitude is 0 (within 1e-6°), rising
    /// in the east on the ASC and setting in the west on the DSC (due north or south at a line's
    /// end, where the two meet); on an MC or IC point its hour angle is 0° or 180°.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void EveryLinePoint_PutsTheBodyOnTheHorizonOrMeridian_ByTierB()
    {
        foreach (ChartResult chart in new[] { Hartwell(), GoldenFixtures.J2000Nyc(_fixture) })
        {
            AcgResult acg = _service.Calculate(chart);
            double gast = TierB.GastDegrees(chart.JulianDay);
            List<string> wrong = [];

            foreach (AcgLine line in acg.Lines)
            {
                (double ra, double dec) = TierB.Equatorial(chart.JulianDay, line.Body);
                double end = 90.0 - Math.Abs(dec);

                foreach (AcgCoordinate point in line.Coordinates)
                {
                    if (line.LineType is AcgLineType.MC or AcgLineType.IC)
                    {
                        double hourAngle = Wrap180(gast + point.Longitude - ra);
                        double expected = line.LineType == AcgLineType.MC ? 0.0 : 180.0;
                        if (Math.Abs(Wrap180(hourAngle - expected)) > OnTheLine)
                        {
                            wrong.Add($"{chart.Input.Name} {line.BodyName} {line.LineType} at {point.Latitude}: hour angle {hourAngle}");
                        }

                        continue;
                    }

                    (double azimuth, double altitude) = TierB.Horizon(chart.JulianDay, point.Latitude, point.Longitude, ra, dec);
                    double east = -Math.Sin(azimuth * Math.PI / 180.0); // swe_azalt: 0 = south, 90 = west, 270 = east
                    bool atEnd = Math.Abs(Math.Abs(point.Latitude) - end) < 1e-9;
                    bool rightSide = atEnd
                        ? Math.Abs(east) < OnTheLine
                        : line.LineType == AcgLineType.ASC ? east > 0.0 : east < 0.0;
                    if (Math.Abs(altitude) > OnTheLine || !rightSide)
                    {
                        wrong.Add($"{chart.Input.Name} {line.BodyName} {line.LineType} at {point.Latitude}: altitude {altitude}, azimuth {azimuth}");
                    }
                }
            }

            wrong.Should().BeEmpty($"every point of every line of {chart.Input.Name} is where its body is angular");
        }
    }

    /// <summary>
    /// The latitude range (Q2): an MC or IC line runs from 85° S to 85° N; an ASC or DSC line has a
    /// point at every whole degree from 85° S to 85° N at which the body rises and sets (by its
    /// Tier B declination), in order, plus the two latitudes where it ends when that is short of 85°.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void LinePoints_AreEveryWholeDegreeWhereTheBodyRisesAndSets_AndItsEnds()
    {
        foreach (ChartResult chart in new[] { Hartwell(), GoldenFixtures.J2000Nyc(_fixture) })
        {
            AcgResult acg = _service.Calculate(chart);

            foreach (AcgLine line in acg.Lines)
            {
                List<double> expected;
                if (line.LineType is AcgLineType.MC or AcgLineType.IC)
                {
                    expected = [-85.0, 85.0];
                }
                else
                {
                    double end = 90.0 - Math.Abs(TierB.Equatorial(chart.JulianDay, line.Body).Declination);
                    expected = Enumerable.Range(-85, 171).Select(l => (double)l).Where(l => end >= 85.0 || Math.Abs(l) < end).ToList();
                    if (end < 85.0)
                    {
                        expected.Insert(0, -end);
                        expected.Add(end);
                    }
                }

                line.Coordinates.Select(c => c.Latitude).Should().Equal(expected, (a, e) => Math.Abs(a - e) < 1e-9,
                    $"{chart.Input.Name} {line.BodyName} {line.LineType} has a point at every whole degree where it is defined, and its ends");
            }
        }
    }

    /// <summary>
    /// No line jumps: neighbouring points of every line, ends included, are within 9° of each
    /// other by great circle (Tier A, haversine). A true line in mundo moves at most 7.63° on the
    /// 95 saved charts; a jump from the rising to the setting curve, which every line made until
    /// branch J, moves tens of degrees. (Measured on the ground because near its end, where it runs
    /// east and west, a true line moves up to about 30° of longitude between whole degrees.)
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void NeighbouringLinePoints_AreWithinNineDegrees_OnTheGround()
    {
        foreach (ChartResult chart in new[] { Hartwell(), GoldenFixtures.J2000Nyc(_fixture) })
        {
            AcgResult acg = _service.Calculate(chart);

            foreach (AcgLine line in acg.Lines.Where(l => l.LineType is AcgLineType.ASC or AcgLineType.DSC))
            {
                for (int i = 1; i < line.Coordinates.Count; i++)
                {
                    double step = Haversine(line.Coordinates[i - 1], line.Coordinates[i]);
                    step.Should().BeLessThanOrEqualTo(9.0,
                        $"{chart.Input.Name} {line.BodyName} {line.LineType} moves between latitudes {line.Coordinates[i - 1].Latitude} and {line.Coordinates[i].Latitude}");
                }
            }
        }
    }

    #region Helpers

    /// <summary>Person A (invented): 1947-10-05 09:30 at UTC−5 (explicit), Harrisburg, Pennsylvania.</summary>
    private ChartResult Hartwell()
    {
        ChartResult chart = GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
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
        chart.JulianDay.Should().BeApproximately(2432464.1041666665, 1e-9, "person A is cast at 14:30 UT");
        return chart;
    }

    private static AcgLine Line(AcgResult acg, CelestialBodyId body, AcgLineType type) =>
        acg.Lines.Single(l => l.Body == body && l.LineType == type);

    private static double LongitudeAt(AcgResult acg, CelestialBodyId body, AcgLineType type, double latitude) =>
        Line(acg, body, type).Coordinates.Single(c => c.Latitude == latitude).Longitude;

    private static double Wrap180(double degrees)
    {
        double x = ((degrees + 180.0) % 360.0 + 360.0) % 360.0;
        return x - 180.0;
    }

    /// <summary>Great-circle distance between two points, in degrees (haversine).</summary>
    private static double Haversine(AcgCoordinate a, AcgCoordinate b)
    {
        double p1 = a.Latitude * Math.PI / 180.0, p2 = b.Latitude * Math.PI / 180.0;
        double dl = (b.Longitude - a.Longitude) * Math.PI / 180.0;
        double h = Math.Pow(Math.Sin((p2 - p1) / 2.0), 2) + Math.Cos(p1) * Math.Cos(p2) * Math.Pow(Math.Sin(dl / 2.0), 2);
        return 2.0 * Math.Asin(Math.Min(1.0, Math.Sqrt(h))) * 180.0 / Math.PI;
    }

    #endregion
}

/// <summary>
/// Tier B for the astrocartography tests: direct calls into the shipped Swiss Ephemeris
/// library, with every flag written out, that do not pass through the code under test.
/// </summary>
internal static class TierB
{
    /// <summary><c>SEFLG_SWIEPH | SEFLG_SPEED | SEFLG_EQUATORIAL</c> (2 + 256 + 2048, swephexp.h).</summary>
    private const int EquatorialFlags = 2 | 256 | 2048;

    /// <summary><c>SE_EQU2HOR</c> (swephexp.h): the input is right ascension and declination of date.</summary>
    private const int EquatorialToHorizon = 1;

    /// <summary><c>swe_azalt</c>'s signature (swephexp.h).</summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SweAzalt(
        double tjdUt, int calcFlag, double[] geopos, double atpress, double attemp, double[] xin, double[] xaz);

    /// <summary>
    /// <c>swe_azalt</c>, which <c>SwissEphInterop</c> does not declare, bound from the same native
    /// library file the Core assembly loads.
    /// </summary>
    private static readonly SweAzalt Azalt = Marshal.GetDelegateForFunctionPointer<SweAzalt>(
        NativeLibrary.GetExport(
            NativeLibrary.Load(
                OperatingSystem.IsWindows() ? "swedll64.dll" : OperatingSystem.IsMacOS() ? "libswe.dylib" : "libswe.so",
                typeof(EphemerisService).Assembly,
                DllImportSearchPath.AssemblyDirectory),
            "swe_azalt"));

    /// <summary>
    /// The body's geocentric apparent right ascension and declination on the true equator of
    /// date, from <c>swe_calc_ut</c>.
    /// </summary>
    /// <param name="julianDayUt">The instant (Julian Day, UT).</param>
    /// <param name="body">The body.</param>
    /// <returns>Right ascension and declination in degrees.</returns>
    public static (double RightAscension, double Declination) Equatorial(double julianDayUt, CelestialBodyId body)
    {
        double[] xx = new double[6];
        StringBuilder error = new(SwissEphInterop.MaxErrorLength);
        int flags = SwissEphInterop.swe_calc_ut(julianDayUt, (int)body, EquatorialFlags, xx, error);
        if (flags < 0)
        {
            throw new InvalidOperationException($"swe_calc_ut failed for {body}: {error}");
        }

        return (xx[0], xx[1]);
    }

    /// <summary>Greenwich apparent sidereal time from <c>swe_sidtime</c>, in degrees.</summary>
    /// <param name="julianDayUt">The instant (Julian Day, UT).</param>
    /// <returns>GAST in degrees.</returns>
    public static double GastDegrees(double julianDayUt) => SwissEphInterop.swe_sidtime(julianDayUt) * 15.0;

    /// <summary>
    /// The azimuth and true (geometric, unrefracted) altitude of a right ascension and declination
    /// of date seen from a place at sea level, from <c>swe_azalt</c>.
    /// </summary>
    /// <param name="julianDayUt">The instant (Julian Day, UT).</param>
    /// <param name="latitude">Geographic latitude (degrees, north positive).</param>
    /// <param name="longitude">Geographic longitude (degrees, east positive).</param>
    /// <param name="rightAscension">Right ascension of date (degrees).</param>
    /// <param name="declination">Declination of date (degrees).</param>
    /// <returns>Azimuth (degrees from south through west) and true altitude (degrees).</returns>
    public static (double Azimuth, double TrueAltitude) Horizon(
        double julianDayUt, double latitude, double longitude, double rightAscension, double declination)
    {
        double[] xaz = new double[3];
        Azalt(julianDayUt, EquatorialToHorizon, [longitude, latitude, 0.0], 0.0, 0.0, [rightAscension, declination, 1.0], xaz);
        return (xaz[0], xaz[1]);
    }
}
