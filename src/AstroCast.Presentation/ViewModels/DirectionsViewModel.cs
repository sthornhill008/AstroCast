using System.Globalization;
using System.Runtime.CompilerServices;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models.PrimaryDirections;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the Directions panel's state (branch PrimaryPanel, 1.5.0): the four settings a
/// primary-direction run takes, the most recent <see cref="PrimaryDirectionResult"/>, and the result line and
/// list the panel shows of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Commands stay on the parent (rule #4).</b> <c>CalculateDirectionsCommand</c> and
/// <c>ExportDirectionsMarkdownCommand</c> live on <see cref="MainViewModel"/>, because they read the parent's
/// chart (<c>LastResult</c>), call the parent's primary-direction service and write its status line. They read
/// <see cref="Settings"/> from this child and write <see cref="CurrentResult"/> back. The Transits panel's
/// fourth technique reads <see cref="Settings"/> too, so the period's hits and the whole-life list agree.
/// </para>
/// <para>
/// <b>Command-state refresh.</b> Each setting's setter and the <see cref="CurrentResult"/> setter raise
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> once on a change: Calculate Directions reads
/// <see cref="HasValidSettings"/>, and Export Directions MD reads <see cref="HasResult"/>.
/// </para>
/// <para>
/// <b>Session only.</b> The settings live for the session (scoping decision 7): nothing persists, as no other
/// panel's options do.
/// </para>
/// </remarks>
public sealed class DirectionsViewModel : ChildViewModelBase
{
    #region Fields

    private PrimaryDirectionKey _key = PrimaryDirectionKey.Naibod;
    private bool _planetsAsSignificators;
    private int _ageLimitYears = DefaultAgeLimitYears;
    private int _birthTimeUncertaintyMinutes;
    private PrimaryDirectionResult? _currentResult;

    /// <summary>The age limit a new panel and <see cref="Clear"/> start from: the engine's default, 100 years.</summary>
    private const int DefaultAgeLimitYears = 100;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the Directions view model.
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    public DirectionsViewModel(MainViewModel parent) : base(parent)
    {
    }

    #endregion

    #region Inputs

    /// <summary>
    /// Gets the keys the Key box offers, in order: Naibod, Ptolemy, the true solar arc in right ascension. Static,
    /// so the Avalonia head binds it with <c>x:Static</c>, as it binds the house systems.
    /// </summary>
    public static IReadOnlyList<PrimaryDirectionKey> AvailableKeys { get; } =
    [
        PrimaryDirectionKey.Naibod,
        PrimaryDirectionKey.Ptolemy,
        PrimaryDirectionKey.SolarArcRightAscension,
    ];

    /// <summary>
    /// Gets or sets the key that turns an arc into years. Naibod by default. A change requests a command refresh.
    /// </summary>
    public PrimaryDirectionKey Key
    {
        get => _key;
        set => SetSetting(ref _key, value, raisesValidity: false);
    }

    /// <summary>
    /// Gets or sets whether Mercury to Pluto are significators beside the angles, the lights and the Part of
    /// Fortune. False by default. A change requests a command refresh.
    /// </summary>
    public bool PlanetsAsSignificators
    {
        get => _planetsAsSignificators;
        set => SetSetting(ref _planetsAsSignificators, value, raisesValidity: false);
    }

    /// <summary>
    /// Gets or sets the age, in years, the list runs to. 100 by default; valid from
    /// <see cref="PrimaryDirectionSettings.MinAgeLimitYears"/> to <see cref="PrimaryDirectionSettings.MaxAgeLimitYears"/>.
    /// A value outside them is held, so the box shows what was typed, and <see cref="HasValidSettings"/> turns
    /// false. A change raises <see cref="HasValidSettings"/> and requests a command refresh.
    /// </summary>
    public int AgeLimitYears
    {
        get => _ageLimitYears;
        set => SetSetting(ref _ageLimitYears, value, raisesValidity: true);
    }

    /// <summary>
    /// Gets or sets the birth-time uncertainty, in minutes either way, each hit's date range is given for; 0, the
    /// default, gives no range. Valid from 0 to <see cref="PrimaryDirectionSettings.MaxBirthTimeUncertaintyMinutes"/>.
    /// A change raises <see cref="HasValidSettings"/> and requests a command refresh.
    /// </summary>
    public int BirthTimeUncertaintyMinutes
    {
        get => _birthTimeUncertaintyMinutes;
        set => SetSetting(ref _birthTimeUncertaintyMinutes, value, raisesValidity: true);
    }

    /// <summary>
    /// Gets a value indicating whether the settings are within the engine's limits: the age limit from
    /// <see cref="PrimaryDirectionSettings.MinAgeLimitYears"/> to <see cref="PrimaryDirectionSettings.MaxAgeLimitYears"/>
    /// and the uncertainty from 0 to <see cref="PrimaryDirectionSettings.MaxBirthTimeUncertaintyMinutes"/>.
    /// Calculate Directions needs it, and the Transits panel's Calculate while its Directions box is checked.
    /// </summary>
    public bool HasValidSettings =>
        _ageLimitYears is >= PrimaryDirectionSettings.MinAgeLimitYears and <= PrimaryDirectionSettings.MaxAgeLimitYears
        && _birthTimeUncertaintyMinutes is >= 0 and <= PrimaryDirectionSettings.MaxBirthTimeUncertaintyMinutes;

    /// <summary>
    /// Gets a new <see cref="PrimaryDirectionSettings"/> from the four settings, as the engine takes them.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown, as the settings' own init accessors throw it, when <see cref="HasValidSettings"/> is false.
    /// </exception>
    public PrimaryDirectionSettings Settings => new()
    {
        Key = _key,
        PlanetsAsSignificators = _planetsAsSignificators,
        AgeLimitYears = _ageLimitYears,
        BirthTimeUncertaintyMinutes = _birthTimeUncertaintyMinutes,
    };

    #endregion

    #region Result

    /// <summary>
    /// Gets or sets the most recent result, produced by <c>MainViewModel.CalculateDirectionsAsync</c>. A change
    /// raises <see cref="HasResult"/>, <see cref="ResultSummary"/> and <see cref="ListText"/> and requests a
    /// command refresh, since Export Directions MD reads <see cref="HasResult"/>.
    /// </summary>
    public PrimaryDirectionResult? CurrentResult
    {
        get => _currentResult;
        set
        {
            if (SetProperty(ref _currentResult, value))
            {
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(ResultSummary));
                OnPropertyChanged(nameof(ListText));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>Gets a value indicating whether a result is available. Derived from <see cref="CurrentResult"/>.</summary>
    public bool HasResult => _currentResult is not null;

    /// <summary>
    /// Gets the result line under the panel's buttons: "{chart}: {n} directions to age {limit}, {key}", then
    /// "; {m} left out" only when m is above 0, "1 direction" in the singular; empty while there is no result.
    /// The limit and the key are the result's own, so the line says which result Export Directions MD will write
    /// after the settings change.
    /// </summary>
    public string ResultSummary
    {
        get
        {
            if (_currentResult is not { } result)
            {
                return string.Empty;
            }

            int count = result.Hits.Count;
            string directions = count == 1 ? "1 direction" : string.Create(CultureInfo.InvariantCulture, $"{count} directions");
            string summary = string.Create(
                CultureInfo.InvariantCulture,
                $"{result.NatalChart.Input.Name}: {directions} to age {result.Settings.AgeLimitYears}, {result.Settings.Key.DisplayName}");

            return result.LeftOutCount > 0
                ? string.Create(CultureInfo.InvariantCulture, $"{summary}; {result.LeftOutCount} left out")
                : summary;
        }
    }

    /// <summary>
    /// Gets the list the panel shows: the whole-life report's table
    /// (<see cref="PrimaryDirectionMarkdownFormatter.FormatTable"/>), so the screen and the file show the same
    /// rows; empty while there is no result.
    /// </summary>
    public string ListText =>
        _currentResult is { } result ? PrimaryDirectionMarkdownFormatter.FormatTable(result) : string.Empty;

    #endregion

    #region Reset

    /// <summary>
    /// Resets every setting to its default and drops the result. Uses the field-bypass pattern, so a single
    /// <see cref="ChildViewModelBase.RequestCommandStateRefresh"/> fires at the end rather than one per setter.
    /// Called from the parent's <c>ClearForm</c>.
    /// </summary>
    public void Clear()
    {
        _key = PrimaryDirectionKey.Naibod;
        OnPropertyChanged(nameof(Key));

        _planetsAsSignificators = false;
        OnPropertyChanged(nameof(PlanetsAsSignificators));

        _ageLimitYears = DefaultAgeLimitYears;
        OnPropertyChanged(nameof(AgeLimitYears));

        _birthTimeUncertaintyMinutes = 0;
        OnPropertyChanged(nameof(BirthTimeUncertaintyMinutes));
        OnPropertyChanged(nameof(HasValidSettings));

        _currentResult = null;
        OnPropertyChanged(nameof(CurrentResult));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(ResultSummary));
        OnPropertyChanged(nameof(ListText));

        RequestCommandStateRefresh();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Sets a setting's field and, on a change, raises <see cref="HasValidSettings"/> when asked and requests one
    /// command refresh.
    /// </summary>
    /// <typeparam name="T">The setting's type.</typeparam>
    /// <param name="field">The setting's backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="raisesValidity">Whether the setting decides <see cref="HasValidSettings"/>.</param>
    /// <param name="propertyName">The setting's property name, supplied by the compiler.</param>
    private void SetSetting<T>(
        ref T field, T value, bool raisesValidity, [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            if (raisesValidity)
                OnPropertyChanged(nameof(HasValidSettings));
            RequestCommandStateRefresh();
        }
    }

    #endregion
}
