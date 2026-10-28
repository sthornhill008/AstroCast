namespace AstroCast.Core.Models;

/// <summary>
/// A single file-type entry offered by a file-picker dialog — the cross-platform
/// replacement for the Win32 pipe-delimited filter string that
/// <see cref="Interfaces.IFileDialogService"/> previously accepted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The Win32 filter syntax
/// (<c>"Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt"</c>) is a
/// Windows-only encoding: a non-Win32 head would have to parse it back apart to
/// drive its own picker. Avalonia's <c>IStorageProvider</c> instead takes a list
/// of <c>FilePickerFileType</c> objects, and the macOS <c>NSSavePanel</c> beneath
/// it is driven by uniform type identifiers rather than glob patterns. This type
/// carries all of that information in a plain BCL shape that
/// <c>AstroCast.Core</c> owns, so each head can project it into whatever its
/// picker wants.
/// </para>
/// <para>
/// <b>Deliberately not an enum.</b> An enum could not carry the display name,
/// the MIME types, or the Apple uniform type identifiers — and the display name
/// is load-bearing, because it is the text the user reads in the dialog's
/// file-type dropdown. See <see cref="FileDialogFileTypes"/> for the predefined
/// instances, which are the only ones the application uses.
/// </para>
/// <para>
/// <b>Reconstructing the Win32 string.</b> The WPF head rebuilds the original
/// filter as <c>"{Name} ({patterns})|{patterns}"</c> per type, joined by
/// <c>'|'</c>, where <c>{patterns}</c> is <see cref="Patterns"/> joined by
/// <c>';'</c>. That rule reproduces every filter string the call sites used
/// before this type was introduced, byte for byte.
/// </para>
/// </remarks>
public sealed class FileDialogFileType
{
    /// <summary>
    /// Initialises a new instance of the <see cref="FileDialogFileType"/> class.
    /// </summary>
    /// <param name="name">
    /// The human-readable type name shown in the dialog's file-type dropdown
    /// (e.g., <c>"Markdown Files"</c>). Casing is significant: it is reproduced
    /// verbatim in the Win32 filter string.
    /// </param>
    /// <param name="patterns">
    /// The glob patterns matching this type (e.g., <c>["*.md"]</c>). Must contain
    /// at least one entry.
    /// </param>
    /// <param name="mimeTypes">
    /// Optional MIME types (e.g., <c>["text/markdown"]</c>). Used by heads whose
    /// picker is MIME-driven; ignored by the WPF head. Defaults to empty.
    /// </param>
    /// <param name="appleUniformTypeIdentifiers">
    /// Optional Apple uniform type identifiers (e.g.,
    /// <c>["net.daringfireball.markdown"]</c>). The macOS native save and open
    /// panels are driven by these rather than by glob patterns. Defaults to empty.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is null, empty, or white space; or
    /// <paramref name="patterns"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="patterns"/> is <see langword="null"/>.
    /// </exception>
    public FileDialogFileType(
        string name,
        IReadOnlyList<string> patterns,
        IReadOnlyList<string>? mimeTypes = null,
        IReadOnlyList<string>? appleUniformTypeIdentifiers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(patterns);

        if (patterns.Count == 0)
        {
            throw new ArgumentException(
                "A file type must declare at least one pattern.", nameof(patterns));
        }

        Name = name;
        Patterns = patterns;
        MimeTypes = mimeTypes ?? [];
        AppleUniformTypeIdentifiers = appleUniformTypeIdentifiers ?? [];
    }

    /// <summary>
    /// Gets the human-readable type name shown in the dialog's file-type dropdown
    /// (e.g., <c>"Markdown Files"</c>).
    /// </summary>
    /// <remarks>
    /// Casing is significant — this string is user-visible and is reproduced
    /// verbatim when the WPF head rebuilds the Win32 filter.
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets the glob patterns matching this type (e.g., <c>["*.md"]</c>).
    /// Never empty.
    /// </summary>
    public IReadOnlyList<string> Patterns { get; }

    /// <summary>
    /// Gets the MIME types for this file type (e.g., <c>["text/markdown"]</c>),
    /// or an empty list when none are declared.
    /// </summary>
    /// <remarks>
    /// Unused by the WPF head; carried for pickers that are MIME-driven.
    /// </remarks>
    public IReadOnlyList<string> MimeTypes { get; }

    /// <summary>
    /// Gets the Apple uniform type identifiers for this file type (e.g.,
    /// <c>["net.daringfireball.markdown"]</c>), or an empty list when none are
    /// declared.
    /// </summary>
    /// <remarks>
    /// Unused by the WPF head; the macOS native panels are driven by these.
    /// </remarks>
    public IReadOnlyList<string> AppleUniformTypeIdentifiers { get; }
}
