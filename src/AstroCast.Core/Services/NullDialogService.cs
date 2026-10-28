using AstroCast.Core.Interfaces;

namespace AstroCast.Core.Services;

/// <summary>
/// The no-op <see cref="IDialogService"/>: every confirmation reports that the
/// user declined. Used when no dialog host is available — a headless host, or a
/// UI head whose panels are not yet wired to anything that can prompt.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <see cref="IDialogService"/> is a required dependency
/// of <c>MainViewModel</c> — it is null-guarded in the constructor, unlike
/// <see cref="IFileDialogService"/>, which is optional and falls back to
/// <see cref="NullFileDialogService"/>. A composition root therefore has to
/// supply something even when nothing in the running UI can raise a prompt. This
/// is that something, and it sits beside <see cref="NullFileDialogService"/> so
/// the two null objects are found together.
/// </para>
/// <para>
/// <b>Returns <see langword="false"/>, deliberately.</b> All three call sites
/// gate a destructive or overwriting action: overwriting an existing chart on
/// save, deleting a chart, and importing over the current database. Returning
/// <see langword="false"/> means an unattended host declines, so the action is
/// skipped and nothing is lost. Returning <see langword="true"/> would silently
/// auto-approve a delete, which is the opposite of what "no one is here to ask"
/// should mean.
/// </para>
/// <para>
/// This also matches the interface's documented contract that anything other than
/// an explicit Yes is "do not proceed", so every call site already handles the
/// value correctly with no special-casing.
/// </para>
/// <para>
/// <b>Not a permanent implementation.</b> A UI head that can display dialogs
/// supplies its own; this is the placeholder for the stage before that exists.
/// </para>
/// </remarks>
public sealed class NullDialogService : IDialogService
{
    /// <summary>
    /// Gets the shared instance. The type is stateless, so one instance serves
    /// every caller.
    /// </summary>
    public static NullDialogService Instance { get; } = new();

    /// <summary>
    /// Returns <see langword="false"/>, reporting that the user declined the
    /// confirmation.
    /// </summary>
    /// <param name="message">Ignored — no dialog is shown.</param>
    /// <param name="title">Ignored — no dialog is shown.</param>
    /// <returns>
    /// A completed task yielding <see langword="false"/>, so the caller skips the
    /// guarded action.
    /// </returns>
    public Task<bool> ConfirmAsync(string message, string title) =>
        Task.FromResult(false);
}
