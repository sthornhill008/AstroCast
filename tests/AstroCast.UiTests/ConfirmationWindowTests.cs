using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Services;

namespace AstroCast.UiTests;

/// <summary>
/// Both heads default a Yes/No prompt to No (O-8, H-03): the Avalonia confirmation window opens with No focused,
/// so Enter and Space decline. Every prompt it shows asks before something that cannot be taken back: an import,
/// an overwrite, a delete, Export Both's Markdown replacing a file.
/// </summary>
public sealed class ConfirmationWindowTests
{
    /// <summary>The window opens with the keyboard focus on No.</summary>
    [AvaloniaFact]
    public void ConfirmationWindow_OpensWithNoFocused()
    {
        ConfirmationWindow window = new("Delete chart 'X'?\n\nThis cannot be undone.", "Confirm Delete");
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Button no = Assert.Single(
            window.GetLogicalDescendants().OfType<Button>(),
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "No");
        Assert.Same(no, window.FocusManager?.GetFocusedElement());

        window.Close();
    }
}
