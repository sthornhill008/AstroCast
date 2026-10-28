using Xunit;

using AstroCast.Avalonia.Services;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.UiTests;

/// <summary>
/// Verifies that the two real Avalonia dialog services degrade to "cancelled"
/// rather than throwing when no owner window exists.
/// </summary>
/// <remarks>
/// <para>
/// <b>What is actually being pinned.</b> Both services must reach a window to do
/// their work: <c>ShowDialog</c> throws without a non-null owner, and
/// <c>StorageProvider</c> hangs off a <c>TopLevel</c>. A headless process has
/// neither. The interfaces already define the degraded outcome — <c>false</c> for
/// a confirmation, <see langword="null"/> for a path — and every call site treats
/// those as "do not proceed" and "write nothing" respectively. So the correct
/// headless behaviour is to return them, and the failure mode being guarded
/// against is an exception escaping into a view-model command.
/// </para>
/// <para>
/// <b>Why <c>[Fact]</c> and not <c>[AvaloniaFact]</c>.</b> These must run with no
/// Avalonia application at all — that absence is the condition under test.
/// Running them inside the headless session would establish a lifetime and
/// exercise the opposite path. This mirrors the reasoning on
/// <see cref="AppPathsTests"/>, for a different reason.
/// </para>
/// <para>
/// <b>Coverage boundary.</b> These pin the no-window path only. The path where a
/// window does exist shows a real modal dialog and blocks on a human, so it is
/// verified by running the application rather than in this suite.
/// </para>
/// </remarks>
public sealed class DialogServiceHeadlessTests
{
    /// <summary>
    /// A confirmation with no owner window resolves to <c>false</c> — decline —
    /// rather than throwing or defaulting to the destructive answer.
    /// </summary>
    [Fact]
    public async Task ConfirmAsync_WithNoOwnerWindow_ReturnsFalse()
    {
        IDialogService service = new AvaloniaDialogService();

        bool confirmed = await service.ConfirmAsync("Overwrite it?", "Chart Exists");

        Assert.False(confirmed);
    }

    /// <summary>
    /// A save picker with no top level resolves to <see langword="null"/>, the
    /// value the interface defines as cancellation.
    /// </summary>
    [Fact]
    public async Task ShowSaveAsync_WithNoTopLevel_ReturnsNull()
    {
        IFileDialogService service = new AvaloniaFileDialogService();

        string? path = await service.ShowSaveAsync(
            "Export Chart as Markdown",
            FileDialogFileTypes.MarkdownAndText,
            "Chart.md",
            ".md");

        Assert.Null(path);
    }

    /// <summary>
    /// An open picker with no top level likewise resolves to
    /// <see langword="null"/>.
    /// </summary>
    [Fact]
    public async Task ShowOpenAsync_WithNoTopLevel_ReturnsNull()
    {
        IFileDialogService service = new AvaloniaFileDialogService();

        string? path = await service.ShowOpenAsync(
            "Import Database",
            FileDialogFileTypes.JsonOnly,
            ".json");

        Assert.Null(path);
    }

    /// <summary>
    /// Null arguments are rejected at the boundary rather than surfacing later as
    /// a null-reference from inside the picker options.
    /// </summary>
    [Fact]
    public async Task ConfirmAsync_WithNullMessage_Throws()
    {
        IDialogService service = new AvaloniaDialogService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ConfirmAsync(null!, "Chart Exists"));
    }
}
