using AstroCast.Core.Interfaces;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Test double for <see cref="IDialogService"/> that records calls and
/// returns configurable responses without showing UI dialogs.
/// </summary>
public sealed class TestDialogService : IDialogService
{
    /// <summary>
    /// Gets or sets the value to return from <see cref="ConfirmAsync"/>.
    /// Default is <c>true</c> (user confirms).
    /// </summary>
    public bool ConfirmResult { get; set; } = true;

    /// <summary>
    /// Gets the number of times <see cref="ConfirmAsync"/> was called.
    /// </summary>
    public int ConfirmCallCount { get; private set; }

    /// <summary>
    /// Gets the message from the last <see cref="ConfirmAsync"/> call.
    /// </summary>
    public string? LastConfirmMessage { get; private set; }

    /// <summary>
    /// Gets the title from the last <see cref="ConfirmAsync"/> call.
    /// </summary>
    public string? LastConfirmTitle { get; private set; }

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string message, string title)
    {
        ConfirmCallCount++;
        LastConfirmMessage = message;
        LastConfirmTitle = title;
        return Task.FromResult(ConfirmResult);
    }
}
