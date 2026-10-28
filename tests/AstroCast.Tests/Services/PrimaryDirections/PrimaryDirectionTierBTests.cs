using System.Globalization;
using System.Runtime.CompilerServices;

using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Services.PrimaryDirections;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Xunit.Abstractions;

namespace AstroCast.Tests.Services.PrimaryDirections;

/// <summary>
/// <see cref="PrimaryDirectionService"/> against Tier B (branch Primary, 1.5.0): pd_truth.py, an independent
/// pyswisseph script over the shipped ephemeris files that holds rules R1 to R10 as the plan states them, its seven
/// lists copied byte for byte into <c>TierB/</c> beside this file. Every hit must be present with the same key
/// (motion, significator, aspect, angle, promissor) and agree in arc, instant, years, months per minute and range;
/// the count of directions left out must agree too.
/// </summary>
/// <param name="fixture">The shared ephemeris fixture.</param>
/// <param name="output">Receives each file's largest deviations.</param>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class PrimaryDirectionTierBTests(EphemerisFixture fixture, ITestOutputHelper output)
{
    /// <summary>Tolerance on an arc, in degrees.</summary>
    private const double ArcTolerance = 1e-4;

    /// <summary>Tolerance on a Julian Day and on a range bound, in days (2.6 s).</summary>
    private const double DayTolerance = 3e-5;

    /// <summary>Tolerance on the years.</summary>
    private const double YearsTolerance = 1e-5;

    /// <summary>Tolerance on the months per minute.</summary>
    private const double MonthsPerMinuteTolerance = 1e-3;

    /// <summary>What names a hit in the Tier B list and in the engine's.</summary>
    /// <param name="Motion">D or C.</param>
    /// <param name="Significator">The significator's name.</param>
    /// <param name="Aspect">The promissor point's relation to its body.</param>
    /// <param name="Angle">The aspect point's signed angle.</param>
    /// <param name="Promissor">The body's name or the star's catalog display name.</param>
    private sealed record HitKey(
        PrimaryDirectionMotion Motion, string Significator, PrimaryDirectionAspect Aspect, double Angle, string Promissor);

    /// <summary>One row of a Tier B list.</summary>
    /// <param name="Key">The hit's key.</param>
    /// <param name="JulianDayUt">The instant as a Julian Day (UT).</param>
    /// <param name="Years">The years of life.</param>
    /// <param name="Arc">The arc, in degrees.</param>
    /// <param name="MonthsPerMinute">The months per minute, or null where the row is blank.</param>
    /// <param name="RangeStartUtc">The range's earlier bound, or null where blank.</param>
    /// <param name="RangeEndUtc">The range's later bound, or null where blank.</param>
    private sealed record TierBRow(
        HitKey Key, double JulianDayUt, double Years, double Arc, double? MonthsPerMinute,
        DateTime? RangeStartUtc, DateTime? RangeEndUtc);

    /// <summary>
    /// Each of the seven lists: the chart, the settings (every other setting the default), the hits and the directions
    /// left out, as pd_truth.py's summary reports them.
    /// </summary>
    /// <param name="file">The list's file, without its extension.</param>
    /// <param name="chart">The fixture: J2000Nyc, Hartwell, M35, N1 or Tromso.</param>
    /// <param name="key">The key.</param>
    /// <param name="planets">Whether Mercury to Pluto are significators.</param>
    /// <param name="uncertaintyMinutes">The birth-time uncertainty, in minutes.</param>
    /// <param name="hits">The number of hits in the list.</param>
    /// <param name="leftOut">The directions left out.</param>
    [Theory]
    [InlineData("j2000nyc_naibod_u4", "J2000Nyc", PrimaryDirectionKey.Naibod, false, 4, 572, 60)]
    [InlineData("j2000nyc_ptolemy", "J2000Nyc", PrimaryDirectionKey.Ptolemy, false, 0, 582, 60)]
    [InlineData("j2000nyc_rasolar", "J2000Nyc", PrimaryDirectionKey.SolarArcRightAscension, false, 0, 572, 60)]
    [InlineData("hartwell_naibod", "Hartwell", PrimaryDirectionKey.Naibod, false, 0, 538, 60)]
    [InlineData("m35_naibod_planets", "M35", PrimaryDirectionKey.Naibod, true, 0, 1159, 280)]
    [InlineData("n1_naibod", "N1", PrimaryDirectionKey.Naibod, false, 0, 440, 96)]
    [InlineData("tromso_naibod", "Tromso", PrimaryDirectionKey.Naibod, false, 0, 185, 1179)]
    public void Calculate_MatchesTierB(
        string file, string chart, PrimaryDirectionKey key, bool planets, int uncertaintyMinutes, int hits, int leftOut)
    {
        List<TierBRow> rows = LoadRows(file + ".tsv");
        rows.Should().HaveCount(hits, "the list holds the hits the summary reports");

        ChartResult natal = Cast(chart);
        PrimaryDirectionSettings settings = new()
        {
            Key = key,
            PlanetsAsSignificators = planets,
            BirthTimeUncertaintyMinutes = uncertaintyMinutes,
        };

        PrimaryDirectionResult result = new PrimaryDirectionService(fixture.EphemerisService).Calculate(natal, settings);

        result.LeftOutCount.Should().Be(leftOut);
        result.Hits.Should().HaveCount(rows.Count);

        Dictionary<HitKey, TierBRow> expected = rows.ToDictionary(r => r.Key);
        Dictionary<HitKey, PrimaryDirectionHit> actual = result.Hits.ToDictionary(
            h => new HitKey(h.Motion, h.Significator, h.Aspect, h.AspectAngle, h.Promissor));
        actual.Keys.Should().BeEquivalentTo(expected.Keys);

        double worstArc = 0.0, worstDay = 0.0, worstYears = 0.0, worstMonths = 0.0, worstRange = 0.0;
        foreach ((HitKey hitKey, TierBRow row) in expected)
        {
            PrimaryDirectionHit hit = actual[hitKey];
            string because = $"of {hitKey}";

            worstArc = Math.Max(worstArc, Math.Abs(hit.Arc - row.Arc));
            worstDay = Math.Max(worstDay, Math.Abs(hit.JulianDayUt - row.JulianDayUt));
            worstYears = Math.Max(worstYears, Math.Abs(hit.Years - row.Years));
            hit.Arc.Should().BeApproximately(row.Arc, ArcTolerance, because);
            hit.JulianDayUt.Should().BeApproximately(row.JulianDayUt, DayTolerance, because);
            hit.Years.Should().BeApproximately(row.Years, YearsTolerance, because);

            if (row.MonthsPerMinute is double months)
            {
                hit.MonthsPerMinute.Should().NotBeNull(because);
                if (hit.MonthsPerMinute is double actualMonths)
                {
                    worstMonths = Math.Max(worstMonths, Math.Abs(actualMonths - months));
                    actualMonths.Should().BeApproximately(months, MonthsPerMinuteTolerance, because);
                }
            }
            else
            {
                hit.MonthsPerMinute.Should().BeNull(because);
            }

            if (row.RangeStartUtc is DateTime rangeStart && row.RangeEndUtc is DateTime rangeEnd)
            {
                hit.RangeStartUtc.Should().NotBeNull(because);
                hit.RangeEndUtc.Should().NotBeNull(because);
                if (hit.RangeStartUtc is DateTime actualStart && hit.RangeEndUtc is DateTime actualEnd)
                {
                    double startDays = Math.Abs((actualStart - rangeStart).TotalDays);
                    double endDays = Math.Abs((actualEnd - rangeEnd).TotalDays);
                    worstRange = Math.Max(worstRange, Math.Max(startDays, endDays));
                    startDays.Should().BeLessThanOrEqualTo(DayTolerance, because);
                    endDays.Should().BeLessThanOrEqualTo(DayTolerance, because);
                }
            }
            else
            {
                hit.RangeStartUtc.Should().BeNull(because);
                hit.RangeEndUtc.Should().BeNull(because);
            }
        }

        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{file}: {rows.Count} hits, {leftOut} left out; largest deviations: arc {worstArc:E2} deg, instant {worstDay:E2} d, years {worstYears:E2}, months/min {worstMonths:E2}, range {worstRange:E2} d"));
    }

    #region Helpers

    /// <summary>Casts a fixture chart by name.</summary>
    /// <param name="name">J2000Nyc, Hartwell, M35, N1 or Tromso.</param>
    /// <returns>The chart.</returns>
    private ChartResult Cast(string name)
    {
        ChartInput input = name switch
        {
            "J2000Nyc" => GoldenFixtures.J2000NycInput(),
            "Hartwell" => GoldenFixtures.HartwellInput(),
            "M35" => GoldenFixtures.M35Input(),
            "N1" => GoldenFixtures.N1Input(),
            "Tromso" => GoldenFixtures.TromsoInput(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown fixture."),
        };
        return GoldenFixtures.CreateChartEngine(fixture).Calculate(input);
    }

    /// <summary>
    /// Reads a Tier B list: tab-separated, LF, one header row, the columns jd_ut, instant_ut, years, motion,
    /// significator, aspect, angle, promissor, arc, arc_dm, months_per_min, range_start_ut and range_end_ut. A
    /// star is named by its Swiss Ephemeris name and mapped to its catalog display name; the instant is compared by
    /// its Julian Day, never by its date string, which N1's prints on the Julian calendar.
    /// </summary>
    /// <param name="fileName">The list's file name.</param>
    /// <returns>The rows.</returns>
    private static List<TierBRow> LoadRows(string fileName)
    {
        string path = TierBPath(fileName);
        File.Exists(path).Should().BeTrue($"the Tier B list '{path}' should exist");

        List<TierBRow> rows = [];
        foreach (string line in File.ReadAllLines(path).Skip(1))
        {
            if (line.Length == 0)
                continue;

            string[] cells = line.Split('\t');
            cells.Should().HaveCount(13, $"of the row '{line}'");

            HitKey key = new(
                cells[3] == "D" ? PrimaryDirectionMotion.Direct : PrimaryDirectionMotion.Converse,
                cells[4],
                AspectOf(cells[5]),
                double.Parse(cells[6], CultureInfo.InvariantCulture),
                PromissorNameOf(cells[7]));

            rows.Add(new TierBRow(
                key,
                double.Parse(cells[0], CultureInfo.InvariantCulture),
                double.Parse(cells[2], CultureInfo.InvariantCulture),
                double.Parse(cells[8], CultureInfo.InvariantCulture),
                cells[10].Length == 0 ? null : double.Parse(cells[10], CultureInfo.InvariantCulture),
                cells[11].Length == 0 ? null : ParseUt(cells[11]),
                cells[12].Length == 0 ? null : ParseUt(cells[12])));
        }

        return rows;
    }

    /// <summary>The aspect for a list's abbreviation.</summary>
    private static PrimaryDirectionAspect AspectOf(string abbreviation) => abbreviation switch
    {
        "cnj" => PrimaryDirectionAspect.Conjunction,
        "sxt" => PrimaryDirectionAspect.Sextile,
        "sqr" => PrimaryDirectionAspect.Square,
        "tri" => PrimaryDirectionAspect.Trine,
        "opp" => PrimaryDirectionAspect.Opposition,
        "ant" => PrimaryDirectionAspect.Antiscion,
        "cant" => PrimaryDirectionAspect.ContraAntiscion,
        _ => throw new ArgumentOutOfRangeException(nameof(abbreviation), abbreviation, "Unknown aspect."),
    };

    /// <summary>A promissor as the engine names it: a star's Swiss Ephemeris name becomes its catalog display name.</summary>
    private static string PromissorNameOf(string listName) =>
        FixedStarCatalog.Stars.FirstOrDefault(s => string.Equals(s.SwissEphName, listName, StringComparison.Ordinal))?.DisplayName
        ?? listName;

    /// <summary>A list's Gregorian UT instant, "yyyy-MM-dd HH:mm:ss".</summary>
    private static DateTime ParseUt(string text) =>
        DateTime.ParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    /// <summary>The path of a Tier B list, in <c>TierB/</c> beside this file, as the goldens are found beside theirs.</summary>
    private static string TierBPath(string fileName, [CallerFilePath] string callerFilePath = "")
    {
        string directory = Path.GetDirectoryName(callerFilePath)
            ?? throw new InvalidOperationException("Unable to determine the test's source directory.");
        return Path.Combine(directory, "TierB", fileName);
    }

    #endregion
}
