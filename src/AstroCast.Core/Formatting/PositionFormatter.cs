using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Centralised formatting methods for astrological positions, aspects,
/// and midpoint activations. These are presentation concerns that were
/// previously embedded in domain model classes.
/// </summary>
/// <remarks>
/// <para>
/// All methods are pure functions operating on domain model data.
/// No side effects or external dependencies.
/// </para>
/// <para>
/// The model classes (<see cref="PlanetPosition"/>, <see cref="AspectResult"/>,
/// <see cref="MidpointActivation"/>, <see cref="HouseData"/>) carry no formatting
/// of their own: every printed form of their values is made here, and callers
/// call these methods directly.
/// </para>
/// </remarks>
public static class PositionFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Formats a <see cref="PlanetPosition"/> as DD°MM′SS″ Sign.
    /// </summary>
    /// <param name="position">The planet position to format.</param>
    /// <returns>Formatted string such as "15°23′47″ Aries".</returns>
    public static string FormatPosition(PlanetPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return string.Create(Inv, $"{position.SignDegrees:D2}\u00b0{position.SignMinutes:D2}\u2032{position.SignSeconds:D2}\u2033 {position.Sign}");
    }

    /// <summary>
    /// Formats an ecliptic longitude as DD°MM′SS″ Sign notation.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in decimal degrees (0-360).</param>
    /// <returns>Formatted string such as "15°23′47″ Aries".</returns>
    public static string FormatLongitude(double longitude)
    {
        double normalised = longitude % 360.0;
        int signIndex = (int)(normalised / 30.0);
        ZodiacSign sign = (ZodiacSign)(signIndex + 1);
        double posInSign = normalised % 30.0;
        int degrees = (int)posInSign;
        int minutes = (int)((posInSign - degrees) * 60.0);
        int seconds = (int)(((posInSign - degrees) * 60.0 - minutes) * 60.0);
        return string.Create(Inv, $"{degrees:D2}\u00b0{minutes:D2}\u2032{seconds:D2}\u2033 {sign}");
    }

    /// <summary>
    /// Formats an <see cref="AspectResult"/> as a display string.
    /// </summary>
    /// <param name="aspect">The aspect result to format.</param>
    /// <returns>
    /// Formatted string such as "Sun          cnj Moon          2°15′ Ap", the orb from
    /// <see cref="FormatOrb"/>.
    /// </returns>
    public static string FormatAspect(AspectResult aspect)
    {
        ArgumentNullException.ThrowIfNull(aspect);
        return string.Create(Inv, $"{aspect.Body1Name,-12} {aspect.Aspect.Abbreviation} {aspect.Body2Name,-12} {FormatOrb(aspect.Orb)} {aspect.ApplyingSeparating}");
    }

    /// <summary>
    /// Formats an ecliptic latitude as a signed DD°MM′SS″ string.
    /// </summary>
    /// <param name="latitude">Ecliptic latitude in decimal degrees (negative = south of ecliptic).</param>
    /// <returns>Formatted string such as "+01°23′47″" or "-00°12′34″".</returns>
    public static string FormatLatitude(double latitude)
    {
        string sign = latitude >= 0 ? "+" : "-";
        double abs = Math.Abs(latitude);
        int degrees = (int)abs;
        int minutes = (int)((abs - degrees) * 60.0);
        int seconds = (int)(((abs - degrees) * 60.0 - minutes) * 60.0);
        return string.Create(Inv, $"{sign}{degrees:D2}\u00b0{minutes:D2}\u2032{seconds:D2}\u2033");
    }

    /// <summary>
    /// Formats a declination as a signed DD°MM′SS″ string.
    /// </summary>
    /// <param name="declination">Declination in decimal degrees (positive = north, negative = south).</param>
    /// <returns>Formatted string such as "+23°26′12″" or "-15°08′45″".</returns>
    public static string FormatDeclination(double declination)
    {
        string sign = declination >= 0 ? "+" : "-";
        double abs = Math.Abs(declination);
        int degrees = (int)abs;
        int minutes = (int)((abs - degrees) * 60.0);
        int seconds = (int)(((abs - degrees) * 60.0 - minutes) * 60.0);
        return string.Create(Inv, $"{sign}{degrees:D2}\u00b0{minutes:D2}\u2032{seconds:D2}\u2033");
    }

    /// <summary>
    /// Formats a right ascension in decimal degrees (0–360) as HHh MMm SSs.
    /// Right ascension is converted from degrees to hours by dividing by 15.
    /// </summary>
    /// <param name="raDegrees">Right ascension in decimal degrees (0–360).</param>
    /// <returns>Formatted string such as "12h 34m 56s".</returns>
    public static string FormatRightAscension(double raDegrees)
    {
        double raHours = raDegrees / 15.0;
        int hours = (int)raHours;
        int minutes = (int)((raHours - hours) * 60.0);
        int seconds = (int)(((raHours - hours) * 60.0 - minutes) * 60.0);
        return string.Create(Inv, $"{hours:D2}h {minutes:D2}m {seconds:D2}s");
    }

    /// <summary>
    /// Formats an orb as <c>D°MM′</c>: the degrees unpadded, the minutes two digits, the degree sign
    /// (U+00B0) and the prime (U+2032), truncated to the minute. The one function every orb printed in
    /// minutes goes through (branch O-D3, 1.3.4): the chart, PDF, synastry, transit, timeline, daily
    /// horoscope, electional, medical, profection and horary exports.
    /// </summary>
    /// <remarks>
    /// The whole minutes are <c>Math.Floor(|orb| × 60 + 1e-9)</c>: no rounding and no carry, so 1.9999° is
    /// <c>1°59′</c>, 0.063724° is <c>0°03′</c>, 2.3083° is <c>2°18′</c> and 0.75° is <c>0°45′</c>, as a
    /// position is truncated to the second. The guard, a billionth of a minute (6e-8 arc-second), lets
    /// a double that lands a hair below a whole minute by arithmetic (2 − 0.01 × 120 = 0.7999999999999998,
    /// which is 0.8° by construction) print that minute, <c>0°48′</c>, and leaves a real sub-minute
    /// difference untouched (R1a). Until 1.3.4 the minutes rounded half-up (1.9999° printed
    /// <c>2°00′</c>) and only the electional export called this; the other exports truncated inline, with
    /// an apostrophe or without the degree sign. Paran orbs (minutes of time), the JSON's numbers and the
    /// orb limits stated in whole degrees do not go through it.
    /// </remarks>
    /// <param name="orbDegrees">Orb in decimal degrees; a negative value is formatted by its magnitude.</param>
    /// <returns>The orb as <c>D°MM′</c>, such as "0°03′" or "2°18′".</returns>
    public static string FormatOrb(double orbDegrees)
    {
        int totalMinutes = (int)Math.Floor(Math.Abs(orbDegrees) * 60.0 + 1e-9);
        return string.Create(Inv, $"{totalMinutes / 60}\u00b0{totalMinutes % 60:D2}\u2032");
    }

    /// <summary>
    /// Formats an arc of direction as <c>D°MM′</c> (branch PrimaryExport, 1.5.0): the whole degrees unpadded,
    /// the minutes truncated to two digits, the degree sign (U+00B0) and the prime (U+2032), as
    /// <see cref="FormatOrb"/> prints an orb: 16.001096° is <c>16°00′</c>, 0.999° is <c>0°59′</c>, 98.9999° is
    /// <c>98°59′</c>. The primary-direction tables print every arc through it.
    /// </summary>
    /// <param name="arcDegrees">The arc in decimal degrees, 0 to 360.</param>
    /// <returns>The arc as <c>D°MM′</c>.</returns>
    public static string FormatArc(double arcDegrees)
    {
        int totalMinutes = (int)Math.Floor(arcDegrees * 60.0 + 1e-9);
        return string.Create(Inv, $"{totalMinutes / 60}\u00b0{totalMinutes % 60:D2}\u2032");
    }

    /// <summary>
    /// Determines whether a declination is out-of-bounds, meaning its absolute value
    /// exceeds the chart's true obliquity of the ecliptic (<see cref="ChartResult.Obliquity"/>).
    /// A declination equal to the obliquity is in bounds.
    /// </summary>
    /// <param name="declination">Declination in decimal degrees, or null if not calculated.</param>
    /// <param name="obliquity">The chart's true obliquity of the ecliptic, in degrees.</param>
    /// <returns>True if the declination has a value and its absolute value exceeds <paramref name="obliquity"/>.</returns>
    public static bool IsOutOfBounds(double? declination, double obliquity)
    {
        return declination.HasValue && Math.Abs(declination.Value) > obliquity;
    }

    /// <summary>
    /// Determines whether a body is out-of-bounds, meaning its declination exceeds the
    /// chart's true obliquity of the ecliptic, as <see cref="IsOutOfBounds(double?, double)"/> reads it.
    /// </summary>
    /// <remarks>
    /// The Sun is never out of bounds, since the bounds are its own greatest declination.
    /// Near a solstice its apparent declination can pass the true obliquity by up to 0.0003°,
    /// through its ecliptic latitude (about 1″).
    /// </remarks>
    /// <param name="position">The body's position.</param>
    /// <param name="obliquity">The chart's true obliquity of the ecliptic, in degrees.</param>
    /// <returns>False for the Sun; otherwise true if the body's declination exceeds <paramref name="obliquity"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="position"/> is <see langword="null"/>.</exception>
    public static bool IsOutOfBounds(PlanetPosition position, double obliquity)
    {
        ArgumentNullException.ThrowIfNull(position);

        if (position.Body == CelestialBodyId.Sun)
            return false;

        return IsOutOfBounds(position.Declination, obliquity);
    }

    /// <summary>
    /// Formats a <see cref="MidpointActivation"/> as a display string.
    /// </summary>
    /// <param name="activation">The midpoint activation to format.</param>
    /// <returns>
    /// Formatted string such as "Sun/Moon         = Mars          0°45′", the orb from
    /// <see cref="FormatOrb"/>.
    /// </returns>
    public static string FormatActivation(MidpointActivation activation)
    {
        ArgumentNullException.ThrowIfNull(activation);
        string harmonic = activation.HarmonicAngle == 0 ? "   " : string.Create(Inv, $"{activation.HarmonicAngle,3}\u00b0");
        return string.Create(Inv, $"{activation.Midpoint.Notation,-16} = {activation.ActivatorName,-12} {harmonic} {FormatOrb(activation.Orb)}");
    }

    /// <summary>
    /// Appends a cross-chart declination aspects table to a <see cref="System.Text.StringBuilder"/>.
    /// Used by synastry, solar return, lunar return, event, and electional formatters.
    /// Only emits the section when at least one aspect exists.
    /// </summary>
    /// <param name="sb">The <see cref="System.Text.StringBuilder"/> to append to.</param>
    /// <param name="aspects">Cross-chart declination aspects sorted by orb ascending.</param>
    /// <param name="sectionTitle">The Markdown heading (e.g., "## Inter-Chart Declination Aspects (±1° orb)").</param>
    /// <param name="prefixA">Prefix for chart A planet names (e.g., "A", "SR", "LR", "EV", "CA").</param>
    /// <param name="headerA">Column header for chart A (e.g., "Person A", "Return", "Event", "Candidate").</param>
    /// <param name="decHeaderA">Column header for chart A declination (e.g., "A Dec", "SR Dec", "Ev Dec").</param>
    /// <param name="prefixB">Prefix for chart B planet names (e.g., "B", "NA").</param>
    /// <param name="headerB">Column header for chart B (e.g., "Person B", "Natal").</param>
    /// <param name="decHeaderB">Column header for chart B declination (e.g., "B Dec", "Na Dec").</param>
    internal static void AppendCrossChartDeclinationTable(
        System.Text.StringBuilder sb,
        IReadOnlyList<DeclinationAspect> aspects,
        string sectionTitle,
        string prefixA, string headerA, string decHeaderA,
        string prefixB, string headerB, string decHeaderB)
    {
        if (aspects.Count == 0)
            return;

        sb.AppendLine(sectionTitle);
        sb.AppendLine();
        sb.AppendLine($"| {headerA} | {decHeaderA} | Aspect | {headerB} | {decHeaderB} | Orb |");
        sb.AppendLine("|---------|--------|--------|-------|--------|-----|");

        foreach (DeclinationAspect a in aspects)
        {
            string decA = FormatDeclination(a.Body1Declination);
            string decB = FormatDeclination(a.Body2Declination);
            sb.AppendLine(
                $"| {prefixA} {a.Body1Name} | {decA} | {a.Abbreviation} | {prefixB} {a.Body2Name} | {decB} | {FormatOrb(a.Orb)} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }
}
