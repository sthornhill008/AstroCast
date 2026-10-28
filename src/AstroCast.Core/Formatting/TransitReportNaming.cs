using AstroCast.Core.Models.Transit;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Names the transit report for the techniques it holds (branch Modes, 1.4.0 item 2; scoping C1f; a fourth
/// technique since branch PrimaryExport, 1.5.0): the document's title, the stem of its default file name
/// and the subject of its save dialog, one of eight forms for the fifteen combinations of Transits, Solar
/// Arcs, Secondary Progressions and Primary Directions. With the transits in, the report is the transit
/// report whatever else it holds; without them it is named for the techniques it holds.
/// </summary>
public static class TransitReportNaming
{
    /// <summary>The eight forms, by the techniques without the transits.</summary>
    /// <param name="Title">The document's title, without the heading mark.</param>
    /// <param name="FileStem">The default file name's stem.</param>
    /// <param name="DialogSubject">The save dialog's subject.</param>
    private readonly record struct Form(string Title, string FileStem, string DialogSubject);

    private static readonly Form Transits = new("Transit Report", "Transits", "Transits");
    private static readonly Form SolarArcs = new("Solar Arc Report", "SolarArcs", "Solar Arcs");
    private static readonly Form Progressions = new("Secondary Progressions Report", "Progressions", "Progressions");
    private static readonly Form SolarArcsProgressions = new("Solar Arc and Progressions Report", "SolarArcsProgressions", "Solar Arcs and Progressions");
    private static readonly Form PrimaryDirections = new("Primary Directions Report", "PrimaryDirections", "Primary Directions");
    private static readonly Form SolarArcsPrimaryDirections = new("Solar Arc and Primary Directions Report", "SolarArcsPrimaryDirections", "Solar Arcs and Primary Directions");
    private static readonly Form ProgressionsPrimaryDirections = new("Progressions and Primary Directions Report", "ProgressionsPrimaryDirections", "Progressions and Primary Directions");
    private static readonly Form SolarArcsProgressionsPrimaryDirections = new("Solar Arc, Progressions and Primary Directions Report", "SolarArcsProgressionsPrimaryDirections", "Solar Arcs, Progressions and Primary Directions");

    /// <summary>
    /// The report's title, without the Markdown heading mark: "Transit Report" whenever the transits are
    /// in; else "Solar Arc Report", "Secondary Progressions Report", "Solar Arc and Progressions Report",
    /// "Primary Directions Report", "Solar Arc and Primary Directions Report", "Progressions and Primary
    /// Directions Report" or "Solar Arc, Progressions and Primary Directions Report".
    /// </summary>
    /// <param name="transits">Whether the transit scan ran.</param>
    /// <param name="solarArcs">Whether the solar arcs are in.</param>
    /// <param name="progressions">Whether the secondary progressions are in.</param>
    /// <param name="primaryDirections">Whether the primary directions are in.</param>
    /// <returns>The title.</returns>
    /// <exception cref="ArgumentException">Thrown when no technique is in.</exception>
    public static string Title(bool transits, bool solarArcs, bool progressions, bool primaryDirections) =>
        FormFor(transits, solarArcs, progressions, primaryDirections).Title;

    /// <summary>
    /// The stem of the default file name, between the chart's name and the dates: "Transits" whenever the
    /// transits are in; else "SolarArcs", "Progressions", "SolarArcsProgressions", "PrimaryDirections",
    /// "SolarArcsPrimaryDirections", "ProgressionsPrimaryDirections" or "SolarArcsProgressionsPrimaryDirections".
    /// </summary>
    /// <param name="transits">Whether the transit scan ran.</param>
    /// <param name="solarArcs">Whether the solar arcs are in.</param>
    /// <param name="progressions">Whether the secondary progressions are in.</param>
    /// <param name="primaryDirections">Whether the primary directions are in.</param>
    /// <returns>The file stem.</returns>
    /// <exception cref="ArgumentException">Thrown when no technique is in.</exception>
    public static string FileStem(bool transits, bool solarArcs, bool progressions, bool primaryDirections) =>
        FormFor(transits, solarArcs, progressions, primaryDirections).FileStem;

    /// <summary>
    /// The subject of the save dialog's title, "Export {Subject} as Markdown": "Transits" whenever the
    /// transits are in; else "Solar Arcs", "Progressions", "Solar Arcs and Progressions", "Primary
    /// Directions", "Solar Arcs and Primary Directions", "Progressions and Primary Directions" or "Solar
    /// Arcs, Progressions and Primary Directions".
    /// </summary>
    /// <param name="transits">Whether the transit scan ran.</param>
    /// <param name="solarArcs">Whether the solar arcs are in.</param>
    /// <param name="progressions">Whether the secondary progressions are in.</param>
    /// <param name="primaryDirections">Whether the primary directions are in.</param>
    /// <returns>The subject.</returns>
    /// <exception cref="ArgumentException">Thrown when no technique is in.</exception>
    public static string DialogSubject(bool transits, bool solarArcs, bool progressions, bool primaryDirections) =>
        FormFor(transits, solarArcs, progressions, primaryDirections).DialogSubject;

    /// <summary>The report's title for a result: the techniques it holds.</summary>
    /// <param name="result">The transit result.</param>
    /// <returns>The title.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public static string Title(TransitResult result) => FormFor(result).Title;

    /// <summary>The default file name's stem for a result.</summary>
    /// <param name="result">The transit result.</param>
    /// <returns>The file stem.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public static string FileStem(TransitResult result) => FormFor(result).FileStem;

    /// <summary>The save dialog's subject for a result.</summary>
    /// <param name="result">The transit result.</param>
    /// <returns>The subject.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public static string DialogSubject(TransitResult result) => FormFor(result).DialogSubject;

    /// <summary>The form for a result: the transits when the scan ran, the techniques it holds otherwise.</summary>
    /// <param name="result">The transit result.</param>
    /// <returns>The form.</returns>
    private static Form FormFor(TransitResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return FormFor(result.IncludesTransits, result.SolarArcs is not null, result.Progressions is not null, result.PrimaryDirections is not null);
    }

    /// <summary>The form for a combination of techniques.</summary>
    /// <param name="transits">Whether the transit scan ran.</param>
    /// <param name="solarArcs">Whether the solar arcs are in.</param>
    /// <param name="progressions">Whether the secondary progressions are in.</param>
    /// <param name="primaryDirections">Whether the primary directions are in.</param>
    /// <returns>The form.</returns>
    /// <exception cref="ArgumentException">Thrown when no technique is in.</exception>
    private static Form FormFor(bool transits, bool solarArcs, bool progressions, bool primaryDirections) =>
        (transits, solarArcs, progressions, primaryDirections) switch
        {
            (true, _, _, _) => Transits,
            (false, true, false, false) => SolarArcs,
            (false, false, true, false) => Progressions,
            (false, true, true, false) => SolarArcsProgressions,
            (false, false, false, true) => PrimaryDirections,
            (false, true, false, true) => SolarArcsPrimaryDirections,
            (false, false, true, true) => ProgressionsPrimaryDirections,
            (false, true, true, true) => SolarArcsProgressionsPrimaryDirections,
            (false, false, false, false) => throw new ArgumentException(
                "A report holds at least one technique: transits, solar arcs, secondary progressions or primary directions.", nameof(transits)),
        };
}
