using System.Reflection;

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Core.Enums;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

using MStyles = Mapsui.Styles;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the translated Map (Astrocartography) panel: its four commands, its
/// cancel key, the five controls gated on a result, all sixteen toggles, and the
/// colour agreement between this panel's legend and the map's own lines.
/// </summary>
/// <remarks>
/// <para>
/// The panel is a control surface, not the map: the map lives in
/// <see cref="AcgMapWindow"/>, a separate floating window, because the panel
/// column is far too narrow for it. So there is nothing here that renders a map,
/// and these tests need no renderer.
/// </para>
/// <para>
/// <b>Commands are asserted by reference, never for non-null.</b> This panel has
/// four buttons wired to four different commands and the realistic mistake is a
/// copy-paste that points one at a neighbour, which builds, renders and enables
/// identically. Comparing the resolved command to the exact instance on the view
/// model is what catches that.
/// </para>
/// </remarks>
public sealed class MapPanelTests
{
    /// <summary>Builds the window and returns it with its view model.</summary>
    private static (MainWindow Window, MainViewModel ViewModel) Show()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        MainWindow window = new() { DataContext = vm, Width = 950, Height = 600 };
        window.Show();
        vm.ActivePanel = NavigationPanel.Map;
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(950, 600));
        window.Arrange(new global::Avalonia.Rect(0, 0, 950, 600));
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    private static StackPanel Root(MainWindow window) =>
        Assert.IsType<StackPanel>(window.FindControl<StackPanel>("MapPanelRoot"));

    private static Button ButtonWith(StackPanel root, string content) =>
        root.GetLogicalDescendants().OfType<Button>()
            .Single(b => (b.Content as string) == content);

    private static CheckBox CheckBoxWith(StackPanel root, string content) =>
        root.GetLogicalDescendants().OfType<CheckBox>()
            .Single(c => (c.Content as string) == content);

    /// <summary>
    /// Each of the four buttons is wired to its own command instance.
    /// </summary>
    [AvaloniaFact]
    public void ActionButtons_WireTheirOwnCommands()
    {
        (MainWindow window, MainViewModel vm) = Show();
        StackPanel root = Root(window);

        Assert.Same(vm.CalculateAcgCommand, ButtonWith(root, "Calculate ACG Map").Command);
        Assert.Same(vm.CancelCommand, ButtonWith(root, "Cancel").Command);
        Assert.Same(vm.ExportAcgMarkdownCommand, ButtonWith(root, "Export ACG MD").Command);
        Assert.Same(vm.OpenAcgMapWindowCommand, ButtonWith(root, "Open Map Window").Command);
    }

    /// <summary>
    /// The Cancel button cancels astrocartography and nothing else.
    /// </summary>
    /// <remarks>
    /// The parameter is compared against <c>MainViewModel.OperationKeys</c>
    /// rather than a repeated literal, so the markup and the registry cannot
    /// drift apart. Its automation name is asserted too: every panel's cancel
    /// button reads "Cancel", so the name is what lets a driver address this one
    /// rather than a neighbour's.
    /// </remarks>
    [AvaloniaFact]
    public void CancelButton_CarriesTheAstrocartographyOperationKey()
    {
        (MainWindow window, _) = Show();
        Button cancel = ButtonWith(Root(window), "Cancel");

        Assert.Equal(
            MainViewModel.OperationKeys.Astrocartography,
            cancel.CommandParameter as string);
        Assert.Equal(
            "Cancel Astrocartography",
            global::Avalonia.Automation.AutomationProperties.GetName(cancel));
    }

    /// <summary>
    /// The five result-dependent controls are hidden until a result exists, and
    /// visible once one does; the three action buttons are always available.
    /// </summary>
    /// <remarks>
    /// Both directions are asserted deliberately. A test that only checks the
    /// visible direction passes just as happily against a control that is never
    /// gated at all.
    /// </remarks>
    [AvaloniaFact]
    public void ResultGatedControls_AppearOnlyWhenThereIsAResult()
    {
        (MainWindow window, MainViewModel vm) = Show();
        StackPanel root = Root(window);

        Button openMap = ButtonWith(root, "Open Map Window");
        TextBlock planetsLabel = root.GetLogicalDescendants().OfType<TextBlock>()
            .Single(t => t.Text == "Planets:");
        TextBox report = root.GetLogicalDescendants().OfType<TextBox>().Single();
        List<WrapPanel> wraps = root.GetLogicalDescendants().OfType<WrapPanel>().ToList();
        Assert.Equal(3, wraps.Count);
        WrapPanel planets = wraps[1];
        WrapPanel lines = wraps[2];

        vm.Astrocartography.HasResult = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(openMap.IsVisible, "Open Map Window is visible with no result.");
        Assert.False(planetsLabel.IsVisible, "The Planets label is visible with no result.");
        Assert.False(planets.IsVisible, "The planet toggles are visible with no result.");
        Assert.False(lines.IsVisible, "The line toggles are visible with no result.");
        Assert.False(report.IsVisible, "The crossing report is visible with no result.");

        // The three action buttons are never gated on a result.
        Assert.True(ButtonWith(root, "Calculate ACG Map").IsVisible);
        Assert.True(ButtonWith(root, "Cancel").IsVisible);
        Assert.True(ButtonWith(root, "Export ACG MD").IsVisible);

        vm.Astrocartography.HasResult = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(openMap.IsVisible, "Open Map Window stayed hidden with a result.");
        Assert.True(planetsLabel.IsVisible, "The Planets label stayed hidden with a result.");
        Assert.True(planets.IsVisible, "The planet toggles stayed hidden with a result.");
        Assert.True(lines.IsVisible, "The line toggles stayed hidden with a result.");
        Assert.True(report.IsVisible, "The crossing report stayed hidden with a result.");
    }

    /// <summary>
    /// The twelve planet toggles and four line toggles each bind to their own
    /// view-model property.
    /// </summary>
    /// <remarks>
    /// Sixteen near-identical checkboxes is precisely the shape where a
    /// copy-paste leaves two of them pointing at one property, which looks
    /// correct until a user unticks Mars and Venus disappears. Each toggle is
    /// driven from the view model and read back individually, and the caption to
    /// property mapping is spelled out because one is irregular: the checkbox
    /// reading "Node" binds <c>ShowNorthNode</c>.
    /// </remarks>
    [AvaloniaFact]
    public void EveryToggle_BindsToItsOwnProperty()
    {
        (MainWindow window, MainViewModel vm) = Show();
        StackPanel root = Root(window);
        AstrocartographyViewModel acg = vm.Astrocartography;

        (string Caption, Action<bool> Set)[] toggles =
        [
            ("Sun", v => acg.ShowSun = v),
            ("Moon", v => acg.ShowMoon = v),
            ("Mercury", v => acg.ShowMercury = v),
            ("Venus", v => acg.ShowVenus = v),
            ("Mars", v => acg.ShowMars = v),
            ("Jupiter", v => acg.ShowJupiter = v),
            ("Saturn", v => acg.ShowSaturn = v),
            ("Uranus", v => acg.ShowUranus = v),
            ("Neptune", v => acg.ShowNeptune = v),
            ("Pluto", v => acg.ShowPluto = v),
            ("Chiron", v => acg.ShowChiron = v),
            ("Node", v => acg.ShowNorthNode = v),
            ("MC", v => acg.ShowMcLines = v),
            ("IC", v => acg.ShowIcLines = v),
            ("ASC", v => acg.ShowAscLines = v),
            ("DSC", v => acg.ShowDscLines = v),
        ];

        Assert.Equal(16, toggles.Length);
        Assert.Equal(
            toggles.Length,
            root.GetLogicalDescendants().OfType<CheckBox>().Count());

        foreach ((string caption, Action<bool> set) in toggles)
        {
            CheckBox box = CheckBoxWith(root, caption);

            set(false);
            Dispatcher.UIThread.RunJobs();
            Assert.False(
                box.IsChecked,
                $"The '{caption}' toggle stayed ticked after its property was cleared, " +
                "so it is bound to a different property than the one named here.");

            set(true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(
                box.IsChecked,
                $"The '{caption}' toggle stayed clear after its property was set.");
        }
    }

    /// <summary>
    /// The legend colour beside each planet matches the colour the map draws
    /// that planet's lines in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a genuine cross-file invariant that nothing enforced before: the
    /// panel's twelve <c>Foreground</c> values and
    /// <c>AcgMapWindow.GetPlanetColor</c> are two hand-maintained copies of one
    /// colour table in two different files, and the WPF head's only protection is
    /// a comment asking the next editor to keep them in step. A legend that
    /// disagrees with the map is worse than no legend, because it is confidently
    /// wrong.
    /// </para>
    /// <para>
    /// The map's table is read by reflection rather than by making it visible,
    /// so no production code changed to enable this test. A rename of that method
    /// therefore fails here, which is the right outcome: the invariant is the
    /// point, and it needs re-pointing at whatever replaced it.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void LegendColours_MatchTheColoursTheMapDrawsWith()
    {
        (MainWindow window, _) = Show();
        StackPanel root = Root(window);

        MethodInfo getPlanetColor = typeof(AcgMapWindow).GetMethod(
            "GetPlanetColor",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "AcgMapWindow.GetPlanetColor was not found. If it was renamed, " +
                "re-point this test at its replacement rather than deleting it: " +
                "it is the only thing keeping the panel legend and the map's " +
                "line colours in agreement.");

        (string Caption, CelestialBodyId Body)[] legend =
        [
            ("Sun", CelestialBodyId.Sun),
            ("Moon", CelestialBodyId.Moon),
            ("Mercury", CelestialBodyId.Mercury),
            ("Venus", CelestialBodyId.Venus),
            ("Mars", CelestialBodyId.Mars),
            ("Jupiter", CelestialBodyId.Jupiter),
            ("Saturn", CelestialBodyId.Saturn),
            ("Uranus", CelestialBodyId.Uranus),
            ("Neptune", CelestialBodyId.Neptune),
            ("Pluto", CelestialBodyId.Pluto),
            ("Chiron", CelestialBodyId.Chiron),
            ("Node", CelestialBodyId.TrueNode),
        ];

        foreach ((string caption, CelestialBodyId body) in legend)
        {
            CheckBox box = CheckBoxWith(root, caption);
            ISolidColorBrush brush = Assert.IsAssignableFrom<ISolidColorBrush>(box.Foreground);
            Color legendColour = brush.Color;

            MStyles.Color mapColour = (MStyles.Color)getPlanetColor.Invoke(null, [body])!;

            Assert.True(
                legendColour.R == mapColour.R
                && legendColour.G == mapColour.G
                && legendColour.B == mapColour.B,
                $"The '{caption}' legend swatch is " +
                $"#{legendColour.R:X2}{legendColour.G:X2}{legendColour.B:X2} but the map " +
                $"draws {body} lines in " +
                $"#{mapColour.R:X2}{mapColour.G:X2}{mapColour.B:X2}. The panel legend and " +
                "AcgMapWindow.GetPlanetColor must carry the same colour for each body.");
        }
    }
}
