using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

using Serilog;

using AstroCast.Core.Interfaces;

namespace AstroCast.ViewModels;

/// <summary>
/// Reusable child view model that owns a single panel's location-search
/// state: the search text input, the results collection, the selected
/// result, and the dropdown-open flag. Each consuming panel composes its
/// own instance (Chart Entry, Electional, Mundane, Relocation) so
/// cross-panel state isolation is preserved by construction.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This VM owns the search lifecycle end-to-end: input
/// text, HTTP call to Nominatim, deserialisation, results population,
/// selection. Follow-on logic when a result is selected (e.g. assigning
/// latitude / longitude into another child VM's form fields or
/// triggering timezone resolution) stays on <see cref="MainViewModel"/>
/// per cross-VM rule #4 — commands that write to sibling VMs stay on
/// the parent. The parent subscribes to <see cref="ChildViewModelBase.PropertyChanged"/>
/// for <see cref="SelectedResult"/> changes and runs the per-panel
/// follow-on.
/// </para>
/// <para>
/// <b>Shared infrastructure.</b> The <see cref="HttpClient"/> and the
/// <see cref="NominatimRateLimiter"/> are both owned by
/// <see cref="MainViewModel"/> and injected via the constructor. All
/// four instances share the same <see cref="HttpClient"/> (per
/// Microsoft guidance) and the same rate limiter so Nominatim's
/// 1-request-per-second usage policy is honoured globally, not just
/// within a single panel.
/// </para>
/// <para>
/// <b>Collection synchronisation.</b> The constructor registers
/// <see cref="Results"/> with
/// <see cref="IUiDispatcher.EnableCollectionSynchronization(System.Collections.IEnumerable, object)"/>
/// on a per-instance lock object so background-thread mutations remain
/// safe and the four instances never serialise on a shared monitor. The
/// dispatcher is read from the parent, so this type names no UI framework.
/// </para>
/// <para>
/// <b>Cancellation.</b> Pre-H-1.3 code did not cancel in-flight
/// searches when a new one began; this class preserves that behaviour
/// exactly. Introducing cancellation here would be a drive-by
/// refactoring, which the H-1.3 prompt explicitly forbids.
/// </para>
/// <para>
/// <b>Status messages.</b> The class writes search progress and
/// outcome messages to <see cref="MainViewModel.StatusText"/> — the
/// same status-bar field the rest of the parent uses. This is a
/// deliberate last-resort back-reference per cross-VM rule #2: the
/// status bar is a parent-owned shared UI surface, and modelling it as
/// an input property on each child would multiply indirection for
/// zero behavioural benefit.
/// </para>
/// </remarks>
public sealed class LocationSearchViewModel : ChildViewModelBase
{
    #region Static helpers

    /// <summary>
    /// JSON serializer options configured for Nominatim API snake_case
    /// responses. Shared across every instance; options are immutable
    /// after construction.
    /// </summary>
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>
    /// Minimum spacing between Nominatim requests per the public
    /// endpoint's 1-request-per-second usage policy.
    /// </summary>
    private static readonly TimeSpan s_minRequestInterval = TimeSpan.FromSeconds(1);

    #endregion

    #region Fields

    private readonly HttpClient _httpClient;
    private readonly NominatimRateLimiter _rateLimiter;
    private readonly ILogger _logger;
    private readonly string _logContext;
    private readonly object _resultsLock = new();

    private string _searchText;
    private LocationSearchResult? _selectedResult;
    private bool _isDropDownOpen;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises a new instance. Each consuming panel composes its own.
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <param name="httpClient">Shared Nominatim <see cref="HttpClient"/>.</param>
    /// <param name="rateLimiter">Shared rate-limit gate.</param>
    /// <param name="logger">Serilog logger.</param>
    /// <param name="logContext">
    /// Panel name used in log messages (e.g. "location", "electional
    /// location", "mundane location", "relocation location"). Preserved
    /// verbatim from the pre-H-1.3 per-panel methods so existing log
    /// grep patterns remain unchanged.
    /// </param>
    /// <param name="initialSearchText">
    /// Optional initial value for <see cref="SearchText"/>. Used to
    /// preserve the Mundane panel's pre-populated default
    /// ("Washington, DC").
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if any required dependency is <see langword="null"/>.
    /// </exception>
    public LocationSearchViewModel(
        MainViewModel parent,
        HttpClient httpClient,
        NominatimRateLimiter rateLimiter,
        ILogger logger,
        string logContext,
        string initialSearchText = "")
        : base(parent)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(logContext);

        _httpClient = httpClient;
        _rateLimiter = rateLimiter;
        _logger = logger;
        _logContext = logContext;
        _searchText = initialSearchText ?? string.Empty;

        Results = new ObservableCollection<LocationSearchResult>();

        // Registered through the parent's IUiDispatcher rather than calling
        // WPF's BindingOperations directly, so this file carries no WPF
        // dependency. Null-conditional: with no dispatcher there is no binding
        // layer to inform, and registration is correctly a no-op.
        parent.UiDispatcher?.EnableCollectionSynchronization(Results, _resultsLock);
    }

    #endregion

    #region Test-Only Accessors (Pass 2 I-2)

    /// <summary>
    /// Test-only accessor for the shared <see cref="HttpClient"/> instance.
    /// Exposed for identity-assertion coverage in
    /// <c>SharedInfrastructureIdentityTests</c>; the Microsoft HttpClient
    /// singleton pattern requires every <see cref="LocationSearchViewModel"/>
    /// composed on the same <see cref="MainViewModel"/> to share one instance.
    /// </summary>
    internal HttpClient HttpClientForTesting => _httpClient;

    /// <summary>
    /// Test-only accessor for the shared <see cref="NominatimRateLimiter"/>
    /// instance. Exposed for identity-assertion coverage in
    /// <c>SharedInfrastructureIdentityTests</c>; Nominatim's global
    /// 1-request-per-second policy requires every
    /// <see cref="LocationSearchViewModel"/> composed on the same
    /// <see cref="MainViewModel"/> to share one rate-limit gate.
    /// </summary>
    internal NominatimRateLimiter RateLimiterForTesting => _rateLimiter;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the search results from the most recent Nominatim call.
    /// Background-safe via
    /// <see cref="IUiDispatcher.EnableCollectionSynchronization(System.Collections.IEnumerable, object)"/>
    /// registered in the constructor.
    /// </summary>
    public ObservableCollection<LocationSearchResult> Results { get; }

    /// <summary>
    /// Gets or sets the text in the panel's location search field.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    /// <summary>
    /// Gets or sets the result currently selected in the dropdown. The
    /// parent subscribes to <see cref="ChildViewModelBase.PropertyChanged"/>
    /// for this property and runs panel-specific follow-on logic
    /// (writing coordinates into a chart-entry form, resolving the IANA
    /// timezone from the selected coordinates, etc.).
    /// </summary>
    public LocationSearchResult? SelectedResult
    {
        get => _selectedResult;
        set => SetProperty(ref _selectedResult, value);
    }

    /// <summary>
    /// Gets or sets whether the Results ComboBox dropdown is open. Set
    /// to <see langword="true"/> after a successful search so the user
    /// sees results immediately.
    /// </summary>
    public bool IsDropDownOpen
    {
        get => _isDropDownOpen;
        set => SetProperty(ref _isDropDownOpen, value);
    }

    #endregion

    #region Commands

    /// <summary>
    /// Runs a Nominatim search against <see cref="SearchText"/>,
    /// populates <see cref="Results"/>, and opens the dropdown.
    /// Honours the shared rate limit.
    /// </summary>
    /// <remarks>
    /// Matches the pre-H-1.3 per-panel behaviour byte-for-byte:
    /// empty / whitespace query short-circuits with a status message;
    /// network or timeout exceptions log a warning and surface a
    /// user-friendly fallback message; any other exception logs an
    /// error with the full stack trace. No in-flight cancellation.
    /// Since 1.7.2 (branch InputChecks, finding N16) a failure that carries
    /// an HTTP status code reads "The place-search service answered {code};
    /// try again in a minute. You can enter coordinates manually.", and the
    /// warning logs the code and the exception.
    /// </remarks>
    public async Task SearchAsync()
    {
        _logger.Debug(
            "Lookup{Context}Async invoked, SearchText='{Text}'",
            _logContext, _searchText);

        if (string.IsNullOrWhiteSpace(_searchText))
        {
            Parent.StatusText = "Enter a location name to search.";
            _logger.Debug("{Context} lookup skipped: empty search text", _logContext);
            return;
        }

        try
        {
            Parent.StatusText = "Searching...";

            await _rateLimiter.WaitForTurnAsync(s_minRequestInterval);

            string query = Uri.EscapeDataString(_searchText.Trim());
            string url =
                $"https://nominatim.openstreetmap.org/search?q={query}&format=jsonv2&limit=5&accept-language=en";

            string json = await _httpClient.GetStringAsync(url);
            List<NominatimResult>? results =
                JsonSerializer.Deserialize<List<NominatimResult>>(json, s_jsonOptions);

            Results.Clear();

            if (results is null || results.Count == 0)
            {
                Parent.StatusText = "No results found.";
                return;
            }

            foreach (NominatimResult r in results)
            {
                if (double.TryParse(r.Lat, CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(r.Lon, CultureInfo.InvariantCulture, out double lon))
                {
                    Results.Add(new LocationSearchResult
                    {
                        DisplayName = CleanDisplayName(r.DisplayName ?? string.Empty),
                        Latitude = lat,
                        Longitude = lon,
                    });
                }
            }

            Parent.StatusText =
                $"Found {Results.Count} result(s). Select one to fill in coordinates.";
            IsDropDownOpen = true;
            _logger.Information(
                "{Context} lookup returned {Count} results", _logContext, Results.Count);
            _logger.Debug(
                "{Context} lookup for '{Query}' returned {Count} results",
                _logContext, _searchText.Trim(), Results.Count);
        }
        catch (HttpRequestException ex)
        {
            // A status code means the service was reached and refused, 429 when asked too often: the
            // connection is not at fault, and the line says so. None means no answer at all (branch
            // InputChecks, 1.7.2, finding N16; until then both read "check internet connection").
            int? statusCode = ex.StatusCode is { } status ? (int)status : null;
            Parent.StatusText = statusCode is { } code
                ? string.Create(
                    CultureInfo.InvariantCulture,
                    $"The place-search service answered {code}; try again in a minute. You can enter coordinates manually.")
                : "Location lookup failed \u2014 check internet connection. You can enter coordinates manually.";
            _logger.Warning(
                ex, "{Context} lookup failed: network error, status code {StatusCode}", _logContext, statusCode);
        }
        catch (TaskCanceledException)
        {
            Parent.StatusText =
                "Location lookup timed out \u2014 check internet connection. You can enter coordinates manually.";
            _logger.Warning("{Context} lookup failed: timeout", _logContext);
        }
        catch (Exception ex)
        {
            Parent.StatusText = $"Location lookup error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to look up {Context}", _logContext);
        }
    }

    /// <summary>
    /// Clears results, selected result, and search text. Used by the
    /// parent's <c>ClearForm</c> and <c>LoadChart</c> flows to reset
    /// panel state after loading a saved chart.
    /// </summary>
    public void Clear()
    {
        Results.Clear();
        SelectedResult = null;
        SearchText = string.Empty;
    }

    /// <summary>
    /// Clears just the results collection and the selected result,
    /// leaving <see cref="SearchText"/> intact. Mirrors the narrower
    /// post-<c>UseNatalLocation</c> cleanup the Electional panel ran
    /// pre-H-1.3 where the search text was intentionally preserved.
    /// </summary>
    public void ClearResults()
    {
        Results.Clear();
        SelectedResult = null;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Strip non-Latin segments from a Nominatim display name. Some
    /// international results return a comma-separated name where
    /// intermediate segments are in the local script — e.g.
    /// "Tehran, بخش مرکزی شهرستان تهران, Tehran County". Splits on
    /// commas, drops any segment containing a letter outside Basic
    /// Latin through Latin Extended-B (U+0041..U+024F), and rejoins
    /// the survivors. Falls back to the original string when
    /// filtering would leave nothing.
    /// </summary>
    /// <param name="displayName">The raw <c>display_name</c> from Nominatim.</param>
    /// <returns>Display name with only Latin-script segments preserved.</returns>
    private static string CleanDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return displayName;

        string[] latinSegments = displayName
            .Split(',')
            .Where(s => !s.Trim().Any(c => char.IsLetter(c) && c > '\u024F'))
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        return latinSegments.Length > 0
            ? string.Join(", ", latinSegments)
            : displayName;
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// DTO for deserialising Nominatim API search results. Property
    /// names map to the JSON snake_case fields via
    /// <see cref="JsonNamingPolicy.SnakeCaseLower"/>.
    /// </summary>
    private sealed class NominatimResult
    {
        /// <summary>Latitude as string (Nominatim returns coordinates as strings).</summary>
        public string? Lat { get; set; }

        /// <summary>Longitude as string (Nominatim returns coordinates as strings).</summary>
        public string? Lon { get; set; }

        /// <summary>Full display name of the location.</summary>
        public string? DisplayName { get; set; }
    }

    #endregion
}
