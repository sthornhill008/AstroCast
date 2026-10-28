using System.Globalization;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a UTC offset as the label a chart shows beside its time: "EST (UTC−5)",
/// "UTC+5.5", "LMT (UTC−4:56:02)", "UTC−5 (explicit)". A value formatter in the manner
/// of <see cref="PositionFormatter"/>: the chart engine and the entry form both call it,
/// so the label a chart is saved with is the label the form showed.
/// </summary>
/// <remarks>
/// <para>
/// <b>The name.</b> A zone's label leads with the zone record's name for the offset when
/// that name is a word ("EST", "BST", "LMT", "PMT"). IANA gives many zones only a numeric
/// name ("+06", "+0330"); that adds nothing to the offset, so it is left out. An explicit
/// offset is the user's own and is never named; it is marked "(explicit)" instead.
/// </para>
/// <para>
/// <b>The offset.</b> A whole or half hour prints as hours, as it always has: "UTC−5",
/// "UTC+5.5". Anything else prints as h:mm, or h:mm:ss when it carries seconds:
/// "UTC+5:45" for Nepal, "UTC−4:56:02" for New York's local mean time. The offset is
/// taken to the nearest second. The sign is "+" for zero and east of Greenwich, U+2212
/// for west.
/// </para>
/// <para>
/// The label is culture-independent: every number is formatted with the invariant culture.
/// </para>
/// </remarks>
public static class OffsetLabelFormatter
{
    /// <summary>Invariant culture used for all numeric formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Seconds in half an hour: offsets on this grid print as hours.</summary>
    private const long HalfHourSeconds = 1800;

    /// <summary>
    /// Formats an offset as a chart's offset label.
    /// </summary>
    /// <param name="offset">The offset from UTC; east of Greenwich is positive.</param>
    /// <param name="zoneName">
    /// The zone record's name for the offset ("EST", "+06"), or <see langword="null"/> when
    /// there is none. Ignored when <paramref name="isExplicit"/> is <see langword="true"/>.
    /// </param>
    /// <param name="isExplicit">Whether the offset is one the user entered, rather than a zone's.</param>
    /// <returns>The label.</returns>
    public static string Format(TimeSpan offset, string? zoneName, bool isExplicit)
    {
        long totalSeconds = (long)Math.Round(offset.TotalSeconds, MidpointRounding.AwayFromZero);
        string sign = totalSeconds >= 0 ? "+" : "−";
        long abs = Math.Abs(totalSeconds);

        string amount;
        if (abs % HalfHourSeconds == 0)
        {
            double hours = abs / 3600.0;
            amount = hours.ToString(abs % 3600 == 0 ? "0" : "0.#", Inv);
        }
        else
        {
            long h = abs / 3600;
            long m = abs % 3600 / 60;
            long s = abs % 60;
            amount = s == 0
                ? string.Create(Inv, $"{h}:{m:00}")
                : string.Create(Inv, $"{h}:{m:00}:{s:00}");
        }

        string utcPart = $"UTC{sign}{amount}";

        if (isExplicit)
            return $"{utcPart} (explicit)";

        return IsWord(zoneName) ? $"{zoneName} ({utcPart})" : utcPart;
    }

    /// <summary>
    /// Whether a zone record's name is a word, made only of ASCII letters, rather than a
    /// numeric name such as "+06".
    /// </summary>
    /// <param name="zoneName">The name, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for a non-empty name of letters only.</returns>
    private static bool IsWord(string? zoneName) =>
        !string.IsNullOrEmpty(zoneName) && zoneName.All(char.IsAsciiLetter);
}
