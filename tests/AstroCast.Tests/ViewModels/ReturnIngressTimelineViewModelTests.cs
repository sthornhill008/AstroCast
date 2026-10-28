using System.ComponentModel;

using AstroCast.Core.Models;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ReturnIngressTimelineViewModel"/> \u2014 the
/// tenth H-1 decomposition child (Session H-1.10) that owns the Return &amp;
/// Ingress Timeline panel: the scan window (start year, end year), the two
/// event-type toggles (include returns, include ingresses), and the most
/// recent <see cref="ReturnIngressTimeline"/> output. Verifies that every
/// input setter raises <see cref="INotifyPropertyChanged.PropertyChanged"/>
/// without signalling
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>, that the
/// result setter preserves the pre-extraction "empty-timeline zeroes the
/// flag" semantic (value-based <c>HasX</c> per H-1.8 / H-1.9 \u2014 the
/// <c>CurrentResult</c> setter notifies but does <b>not</b> flip
/// <c>HasResult</c>, and the parent command writes both properties in
/// sequence), that <see cref="ReturnIngressTimelineViewModel.Clear"/> uses
/// the field-bypass pattern to emit exactly one refresh pulse, and that
/// <see cref="MainViewModel.ReturnIngressTimeline"/> is a stable
/// eagerly-composed child.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReturnIngressTimelineViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Constructs a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="MainViewModelTests"/> and returns its
    /// <see cref="MainViewModel.ReturnIngressTimeline"/> child. We construct
    /// the parent rather than the child in isolation because the child
    /// derives from <see cref="ChildViewModelBase"/>, whose constructor
    /// requires a real <see cref="MainViewModel"/> parent.
    /// </summary>
    private static (MainViewModel Parent, ReturnIngressTimelineViewModel Child) CreateChild()
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        MainViewModel parent = new(
            new StubChartEngine(),
            new StubChartRepository(),
            new StubEphemerisService(),
            new StubSynastryService(),
            new StubCompositeService(),
            new StubSolarReturnService(),
            new StubLunarReturnService(),
            new StubPlanetaryReturnService(),
            new StubCycleTimelineService(),
            new StubGreatConjunctionService(),
            new StubIngressService(),
            new StubLunationService(),
            new StubRelocationService(),
            new StubMundaneAspectService(),
            new StubEclipseService(),
            new StubReturnIngressTimelineService(),
            new StubSynodicCycleService(),
            new StubTransitService(),
            new StubElectionalService(),
            new StubAstrocartographyService(),
            new TestDialogService(),
            new OperationCancellationRegistry(),
            logger);
        return (parent, parent.ReturnIngressTimeline);
    }

    /// <summary>
    /// Helper that fabricates a minimally-populated
    /// <see cref="ReturnIngressTimeline"/> with the specified total count.
    /// A timeline with zero entries models the pre-extraction "empty scan"
    /// case \u2014 the service still returns a non-null object, and the
    /// <c>HasResult</c> flag should stay false.
    /// </summary>
    private static ReturnIngressTimeline MakeTimeline(int count) => new()
    {
        NativeName = "Test Native",
        Entries = [],
        ScanStart = new DateTime(2025, 1, 1),
        ScanEnd = new DateTime(2028, 12, 31),
        Count = count,
        ReturnPlanets = ["Mercury", "Venus", "Mars", "Jupiter", "Saturn"],
        IngressPlanets = ["Jupiter", "Saturn", "Uranus", "Neptune", "Pluto", "Chiron"],
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    #endregion

    #region Defaults

    /// <summary>
    /// Verifies that <see cref="ReturnIngressTimelineViewModel.StartYear"/>
    /// defaults to <see cref="DateTime.Today"/>.Year, matching the
    /// pre-extraction <c>_ritStartYear</c> field initialiser on
    /// <see cref="MainViewModel"/>.
    /// </summary>
    [Fact]
    public void StartYear_DefaultsToCurrentYear()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();

        child.StartYear.Should().Be(DateTime.Today.Year);
    }

    /// <summary>
    /// Verifies that <see cref="ReturnIngressTimelineViewModel.EndYear"/>
    /// defaults to <see cref="DateTime.Today"/>.Year + 3, matching the
    /// pre-extraction <c>_ritEndYear</c> field initialiser.
    /// </summary>
    [Fact]
    public void EndYear_DefaultsToCurrentYearPlusThree()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();

        child.EndYear.Should().Be(DateTime.Today.Year + 3);
    }

    /// <summary>
    /// Verifies both event-type toggles default to <see langword="true"/>,
    /// matching the pre-extraction <c>_ritIncludeReturns</c> and
    /// <c>_ritIncludeIngresses</c> field initialisers.
    /// </summary>
    [Fact]
    public void IncludeToggles_DefaultTrue()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();

        child.IncludeReturns.Should().BeTrue();
        child.IncludeIngresses.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that <see cref="ReturnIngressTimelineViewModel.CurrentResult"/>
    /// and <see cref="ReturnIngressTimelineViewModel.HasResult"/> default to
    /// null and false respectively \u2014 no timeline available until the
    /// parent command calculates one.
    /// </summary>
    [Fact]
    public void ResultAndFlag_DefaultNullAndFalse()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();

        child.CurrentResult.Should().BeNull();
        child.HasResult.Should().BeFalse();
    }

    #endregion

    #region Input Setter Plumbing

    /// <summary>
    /// Verifies that the <see cref="ReturnIngressTimelineViewModel.StartYear"/>
    /// setter raises <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// without signalling
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>.
    /// Command <c>CanExecute</c> does not depend on the scan window.
    /// </summary>
    [Fact]
    public void StartYear_Set_NotifiesWithoutRefresh()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();
        List<string?> notified = [];
        int refreshed = 0;
        child.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        child.CommandStateRefreshRequested += (_, _) => refreshed++;

        child.StartYear = 2030;

        notified.Should().Contain(nameof(ReturnIngressTimelineViewModel.StartYear));
        refreshed.Should().Be(0);
    }

    /// <summary>
    /// Verifies that the <see cref="ReturnIngressTimelineViewModel.EndYear"/>
    /// setter raises <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// without signalling
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>.
    /// </summary>
    [Fact]
    public void EndYear_Set_NotifiesWithoutRefresh()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();
        List<string?> notified = [];
        int refreshed = 0;
        child.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        child.CommandStateRefreshRequested += (_, _) => refreshed++;

        child.EndYear = 2035;

        notified.Should().Contain(nameof(ReturnIngressTimelineViewModel.EndYear));
        refreshed.Should().Be(0);
    }

    /// <summary>
    /// Verifies that the
    /// <see cref="ReturnIngressTimelineViewModel.IncludeReturns"/> setter
    /// raises <see cref="INotifyPropertyChanged.PropertyChanged"/> without
    /// signalling <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>.
    /// </summary>
    [Fact]
    public void IncludeReturns_Set_NotifiesWithoutRefresh()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();
        List<string?> notified = [];
        int refreshed = 0;
        child.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        child.CommandStateRefreshRequested += (_, _) => refreshed++;

        child.IncludeReturns = false;

        notified.Should().Contain(nameof(ReturnIngressTimelineViewModel.IncludeReturns));
        refreshed.Should().Be(0);
    }

    /// <summary>
    /// Verifies that the
    /// <see cref="ReturnIngressTimelineViewModel.IncludeIngresses"/> setter
    /// raises <see cref="INotifyPropertyChanged.PropertyChanged"/> without
    /// signalling <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>.
    /// </summary>
    [Fact]
    public void IncludeIngresses_Set_NotifiesWithoutRefresh()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();
        List<string?> notified = [];
        int refreshed = 0;
        child.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        child.CommandStateRefreshRequested += (_, _) => refreshed++;

        child.IncludeIngresses = false;

        notified.Should().Contain(nameof(ReturnIngressTimelineViewModel.IncludeIngresses));
        refreshed.Should().Be(0);
    }

    #endregion

    #region Result-Setter Plumbing

    /// <summary>
    /// Verifies that the
    /// <see cref="ReturnIngressTimelineViewModel.CurrentResult"/> setter
    /// notifies but does <b>not</b> flip
    /// <see cref="ReturnIngressTimelineViewModel.HasResult"/> and does
    /// <b>not</b> raise
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>. The
    /// parent command writes both <c>CurrentResult</c> and <c>HasResult</c>
    /// in sequence to preserve the pre-extraction "empty-timeline zeroes the
    /// flag" semantic (value-based <c>HasX</c> per H-1.8 / H-1.9).
    /// </summary>
    [Fact]
    public void CurrentResult_Set_NotifiesButDoesNotFlagOrRefresh()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();
        List<string?> notified = [];
        int refreshed = 0;
        child.PropertyChanged += (_, e) => notified.Add(e.PropertyName);
        child.CommandStateRefreshRequested += (_, _) => refreshed++;

        child.CurrentResult = MakeTimeline(count: 5);

        notified.Should().Contain(nameof(ReturnIngressTimelineViewModel.CurrentResult));
        notified.Should().NotContain(nameof(ReturnIngressTimelineViewModel.HasResult));
        child.HasResult.Should().BeFalse();
        refreshed.Should().Be(0);
    }

    /// <summary>
    /// Verifies that the
    /// <see cref="ReturnIngressTimelineViewModel.HasResult"/> setter fires
    /// exactly one <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>
    /// event so the parent re-evaluates <c>ExportRITimelineCommand</c>.
    /// </summary>
    [Fact]
    public void HasResult_Set_FiresRefresh()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();
        int refreshed = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshed++;

        child.HasResult = true;

        refreshed.Should().Be(1);
    }

    #endregion

    #region Clear Semantics

    /// <summary>
    /// Verifies that <see cref="ReturnIngressTimelineViewModel.Clear"/>
    /// resets every field to its default and emits exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> pulse
    /// at the end (H-1.2 field-bypass pattern). Seeds every mutable field
    /// to a non-default value before clearing to verify full restoration.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllStateFiresRefreshOnce()
    {
        (_, ReturnIngressTimelineViewModel child) = CreateChild();

        // Seed every mutable field.
        child.StartYear = 1999;
        child.EndYear = 2099;
        child.IncludeReturns = false;
        child.IncludeIngresses = false;
        child.CurrentResult = MakeTimeline(count: 42);
        child.HasResult = true;

        int refreshed = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshed++;

        child.Clear();

        child.StartYear.Should().Be(DateTime.Today.Year);
        child.EndYear.Should().Be(DateTime.Today.Year + 3);
        child.IncludeReturns.Should().BeTrue();
        child.IncludeIngresses.Should().BeTrue();
        child.CurrentResult.Should().BeNull();
        child.HasResult.Should().BeFalse();
        refreshed.Should().Be(1);
    }

    #endregion

    #region Composition Invariants

    /// <summary>
    /// Verifies <see cref="MainViewModel.ReturnIngressTimeline"/> returns
    /// the same eagerly-composed instance across multiple reads.
    /// </summary>
    [Fact]
    public void ReturnIngressTimeline_ReferenceIsStableAcrossReads()
    {
        (MainViewModel parent, ReturnIngressTimelineViewModel child) = CreateChild();

        parent.ReturnIngressTimeline.Should().BeSameAs(child);
        parent.ReturnIngressTimeline.Should().BeSameAs(parent.ReturnIngressTimeline);
    }

    #endregion
}
