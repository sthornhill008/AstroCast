namespace AstroCast.Core.Interfaces;

/// <summary>
/// Abstraction for user-facing dialog boxes (confirmation, alerts).
/// Allows the ViewModel to request user confirmation without coupling
/// to a specific UI framework (e.g., WPF MessageBox).
/// </summary>
/// <remarks>
/// <para>
/// <b>Asynchronous by contract.</b> <see cref="ConfirmAsync"/> returns a
/// <see cref="Task{TResult}"/> because Avalonia has no <c>MessageBox</c> — every
/// dialog route there is a task-returning show call on a window. The WPF head's
/// <c>MessageBox.Show</c> is synchronous and simply returns an already-completed
/// task, which introduces no extra blocking because <c>MessageBox.Show</c> is
/// itself the modal UI-thread call.
/// </para>
/// <para>
/// <b>No third state.</b> The result is a plain <see cref="bool"/>, not
/// <c>bool?</c>: the dialog offers Yes and No only, and every call site treats
/// anything other than an explicit Yes as "do not proceed". Implementations must
/// return <see langword="false"/> rather than throwing when the user dismisses
/// the dialog without choosing.
/// </para>
/// </remarks>
public interface IDialogService
{
    /// <summary>
    /// Displays a Yes/No confirmation dialog and returns the user's choice.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="title">The dialog title/caption.</param>
    /// <returns>
    /// A task yielding <c>true</c> if the user confirmed (Yes); <c>false</c>
    /// otherwise. Callers treat <c>false</c> as "skip the action".
    /// </returns>
    Task<bool> ConfirmAsync(string message, string title);
}
