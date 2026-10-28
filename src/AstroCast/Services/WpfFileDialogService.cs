using Microsoft.Win32;

using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Services;

/// <summary>
/// WPF implementation of <see cref="IFileDialogService"/> using
/// <see cref="SaveFileDialog"/> and <see cref="OpenFileDialog"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the WPF half of the file-picker seam described on
/// <see cref="IFileDialogService"/>. It sets exactly the properties the export and
/// import commands set before the abstraction was introduced — <c>Title</c>,
/// <c>Filter</c>, <c>FileName</c>, <c>DefaultExt</c> — and calls the parameterless
/// <c>ShowDialog()</c> overload, so the dialogs behave identically to before on
/// Windows.
/// </para>
/// <para>
/// <b>Synchronous underneath, task-returning outside.</b> <c>ShowDialog()</c> is a
/// blocking modal call on the UI thread and has no asynchronous overload. Wrapping
/// its result in <see cref="Task.FromResult{TResult}"/> satisfies the async
/// contract without introducing any additional blocking or thread hop: the method
/// returns an already-completed task, so <c>await</c> at the call site resumes
/// synchronously. Deliberately not offloaded to a background thread — WPF dialogs
/// must be shown on the UI thread.
/// </para>
/// <para>
/// Returning <see langword="null"/> on cancel replaces the previous
/// <c>ShowDialog() == true</c> / <c>!= true</c> checks at the call sites.
/// </para>
/// </remarks>
public sealed class WpfFileDialogService : IFileDialogService
{
    /// <inheritdoc />
    public Task<string?> ShowSaveAsync(
        string title,
        IReadOnlyList<FileDialogFileType> fileTypes,
        string defaultFileName,
        string defaultExt)
    {
        SaveFileDialog dialog = new()
        {
            Title = title,
            Filter = BuildWin32Filter(fileTypes),
            FileName = defaultFileName,
            DefaultExt = defaultExt,
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    /// <inheritdoc />
    public Task<string?> ShowOpenAsync(
        string title,
        IReadOnlyList<FileDialogFileType> fileTypes,
        string defaultExt)
    {
        OpenFileDialog dialog = new()
        {
            Title = title,
            Filter = BuildWin32Filter(fileTypes),
            DefaultExt = defaultExt,
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FileName : null);
    }

    /// <summary>
    /// Rebuilds the Win32 pipe-delimited filter string from the structured file
    /// types.
    /// </summary>
    /// <param name="fileTypes">The file types to offer, in display order.</param>
    /// <returns>
    /// A Win32 filter string such as
    /// <c>"Markdown Files (*.md)|*.md|Text Files (*.txt)|*.txt"</c>.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Each type contributes a <c>"{Name} ({patterns})|{patterns}"</c> pair, where
    /// <c>{patterns}</c> is the type's patterns joined by <c>';'</c> (the Win32
    /// separator for multiple patterns within one entry); the pairs are then
    /// joined by <c>'|'</c>.
    /// </para>
    /// <para>
    /// This is the exact inverse of the literals the call sites used to pass, and
    /// reproduces all four of them byte for byte — including the lower-case
    /// <c>"Markdown files"</c> of the medical report export, which differs from
    /// the capitalised <c>"Markdown Files"</c> used everywhere else. That
    /// inconsistency is preserved deliberately; see
    /// <see cref="FileDialogFileTypes.MarkdownLowercase"/>.
    /// </para>
    /// </remarks>
    internal static string BuildWin32Filter(IReadOnlyList<FileDialogFileType> fileTypes)
    {
        ArgumentNullException.ThrowIfNull(fileTypes);

        return string.Join("|", fileTypes.Select(type =>
        {
            string patterns = string.Join(";", type.Patterns);
            return $"{type.Name} ({patterns})|{patterns}";
        }));
    }
}
