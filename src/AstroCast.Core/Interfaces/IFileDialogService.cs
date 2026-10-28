using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Abstraction for the system file-picker dialogs used by the export and import
/// commands. Allows a view model to ask the user for a file path without coupling
/// to a specific UI framework's dialog types (e.g., WPF's
/// <c>Microsoft.Win32.SaveFileDialog</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The export commands previously constructed
/// <c>Microsoft.Win32.SaveFileDialog</c> / <c>OpenFileDialog</c> inline. Despite
/// the <c>Microsoft.Win32</c> namespace those are WPF types living in
/// <c>PresentationFramework</c>, so every export command pinned the view-model
/// layer to WPF. This interface is the cross-platform seam: the WPF head
/// implements it with the Win32 dialogs today, and an Avalonia head will
/// implement it with <c>IStorageProvider</c> (which surfaces the native
/// <c>NSSavePanel</c> on macOS) without the view models changing.
/// </para>
/// <para>
/// <b>Asynchronous by contract.</b> Both members return a <see cref="Task{TResult}"/>
/// because Avalonia's <c>IStorageProvider</c> is entirely task-returning — the
/// synchronous dialog classes were removed in Avalonia 12, so there is no
/// blocking route to model. The WPF head's <c>ShowDialog()</c> is synchronous and
/// simply returns an already-completed task; that introduces no extra blocking,
/// since <c>ShowDialog</c> is itself the modal UI-thread call.
/// </para>
/// <para>
/// <b>Cancellation is the only outcome callers distinguish.</b> Both members
/// return the chosen path, or <see langword="null"/> when the user cancels. Every
/// call site guards on that null and writes nothing. The parameters are exactly
/// the dialog properties the call sites set; nothing sets an initial directory, an
/// owner window, or multi-select, so none is modelled here.
/// </para>
/// <para>
/// <b>defaultExt is required, not optional.</b> Every call site sets it, and one
/// (the medical astrology report export) supplies a suggested file name with no
/// extension at all, relying on the dialog to append one. Making it optional
/// would silently change the produced filename at that site. It stays a separate
/// parameter rather than folding into the file types because Avalonia likewise
/// keeps <c>DefaultExtension</c> separate from its file-type list.
/// </para>
/// <para>
/// <b>File types.</b> The Win32 pipe-delimited filter string this interface
/// originally accepted has been replaced by <see cref="FileDialogFileType"/>,
/// which carries the display name, glob patterns, MIME types and Apple uniform
/// type identifiers each head needs. See <see cref="FileDialogFileTypes"/> for
/// the predefined sets; the WPF head rebuilds the original Win32 string from
/// them, byte for byte.
/// </para>
/// <para>
/// <b>Null object.</b> Callers should depend on a non-nullable instance and use
/// <see cref="Services.NullFileDialogService"/> when no picker is available,
/// rather than holding a nullable reference. A null-conditional call such as
/// <c>_service?.ShowSaveAsync(...)</c> still compiles once awaited, but yields a
/// null <see cref="Task{TResult}"/> and therefore a
/// <see cref="NullReferenceException"/> at runtime — silently converting the
/// intended no-op into a crash.
/// </para>
/// </remarks>
public interface IFileDialogService
{
    /// <summary>
    /// Prompts the user for a location to save a file.
    /// </summary>
    /// <param name="title">The dialog caption.</param>
    /// <param name="fileTypes">
    /// The file types offered in the dialog's type dropdown, in display order.
    /// Use a predefined set from <see cref="FileDialogFileTypes"/>.
    /// </param>
    /// <param name="defaultFileName">The pre-filled file name.</param>
    /// <param name="defaultExt">
    /// The extension appended when <paramref name="defaultFileName"/> carries none
    /// (e.g., <c>".md"</c>).
    /// </param>
    /// <returns>
    /// A task yielding the full path the user chose, or <see langword="null"/> if
    /// the dialog was cancelled.
    /// </returns>
    Task<string?> ShowSaveAsync(
        string title,
        IReadOnlyList<FileDialogFileType> fileTypes,
        string defaultFileName,
        string defaultExt);

    /// <summary>
    /// Prompts the user to select an existing file to open.
    /// </summary>
    /// <param name="title">The dialog caption.</param>
    /// <param name="fileTypes">
    /// The file types offered in the dialog's type dropdown, in display order.
    /// Use a predefined set from <see cref="FileDialogFileTypes"/>.
    /// </param>
    /// <param name="defaultExt">The default extension (e.g., <c>".json"</c>).</param>
    /// <returns>
    /// A task yielding the full path the user chose, or <see langword="null"/> if
    /// the dialog was cancelled.
    /// </returns>
    /// <remarks>
    /// No pre-filled file name is offered: the single call site (database import)
    /// does not set one, matching normal open-dialog behaviour.
    /// </remarks>
    Task<string?> ShowOpenAsync(
        string title,
        IReadOnlyList<FileDialogFileType> fileTypes,
        string defaultExt);
}
