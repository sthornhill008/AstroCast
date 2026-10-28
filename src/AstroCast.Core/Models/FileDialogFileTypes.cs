namespace AstroCast.Core.Models;

/// <summary>
/// The predefined <see cref="FileDialogFileType"/> instances and type sets used by
/// the export and import commands.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why predefined rather than constructed per call site.</b> There are 26
/// file-dialog call sites but only four distinct filter combinations between
/// them. Building the type list inline at each site would duplicate the same
/// literals 26 times and invite exactly the kind of drift documented on
/// <see cref="MarkdownLowercase"/> below. The four sets here map one-to-one onto
/// the four Win32 filter strings the call sites passed before this type existed.
/// </para>
/// <para>
/// <b>These reproduce the previous filter strings exactly.</b> Rebuilding each
/// set as <c>"{Name} ({patterns})|{patterns}"</c> per type, joined by <c>'|'</c>,
/// yields byte-for-byte the four strings that were previously passed:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <see cref="MarkdownAndText"/> —
///     <c>"Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt"</c> (21 sites)
///   </description></item>
///   <item><description>
///     <see cref="JsonOnly"/> — <c>"JSON Files (*.json)|*.json"</c> (2 sites)
///   </description></item>
///   <item><description>
///     <see cref="PdfOnly"/> — <c>"PDF Files (*.pdf)|*.pdf"</c> (2 sites)
///   </description></item>
///   <item><description>
///     <see cref="MarkdownAndAllFiles"/> —
///     <c>"Markdown files (*.md)|*.md|All files (*.*)|*.*"</c> (1 site)
///   </description></item>
/// </list>
/// </remarks>
public static class FileDialogFileTypes
{
    /// <summary>
    /// Markdown documents, named <c>"Markdown Files"</c> with a capital F.
    /// Used by 21 of the 22 Markdown export sites.
    /// </summary>
    /// <remarks>
    /// Not interchangeable with <see cref="MarkdownLowercase"/> — see that
    /// member's remarks.
    /// </remarks>
    public static FileDialogFileType Markdown { get; } = new(
        "Markdown Files",
        ["*.md"],
        ["text/markdown"],
        ["net.daringfireball.markdown"]);

    /// <summary>
    /// Markdown documents, named <c>"Markdown files"</c> with a lower-case f.
    /// Used by exactly one call site: the medical astrology report export.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is a deliberate duplicate, not an oversight.</b> The medical
    /// export has always passed <c>"Markdown files (*.md)|*.md|All files (*.*)|*.*"</c>
    /// while the other 21 Markdown exports pass
    /// <c>"Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt"</c>. The casing
    /// difference is user-visible: it is the text in the dialog's file-type
    /// dropdown.
    /// </para>
    /// <para>
    /// Collapsing this into <see cref="Markdown"/> would silently change what
    /// that dialog reads. The introduction of this structured type was required
    /// to be a behaviour-preserving indirection, so the inconsistency is carried
    /// forward verbatim and named here so it is visible rather than hidden. If
    /// the casing is ever normalised, that is a deliberate UI change and this
    /// member should be deleted in the same commit.
    /// </para>
    /// </remarks>
    public static FileDialogFileType MarkdownLowercase { get; } = new(
        "Markdown files",
        ["*.md"],
        ["text/markdown"],
        ["net.daringfireball.markdown"]);

    /// <summary>Plain-text documents, named <c>"Text Files"</c>.</summary>
    public static FileDialogFileType Text { get; } = new(
        "Text Files",
        ["*.txt"],
        ["text/plain"],
        ["public.plain-text"]);

    /// <summary>PDF documents, named <c>"PDF Files"</c>.</summary>
    public static FileDialogFileType Pdf { get; } = new(
        "PDF Files",
        ["*.pdf"],
        ["application/pdf"],
        ["com.adobe.pdf"]);

    /// <summary>JSON documents, named <c>"JSON Files"</c>.</summary>
    public static FileDialogFileType Json { get; } = new(
        "JSON Files",
        ["*.json"],
        ["application/json"],
        ["public.json"]);

    /// <summary>
    /// The unrestricted entry, named <c>"All files"</c> with pattern
    /// <c>*.*</c>. Used by exactly one call site (the medical report export).
    /// </summary>
    public static FileDialogFileType AllFiles { get; } = new(
        "All files",
        ["*.*"],
        ["*/*"],
        ["public.item"]);

    /// <summary>
    /// Markdown then plain text — the filter used by 21 of the 26 file-dialog
    /// call sites. Equivalent to
    /// <c>"Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt"</c>.
    /// </summary>
    public static IReadOnlyList<FileDialogFileType> MarkdownAndText { get; } =
        [Markdown, Text];

    /// <summary>
    /// PDF only — equivalent to <c>"PDF Files (*.pdf)|*.pdf"</c>.
    /// Used by the PDF export and the combined PDF + Markdown export.
    /// </summary>
    public static IReadOnlyList<FileDialogFileType> PdfOnly { get; } = [Pdf];

    /// <summary>
    /// JSON only — equivalent to <c>"JSON Files (*.json)|*.json"</c>.
    /// Used by the database export and the database import.
    /// </summary>
    public static IReadOnlyList<FileDialogFileType> JsonOnly { get; } = [Json];

    /// <summary>
    /// Lower-case-named Markdown then the unrestricted entry — equivalent to
    /// <c>"Markdown files (*.md)|*.md|All files (*.*)|*.*"</c>. Used by exactly
    /// one call site, the medical astrology report export. See
    /// <see cref="MarkdownLowercase"/> for why this differs from
    /// <see cref="MarkdownAndText"/>.
    /// </summary>
    public static IReadOnlyList<FileDialogFileType> MarkdownAndAllFiles { get; } =
        [MarkdownLowercase, AllFiles];
}
