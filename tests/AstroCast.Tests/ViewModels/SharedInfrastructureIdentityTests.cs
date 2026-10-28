using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Post-H-1 Pass 2 (I-2) reference-identity coverage for the shared
/// infrastructure that every <see cref="LocationSearchViewModel"/> must
/// receive from its composing <see cref="MainViewModel"/>. Prior coverage
/// asserted that each panel could successfully compose a
/// <c>LocationSearchViewModel</c>; this suite additionally asserts the
/// underlying <see cref="HttpClient"/> and
/// <see cref="NominatimRateLimiter"/> references are the exact instances
/// the panel was passed, which is the contract Nominatim's
/// 1-request-per-second global policy depends on.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why identity matters.</b> Nominatim's fair-use policy is global per
/// IP; if any panel composed its own <see cref="HttpClient"/> or its own
/// <see cref="NominatimRateLimiter"/>, the rate limit would no longer be
/// enforced across panels. The H-1.3 decomposition was carefully wired so
/// that a single static <see cref="HttpClient"/> and a single
/// per-<see cref="MainViewModel"/> rate limiter are threaded through every
/// <see cref="LocationSearchViewModel"/>. Refactors that break this wiring
/// would silently degrade the gate — identity assertions are the only way
/// to catch that regression at compile/test time.
/// </para>
/// <para>
/// <b>Expected references.</b>
/// <list type="bullet">
///   <item><description>
///     <see cref="HttpClient"/>: the same instance across <b>every</b>
///     <see cref="LocationSearchViewModel"/> on a <see cref="MainViewModel"/>
///     <b>and</b> across separate <see cref="MainViewModel"/> instances
///     (because <c>s_httpClient</c> is <c>private static readonly</c>).
///   </description></item>
///   <item><description>
///     <see cref="NominatimRateLimiter"/>: the same instance across <b>every</b>
///     <see cref="LocationSearchViewModel"/> on a <see cref="MainViewModel"/>,
///     but a <b>different</b> instance across separate
///     <see cref="MainViewModel"/> instances (because
///     <c>_nominatimRateLimiter</c> is <c>private readonly</c>).
///   </description></item>
/// </list>
/// </para>
/// <para>
/// <b>Accessor pattern.</b> <see cref="LocationSearchViewModel"/> exposes
/// the two dependencies via <c>internal</c> test-only accessors
/// (<see cref="LocationSearchViewModel.HttpClientForTesting"/> and
/// <see cref="LocationSearchViewModel.RateLimiterForTesting"/>). The
/// <c>InternalsVisibleTo</c> attribute on <c>AstroCast.csproj</c> grants
/// this test project access so the assertions can compare by reference
/// without resorting to reflection.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class SharedInfrastructureIdentityTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a <see cref="MainViewModel"/> using the shared stub set from
    /// <see cref="MainViewModelTests"/>. Identity tests do not exercise any
    /// service behaviour; the stubs exist solely to satisfy the
    /// <see cref="MainViewModel"/> constructor contract.
    /// </summary>
    private static MainViewModel CreateViewModel()
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        return new MainViewModel(
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
    }

    #endregion

    #region HttpClient Identity

    /// <summary>
    /// Every <see cref="LocationSearchViewModel"/> composed on a single
    /// <see cref="MainViewModel"/> must share one <see cref="HttpClient"/>
    /// instance. The four panels audited in the H-1 decomposition are
    /// ChartEntry, Electional, Mundane, and Relocation.
    /// </summary>
    [Fact]
    public void AllFourLocationSearches_ShareSameHttpClient_WithinOneMainViewModel()
    {
        MainViewModel vm = CreateViewModel();

        HttpClient chartEntryClient = vm.ChartEntryLocationSearch.HttpClientForTesting;
        HttpClient electionalClient = vm.Electional.LocationSearch.HttpClientForTesting;
        HttpClient mundaneClient = vm.Mundane.LocationSearch.HttpClientForTesting;
        HttpClient relocationClient = vm.Relocation.LocationSearch.HttpClientForTesting;

        electionalClient.Should().BeSameAs(chartEntryClient,
            "Electional and ChartEntry panels must share one HttpClient for the global Nominatim rate-limit contract");
        mundaneClient.Should().BeSameAs(chartEntryClient,
            "Mundane and ChartEntry panels must share one HttpClient for the global Nominatim rate-limit contract");
        relocationClient.Should().BeSameAs(chartEntryClient,
            "Relocation and ChartEntry panels must share one HttpClient for the global Nominatim rate-limit contract");
    }

    /// <summary>
    /// <c>s_httpClient</c> is declared <c>private static readonly</c>, so two
    /// distinct <see cref="MainViewModel"/> instances (e.g. main window and
    /// any future auxiliary window) must resolve to the same shared client.
    /// This test guards against an accidental promotion of
    /// <c>s_httpClient</c> to a per-instance field during a refactor.
    /// </summary>
    [Fact]
    public void HttpClient_IsStaticSingleton_AcrossMainViewModelInstances()
    {
        MainViewModel vmA = CreateViewModel();
        MainViewModel vmB = CreateViewModel();

        HttpClient clientA = vmA.ChartEntryLocationSearch.HttpClientForTesting;
        HttpClient clientB = vmB.ChartEntryLocationSearch.HttpClientForTesting;

        clientB.Should().BeSameAs(clientA,
            "s_httpClient is declared private static readonly; every MainViewModel instance must share the same HttpClient");
    }

    #endregion

    #region NominatimRateLimiter Identity

    /// <summary>
    /// Every <see cref="LocationSearchViewModel"/> composed on a single
    /// <see cref="MainViewModel"/> must share one
    /// <see cref="NominatimRateLimiter"/> instance so the 1-req/sec gate
    /// applies across all four panels uniformly.
    /// </summary>
    [Fact]
    public void AllFourLocationSearches_ShareSameRateLimiter_WithinOneMainViewModel()
    {
        MainViewModel vm = CreateViewModel();

        NominatimRateLimiter chartEntryLimiter =
            vm.ChartEntryLocationSearch.RateLimiterForTesting;
        NominatimRateLimiter electionalLimiter =
            vm.Electional.LocationSearch.RateLimiterForTesting;
        NominatimRateLimiter mundaneLimiter =
            vm.Mundane.LocationSearch.RateLimiterForTesting;
        NominatimRateLimiter relocationLimiter =
            vm.Relocation.LocationSearch.RateLimiterForTesting;

        electionalLimiter.Should().BeSameAs(chartEntryLimiter,
            "Electional and ChartEntry panels must share one NominatimRateLimiter so the 1-req/sec gate serialises across panels");
        mundaneLimiter.Should().BeSameAs(chartEntryLimiter,
            "Mundane and ChartEntry panels must share one NominatimRateLimiter so the 1-req/sec gate serialises across panels");
        relocationLimiter.Should().BeSameAs(chartEntryLimiter,
            "Relocation and ChartEntry panels must share one NominatimRateLimiter so the 1-req/sec gate serialises across panels");
    }

    /// <summary>
    /// <c>_nominatimRateLimiter</c> is an instance field on
    /// <see cref="MainViewModel"/>, not a static one. Two separate
    /// <see cref="MainViewModel"/> instances must therefore have distinct
    /// rate limiters. The global per-IP fairness guarantee is expected to be
    /// handled one layer up (a single <see cref="MainViewModel"/> instance
    /// per process); this test documents the current scope of sharing so
    /// future reviewers know the limit is window-local, not process-wide.
    /// </summary>
    [Fact]
    public void NominatimRateLimiter_IsPerInstance_AcrossMainViewModelInstances()
    {
        MainViewModel vmA = CreateViewModel();
        MainViewModel vmB = CreateViewModel();

        NominatimRateLimiter limiterA =
            vmA.ChartEntryLocationSearch.RateLimiterForTesting;
        NominatimRateLimiter limiterB =
            vmB.ChartEntryLocationSearch.RateLimiterForTesting;

        limiterB.Should().NotBeSameAs(limiterA,
            "_nominatimRateLimiter is a per-instance field; distinct MainViewModels must own distinct rate limiters");
    }

    #endregion
}
