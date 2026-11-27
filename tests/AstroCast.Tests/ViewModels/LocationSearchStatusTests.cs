using System.Net;

using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// What the status line says when a place lookup fails (branch InputChecks, 1.7.2, finding N16):
/// <see cref="LocationSearchViewModel.SearchAsync"/>.
/// </summary>
/// <remarks>
/// A failure with a status code is an answer from the place-search service, a refusal such as 429 (too many
/// requests), and the line says so with the code; a failure with none is no answer at all, and keeps the sentence
/// about the connection. Until 1.7.2 both read "check internet connection", so a refusal from a service that had
/// plainly been reached sent the user to their network. The lookup runs over an HTTP handler double; nothing leaves
/// the machine.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class LocationSearchStatusTests
{
    private const string NoAnswer =
        "Location lookup failed \u2014 check internet connection. You can enter coordinates manually.";

    /// <summary>The service answers with a refusal: the line gives its code and says to try again.</summary>
    /// <param name="code">The HTTP status code the service answers with.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(429)]
    [InlineData(403)]
    [InlineData(503)]
    public async Task Search_ServiceAnswersWithARefusal_SaysItsCode(int code)
    {
        RecordingSink log = new();
        (MainViewModel main, LocationSearchViewModel search) = Lookup(
            _ => new HttpResponseMessage((HttpStatusCode)code), log);

        await search.SearchAsync();

        main.StatusText.Should().Be(
            $"The place-search service answered {code}; try again in a minute. You can enter coordinates manually.");
        search.Results.Should().BeEmpty();
        LogEvent warning = log.Events.Should().ContainSingle(e => e.Level == LogEventLevel.Warning).Which;
        warning.Exception.Should().BeOfType<HttpRequestException>("the log keeps the exception");
        warning.Properties["StatusCode"].ToString().Should().Be(code.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>No answer at all, the request itself failing: the sentence about the connection stays.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Search_NoAnswer_KeepsTheConnectionSentence()
    {
        RecordingSink log = new();
        (MainViewModel main, LocationSearchViewModel search) = Lookup(
            _ => throw new HttpRequestException("No such host is known."), log);

        await search.SearchAsync();

        main.StatusText.Should().Be(NoAnswer);
        LogEvent warning = log.Events.Should().ContainSingle(e => e.Level == LogEventLevel.Warning).Which;
        warning.Exception.Should().BeOfType<HttpRequestException>("the log keeps the exception");
        warning.Properties["StatusCode"].ToString().Should().Be("null");
    }

    /// <summary>An answer with places still fills the results, as before.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Search_ServiceAnswersWithPlaces_FillsTheResults()
    {
        const string body =
            """[{"display_name":"Harrisburg, Dauphin County, Pennsylvania, United States","lat":"40.2737","lon":"-76.8844"}]""";
        (MainViewModel main, LocationSearchViewModel search) = Lookup(
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) }, new RecordingSink());

        await search.SearchAsync();

        main.StatusText.Should().Be("Found 1 result(s). Select one to fill in coordinates.");
        search.Results.Should().ContainSingle().Which.Latitude.Should().Be(40.2737);
    }

    /// <summary>A lookup for "Harrisburg" whose HTTP requests are answered by the given function.</summary>
    /// <param name="answer">The answer to every request: a response, or an exception thrown.</param>
    /// <param name="log">The sink that receives the lookup's log events.</param>
    /// <returns>The parent view model, whose status line the lookup writes, and the lookup.</returns>
    private static (MainViewModel Main, LocationSearchViewModel Search) Lookup(
        Func<HttpRequestMessage, HttpResponseMessage> answer, RecordingSink log)
    {
        MainViewModel main = MainViewModelFactory.Create();
        LocationSearchViewModel search = new(
            main,
            new HttpClient(new AnsweringHandler(answer)),
            new NominatimRateLimiter(),
            new LoggerConfiguration().MinimumLevel.Warning().WriteTo.Sink(log).CreateLogger(),
            "location")
        {
            SearchText = "Harrisburg",
        };
        return (main, search);
    }

    /// <summary>An HTTP handler that answers every request with a function, and never reaches the network.</summary>
    /// <param name="answer">The answer to every request: a response, or an exception thrown.</param>
    private sealed class AnsweringHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        /// <inheritdoc />
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(answer(request));
    }

    /// <summary>Collects every log event written to it.</summary>
    private sealed class RecordingSink : ILogEventSink
    {
        /// <summary>Gets the events, in the order written.</summary>
        public List<LogEvent> Events { get; } = [];

        /// <inheritdoc />
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
