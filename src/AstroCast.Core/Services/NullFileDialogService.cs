using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// The no-op <see cref="IFileDialogService"/>: every prompt reports that the user
/// cancelled. Used when no file picker is available — a headless host, or a test
/// that never exercises an export or import command.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a null object rather than a nullable reference.</b> The call sites
/// previously held an <c>IFileDialogService?</c> and invoked it with
/// <c>_service?.ShowSave(...)</c>, so a missing picker produced a null path and
/// the command silently wrote nothing. That pattern does not survive the move to
/// task-returning members: <c>await _service?.ShowSaveAsync(...)</c> still
/// compiles, but a null service makes the awaited expression a null
/// <see cref="Task{TResult}"/> and throws <see cref="NullReferenceException"/> —
/// turning a deliberate no-op into a crash.
/// </para>
/// <para>
/// Substituting this instance keeps the dependency non-nullable, lets
/// <c>await</c> compose cleanly, and reproduces the previous behaviour exactly:
/// both members return <see langword="null"/>, which every call site already
/// treats as "the user cancelled — write nothing".
/// </para>
/// <para>
/// <b>Returns null, never an empty string.</b> The call sites guard with
/// <c>path is null</c> / <c>path is not null</c>; an empty string would pass
/// those guards and drive a write to an invalid path.
/// </para>
/// </remarks>
public sealed class NullFileDialogService : IFileDialogService
{
    /// <summary>
    /// Gets the shared instance. The type is stateless, so one instance serves
    /// every caller.
    /// </summary>
    public static NullFileDialogService Instance { get; } = new();

    /// <summary>
    /// Returns <see langword="null"/>, reporting that the user cancelled the
    /// save dialog.
    /// </summary>
    /// <param name="title">Ignored — no dialog is shown.</param>
    /// <param name="fileTypes">Ignored — no dialog is shown.</param>
    /// <param name="defaultFileName">Ignored — no dialog is shown.</param>
    /// <param name="defaultExt">Ignored — no dialog is shown.</param>
    /// <returns>
    /// A completed task yielding <see langword="null"/>.
    /// </returns>
    public Task<string?> ShowSaveAsync(
        string title,
        IReadOnlyList<FileDialogFileType> fileTypes,
        string defaultFileName,
        string defaultExt) => Task.FromResult<string?>(null);

    /// <summary>
    /// Returns <see langword="null"/>, reporting that the user cancelled the
    /// open dialog.
    /// </summary>
    /// <param name="title">Ignored — no dialog is shown.</param>
    /// <param name="fileTypes">Ignored — no dialog is shown.</param>
    /// <param name="defaultExt">Ignored — no dialog is shown.</param>
    /// <returns>
    /// A completed task yielding <see langword="null"/>.
    /// </returns>
    public Task<string?> ShowOpenAsync(
        string title,
        IReadOnlyList<FileDialogFileType> fileTypes,
        string defaultExt) => Task.FromResult<string?>(null);
}
