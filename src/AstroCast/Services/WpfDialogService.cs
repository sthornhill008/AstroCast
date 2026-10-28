using System.Windows;

using AstroCast.Core.Interfaces;

namespace AstroCast.Services;

/// <summary>
/// WPF implementation of <see cref="IDialogService"/> using <see cref="MessageBox"/>. The Yes/No prompt
/// defaults to No, so Enter declines, as the Avalonia head's confirmation window does (O-8, H-03; Steve's
/// ruling of 2026-10-14: both heads default a Yes/No prompt to No). Every prompt asks before something that
/// cannot be taken back: an import, an overwrite, a delete, Export Both's Markdown replacing a file. Until
/// 1.3.4 the box defaulted to Yes, so Enter confirmed.
/// </summary>
/// <remarks>
/// <b>Synchronous underneath, task-returning outside.</b> <see cref="MessageBox.Show(string, string, MessageBoxButton, MessageBoxImage, MessageBoxResult, MessageBoxOptions)"/>
/// is a blocking modal call on the UI thread and has no asynchronous overload.
/// Wrapping its result in <see cref="Task.FromResult{TResult}"/> satisfies the
/// async contract without introducing any additional blocking or thread hop — the
/// returned task is already complete, so <c>await</c> at the call site resumes
/// synchronously. Deliberately not offloaded to a background thread; WPF message
/// boxes must be shown on the UI thread.
/// </remarks>
public sealed class WpfDialogService : IDialogService
{
    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string message, string title)
    {
        MessageBoxResult result = MessageBox.Show(
            message,
            title,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No,
            MessageBoxOptions.None);

        return Task.FromResult(result == MessageBoxResult.Yes);
    }
}
