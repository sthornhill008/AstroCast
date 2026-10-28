using AstroCast.Core.Services;

using Serilog;

namespace AstroCast.Tests.Fixtures;

/// <summary>
/// Shared test fixture that initialises the Swiss Ephemeris once for all tests
/// in the "Ephemeris" collection. Implements <see cref="IDisposable"/> to call
/// <c>swe_close()</c> exactly once at the end of the entire test run, preventing
/// the test host crash caused by multiple <c>swe_close()</c> calls.
/// </summary>
public sealed class EphemerisFixture : IDisposable
{
    /// <summary>The shared ephemeris service instance.</summary>
    public EphemerisService EphemerisService { get; }

    /// <summary>
    /// Initialises the Swiss Ephemeris with the test ephemeris data path.
    /// </summary>
    public EphemerisFixture()
    {
        string ephePath = Path.Combine(AppContext.BaseDirectory, "ephe");
        ILogger logger = new LoggerConfiguration().CreateLogger();
        EphemerisService = new EphemerisService(ephePath, logger);
    }

    /// <summary>
    /// Releases Swiss Ephemeris resources exactly once.
    /// </summary>
    public void Dispose()
    {
        EphemerisService.Dispose();
    }
}
