using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the chart-entry form state: subject name,
/// date/time, timezone, location, event type, gender, optional notes, and
/// horary / house-system selections. Extracted from <see cref="MainViewModel"/>
/// by Session H-1.2 as the first real decomposition child.
/// </summary>
/// <remarks>
/// <para>
/// All bindings for these fields live on <c>ChartEntry.X</c> in
/// <c>MainWindow.xaml</c>. Commands that act on this state (calculate, save,
/// export) remain on the parent: the child raises
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> from setters
/// whose changes affect <c>CanExecute</c>, and the parent subscribes to that
/// event in its constructor and calls its private <c>RefreshCommandStates</c>.
/// </para>
/// <para>
/// The parent also subscribes to <see cref="ChildViewModelBase.PropertyChanged"/>
/// for <see cref="TimeZone"/> so it can re-apply the substring filter on its
/// timezone <c>CollectionViewSource</c>. The filter infrastructure itself
/// stays on the parent — this child never reaches into parent-private fields.
/// </para>
/// </remarks>
public sealed class ChartEntryViewModel : ChildViewModelBase
{
    #region Fields

    /// <summary>
    /// The link macOS and Linux keep to the machine's zone file. A fixed Unix path, read
    /// only off Windows.
    /// </summary>
    private const string LocaltimePath = "/etc/localtime";

    private string _name = string.Empty;
    private DateTime _eventDate = DateTime.Today;
    private string _eventTime = "12:00:00";
    private string _timeZone = string.Empty;
    private string? _selectedTimezone;
    private string _latitudeText = "0";
    private string _longitudeText = "0";
    private string _locationName = string.Empty;
    private string _eventType = "Birth";
    private Gender _gender = Gender.Unspecified;
    private string _sourceNotes = string.Empty;
    private string _notes = string.Empty;
    private string _keywords = string.Empty;
    private bool _useExplicitOffset;
    private string _utcOffsetHoursText = "-5";
    private HouseSystem _houseSystem = HouseSystem.Placidus;
    private bool _includeMinorPoints;
    private string _horaryQuestion = string.Empty;
    private string _horaryCategory = string.Empty;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the chart-entry view model, seeding the timezone field
    /// from the host system's IANA zone.
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    public ChartEntryViewModel(MainViewModel parent) : base(parent)
    {
        // Detect system timezone as IANA identifier
        _timeZone = GetLocalIanaTimezone(parent.TimeZoneRules);
    }

    #endregion

    #region Properties — Form Input

    /// <summary>Gets or sets the chart subject name.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                RequestCommandStateRefresh();
        }
    }

    /// <summary>Gets or sets the event date.</summary>
    public DateTime EventDate
    {
        get => _eventDate;
        set
        {
            if (SetProperty(ref _eventDate, value))
                OnPropertyChanged(nameof(ResolvedOffsetDisplay));
        }
    }

    /// <summary>Gets or sets the event time as HH:mm:ss string.</summary>
    public string EventTime
    {
        get => _eventTime;
        set
        {
            if (SetProperty(ref _eventTime, value))
                OnPropertyChanged(nameof(ResolvedOffsetDisplay));
        }
    }

    /// <summary>Gets or sets the IANA timezone identifier.</summary>
    /// <remarks>
    /// The parent (<see cref="MainViewModel"/>) subscribes to
    /// <see cref="ChildViewModelBase.PropertyChanged"/> for this property name
    /// and re-applies the substring filter on its timezone
    /// <c>CollectionViewSource</c>. The filter infrastructure stays on the
    /// parent so this child never reaches into parent-private state.
    /// </remarks>
    public string TimeZone
    {
        get => _timeZone;
        set
        {
            if (SetProperty(ref _timeZone, value))
            {
                string trimmed = value?.Trim() ?? string.Empty;
                // Parent.AvailableTimezones is a last-resort read: the list is
                // a shared, immutable lookup seeded from TimezoneProvider and
                // also consumed by the parent's ElectionalTimeZone setter, so
                // duplicating it on the child would be redundant state.
                IsTimezoneValid = string.IsNullOrEmpty(trimmed)
                    || Parent.AvailableTimezones.Any(tz =>
                        tz.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                OnPropertyChanged(nameof(IsTimezoneValid));

                // Sync SelectedTimezone for ComboBox SelectedItem binding
                string? match = Parent.AvailableTimezones.FirstOrDefault(tz =>
                    tz.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                if (match != null && _selectedTimezone != match)
                {
                    _selectedTimezone = match;
                    OnPropertyChanged(nameof(SelectedTimezone));
                }

                RequestCommandStateRefresh();
                OnPropertyChanged(nameof(ResolvedOffsetDisplay));
            }
        }
    }

    /// <summary>
    /// Gets or sets the currently selected timezone from the dropdown list.
    /// Syncs with the <see cref="TimeZone"/> text property for programmatic selection.
    /// </summary>
    public string? SelectedTimezone
    {
        get => _selectedTimezone;
        set
        {
            if (SetProperty(ref _selectedTimezone, value) && value != null)
            {
                if (!string.Equals(_timeZone, value, StringComparison.OrdinalIgnoreCase))
                {
                    TimeZone = value;
                }
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current timezone string is a
    /// recognized IANA timezone identifier from the available list.
    /// </summary>
    public bool IsTimezoneValid { get; private set; } = true;

    /// <summary>
    /// Gets or sets the geographic latitude as text for data binding.
    /// Bound to a TextBox with UpdateSourceTrigger=PropertyChanged so that
    /// partial input like "37." or "-" is accepted without WPF rejecting the value.
    /// Parsed to double when building the ChartInput.
    /// </summary>
    public string LatitudeText
    {
        get => _latitudeText;
        set => SetProperty(ref _latitudeText, value);
    }

    /// <summary>
    /// Gets or sets the geographic longitude as text for data binding.
    /// Bound to a TextBox with UpdateSourceTrigger=PropertyChanged so that
    /// partial input like "-79." is accepted without WPF rejecting the value.
    /// Parsed to double when building the ChartInput.
    /// </summary>
    public string LongitudeText
    {
        get => _longitudeText;
        set => SetProperty(ref _longitudeText, value);
    }

    /// <summary>Gets or sets the human-readable location name.</summary>
    public string LocationName
    {
        get => _locationName;
        set
        {
            if (SetProperty(ref _locationName, value))
                RequestCommandStateRefresh();
        }
    }

    /// <summary>Gets or sets the event type (Birth, Event, Horary, Electional).</summary>
    public string EventType
    {
        get => _eventType;
        set
        {
            if (SetProperty(ref _eventType, value))
            {
                OnPropertyChanged(nameof(IsHorary));
                OnPropertyChanged(nameof(IsElectional));
            }
        }
    }

    /// <summary>Gets or sets the gender of the chart subject.</summary>
    public Gender Gender
    {
        get => _gender;
        set => SetProperty(ref _gender, value);
    }

    /// <summary>Gets or sets the source notes (e.g., "birth certificate").</summary>
    public string SourceNotes
    {
        get => _sourceNotes;
        set => SetProperty(ref _sourceNotes, value);
    }

    /// <summary>Gets or sets optional notes for the chart.</summary>
    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    /// <summary>Gets or sets comma-separated keywords for categorisation and filtering.</summary>
    public string Keywords
    {
        get => _keywords;
        set => SetProperty(ref _keywords, value);
    }

    /// <summary>Gets or sets the horary question text.</summary>
    public string HoraryQuestion
    {
        get => _horaryQuestion;
        set => SetProperty(ref _horaryQuestion, value);
    }

    /// <summary>
    /// The horary question category for house mapping (e.g., "Career", "Relationship").
    /// Only relevant when EventType is "Horary".
    /// </summary>
    public string HoraryCategory
    {
        get => _horaryCategory;
        set => SetProperty(ref _horaryCategory, value);
    }

    /// <summary>Available horary question categories for the dropdown.</summary>
    public static IReadOnlyList<string> AvailableHoraryCategories { get; } =
    [
        "Career",
        "Money",
        "Relationship",
        "Love",
        "Home",
        "Health",
        "Travel",
        "Education",
        "Legal",
        "Children",
        "Siblings",
        "Death / Inheritance",
        "Friends",
        "Hidden Matters",
        "Lost Objects",
        "General",
    ];

    /// <summary>Gets whether the current event type is Horary.</summary>
    public bool IsHorary => EventType == "Horary";

    /// <summary>Gets whether the current event type is Electional.</summary>
    public bool IsElectional => EventType == "Electional";

    /// <summary>
    /// Gets or sets whether to use an explicit UTC offset instead of IANA
    /// timezone lookup. Essential for historical charts with uncertain DST.
    /// </summary>
    public bool UseExplicitOffset
    {
        get => _useExplicitOffset;
        set
        {
            if (SetProperty(ref _useExplicitOffset, value))
                OnPropertyChanged(nameof(ResolvedOffsetDisplay));
        }
    }

    /// <summary>
    /// Gets or sets the UTC offset in hours as text (e.g., "-5" for EST).
    /// Bound to a TextBox; read by <see cref="TryParseUtcOffsetHours"/> when building the
    /// ChartInput: decimal hours with a point in any culture, within −14 to +14.
    /// </summary>
    public string UtcOffsetHoursText
    {
        get => _utcOffsetHoursText;
        set
        {
            if (SetProperty(ref _utcOffsetHoursText, value))
                OnPropertyChanged(nameof(ResolvedOffsetDisplay));
        }
    }

    /// <summary>Gets or sets the house system for chart calculation.</summary>
    public HouseSystem SelectedHouseSystem
    {
        get => _houseSystem;
        set => SetProperty(ref _houseSystem, value);
    }

    /// <summary>
    /// Gets or sets the session's minor-points switch: whether the charts cast from this form carry Ceres,
    /// Pallas, Juno, Vesta and Black Moon Lilith (<see cref="ChartInput.IncludeMinorPoints"/>; branch
    /// AsteroidsPanel, 1.6.0). Bound to the Points row's CheckBox in the Houses section of both heads.
    /// </summary>
    /// <remarks>
    /// Off at every launch and never saved: the row has no column for it. <see cref="Clear"/> and
    /// <see cref="PopulateFrom"/> leave it as it is (R2: Clear Form and loading a saved chart keep the session's
    /// value). <see cref="MainViewModel"/> reads it into every chart input it builds or maps, and recasts the
    /// Chart panel's chart when it changes. It gates no command, so the setter requests no refresh.
    /// </remarks>
    public bool IncludeMinorPoints
    {
        get => _includeMinorPoints;
        set => SetProperty(ref _includeMinorPoints, value);
    }

    /// <summary>Available house systems for the ComboBox.</summary>
    public static IReadOnlyList<HouseSystem> AvailableHouseSystems { get; } =
    [
        HouseSystem.Placidus,
        HouseSystem.Koch,
        HouseSystem.Equal,
        HouseSystem.WholeSign,
        HouseSystem.Regiomontanus,
        HouseSystem.Campanus,
        HouseSystem.Alcabitius,
        HouseSystem.Porphyry,
        HouseSystem.Morinus,
    ];

    /// <summary>
    /// Live-resolved UTC offset label for the current timezone and date/time,
    /// displayed in the chart entry form so the user can verify correctness
    /// before calculating. Updates automatically when timezone, date, time,
    /// or explicit offset fields change.
    /// </summary>
    /// <remarks>
    /// When <see cref="UseExplicitOffset"/> is true, shows the explicit offset
    /// (e.g., "UTC\u22125 (explicit)"), read by <see cref="TryParseUtcOffsetHours"/> as
    /// Calculate &amp; Save reads it, or "\u2014" when it is refused. When false,
    /// resolves the offset from the IANA timezone database for the current date/time
    /// (e.g., "EST (UTC\u22125)").
    /// Shows "\u2014" if the timezone is invalid or the offset cannot be resolved.
    /// The label is the chart engine's (<see cref="OffsetLabelFormatter"/>), on the same
    /// time-zone rules and by the same rule for a repeated or skipped hour (D17), so it
    /// is the label the chart will be calculated and saved with.
    /// </remarks>
    public string ResolvedOffsetDisplay
    {
        get
        {
            try
            {
                if (UseExplicitOffset)
                {
                    if (TryParseUtcOffsetHours(UtcOffsetHoursText, out double explicitOffset, out _))
                        return OffsetLabelFormatter.Format(TimeSpan.FromHours(explicitOffset), null, isExplicit: true);
                    return "\u2014";
                }

                string tz = TimeZone?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(tz))
                    return "\u2014";

                ZoneInstant resolved = Parent.TimeZoneRules.ToUniversal(GetCurrentLocalDateTime(), tz);
                return OffsetLabelFormatter.Format(resolved.Offset, resolved.Abbreviation, isExplicit: false);
            }
            catch
            {
                return "\u2014";
            }
        }
    }

    #endregion

    #region Population & Reset

    /// <summary>
    /// Populates every form field from a persisted <see cref="ChartEntity"/>.
    /// Uses direct field assignment + per-property <c>PropertyChanged</c>
    /// raises so a single <see cref="ChildViewModelBase.RequestCommandStateRefresh"/>
    /// fires at the end, instead of cascading through each setter.
    /// </summary>
    /// <remarks>
    /// The date and time fields show the stored wall-clock reading exactly as
    /// written, read by <see cref="ChartMapper.ParseStoredLocalDateTime"/>: an
    /// offset suffix on an older row (the saving machine's, not the chart's) is
    /// ignored, so the form shows the same clock on any machine and a re-save
    /// keeps it.
    /// </remarks>
    /// <param name="entity">The persisted chart to load into the form.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="entity"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="FormatException">
    /// Thrown if the entity's stored local date/time is not a valid date/time string.
    /// </exception>
    public void PopulateFrom(ChartEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        DateTime local = ChartMapper.ParseStoredLocalDateTime(entity.LocalDateTime);

        _name = entity.Name;
        OnPropertyChanged(nameof(Name));

        _eventDate = local.Date;
        OnPropertyChanged(nameof(EventDate));

        _eventTime = local.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(EventTime));

        _timeZone = entity.TimeZone;
        OnPropertyChanged(nameof(TimeZone));

        string trimmedTz = _timeZone?.Trim() ?? string.Empty;
        IsTimezoneValid = string.IsNullOrEmpty(trimmedTz)
            || Parent.AvailableTimezones.Any(tz =>
                tz.Equals(trimmedTz, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(IsTimezoneValid));

        string? tzMatch = Parent.AvailableTimezones.FirstOrDefault(tz =>
            tz.Equals(trimmedTz, StringComparison.OrdinalIgnoreCase));
        _selectedTimezone = tzMatch;
        OnPropertyChanged(nameof(SelectedTimezone));

        _latitudeText = entity.Latitude.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(LatitudeText));

        _longitudeText = entity.Longitude.ToString(CultureInfo.InvariantCulture);
        OnPropertyChanged(nameof(LongitudeText));

        _locationName = entity.LocationName;
        OnPropertyChanged(nameof(LocationName));

        _eventType = entity.EventType;
        OnPropertyChanged(nameof(EventType));

        _gender = Enum.TryParse<Gender>(entity.Gender, out Gender g) ? g : Gender.Unspecified;
        OnPropertyChanged(nameof(Gender));

        _sourceNotes = entity.SourceNotes ?? string.Empty;
        OnPropertyChanged(nameof(SourceNotes));

        _useExplicitOffset = entity.UseExplicitOffset;
        OnPropertyChanged(nameof(UseExplicitOffset));

        _utcOffsetHoursText = entity.UtcOffsetHours.HasValue
            ? entity.UtcOffsetHours.Value.ToString(CultureInfo.InvariantCulture)
            : "-5";
        OnPropertyChanged(nameof(UtcOffsetHoursText));

        _notes = entity.Notes ?? string.Empty;
        OnPropertyChanged(nameof(Notes));

        _keywords = entity.Keywords ?? string.Empty;
        OnPropertyChanged(nameof(Keywords));

        _horaryQuestion = entity.HoraryQuestion ?? string.Empty;
        OnPropertyChanged(nameof(HoraryQuestion));

        _horaryCategory = entity.HoraryCategory ?? string.Empty;
        OnPropertyChanged(nameof(HoraryCategory));

        _houseSystem = string.IsNullOrEmpty(entity.HouseSystem)
            ? HouseSystem.Placidus
            : (HouseSystem)entity.HouseSystem[0];
        OnPropertyChanged(nameof(SelectedHouseSystem));

        // Dependent / computed properties
        OnPropertyChanged(nameof(IsHorary));
        OnPropertyChanged(nameof(IsElectional));
        OnPropertyChanged(nameof(ResolvedOffsetDisplay));

        RequestCommandStateRefresh();
    }

    /// <summary>
    /// Resets every form field to its initial value using direct field
    /// assignment + per-property <c>PropertyChanged</c> raises so a single
    /// <see cref="ChildViewModelBase.RequestCommandStateRefresh"/> fires at
    /// the end, instead of cascading through each refresh-firing setter
    /// (Name, TimeZone, LocationName). Mirrors the field-bypass pattern
    /// used by <see cref="PopulateFrom"/> on the same class and codified
    /// in the H-1 Decomposition Pattern doc. The parent's <c>ClearForm</c>
    /// delegates the chart-entry portion of the reset to this method.
    /// </summary>
    public void Clear()
    {
        _name = string.Empty;
        OnPropertyChanged(nameof(Name));

        _eventDate = DateTime.Today;
        OnPropertyChanged(nameof(EventDate));

        _eventTime = "12:00:00";
        OnPropertyChanged(nameof(EventTime));

        _timeZone = GetLocalIanaTimezone(Parent.TimeZoneRules);
        OnPropertyChanged(nameof(TimeZone));

        string trimmedTz = _timeZone?.Trim() ?? string.Empty;
        IsTimezoneValid = string.IsNullOrEmpty(trimmedTz)
            || Parent.AvailableTimezones.Any(tz =>
                tz.Equals(trimmedTz, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(IsTimezoneValid));

        string? tzMatch = Parent.AvailableTimezones.FirstOrDefault(tz =>
            tz.Equals(trimmedTz, StringComparison.OrdinalIgnoreCase));
        _selectedTimezone = tzMatch;
        OnPropertyChanged(nameof(SelectedTimezone));

        _latitudeText = "0";
        OnPropertyChanged(nameof(LatitudeText));

        _longitudeText = "0";
        OnPropertyChanged(nameof(LongitudeText));

        _locationName = string.Empty;
        OnPropertyChanged(nameof(LocationName));

        _eventType = "Birth";
        OnPropertyChanged(nameof(EventType));

        _gender = Gender.Unspecified;
        OnPropertyChanged(nameof(Gender));

        _sourceNotes = string.Empty;
        OnPropertyChanged(nameof(SourceNotes));

        _useExplicitOffset = false;
        OnPropertyChanged(nameof(UseExplicitOffset));

        _utcOffsetHoursText = "-5";
        OnPropertyChanged(nameof(UtcOffsetHoursText));

        _notes = string.Empty;
        OnPropertyChanged(nameof(Notes));

        _keywords = string.Empty;
        OnPropertyChanged(nameof(Keywords));

        _horaryQuestion = string.Empty;
        OnPropertyChanged(nameof(HoraryQuestion));

        _horaryCategory = string.Empty;
        OnPropertyChanged(nameof(HoraryCategory));

        // Dependent / computed properties
        OnPropertyChanged(nameof(IsHorary));
        OnPropertyChanged(nameof(IsElectional));
        OnPropertyChanged(nameof(ResolvedOffsetDisplay));

        RequestCommandStateRefresh();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// The largest explicit offset the box accepts, in hours either side of UTC. Real offsets
    /// lie within −12 to +14.
    /// </summary>
    public const double MaxUtcOffsetHours = 14.0;

    /// <summary>
    /// Reads the explicit UTC offset box (O-D4): decimal hours with a point, in any culture,
    /// as the latitude and longitude boxes are read, within −14 to +14 hours. Every place
    /// that reads the box reads it here — Calculate &amp; Save, the electional search's natal
    /// fields, "Set to now" and the form's live label.
    /// </summary>
    /// <remarks>
    /// The box was read in the machine's culture with no range check, so a separator the
    /// culture took for grouping multiplied the offset by ten or a hundred and the chart was
    /// cast and saved hours or weeks away (E3-1): "-5,5" under en-US and "-5.5" under de-DE
    /// were both −55. A comma is now never a decimal or a grouping separator: "-5,5" is
    /// refused, not read as −55.
    /// </remarks>
    /// <param name="text">The box's text.</param>
    /// <param name="hours">The offset in hours, east of Greenwich positive, when the text is valid.</param>
    /// <param name="error">
    /// The message the form shows when the text is not valid, in the manner of a bad
    /// latitude's; <see langword="null"/> when it is valid.
    /// </param>
    /// <returns><see langword="true"/> when the text is an offset the form accepts.</returns>
    public static bool TryParseUtcOffsetHours(string? text, out double hours, [NotNullWhen(false)] out string? error)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out hours)
            || !double.IsFinite(hours))
        {
            hours = 0;
            error = "Invalid UTC offset. Use decimal hours (e.g., -5 for EST, 5.5 for India).";
            return false;
        }

        if (hours < -MaxUtcOffsetHours || hours > MaxUtcOffsetHours)
        {
            hours = 0;
            error = "UTC offset must be between -14 and +14 hours.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>The largest latitude the coordinate boxes accept, in degrees either side of the equator.</summary>
    public const double MaxLatitude = 90.0;

    /// <summary>The largest longitude the coordinate boxes accept, in degrees either side of Greenwich.</summary>
    public const double MaxLongitude = 180.0;

    /// <summary>
    /// Reads a latitude box: decimal degrees with a point, in any culture, within −90 to +90.
    /// Every latitude box reads here (1.4.0, port plan §9 item 9): the Chart panel's at
    /// Calculate &amp; Save and when the electional search casts the natal chart from the form,
    /// the electional event's, and the Relocation panel's on every change.
    /// </summary>
    /// <remarks>
    /// The Chart and Electional panels read their boxes with the overload that allows
    /// thousands separators, so "-7,5" was −75, the defect O-D4 removed from the offset box
    /// (E3-1), and the Electional panel had no range check. A comma is now refused, as it is
    /// in <see cref="TryParseUtcOffsetHours"/>. The messages are the Chart panel's.
    /// </remarks>
    /// <param name="text">The box's text.</param>
    /// <param name="degrees">The latitude in degrees, north positive, when the text is valid.</param>
    /// <param name="error">The message when the text is not valid; <see langword="null"/> when it is.</param>
    /// <returns><see langword="true"/> when the text is a latitude the form accepts.</returns>
    public static bool TryParseLatitude(string? text, out double degrees, [NotNullWhen(false)] out string? error) =>
        TryParseDegrees(
            text,
            MaxLatitude,
            "Invalid latitude. Use decimal degrees (e.g., 37.4380).",
            "Latitude must be between -90 and 90 degrees.",
            out degrees,
            out error);

    /// <summary>
    /// Reads a longitude box: decimal degrees with a point, in any culture, within −180 to +180.
    /// Read wherever <see cref="TryParseLatitude"/> is, with the same rules.
    /// </summary>
    /// <param name="text">The box's text.</param>
    /// <param name="degrees">The longitude in degrees, east positive, when the text is valid.</param>
    /// <param name="error">The message when the text is not valid; <see langword="null"/> when it is.</param>
    /// <returns><see langword="true"/> when the text is a longitude the form accepts.</returns>
    public static bool TryParseLongitude(string? text, out double degrees, [NotNullWhen(false)] out string? error) =>
        TryParseDegrees(
            text,
            MaxLongitude,
            "Invalid longitude. Use decimal degrees (e.g., -79.1877).",
            "Longitude must be between -180 and 180 degrees.",
            out degrees,
            out error);

    /// <summary>
    /// Reads decimal degrees with a point under the invariant culture and checks them against
    /// ±<paramref name="limit"/>.
    /// </summary>
    /// <param name="text">The box's text.</param>
    /// <param name="limit">The largest magnitude accepted.</param>
    /// <param name="invalid">The message for text that is not a finite number.</param>
    /// <param name="outOfRange">The message for a number beyond the limit.</param>
    /// <param name="degrees">The degrees read, or 0 when refused.</param>
    /// <param name="error">The message when refused; <see langword="null"/> when accepted.</param>
    /// <returns><see langword="true"/> when the text is accepted.</returns>
    private static bool TryParseDegrees(
        string? text,
        double limit,
        string invalid,
        string outOfRange,
        out double degrees,
        [NotNullWhen(false)] out string? error)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out degrees)
            || !double.IsFinite(degrees))
        {
            degrees = 0;
            error = invalid;
            return false;
        }

        if (degrees < -limit || degrees > limit)
        {
            degrees = 0;
            error = outOfRange;
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Constructs a DateTime from the current <see cref="EventDate"/> and
    /// <see cref="EventTime"/> fields. Returns noon on EventDate if EventTime
    /// cannot be parsed.
    /// </summary>
    private DateTime GetCurrentLocalDateTime()
    {
        if (TimeSpan.TryParse(EventTime, out TimeSpan time))
            return EventDate.Date.Add(time);
        return EventDate.Date.AddHours(12);
    }

    /// <summary>
    /// Get the IANA timezone identifier for the local system timezone: the machine's zone
    /// mapped into the offered list (card 7), or blank if it is not offered.
    /// </summary>
    /// <param name="rules">The time-zone rules whose zones are offered.</param>
    /// <returns>An offered zone, or <see cref="string.Empty"/>.</returns>
    /// <remarks>
    /// On macOS and Linux the zone <c>/etc/localtime</c> links to comes first, then
    /// <see cref="TimeZoneInfo.Local"/>, whose Windows identifier is mapped through CLDR
    /// (<see cref="TimezoneProvider.FindMachineZone"/>). The Windows identifier itself is
    /// never returned: it was once the fallback here, and is not a zone the form offers.
    /// </remarks>
    private static string GetLocalIanaTimezone(ITimeZoneRules rules)
    {
        string? linkTarget = null;
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                linkTarget = new FileInfo(LocaltimePath).LinkTarget;
            }
            catch (IOException)
            {
                // No readable link: the platform's local zone decides.
            }
            catch (UnauthorizedAccessException)
            {
                // No readable link: the platform's local zone decides.
            }
        }

        return TimezoneProvider.FindMachineZone(linkTarget, TimeZoneInfo.Local.Id, rules);
    }

    #endregion
}
