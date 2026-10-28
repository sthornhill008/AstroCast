using System.ComponentModel;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ChartEntryViewModel"/> — the first real
/// decomposition child introduced in Session H-1.2. Verifies setter
/// plumbing (PropertyChanged + CommandStateRefreshRequested), timezone
/// validation against the parent-owned <c>AvailableTimezones</c> list,
/// dependent-property notifications for <c>IsHorary</c> / <c>IsElectional</c>,
/// and the <see cref="ChartEntryViewModel.Clear"/> /
/// <see cref="ChartEntryViewModel.PopulateFrom"/> round-trips.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ChartEntryViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="MainViewModelTests"/>, then returns its
    /// <see cref="MainViewModel.ChartEntry"/> child. We construct the parent
    /// rather than the child in isolation because the child's constructor
    /// calls <c>base(parent)</c> and several setters read
    /// <c>Parent.AvailableTimezones</c>.
    /// </summary>
    private static (MainViewModel Parent, ChartEntryViewModel Child) CreateChild()
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
        return (parent, parent.ChartEntry);
    }

    #endregion

    #region Setter Plumbing

    /// <summary>
    /// Setting <see cref="ChartEntryViewModel.Name"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for "Name" and
    /// fire <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>
    /// because Name gates the parent's <c>CanCalculate</c>.
    /// </summary>
    [Fact]
    public void Name_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, ChartEntryViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.Name = "Test Subject";

        // Assert
        changed.Should().Contain("Name",
            "the setter must notify bindings that Name changed");
        refreshCount.Should().Be(1,
            "Name gates CanCalculate, so the setter must signal the parent once");
    }

    #endregion

    #region Timezone Validation

    /// <summary>
    /// When <see cref="ChartEntryViewModel.TimeZone"/> is set to a recognized
    /// IANA zone from the parent's <c>AvailableTimezones</c>, the child must
    /// flag <c>IsTimezoneValid = true</c> and sync
    /// <see cref="ChartEntryViewModel.SelectedTimezone"/> to the exact-case
    /// match so the ComboBox's <c>SelectedItem</c> binding updates.
    /// </summary>
    [Fact]
    public void TimeZone_SetToKnownIanaZone_SetsIsTimezoneValidTrueAndSyncsSelectedTimezone()
    {
        // Arrange — force the initial zone to something the test machine is
        // unlikely to match so the next setter call is guaranteed to be a
        // state change (SetProperty returns true and the sync block runs).
        (_, ChartEntryViewModel child) = CreateChild();
        child.TimeZone = "Etc/UTC";

        // Act
        child.TimeZone = "America/New_York";

        // Assert
        child.IsTimezoneValid.Should().BeTrue(
            "America/New_York is a well-known IANA identifier present in AvailableTimezones");
        child.SelectedTimezone.Should().Be("America/New_York",
            "SelectedTimezone must mirror the TimeZone text when a match is found");
    }

    /// <summary>
    /// When <see cref="ChartEntryViewModel.TimeZone"/> is set to a string that
    /// isn't present in the parent's <c>AvailableTimezones</c>, the child must
    /// flag <c>IsTimezoneValid = false</c> so the ComboBox's red-border
    /// DataTrigger activates.
    /// </summary>
    [Fact]
    public void TimeZone_SetToUnrecognized_SetsIsTimezoneValidFalse()
    {
        // Arrange
        (_, ChartEntryViewModel child) = CreateChild();

        // Act
        child.TimeZone = "Not/A_RealZone";

        // Assert
        child.IsTimezoneValid.Should().BeFalse(
            "an unknown zone string must fail validation so the UI can warn the user");
    }

    #endregion

    #region Dependent Properties

    /// <summary>
    /// Setting <see cref="ChartEntryViewModel.EventType"/> to "Horary" must
    /// raise <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// <c>IsHorary</c> and <c>IsElectional</c> because the XAML
    /// <c>Visibility</c> trigger on the Horary panel binds to
    /// <c>ChartEntry.IsHorary</c>.
    /// </summary>
    [Fact]
    public void EventType_SetToHorary_RaisesIsHoraryChanged()
    {
        // Arrange
        (_, ChartEntryViewModel child) = CreateChild();
        List<string> changed = [];
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        // Act
        child.EventType = "Horary";

        // Assert
        child.IsHorary.Should().BeTrue();
        changed.Should().Contain("IsHorary",
            "the Horary panel's Visibility binding depends on this notification");
        changed.Should().Contain("IsElectional",
            "both computed flags flip together when EventType changes");
    }

    #endregion

    #region Clear / PopulateFrom Round-Trip

    /// <summary>
    /// <see cref="ChartEntryViewModel.Clear"/> must reset every form field
    /// to its initial value. The parent's <c>ClearForm</c> delegates to this
    /// method, so any drift breaks the Clear button.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllFormFieldsToInitialState()
    {
        // Arrange — populate the form with non-default values
        (_, ChartEntryViewModel child) = CreateChild();
        child.Name = "Populated";
        child.LatitudeText = "51.5";
        child.LongitudeText = "-0.12";
        child.LocationName = "London";
        child.EventType = "Horary";
        child.Gender = Gender.Female;
        child.SourceNotes = "source";
        child.Notes = "notes";
        child.Keywords = "a, b";
        child.UseExplicitOffset = true;
        child.UtcOffsetHoursText = "3";
        child.HoraryQuestion = "Will the contract close?";
        child.HoraryCategory = "Career";

        // Act
        child.Clear();

        // Assert
        child.Name.Should().BeEmpty();
        child.LatitudeText.Should().Be("0");
        child.LongitudeText.Should().Be("0");
        child.LocationName.Should().BeEmpty();
        child.EventType.Should().Be("Birth");
        child.Gender.Should().Be(Gender.Unspecified);
        child.SourceNotes.Should().BeEmpty();
        child.Notes.Should().BeEmpty();
        child.Keywords.Should().BeEmpty();
        child.UseExplicitOffset.Should().BeFalse();
        child.UtcOffsetHoursText.Should().Be("-5");
        child.HoraryQuestion.Should().BeEmpty();
        child.HoraryCategory.Should().BeEmpty();
        child.EventTime.Should().Be("12:00:00");
    }

    /// <summary>
    /// <see cref="ChartEntryViewModel.Clear"/> must use the field-bypass
    /// pattern so it emits exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> at the
    /// end, regardless of how many refresh-firing setters would have
    /// cascaded had Clear used property setters instead of direct field
    /// writes. Pre-fix, Clear invoked the Name, TimeZone, and LocationName
    /// setters and fired three refresh pulses per reset. This test pins
    /// the invariant codified in the H-1 Decomposition Pattern doc; every
    /// other child VM test file asserts the same contract. ChartEntry was
    /// H-1.2 \u2014 the first decomposition child \u2014 and the test was
    /// missing because the field-bypass pattern crystallised after its
    /// extraction.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllFieldsAndFiresRefreshOnce()
    {
        // Arrange \u2014 populate every field Clear() is expected to reset.
        (_, ChartEntryViewModel child) = CreateChild();
        child.Name = "Populated";
        child.EventDate = new DateTime(1985, 3, 17);
        child.EventTime = "06:30:00";
        child.TimeZone = "America/New_York";
        child.LatitudeText = "51.5";
        child.LongitudeText = "-0.12";
        child.LocationName = "London";
        child.EventType = "Horary";
        child.Gender = Gender.Female;
        child.SourceNotes = "source";
        child.Notes = "notes";
        child.Keywords = "a, b";
        child.UseExplicitOffset = true;
        child.UtcOffsetHoursText = "3";
        child.HoraryQuestion = "Will the contract close?";
        child.HoraryCategory = "Career";

        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.Clear();

        // Assert \u2014 exactly one refresh pulse, all fields reset to defaults
        refreshCount.Should().Be(1,
            "Clear uses the field-bypass pattern so exactly one refresh " +
            "fires at the end rather than cascading through each setter");
        child.Name.Should().BeEmpty();
        child.EventDate.Should().Be(DateTime.Today);
        child.EventTime.Should().Be("12:00:00");
        child.LatitudeText.Should().Be("0");
        child.LongitudeText.Should().Be("0");
        child.LocationName.Should().BeEmpty();
        child.EventType.Should().Be("Birth");
        child.Gender.Should().Be(Gender.Unspecified);
        child.SourceNotes.Should().BeEmpty();
        child.Notes.Should().BeEmpty();
        child.Keywords.Should().BeEmpty();
        child.UseExplicitOffset.Should().BeFalse();
        child.UtcOffsetHoursText.Should().Be("-5");
        child.HoraryQuestion.Should().BeEmpty();
        child.HoraryCategory.Should().BeEmpty();
    }

    /// <summary>
    /// <see cref="ChartEntryViewModel.PopulateFrom"/> must mirror every
    /// persisted field onto the form and raise a single
    /// <c>CommandStateRefreshRequested</c> at the end rather than cascading
    /// through each setter (the field-bypass pattern documented in the H-1
    /// pattern guide).
    /// </summary>
    [Fact]
    public void PopulateFrom_SetsAllPropertiesFromEntityAndFiresSingleRefresh()
    {
        // Arrange
        (_, ChartEntryViewModel child) = CreateChild();
        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        ChartEntity entity = new()
        {
            Name = "Jane Doe",
            EventType = "Birth",
            LocalDateTime = "1975-06-15T14:30:00",
            TimeZone = "America/Chicago",
            UtcDateTime = "1975-06-15T19:30:00Z",
            JulianDay = 2442579.3125,
            Latitude = 41.8781,
            Longitude = -87.6298,
            LocationName = "Chicago, IL",
            Gender = "Female",
            SourceNotes = "birth certificate",
            Notes = "notes",
            Keywords = "family",
            HouseSystem = "P",
            CreatedAt = "2026-04-17T00:00:00Z",
            UpdatedAt = "2026-04-17T00:00:00Z",
        };

        // Act
        child.PopulateFrom(entity);

        // Assert
        child.Name.Should().Be("Jane Doe");
        child.EventDate.Should().Be(new DateTime(1975, 6, 15));
        child.EventTime.Should().Be("14:30:00");
        child.TimeZone.Should().Be("America/Chicago");
        child.LatitudeText.Should().Be("41.8781");
        child.LongitudeText.Should().Be("-87.6298");
        child.LocationName.Should().Be("Chicago, IL");
        child.EventType.Should().Be("Birth");
        child.Gender.Should().Be(Gender.Female);
        child.SourceNotes.Should().Be("birth certificate");
        child.Notes.Should().Be("notes");
        child.Keywords.Should().Be("family");
        child.SelectedHouseSystem.Should().Be(HouseSystem.Placidus);
        refreshCount.Should().Be(1,
            "PopulateFrom bypasses setters and fires a single refresh at the end");
    }

    /// <summary>
    /// <see cref="ChartEntryViewModel.PopulateFrom"/> must show a stored
    /// wall-clock reading exactly as written, whatever offset an older row
    /// carries: a <c>-04:00</c> stamp and a <c>+09:00</c> stamp both show
    /// 09:06:30 on 19 July 2026. On any one machine at most one of the two
    /// stamps can equal the local offset, so the pair cannot pass by accident
    /// of the time zone the test runs in.
    /// </summary>
    [Fact]
    public void PopulateFrom_ShowsStoredClockAsWritten_WhateverOffsetTheRowCarries()
    {
        foreach (string stored in new[] { "2026-07-19T09:06:30.0000000-04:00", "2026-07-19T09:06:30.0000000+09:00" })
        {
            // Arrange
            (_, ChartEntryViewModel child) = CreateChild();
            ChartEntity entity = new()
            {
                Name = "Stamped Row",
                EventType = "Event",
                LocalDateTime = stored,
                TimeZone = "Europe/London",
                UtcDateTime = "2026-07-19T08:06:30.0000000Z",
                JulianDay = 2461240.8378472222,
                Latitude = 51.4613531,
                Longitude = -0.3032767,
                LocationName = "Richmond, Greater London",
                CreatedAt = "2026-10-02T00:00:00Z",
                UpdatedAt = "2026-10-02T00:00:00Z",
            };

            // Act
            child.PopulateFrom(entity);

            // Assert
            child.EventDate.Should().Be(new DateTime(2026, 7, 19), $"the date as written in '{stored}'");
            child.EventTime.Should().Be("09:06:30", $"the time as written in '{stored}'");
        }
    }

    #endregion

    #region Minor Points Switch (branch AsteroidsPanel, 1.6.0)

    /// <summary>The Points box is off at every launch: the session's switch starts unset (topic 8).</summary>
    [Fact]
    public void IncludeMinorPoints_IsOffWhenCreated()
    {
        (_, ChartEntryViewModel child) = CreateChild();

        child.IncludeMinorPoints.Should().BeFalse("the five are opt-in, off at every launch");
    }

    /// <summary>
    /// <see cref="ChartEntryViewModel.Clear"/> leaves the switch: it belongs to the session, not the form (R2).
    /// </summary>
    [Fact]
    public void Clear_LeavesIncludeMinorPoints()
    {
        (_, ChartEntryViewModel child) = CreateChild();
        child.Name = "Populated";
        child.IncludeMinorPoints = true;

        child.Clear();

        child.Name.Should().BeEmpty("Clear ran");
        child.IncludeMinorPoints.Should().BeTrue("Clear Form keeps the session's switch");
    }

    /// <summary>
    /// <see cref="ChartEntryViewModel.PopulateFrom"/> leaves the switch: a row holds no value for it (R2).
    /// </summary>
    [Fact]
    public void PopulateFrom_LeavesIncludeMinorPoints()
    {
        (_, ChartEntryViewModel child) = CreateChild();
        child.IncludeMinorPoints = true;
        ChartEntity entity = new()
        {
            Name = "Jane Doe",
            EventType = "Birth",
            LocalDateTime = "1975-06-15T14:30:00",
            TimeZone = "America/Chicago",
            UtcDateTime = "1975-06-15T19:30:00Z",
            JulianDay = 2442579.3125,
            Latitude = 41.8781,
            Longitude = -87.6298,
            LocationName = "Chicago, IL",
            HouseSystem = "K",
            CreatedAt = "2026-10-20T00:00:00Z",
            UpdatedAt = "2026-10-20T00:00:00Z",
        };

        child.PopulateFrom(entity);

        child.Name.Should().Be("Jane Doe", "PopulateFrom ran");
        child.SelectedHouseSystem.Should().Be(HouseSystem.Koch, "the row's house system is read");
        child.IncludeMinorPoints.Should().BeTrue("loading a saved chart keeps the session's switch");
    }

    #endregion
}
