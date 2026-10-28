using System.Globalization;
using System.Runtime.CompilerServices;

namespace AstroCast.Tests.Fixtures;

/// <summary>
/// One row of the civil-time truth table: a wall-clock reading in a zone and the
/// instant it denotes under IANA's rules, with the offset and the zone record's name
/// in force at that instant.
/// </summary>
/// <param name="Set">The set the row belongs to: <c>grid</c>, <c>edges</c> or <c>lmt</c>.</param>
/// <param name="Zone">The IANA zone identifier.</param>
/// <param name="Local">The wall-clock reading, <see cref="DateTimeKind.Unspecified"/>.</param>
/// <param name="Kind">
/// How the reading maps: <c>unique</c>, <c>repeated</c> (it occurs twice; the later
/// instant is expected) or <c>skipped</c> (it never occurs; the first instant after the
/// gap is expected).
/// </param>
/// <param name="ExpectedUtc">The instant, to the second, <see cref="DateTimeKind.Utc"/>.</param>
/// <param name="OffsetSeconds">The offset from UTC in force at that instant, in seconds.</param>
/// <param name="Abbreviation">The zone record's name for that offset, e.g. "EST", "LMT" or "+06".</param>
public sealed record TimeZoneVector(
    string Set,
    string Zone,
    DateTime Local,
    string Kind,
    DateTime ExpectedUtc,
    int OffsetSeconds,
    string Abbreviation);

/// <summary>
/// Reads <c>TimeZoneVectors/tz-vectors-2026e.csv</c>, the civil-time truth table the
/// time-zone tests compare against.
/// </summary>
/// <remarks>
/// <para>
/// The table is generated outside AstroCast by <c>TimeZoneVectors/make_tz_vectors.py</c>:
/// Python's <c>zoneinfo</c> over the pip <c>tzdata</c> 2026.5 package (IANA 2026e), with
/// the system zoneinfo directory cleared. Nothing in it comes from NodaTime or from
/// AstroCast's own conversion. The script refuses to run on any other release.
/// </para>
/// <para>
/// It holds 4,478 rows in three sets over 13 zones: the grid (1,612), every repeated and
/// skipped interval from 1890 to 2040 at its midpoint (1,822), and local mean time every
/// fifth year (1,044). The tests group the rows by zone and set, so the suite gains 39
/// theory cases rather than one per row.
/// </para>
/// <para>
/// The file is found beside this source file, as the goldens are, so it needs no copy
/// into the build output.
/// </para>
/// </remarks>
public static class TimeZoneVectorTable
{
    /// <summary>The IANA release the table was generated from.</summary>
    public const string DataVersion = "2026e";

    /// <summary>The table's file name, inside the <c>TimeZoneVectors</c> directory.</summary>
    public const string FileName = "tz-vectors-2026e.csv";

    private const string Header = "set,zone,local,kind,expectedUtc,offsetSeconds,abbreviation";

    private const string ReadingFormat = "yyyy-MM-dd'T'HH:mm:ss";

    private static readonly Lazy<(string VersionLine, IReadOnlyList<TimeZoneVector> Rows)> s_table =
        new(() => Load());

    /// <summary>
    /// The table's first line, a comment naming the IANA release, the tzdata package and
    /// the D17 rule the expected instants follow.
    /// </summary>
    public static string VersionLine => s_table.Value.VersionLine;

    /// <summary>Every row, in file order: the grid, then the edges, then local mean time.</summary>
    public static IReadOnlyList<TimeZoneVector> Rows => s_table.Value.Rows;

    /// <summary>
    /// Every (zone, set) pair in the table, in file order: the 39 theory cases.
    /// </summary>
    public static TheoryData<string, string> ZoneSets
    {
        get
        {
            TheoryData<string, string> data = new();
            foreach ((string zone, string set) in Rows.Select(r => (r.Zone, r.Set)).Distinct())
                data.Add(zone, set);
            return data;
        }
    }

    /// <summary>The rows of one zone in one set.</summary>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="set">The set: <c>grid</c>, <c>edges</c> or <c>lmt</c>.</param>
    /// <returns>The rows, in file order.</returns>
    public static IReadOnlyList<TimeZoneVector> For(string zone, string set) =>
        Rows.Where(r => r.Zone == zone && r.Set == set).ToList();

    /// <summary>
    /// Reads and parses the table that sits beside this source file.
    /// </summary>
    /// <param name="sourceFilePath">Supplied by the compiler: this file's path.</param>
    /// <returns>The version line and the rows.</returns>
    /// <exception cref="InvalidDataException">Thrown when the file is not laid out as expected.</exception>
    private static (string VersionLine, IReadOnlyList<TimeZoneVector> Rows) Load(
        [CallerFilePath] string sourceFilePath = "")
    {
        string directory = Path.GetDirectoryName(sourceFilePath)
            ?? throw new InvalidDataException($"No directory in '{sourceFilePath}'.");
        string path = Path.Combine(directory, "TimeZoneVectors", FileName);

        string[] lines = File.ReadAllLines(path);
        if (lines.Length < 2 || !lines[0].StartsWith('#') || lines[1] != Header)
            throw new InvalidDataException($"'{path}' does not start with a version line and the header '{Header}'.");

        List<TimeZoneVector> rows = new(lines.Length - 2);
        foreach (string line in lines.Skip(2))
        {
            if (line.Length == 0)
                continue;

            string[] f = line.Split(',');
            if (f.Length != 7)
                throw new InvalidDataException($"'{path}': expected 7 fields in '{line}'.");

            rows.Add(new TimeZoneVector(
                f[0],
                f[1],
                DateTime.ParseExact(f[2], ReadingFormat, CultureInfo.InvariantCulture),
                f[3],
                DateTime.SpecifyKind(
                    DateTime.ParseExact(f[4], ReadingFormat, CultureInfo.InvariantCulture), DateTimeKind.Utc),
                int.Parse(f[5], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture),
                f[6]));
        }

        return (lines[0], rows);
    }
}
