using Avalonia.Headless.XUnit;

using Xunit;

using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Verifies that this assembly can construct a real <see cref="MainViewModel"/>
/// from the shared test doubles, and that the constructed view model is live
/// rather than inert.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this test is really for.</b> Until Phase 4 slice 1.5 the doubles for
/// <see cref="MainViewModel"/>'s 23 required services were nested inside a class
/// in <c>AstroCast.Tests</c>, which this assembly cannot reference:
/// <c>Avalonia.Headless.XUnit</c> requires xunit v3 and that project is on v2.
/// The doubles moved to the framework-free <c>AstroCast.TestDoubles</c> library
/// precisely so this file could exist.
/// </para>
/// <para>
/// So this is not really a test of <see cref="MainViewModel"/> — that is covered
/// thoroughly in <c>AstroCast.Tests</c>. It is a test of the packaging: if the
/// extraction ever regresses, or someone adds a test-framework reference to
/// <c>AstroCast.TestDoubles</c> and re-splits the assemblies, this file stops
/// compiling. That is the point. It is the guard that keeps the seam open for
/// slice 2's Chart panel, which needs a bound view model to test against.
/// </para>
/// <para>
/// <b>The attribute must be <see cref="AvaloniaFactAttribute"/>.</b> A plain
/// <c>[Fact]</c> runs off the headless session's UI thread. Construction happens
/// to survive that today, but any later assertion that touches a control would
/// fail with a thread-affinity error naming neither the attribute nor the cause,
/// so the whole file stays on the Avalonia attribute.
/// </para>
/// </remarks>
public sealed class MainViewModelConstructionTests
{
    /// <summary>
    /// A real <see cref="MainViewModel"/> can be constructed here, and it starts
    /// on the panel the shell expects to show first.
    /// </summary>
    [AvaloniaFact]
    public void MainViewModel_IsConstructible_AndStartsOnTheChartPanel()
    {
        MainViewModel vm = MainViewModelFactory.Create();

        Assert.Equal(NavigationPanel.Chart, vm.ActivePanel);
        Assert.True(vm.IsChartPanelVisible);
        Assert.False(vm.IsTransitsPanelVisible);
    }

    /// <summary>
    /// The constructed view model is live: the command the navigation rail binds
    /// to actually moves <see cref="MainViewModel.ActivePanel"/> and flips the
    /// derived visibility flags the markup binds to.
    /// </summary>
    /// <remarks>
    /// Construction alone would pass even if the view model were a hollow shell,
    /// so this drives the same <c>NavigateToPanelCommand</c> the nine rail
    /// buttons invoke and asserts the observable consequence. It is the half of
    /// the seam that proves the doubles are wired, not merely accepted.
    /// </remarks>
    [AvaloniaFact]
    public void NavigateToPanelCommand_MovesTheActivePanel()
    {
        MainViewModel vm = MainViewModelFactory.Create();

        vm.NavigateToPanelCommand.Execute(NavigationPanel.Transits);

        Assert.Equal(NavigationPanel.Transits, vm.ActivePanel);
        Assert.True(vm.IsTransitsPanelVisible);
        Assert.False(vm.IsChartPanelVisible);
    }
}
