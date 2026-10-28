using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the Export panel's command wiring — the parts of it that a build cannot
/// check.
/// </summary>
/// <remarks>
/// <para>
/// Compiled bindings already guarantee that every <c>Command</c> path resolves to
/// a real member of <see cref="MainViewModel"/>; a typo there is an
/// <c>AVLN2000</c> build error, so no test is needed for it. What compiled
/// bindings do <b>not</b> check is everything that is a plain string or a missing
/// attribute rather than a binding path — and the Export panel is almost entirely
/// buttons, so those are the whole surface.
/// </para>
/// <para>
/// The panel is 20 buttons in 8 groups, the largest concentration of commands in
/// the window, and its buttons all look alike. A dropped attribute during a
/// copy-paste of the surrounding markup is the realistic failure mode here, not a
/// mistyped view-model member.
/// </para>
/// </remarks>
public sealed class ExportPanelTests
{
    /// <summary>
    /// The number of buttons in the Export panel, counted from the WPF original.
    /// </summary>
    /// <remarks>
    /// Chart Export 5 (PDF, Markdown, JSON, Both, Cancel PDF); Synastry,
    /// Composite &amp; Davison 3 (the third since 1.7.0, branch DavisonPanel);
    /// Transits 2; Primary Directions 1 (since 1.5.0, branch PrimaryPanel);
    /// Daily Horoscope 2; Returns 5; Electional 1; Database Backup 2. A
    /// change to this number is a change to the panel's
    /// contents, which should be a deliberate edit to this constant rather than a
    /// silent drift.
    /// </remarks>
    private const int ExpectedButtonCount = 21;

    /// <summary>
    /// Builds the window with a real view model, switched to the Export panel and
    /// laid out.
    /// </summary>
    /// <returns>The Export panel's root, already measured and arranged.</returns>
    /// <remarks>
    /// The panel must be made visible before layout. All ten panels live in the
    /// visual tree simultaneously, but a collapsed one measures to zero, and
    /// assertions about its contents would then pass against nothing.
    /// </remarks>
    private static StackPanel ShowExportPanel()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Export);

        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1200));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1200));
        Dispatcher.UIThread.RunJobs();

        return Assert.IsType<StackPanel>(window.FindControl<StackPanel>("ExportPanelRoot"));
    }

    /// <summary>
    /// Every button in the Export panel is bound to a command.
    /// </summary>
    /// <remarks>
    /// A button whose <c>Command</c> attribute was dropped still builds, still
    /// renders, and still looks correct — it is simply inert when clicked. That is
    /// the one failure in this panel that neither the compiler nor a glance at the
    /// running window would reveal.
    /// </remarks>
    [AvaloniaFact]
    public void EveryExportButton_IsBoundToACommand()
    {
        StackPanel panel = ShowExportPanel();

        List<Button> buttons = panel.GetLogicalDescendants().OfType<Button>().ToList();

        Assert.Equal(ExpectedButtonCount, buttons.Count);
        Assert.All(buttons, b => Assert.NotNull(b.Command));
    }

    /// <summary>
    /// The Cancel PDF button passes the cancellation registry's key as its
    /// command parameter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the load-bearing string in the panel. <c>CancelCommand</c>'s
    /// <c>CanExecute</c> is <c>CanCancel(parameter as string)</c>, which asks the
    /// registry whether that specific operation is active. With the parameter
    /// missing or misspelled the cast yields <see langword="null"/> and the button
    /// falls back to testing whether <i>any</i> operation is running — so it would
    /// enable during an unrelated export and offer to cancel something it cannot.
    /// </para>
    /// <para>
    /// Nothing about that fails a build: the attribute is a plain string, not a
    /// binding path, so compiled bindings never see it.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void CancelPdfButton_PassesTheRegistryKeyAsItsCommandParameter()
    {
        StackPanel panel = ShowExportPanel();

        Button cancel = Assert.Single(
            panel.GetLogicalDescendants().OfType<Button>(),
            b => b.Content as string == "Cancel PDF");

        Assert.Equal("pdfSave", cancel.CommandParameter);
    }

    /// <summary>
    /// The Export JSON button writes the chart's JSON: the button whose automation name is "Export JSON" is
    /// bound to the view model's <c>ExportChartJsonCommand</c> (O-D2 option b). Until 1.3.4 that name belonged
    /// to the database backup's export, now "Export Backup".
    /// </summary>
    [AvaloniaFact]
    public void ExportJsonButton_IsBoundToTheChartJsonCommand()
    {
        StackPanel panel = ShowExportPanel();
        MainViewModel vm = Assert.IsType<MainViewModel>(panel.DataContext);

        Button exportJson = Assert.Single(
            panel.GetLogicalDescendants().OfType<Button>(),
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "Export JSON");

        Assert.Same(vm.ExportChartJsonCommand, exportJson.Command);
    }

    /// <summary>
    /// Every Export panel button exposes an automation name.
    /// </summary>
    /// <remarks>
    /// Matches the guarantee <c>NavigationAccessibilityTests</c> makes for the
    /// navigation rail. These buttons all carry literal text content, so an
    /// assistive technology would not be left with nothing — but the name is what
    /// makes each one addressable, and a panel of 18 near-identical buttons is
    /// exactly where that matters most.
    /// </remarks>
    [AvaloniaFact]
    public void EveryExportButton_ExposesAnAutomationName()
    {
        StackPanel panel = ShowExportPanel();

        List<Button> buttons = panel.GetLogicalDescendants().OfType<Button>().ToList();

        Assert.All(
            buttons,
            b => Assert.False(
                string.IsNullOrWhiteSpace(
                    global::Avalonia.Automation.AutomationProperties.GetName(b)),
                $"Export button '{b.Content}' has no AutomationProperties.Name."));
    }

    /// <summary>
    /// Export Davison MD is bound to the Davison export command, in the group that now reads "Synastry,
    /// Composite &amp; Davison" (branch DavisonPanel, 1.7.0).
    /// </summary>
    [AvaloniaFact]
    public void ExportDavisonButton_IsBoundToItsCommand()
    {
        StackPanel panel = ShowExportPanel();
        MainViewModel vm = Assert.IsType<MainViewModel>(panel.DataContext);

        Button exportDavison = Assert.Single(
            panel.GetLogicalDescendants().OfType<Button>(),
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "Export Davison MD");
        Assert.Same(vm.ExportDavisonMarkdownCommand, exportDavison.Command);

        Assert.Single(
            panel.GetLogicalDescendants().OfType<TextBlock>(),
            t => t.Text == "Synastry, Composite & Davison");
        Assert.DoesNotContain(
            panel.GetLogicalDescendants().OfType<TextBlock>(),
            t => t.Text == "Synastry & Composite");
    }
}
