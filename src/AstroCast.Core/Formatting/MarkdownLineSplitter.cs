namespace AstroCast.Core.Formatting;

/// <summary>
/// Line-splitting helper for Markdown formatters that embed the output of
/// another formatter by splitting it into lines and re-joining a subset.
/// </summary>
/// <remarks>
/// Sub-formatters build their output with <c>StringBuilder.AppendLine</c>,
/// which terminates lines with <see cref="Environment.NewLine"/> -- CRLF on
/// Windows, LF on Unix-like systems. Splitting that output on '\n' alone
/// leaves a trailing '\r' on every line on Windows; when an embedding
/// formatter re-joins a subset of those lines, the last kept line's stray
/// '\r' meets the embedder's own CRLF terminator and produces a CR CR LF
/// sequence that LF-normalisation expands into a phantom blank line. That
/// made embedded-section output differ between Windows and macOS (found by
/// the 2026-07 macOS acceptance run of the golden suites). This helper
/// strips every CR before splitting so embed logic is line-ending-agnostic
/// and formatter output is byte-identical across platforms after
/// normalisation.
/// </remarks>
internal static class MarkdownLineSplitter
{
    /// <summary>
    /// Split formatter output into lines regardless of the line-ending
    /// convention it was produced with.
    /// </summary>
    /// <param name="value">Formatter output using CRLF, LF, or bare CR line endings.</param>
    /// <returns>The lines of <paramref name="value"/> with no line-ending characters retained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    internal static string[] SplitLines(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
    }
}
