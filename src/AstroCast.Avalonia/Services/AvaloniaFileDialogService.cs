using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Avalonia.Services;

/// <summary>
/// Avalonia implementation of <see cref="IFileDialogService"/>, backed by
/// <see cref="IStorageProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the Avalonia half of the file-picker seam. <see cref="IStorageProvider"/>
/// is the abstraction Avalonia surfaces over each platform's native picker — the
/// Win32 common dialogs here, <c>NSSavePanel</c>/<c>NSOpenPanel</c> on macOS — so
/// the dialogs are the real system ones on both targets rather than a drawn
/// substitute.
/// </para>
/// <para>
/// <b>Apple uniform type identifiers are why <see cref="FileDialogFileType"/>
/// carries them.</b> The macOS panels filter by UTI, not by glob pattern, so a
/// type mapped with patterns alone would offer the user a picker that greys out
/// the very files it is meant to accept. Slice 0 put the identifiers on the model
/// for exactly this projection; this is the first code to consume them.
/// </para>
/// <para>
/// <b>Empty lists are mapped to <see langword="null"/> — defensively, not as a
/// fix.</b> <see cref="FilePickerFileType"/> treats an unset collection as "not
/// specified" and an empty collection as "specified, and matches nothing", so an
/// empty <see cref="FileDialogFileType.MimeTypes"/> or
/// <see cref="FileDialogFileType.AppleUniformTypeIdentifiers"/> passed straight
/// through would produce a type that matches no file at all on a platform that
/// consults that collection.
/// </para>
/// <para>
/// <b>No current call site can reach it.</b> All six predefined
/// <see cref="FileDialogFileTypes"/> members declare non-empty MIME and UTI
/// lists, and every one of the 26 dialog sites uses one of the four predefined
/// sets built from them, so the empty branch is unreachable from the
/// application as it stands. An earlier note described this mapping as
/// correcting an observable defect; it does not, and never did. It is retained
/// because <see cref="FileDialogFileType"/> permits the empty shape and a type
/// added later without a UTI would otherwise fail on macOS only — a defect that
/// would not reproduce on the machine most likely to introduce it.
/// </para>
/// <para>
/// <b>No top-level means "cancelled", never an exception.</b> The same discipline
/// <see cref="AvaloniaUiDispatcher"/> applies to the dispatcher: a headless
/// process has no window and therefore no storage provider, so both members return
/// <see langword="null"/> — the value the interface already defines as
/// cancellation and which every call site guards on by writing nothing.
/// </para>
/// <para>
/// <b>That probe must precede any dispatcher access.</b> An earlier revision
/// marshalled onto the UI thread first and probed inside the marshalled delegate.
/// It passed in isolation and failed in the full suite with
/// <see cref="TaskCanceledException"/>, because the headless UI tests latch the
/// process-wide <see cref="Dispatcher.UIThread"/> and then tear their session
/// down, leaving a dispatcher that accepts posts and cancels them. Checking
/// <see cref="Application.Current"/> first keeps the headless path away from the
/// dispatcher entirely.
/// </para>
/// </remarks>
public sealed class AvaloniaFileDialogService : IFileDialogService
{
    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <see cref="FilePickerSaveOptions.ShowOverwritePrompt"/> is set so the native
    /// picker warns before replacing an existing file, matching
    /// <c>SaveFileDialog.OverwritePrompt</c>, which the WPF head leaves at its
    /// default of <see langword="true"/>. Without it the two heads would differ on
    /// silent overwrite.
    /// </para>
    /// <para>
    /// Returns <see langword="null"/> when the chosen location has no local
    /// filesystem path. Every call site writes with <c>System.IO</c>, which cannot
    /// address a virtual or remote storage item, so reporting cancellation is the
    /// honest outcome — the alternative is handing back a path that then fails at
    /// the write.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any argument is <see langword="null"/>.
    /// </exception>
    public async Task<string?> ShowSaveAsync(
        string title,
        IReadOnlyList<FileDialogFileType> fileTypes,
        string defaultFileName,
        string defaultExt)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(fileTypes);
        ArgumentNullException.ThrowIfNull(defaultFileName);
        ArgumentNullException.ThrowIfNull(defaultExt);

        return await OnUiThreadAsync(async () =>
        {
            IStorageProvider? storage = ResolveStorageProvider();

            if (storage is null || !storage.CanSave)
            {
                return null;
            }

            FilePickerSaveOptions options = new()
            {
                Title = title,
                SuggestedFileName = defaultFileName,
                DefaultExtension = NormaliseExtension(defaultExt),
                FileTypeChoices = MapFileTypes(fileTypes),
                ShowOverwritePrompt = true,
            };

            IStorageFile? file = await storage.SaveFilePickerAsync(options);
            return file?.TryGetLocalPath();
        });
    }

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="defaultExt"/> has no counterpart in
    /// <see cref="FilePickerOpenOptions"/> and is accepted only to satisfy the
    /// interface. It is meaningful for a save dialog, which appends it to a
    /// name the user typed without one; an open dialog selects an existing file,
    /// so there is nothing to append to. The WPF head sets
    /// <c>OpenFileDialog.DefaultExt</c> with equally little effect.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="title"/>, <paramref name="fileTypes"/>, or
    /// <paramref name="defaultExt"/> is <see langword="null"/>.
    /// </exception>
    public async Task<string?> ShowOpenAsync(
        string title,
        IReadOnlyList<FileDialogFileType> fileTypes,
        string defaultExt)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(fileTypes);
        ArgumentNullException.ThrowIfNull(defaultExt);

        return await OnUiThreadAsync(async () =>
        {
            IStorageProvider? storage = ResolveStorageProvider();

            if (storage is null || !storage.CanOpen)
            {
                return null;
            }

            FilePickerOpenOptions options = new()
            {
                Title = title,
                FileTypeFilter = MapFileTypes(fileTypes),
                AllowMultiple = false,
            };

            IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(options);
            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        });
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread, joining it inline when the
    /// caller is already there.
    /// </summary>
    /// <param name="action">The picker invocation to run.</param>
    /// <returns>The chosen path, or <see langword="null"/>.</returns>
    /// <remarks>
    /// Showing a picker is a UI-thread operation. Call sites await these members
    /// from the UI thread today, so the fast path is the normal one; the marshal
    /// exists so a future caller awaiting from a background continuation does not
    /// crash.
    /// </remarks>
    private static Task<string?> OnUiThreadAsync(Func<Task<string?>> action)
    {
        // The window probe must come FIRST, before anything touches
        // Dispatcher.UIThread. See the type-level remarks.
        if (!HasOwnerWindow)
        {
            return Task.FromResult<string?>(null);
        }

        return Dispatcher.UIThread.CheckAccess()
            ? action()
            // The Func<Task<T>> overload of InvokeAsync already unwraps,
            // returning Task<T> rather than Task<Task<T>>.
            : Dispatcher.UIThread.InvokeAsync(action);
    }

    /// <summary>
    /// Gets a value indicating whether a desktop main window exists to hang a
    /// picker off.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="Application.Current"/> and the lifetime only. It must not
    /// consult <see cref="Dispatcher.UIThread"/>, because reading that property
    /// latches the process-wide UI thread as a side effect and, once a headless
    /// session has come and gone, posting to it yields a cancelled operation.
    /// </remarks>
    private static bool HasOwnerWindow =>
        Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime { MainWindow: not null };

    /// <summary>
    /// Projects the application's file types onto Avalonia's picker types.
    /// </summary>
    /// <param name="fileTypes">The types to offer, in display order.</param>
    /// <returns>The equivalent <see cref="FilePickerFileType"/> list.</returns>
    /// <remarks>
    /// <see cref="FileDialogFileType.Name"/> is carried through verbatim because it
    /// is the user-visible text in the picker's type dropdown. MIME types and Apple
    /// uniform type identifiers are omitted rather than passed as empty lists; see
    /// the type-level remarks for why that distinction matters.
    /// </remarks>
    private static IReadOnlyList<FilePickerFileType> MapFileTypes(
        IReadOnlyList<FileDialogFileType> fileTypes)
    {
        List<FilePickerFileType> mapped = new(fileTypes.Count);

        foreach (FileDialogFileType type in fileTypes)
        {
            mapped.Add(new FilePickerFileType(type.Name)
            {
                Patterns = type.Patterns,
                MimeTypes = type.MimeTypes.Count > 0 ? type.MimeTypes : null,
                AppleUniformTypeIdentifiers = type.AppleUniformTypeIdentifiers.Count > 0
                    ? type.AppleUniformTypeIdentifiers
                    : null,
            });
        }

        return mapped;
    }

    /// <summary>
    /// Strips the leading dot from an extension, if present.
    /// </summary>
    /// <param name="extension">The extension as the call sites supply it (e.g., <c>".md"</c>).</param>
    /// <returns>The extension without a leading dot (e.g., <c>"md"</c>).</returns>
    /// <remarks>
    /// The interface's callers pass the dotted form, which is what WPF's
    /// <c>DefaultExt</c> accepts. Avalonia forwards this value to the platform
    /// backend — on Windows to the Win32 <c>lpstrDefExt</c> field, which expects the
    /// extension without a separator and appends nothing if given one.
    /// </remarks>
    private static string NormaliseExtension(string extension) =>
        extension.StartsWith('.') ? extension[1..] : extension;

    /// <summary>
    /// Resolves the storage provider from the application's main window.
    /// </summary>
    /// <returns>
    /// The provider, or <see langword="null"/> when the process has no Avalonia
    /// application, is not a classic desktop application, or has not yet created
    /// its main window.
    /// </returns>
    private static IStorageProvider? ResolveStorageProvider()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        Window? window = desktop.MainWindow;
        return window is null ? null : TopLevel.GetTopLevel(window)?.StorageProvider;
    }
}
