using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Core.Models;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the translated Returns panel: its ten sections, thirty-six buttons,
/// twelve cancel keys, the enablement split between chart-dependent and mundane
/// features, and the two markup shapes this panel is the only user of.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing here can be found by its caption.</b> "Cancel" appears twelve
/// times, "Export MD" three times, "Find Conjunctions" twice and "Year" four
/// times. Every lookup in this file goes through
/// <c>AutomationProperties.GetName</c>, which is also what a UI driver must use
/// against this panel.
/// </para>
/// <para>
/// <b>Commands are asserted by reference.</b> Thirty-six buttons across ten
/// sections is the largest surface in the app for a copy-paste that points one
/// button at a neighbour's command: it builds, renders and enables identically,
/// and only an instance comparison catches it.
/// </para>
/// <para>
/// <b>The gating tests assert the SPLIT, not a transition.</b>
/// <see cref="MainViewModel.HasResult"/> has a private setter and the per-feature
/// <c>Has*Result</c> flags are computed from private fields, and
/// <c>InternalsVisibleTo</c> names <c>AstroCast.Tests</c> only — so this project
/// cannot flip them. Asserting one direction alone would be vacuous in either
/// direction (a never-executable command passes a "disabled" assertion; an
/// ungated one passes an "enabled" assertion). What is asserted instead is that
/// the two classes <i>differ</i> in the same no-chart state: the four
/// chart-dependent calculates are unavailable while all nine mundane ones are
/// available. Regating a mundane command fails the first assertion; dropping the
/// <c>HasResult</c> guard from a return command fails the second.
/// </para>
/// </remarks>
public sealed class ReturnsPanelTests
{
    /// <summary>Builds the window with the Returns panel active.</summary>
    private static (MainWindow Window, MainViewModel ViewModel) Show()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        MainWindow window = new() { DataContext = vm, Width = 950, Height = 600 };
        window.Show();
        vm.ActivePanel = NavigationPanel.Returns;
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(950, 600));
        window.Arrange(new global::Avalonia.Rect(0, 0, 950, 600));
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    private static StackPanel Root(MainWindow window) =>
        Assert.IsType<StackPanel>(window.FindControl<StackPanel>("ReturnsPanelRoot"));

    /// <summary>
    /// The panel's push buttons, excluding the two check boxes.
    /// </summary>
    /// <remarks>
    /// <b>In Avalonia <c>ToggleButton</c> derives from <c>Button</c>, so
    /// <c>OfType&lt;Button&gt;()</c> returns check boxes too.</b> WPF's
    /// <c>ToggleButton</c> derives from <c>ButtonBase</c> and does not, so a
    /// button census carried over from WPF thinking counts two too many here —
    /// this panel's "Include snapshots" and "Cardinal only". Excluding
    /// <c>ToggleButton</c> rather than <c>CheckBox</c> keeps the filter correct if
    /// a radio button or toggle switch is ever added.
    /// </remarks>
    private static List<Button> PushButtons(StackPanel root) =>
        [.. root.GetLogicalDescendants().OfType<Button>().Where(b => b is not ToggleButton)];

    /// <summary>Describes a button for an assertion message: content, automation name, type.</summary>
    private static string Describe(Button b)
    {
        string content = b.Content as string ?? "<no content>";
        string name = global::Avalonia.Automation.AutomationProperties.GetName(b) ?? "<no name>";
        return content + " / " + name + " / " + b.GetType().Name;
    }

    /// <summary>Finds the single control of type <typeparamref name="T"/> with this automation name.</summary>
    private static T ByName<T>(StackPanel root, string name) where T : Control =>
        root.GetLogicalDescendants().OfType<T>()
            .Single(c => global::Avalonia.Automation.AutomationProperties.GetName(c) == name);

    /// <summary>
    /// The twelve cancel buttons, each with the operation key it must carry.
    /// </summary>
    /// <remarks>
    /// The right-hand column names the <see cref="MainViewModel.OperationKeys"/>
    /// constant rather than repeating a literal, so retargeting or renaming a
    /// constant carries this table with it. (C# inlines a <c>const string</c> at
    /// the call site, so the emitted comparison is against a literal — the value
    /// this brings is that the <i>source</i> cannot drift from the registry
    /// silently, not that the IL holds a reference.)
    /// </remarks>
    private static readonly (string Name, string Key)[] CancelButtons =
    [
        ("Cancel solar return",       MainViewModel.OperationKeys.SolarReturn),
        ("Cancel lunar return",       MainViewModel.OperationKeys.LunarReturn),
        ("Cancel planetary return",   MainViewModel.OperationKeys.PlanetaryReturn),
        ("Cancel cycle timeline",     MainViewModel.OperationKeys.PlanetaryCycle),
        ("Cancel great conjunctions", MainViewModel.OperationKeys.GreatConjunctions),
        ("Cancel synodic cycles",     MainViewModel.OperationKeys.SynodicCycle),
        ("Cancel ingress series",     MainViewModel.OperationKeys.IngressSeries),
        ("Cancel cardinal ingresses", MainViewModel.OperationKeys.CardinalIngresses),
        ("Cancel lunation series",    MainViewModel.OperationKeys.LunationSeries),
        ("Cancel lunation charts",    MainViewModel.OperationKeys.LunationCharts),
        ("Cancel eclipses",           MainViewModel.OperationKeys.Eclipses),
        ("Cancel mundane aspects",    MainViewModel.OperationKeys.MundaneAspects),
    ];

    /// <summary>
    /// Every one of the twelve cancel buttons carries its own operation key, and
    /// the twelve keys are distinct.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is the highest-value test in the slice.</b>
    /// <c>CommandParameter</c> is a plain string and is invisible to compiled
    /// bindings, so nothing in either head's build checks these twelve values.
    /// <c>MainViewModel.CanCancel</c> returns <c>HasActiveOperation</c> for a
    /// blank key and <c>IsOperationActive(key)</c> otherwise, so a typo degrades
    /// a cancel button into "cancel whatever is running" and a swap makes two
    /// buttons each drive the other's operation — both build, and both pass every
    /// other test in this file.
    /// </para>
    /// <para>
    /// <b>Distinctness is asserted separately from correctness.</b> Checking each
    /// key against a constant proves each is <i>a</i> valid key; it does not prove
    /// they are twelve <i>different</i> keys. If two sections shared one, cancelling
    /// either would cancel both. The WPF source was checked at translation time
    /// and has twelve distinct keys, so this guards the translation rather than
    /// recording an upstream defect.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void CancelButtons_CarryTwelveDistinctOperationKeys()
    {
        (MainWindow window, _) = Show();
        StackPanel root = Root(window);

        List<Button> allCancels = PushButtons(root)
            .Where(b => (b.Content as string) == "Cancel")
            .ToList();
        Assert.True(
            allCancels.Count == CancelButtons.Length,
            $"Expected {CancelButtons.Length} Cancel buttons, found {allCancels.Count}. " +
            "A new one must be registered in CancelButtons deliberately.");

        List<string> observed = [];
        foreach ((string name, string key) in CancelButtons)
        {
            Button cancel = ByName<Button>(root, name);
            string? parameter = cancel.CommandParameter as string;

            Assert.True(
                parameter == key,
                $"'{name}' carries operation key '{parameter}', expected '{key}'. " +
                "A wrong key here cancels the wrong operation, silently.");

            observed.Add(key);
        }

        Assert.True(
            observed.Distinct(StringComparer.Ordinal).Count() == observed.Count,
            "Two cancel buttons share one operation key, so cancelling either " +
            "would cancel both: " +
            string.Join(", ", observed.GroupBy(k => k, StringComparer.Ordinal)
                .Where(g => g.Count() > 1).Select(g => g.Key)));

        Assert.All(allCancels, b => Assert.Same(
            ((MainViewModel)window.DataContext!).CancelCommand, b.Command));
    }

    /// <summary>
    /// The twenty-four non-cancel buttons, each with the command it must invoke.
    /// </summary>
    private static (string Name, Func<MainViewModel, object> Command)[] ActionButtons =>
    [
        ("Calculate Solar Return",             vm => vm.CalculateSolarReturnCommand),
        ("Export Solar Return MD",             vm => vm.ExportSolarReturnMarkdownCommand),
        ("Calculate Lunar Returns",            vm => vm.CalculateLunarReturnCommand),
        ("Export Lunar Return MD",             vm => vm.ExportLunarReturnMarkdownCommand),
        ("Calculate Return",                   vm => vm.CalculatePlanetaryReturnCommand),
        ("Export Return MD",                   vm => vm.ExportPlanetaryReturnMarkdownCommand),
        ("Calculate Cycle Timeline",           vm => vm.CalculateCycleTimelineCommand),
        ("Export Cycle Timeline MD",           vm => vm.ExportCycleTimelineMarkdownCommand),
        ("Lookup mundane location",            vm => vm.LookupMundaneLocationCommand),
        ("Find Great Conjunctions",            vm => vm.FindGreatConjunctionsCommand),
        ("Export Great Conjunction Series MD", vm => vm.ExportGCSeriesMarkdownCommand),
        ("Find Synodic Cycles",                vm => vm.FindSynodicCyclesCommand),
        ("Export Synodic Cycle MD",            vm => vm.ExportSynodicCycleMdCommand),
        ("Find Ingresses",                     vm => vm.FindIngressesCommand),
        ("Calculate Cardinal Charts",          vm => vm.CalculateCardinalIngressesCommand),
        ("Export Ingress Series MD",           vm => vm.ExportIngressSeriesMarkdownCommand),
        ("Export Cardinal Ingresses MD",       vm => vm.ExportCardinalIngressesMarkdownCommand),
        ("Find Lunations",                     vm => vm.FindLunationsCommand),
        ("Calculate Lunation Charts",          vm => vm.CalculateLunationChartsCommand),
        ("Export Lunation Series MD",          vm => vm.ExportLunationSeriesCommand),
        ("Find Eclipses",                      vm => vm.FindEclipsesCommand),
        ("Export Eclipse Series MD",           vm => vm.ExportEclipseSeriesCommand),
        ("Find Mundane Aspects",               vm => vm.FindMundaneAspectsCommand),
        ("Export Mundane Aspect Series MD",    vm => vm.ExportMASeriesCommand),
    ];

    /// <summary>
    /// Each of the twenty-four action buttons invokes its own command instance.
    /// </summary>
    [AvaloniaFact]
    public void ActionButtons_WireTheirOwnCommands()
    {
        (MainWindow window, MainViewModel vm) = Show();
        StackPanel root = Root(window);

        List<Button> found = PushButtons(root);
        string inventory = string.Join(" | ", found.Select(Describe));
        Assert.True(
            found.Count == ActionButtons.Length + CancelButtons.Length,
            $"Expected {ActionButtons.Length + CancelButtons.Length} buttons in the " +
            $"Returns panel, found {found.Count}: {inventory}");

        foreach ((string name, Func<MainViewModel, object> command) in ActionButtons)
        {
            Assert.Same(command(vm), ByName<Button>(root, name).Command);
        }
    }

    /// <summary>
    /// The four chart-dependent calculates are unavailable without a chart while
    /// all nine mundane operations are available in the same state.
    /// </summary>
    /// <remarks>
    /// See the class remarks: the split is the assertion, because neither flag is
    /// settable from this project. Avalonia routes command gating through
    /// <c>IsEffectivelyEnabled</c>, not <c>IsEnabled</c> — the latter stays at
    /// whatever the markup left it and would pass against a completely ungated
    /// button.
    /// </remarks>
    [AvaloniaFact]
    public void ChartDependentAndMundaneOperations_GateDifferently()
    {
        (MainWindow window, _) = Show();
        StackPanel root = Root(window);

        string[] needChart =
        [
            "Calculate Solar Return",
            "Calculate Lunar Returns",
            "Calculate Return",
            "Calculate Cycle Timeline",
        ];

        string[] needNothing =
        [
            "Lookup mundane location",
            "Find Great Conjunctions",
            "Find Synodic Cycles",
            "Find Ingresses",
            "Calculate Cardinal Charts",
            "Find Lunations",
            "Calculate Lunation Charts",
            "Find Eclipses",
            "Find Mundane Aspects",
        ];

        foreach (string name in needChart)
        {
            Assert.False(
                ByName<Button>(root, name).IsEffectivelyEnabled,
                $"'{name}' is available with no calculated chart; it gates on HasResult.");
        }

        foreach (string name in needNothing)
        {
            Assert.True(
                ByName<Button>(root, name).IsEffectivelyEnabled,
                $"'{name}' is unavailable with no chart, but mundane operations " +
                "do not need one. A HasResult gate has been added by mistake.");
        }
    }

    /// <summary>
    /// All eleven export buttons are unavailable until their own feature has run.
    /// </summary>
    /// <remarks>
    /// Paired with <see cref="ChartDependentAndMundaneOperations_GateDifferently"/>,
    /// which proves the same window state leaves nine buttons enabled — so this is
    /// a real gate rather than everything being uniformly disabled.
    /// </remarks>
    [AvaloniaFact]
    public void ExportButtons_AreGatedOnTheirOwnResult()
    {
        (MainWindow window, _) = Show();
        StackPanel root = Root(window);

        string[] exports =
        [
            "Export Solar Return MD",
            "Export Lunar Return MD",
            "Export Return MD",
            "Export Cycle Timeline MD",
            "Export Great Conjunction Series MD",
            "Export Synodic Cycle MD",
            "Export Ingress Series MD",
            "Export Cardinal Ingresses MD",
            "Export Lunation Series MD",
            "Export Eclipse Series MD",
            "Export Mundane Aspect Series MD",
        ];

        Assert.Equal(11, exports.Length);

        foreach (string name in exports)
        {
            Assert.False(
                ByName<Button>(root, name).IsEffectivelyEnabled,
                $"'{name}' is available before its feature has produced a result.");
        }
    }

    /// <summary>
    /// The three static option collections are bound through <c>x:Static</c> and
    /// resolve to the exact collections the view models expose.
    /// </summary>
    /// <remarks>
    /// <c>AvailableReturnPlanets</c>, <c>AvailableReturnFindModes</c> and
    /// <c>AvailableIngressPlanets</c> are <c>static</c>; a compiled binding cannot
    /// reach a static through an instance path, so these must use
    /// <c>{x:Static}</c>. That much the compiler enforces. What it does not
    /// enforce is that each points at the <i>right</i> collection — the two
    /// <c>CelestialBodyId</c> lists are interchangeable to the type system, so
    /// aiming the ingress picker at the return-planet list would build and render.
    /// </remarks>
    [AvaloniaFact]
    public void StaticOptionCollections_ResolveToTheirOwnSource()
    {
        (MainWindow window, MainViewModel vm) = Show();
        StackPanel root = Root(window);

        Assert.Same(
            ReturnsViewModel.AvailableReturnPlanets,
            ByName<ComboBox>(root, "Planetary return planet").ItemsSource);
        Assert.Same(
            ReturnsViewModel.AvailableReturnFindModes,
            ByName<ComboBox>(root, "Planetary return find mode").ItemsSource);
        Assert.Same(
            MundaneViewModel.AvailableIngressPlanets,
            ByName<ComboBox>(root, "Ingress planet").ItemsSource);

        // The instance-backed three, for the same reason.
        Assert.Same(
            vm.Cycles.SynodicBodyOptions,
            ByName<ComboBox>(root, "Synodic body A").ItemsSource);
        Assert.Same(
            vm.Cycles.SynodicBodyOptions,
            ByName<ComboBox>(root, "Synodic body B").ItemsSource);
        Assert.Same(
            vm.Mundane.AvailableMundanePlanets,
            ByName<ComboBox>(root, "Mundane aspect body 1").ItemsSource);
        Assert.Same(
            vm.Mundane.AvailableHarmonicModes,
            ByName<ComboBox>(root, "Mundane aspect harmonic").ItemsSource);
    }

    /// <summary>
    /// Exactly one of the Return # and Year pairs is visible in each find mode.
    /// </summary>
    /// <remarks>
    /// Four controls share <c>Grid.Row="2"</c> of the Planetary Return grid, two
    /// visible at a time, swapped by <c>IsReturnByNumber</c> /
    /// <c>IsReturnByYear</c>. A broken visibility binding here does not throw — it
    /// renders both pairs on top of each other, which reads as a rendering
    /// artefact rather than a binding fault.
    /// </remarks>
    [AvaloniaFact]
    public void PlanetaryReturnRow_ShowsOnePairPerFindMode()
    {
        (MainWindow window, MainViewModel vm) = Show();
        StackPanel root = Root(window);

        TextBox number = ByName<TextBox>(root, "Planetary return number");
        TextBox year = ByName<TextBox>(root, "Planetary return year");

        vm.Returns.SelectedReturnFindMode = ReturnFindMode.ByReturnNumber;
        Dispatcher.UIThread.RunJobs();
        Assert.True(number.IsVisible, "Return # is hidden in ByReturnNumber mode.");
        Assert.False(year.IsVisible, "Year is visible in ByReturnNumber mode; both pairs overlap.");

        vm.Returns.SelectedReturnFindMode = ReturnFindMode.ByYear;
        Dispatcher.UIThread.RunJobs();
        Assert.True(year.IsVisible, "Year is hidden in ByYear mode.");
        Assert.False(number.IsVisible, "Return # is visible in ByYear mode; both pairs overlap.");
    }

    /// <summary>
    /// The Lunar Return picker formats its items through the multi-binding, with
    /// the date format specifier applied, and says the time is UTC.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This head's only other <c>MultiBinding</c> (the saved-charts pane) formats
    /// three bare values; this one is the first to carry a format specifier
    /// (<c>{1:yyyy-MM-dd HH:mm}</c>). Verified against a realised container rather
    /// than assumed to behave as WPF's does.
    /// </para>
    /// <para>
    /// The bound value is the return's UTC instant: a return chart is cast at a UTC wall
    /// clock with an explicit offset of zero. Until branch O-4 (E3-2) the item printed it
    /// with no clock, so a return at 17:40 in New York read "21:40".
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void LunarReturnPicker_FormatsItemsWithTheDateSpecifier()
    {
        (MainWindow window, MainViewModel vm) = Show();
        StackPanel root = Root(window);
        ComboBox picker = ByName<ComboBox>(root, "Lunar return results");

        Assert.NotNull(picker.ItemTemplate);

        LunarReturnResult sample = SampleLunarReturn(
            sequenceNumber: 3,
            local: new DateTime(2026, 8, 9, 14, 5, 0));

        Control? realised = picker.ItemTemplate!.Build(sample);
        Assert.NotNull(realised);
        realised!.DataContext = sample;
        Dispatcher.UIThread.RunJobs();

        TextBlock text = Assert.IsType<TextBlock>(realised);
        Assert.Equal("#3 — 2026-08-09 14:05 UTC", text.Text);
    }

    /// <summary>
    /// A minimally-populated <see cref="LunarReturnResult"/> for realising the
    /// picker's item template. Only <c>SequenceNumber</c> and
    /// <c>ReturnChart.Input.LocalDateTime</c> reach the template; everything else
    /// is filled to satisfy the required members.
    /// </summary>
    private static LunarReturnResult SampleLunarReturn(int sequenceNumber, DateTime local) => new()
    {
        NatalChart = SampleChart(local),
        ReturnChart = SampleChart(local),
        SequenceNumber = sequenceNumber,
        NatalMoonLongitude = 0d,
        ReturnMoonLongitude = 0d,
        ReturnLocationName = "Nowhere",
        ReturnLatitude = 0d,
        ReturnLongitude = 0d,
        PrecisionArcSeconds = 0d,
        EngineVersion = "test",
        CalculatedAt = local,
    };

    /// <summary>An empty chart carrying only the local date/time the template reads.</summary>
    private static ChartResult SampleChart(DateTime local) => new()
    {
        Input = new ChartInput
        {
            Name = "Sample",
            LocalDateTime = local,
            TimeZone = "UTC",
            Latitude = 0d,
            Longitude = 0d,
            LocationName = "Nowhere",
        },
        JulianDay = 0d,
        Obliquity = 23.4367,
        UtcDateTime = local,
        Positions = [],
        Houses = new HouseData
        {
            Cusps = [],
            Ascendant = 0d,
            Midheaven = 0d,
            Armc = 0d,
            Vertex = 0d,
        },
        Aspects = [],
        Midpoints = [],
        MidpointActivations = [],
        Dignities = [],
        MoonCondition = null,
        ChartContext = null,
        FixedStarConjunctions = [],
        ResolvedUtcOffsetHours = 0d,
        ResolvedOffsetLabel = "UTC",
        EngineVersion = "test",
        CalculatedAt = local,
    };

    /// <summary>
    /// The ten section headers appear in the WPF panel's order.
    /// </summary>
    [AvaloniaFact]
    public void SectionHeaders_MatchTheWpfOrder()
    {
        (MainWindow window, _) = Show();
        StackPanel root = Root(window);

        string[] expected =
        [
            "Annual Profection",
            "Solar Return",
            "Lunar Return",
            "Planetary Return",
            "Mundane Location",
            "Great Conjunctions",
            "Synodic Cycles",
            "Ingress Charts",
            "Lunation Charts",
            "Eclipse Calculator",
            "Mundane Aspect Finder",
        ];

        string[] actual = root.GetLogicalDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("sectionHeader"))
            .Select(t => t.Text ?? string.Empty)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Every interactive control in the panel carries a unique automation name.
    /// </summary>
    /// <remarks>
    /// Uniqueness is the point, not mere presence: with "Cancel" twelve times and
    /// "Export MD" three times, a duplicated automation name would make a driver
    /// (and every lookup in this file) throw on an ambiguous match, or worse,
    /// address the wrong control.
    /// </remarks>
    [AvaloniaFact]
    public void InteractiveControls_CarryUniqueAutomationNames()
    {
        (MainWindow window, _) = Show();
        StackPanel root = Root(window);

        List<Control> interactive = root.GetLogicalDescendants().OfType<Control>()
            .Where(c => c is Button or TextBox or ComboBox or CheckBox or CalendarDatePicker)
            .ToList();

        List<string> names = [];
        foreach (Control c in interactive)
        {
            string? name = global::Avalonia.Automation.AutomationProperties.GetName(c);
            Assert.False(
                string.IsNullOrWhiteSpace(name),
                $"A {c.GetType().Name} in the Returns panel has no automation name.");
            names.Add(name!);
        }

        string[] duplicates = [.. names.GroupBy(n => n, StringComparer.Ordinal)
            .Where(g => g.Count() > 1).Select(g => g.Key)];

        Assert.True(
            duplicates.Length == 0,
            "Duplicate automation names in the Returns panel: " + string.Join(", ", duplicates));
    }
}
