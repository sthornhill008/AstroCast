using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

using AstroCast.Core.Interfaces;

namespace AstroCast.Avalonia.Services;

/// <summary>
/// Avalonia implementation of <see cref="IDialogService"/>, backed by the
/// hand-rolled <see cref="ConfirmationWindow"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the Avalonia half of the confirmation seam. Unlike the WPF head, which
/// calls the synchronous <c>MessageBox.Show</c> and wraps an already-completed
/// task, this implementation is genuinely asynchronous: Avalonia's
/// <see cref="Window.ShowDialog{TResult}"/> is the only modal route and it returns
/// a task that completes when the window closes.
/// </para>
/// <para>
/// <b>No owner means "No", never an exception.</b>
/// <see cref="Window.ShowDialog{TResult}"/> requires a non-null owner and throws
/// otherwise, so a headless process — the UI test host, or any unit test that
/// constructs a real view model — would crash rather than proceed if this called
/// straight through. The same discipline
/// <see cref="AvaloniaUiDispatcher"/> applies to the dispatcher applies here: probe
/// for the owner first and degrade to the documented cancelled result. Returning
/// <see langword="false"/> is the correct degradation because every call site
/// treats it as "do not proceed", so an unattended process declines a destructive
/// action instead of silently performing one.
/// </para>
/// <para>
/// <b>Why the owner is resolved per call.</b> The main window does not exist when
/// the composition root builds this service — the service is constructed before
/// <c>desktop.MainWindow</c> is assigned — so caching it at construction would
/// capture <see langword="null"/> forever. Resolving through
/// <see cref="Application.Current"/> on each call also means the dialog follows
/// the main window if it is ever replaced.
/// </para>
/// <para>
/// <b>Thread affinity.</b> Showing a window is a UI-thread operation. Call sites
/// today await this from the UI thread, but the marshal through
/// <see cref="Dispatcher"/> costs nothing when already there and removes a
/// latent crash for any future caller that awaits from a background continuation.
/// </para>
/// <para>
/// <b>The owner probe must precede any dispatcher access, and the order is not
/// cosmetic.</b> An earlier revision marshalled first and probed inside the
/// marshalled delegate. That passed in isolation and failed in the full suite
/// with <see cref="TaskCanceledException"/>: the headless UI tests establish an
/// Avalonia session, which latches the process-wide
/// <see cref="Dispatcher.UIThread"/>, and tear it down when they finish. A later
/// plain test then posted onto that dead dispatcher, and the operation was
/// cancelled rather than run. Probing <see cref="Application.Current"/> first
/// means the headless path never touches the dispatcher at all — the same
/// conclusion <see cref="AvaloniaUiDispatcher"/> reached, arrived at again from a
/// different direction.
/// </para>
/// </remarks>
public sealed class AvaloniaDialogService : IDialogService
{
    /// <inheritdoc />
    /// <remarks>
    /// Returns <see langword="false"/> without showing anything when no owner
    /// window is available; see the type-level remarks.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="message"/> or <paramref name="title"/> is
    /// <see langword="null"/>.
    /// </exception>
    public Task<bool> ConfirmAsync(string message, string title)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(title);

        // The owner probe must come FIRST, before anything touches
        // Dispatcher.UIThread. See the type-level remarks: on the headless path
        // the dispatcher is not merely useless, it is actively harmful.
        Window? owner = ResolveOwner();

        if (owner is null)
        {
            return Task.FromResult(false);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            return ShowAsync(owner, message, title);
        }

        // The Func<Task<T>> overload of InvokeAsync already unwraps, returning
        // Task<T> rather than Task<Task<T>>.
        return Dispatcher.UIThread.InvokeAsync(() => ShowAsync(owner, message, title));
    }

    /// <summary>
    /// Shows the confirmation window on the UI thread and awaits the user's choice.
    /// </summary>
    /// <param name="owner">The already-resolved owner window.</param>
    /// <param name="message">The message body.</param>
    /// <param name="title">The window title.</param>
    /// <returns>
    /// A task yielding <see langword="true"/> when the user chose Yes, and
    /// <see langword="false"/> when they chose No or dismissed the window.
    /// </returns>
    private static Task<bool> ShowAsync(Window owner, string message, string title)
    {
        ConfirmationWindow dialog = new(message, title);
        return dialog.ShowDialog<bool>(owner);
    }

    /// <summary>
    /// Resolves the window that should own the dialog.
    /// </summary>
    /// <returns>
    /// The application's main window, or <see langword="null"/> when the process
    /// has no Avalonia application, is not a classic desktop application, or has
    /// not yet created its main window.
    /// </returns>
    private static Window? ResolveOwner() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
}
