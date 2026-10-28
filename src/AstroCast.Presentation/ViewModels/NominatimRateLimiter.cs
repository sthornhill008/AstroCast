namespace AstroCast.ViewModels;

/// <summary>
/// Serialises outbound Nominatim geocoding requests to honour the public
/// endpoint's 1-request-per-second usage policy. A single instance is
/// shared across every <see cref="LocationSearchViewModel"/> composed by
/// <see cref="MainViewModel"/> so requests issued from different panels
/// still respect the global rate limit.
/// </summary>
/// <remarks>
/// Extracted in Session H-1.3 from the previous
/// <c>_lastGeocodingRequest</c> timestamp field on <see cref="MainViewModel"/>
/// when the four duplicated location-search blocks were consolidated into
/// a reusable child view model. Preserves existing behaviour verbatim:
/// no locking, no ordering guarantees between concurrent callers — the
/// same "last writer wins" semantics that the pre-H-1.3 field had.
/// </remarks>
public sealed class NominatimRateLimiter
{
    private DateTime _lastRequestUtc = DateTime.MinValue;

    /// <summary>
    /// Awaits whatever remaining delay is required so that the call-site
    /// can issue its next request without violating the minimum spacing
    /// between Nominatim hits. Updates the internal timestamp before
    /// returning so subsequent callers queue against this call.
    /// </summary>
    /// <param name="minInterval">Minimum spacing between requests.</param>
    public async Task WaitForTurnAsync(TimeSpan minInterval)
    {
        TimeSpan elapsed = DateTime.UtcNow - _lastRequestUtc;
        if (elapsed < minInterval)
        {
            await Task.Delay(minInterval - elapsed);
        }
        _lastRequestUtc = DateTime.UtcNow;
    }
}
