using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;

namespace AstroCast.UiTests;

/// <summary>
/// A stub HTTP handler for the map window's tile layer (rule 5 of the map-tiles branch): it
/// records each request's URL and User-Agent and answers every one with a 1×1 PNG, so no
/// test reaches a tile server.
/// </summary>
internal sealed class RecordingTileHandler : HttpMessageHandler
{
    /// <summary>A 1×1 transparent PNG, the answer to every request.</summary>
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private readonly ConcurrentQueue<RecordedTileRequest> _requests = new();

    /// <summary>Gets the requests recorded so far, in the order they arrived.</summary>
    public IReadOnlyList<RecordedTileRequest> Requests => _requests.ToArray();

    /// <summary>Records the request and answers it with <see cref="OnePixelPng"/>.</summary>
    /// <param name="request">The tile request.</param>
    /// <param name="cancellationToken">Unused; the answer is immediate.</param>
    /// <returns>An HTTP 200 carrying the PNG.</returns>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string userAgent = request.Headers.TryGetValues("User-Agent", out IEnumerable<string>? values)
            ? string.Join(" ", values)
            : string.Empty;
        _requests.Enqueue(new RecordedTileRequest(
            request.RequestUri ?? throw new InvalidOperationException("A tile request had no URL."),
            userAgent));

        HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new ByteArrayContent(OnePixelPng) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return Task.FromResult(response);
    }
}

/// <summary>One tile request as <see cref="RecordingTileHandler"/> saw it.</summary>
/// <param name="Uri">The requested URL.</param>
/// <param name="UserAgent">The User-Agent header sent, or empty when none was.</param>
internal sealed record RecordedTileRequest(Uri Uri, string UserAgent);
