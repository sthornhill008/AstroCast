using System.Globalization;

namespace AstroCast.Tests.Fixtures;

/// <summary>
/// Runs the code inside a <c>using</c> block under a named culture, then restores
/// the culture that was current before it. Used to prove that Core's exports and
/// result strings do not depend on the machine's regional settings.
/// </summary>
/// <remarks>
/// <para>
/// <b>Safe under xUnit's parallel execution.</b> The scope sets
/// <see cref="CultureInfo.CurrentCulture"/> and
/// <see cref="CultureInfo.CurrentUICulture"/>, which .NET keeps per asynchronous
/// flow, so the change is seen only by the test that made it and by work that
/// test starts. It never touches <see cref="CultureInfo.DefaultThreadCurrentCulture"/>
/// or any other process-wide setting, so a test running at the same time on
/// another thread keeps its own culture.
/// </para>
/// <para>
/// <b>The three cultures</b> in <see cref="OtherCultures"/> each break a
/// different assumption: de-DE writes a decimal comma and German day and month
/// names; sv-SE writes a decimal comma and U+2212 as the negative sign, so a
/// negative integer changes too; th-TH counts years in the Buddhist calendar, so
/// every <c>yyyy</c> pattern changes (2026 is 2569).
/// </para>
/// </remarks>
public sealed class CultureScope : IDisposable
{
    private static readonly AsyncLocal<string?> s_active = new();

    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;
    private readonly string? _previousActive;
    private bool _disposed;

    /// <summary>
    /// Makes <paramref name="cultureName"/> the current culture and UI culture of
    /// the calling flow until the scope is disposed.
    /// </summary>
    /// <param name="cultureName">A culture name such as <c>"de-DE"</c>.</param>
    /// <exception cref="CultureNotFoundException">
    /// Thrown when <paramref name="cultureName"/> names no culture the platform knows.
    /// </exception>
    public CultureScope(string cultureName)
    {
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);

        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        _previousActive = s_active.Value;

        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        s_active.Value = culture.Name;
    }

    /// <summary>
    /// The culture of the innermost scope active on the calling flow, or
    /// <see langword="null"/> when no scope is active there.
    /// </summary>
    public static string? ActiveCulture => s_active.Value;

    /// <summary>
    /// The cultures every culture-independence test runs under: de-DE, sv-SE and th-TH.
    /// </summary>
    public static TheoryData<string> OtherCultures => new() { "de-DE", "sv-SE", "th-TH" };

    /// <summary>
    /// Restores the culture, UI culture and active scope that were current when
    /// this scope was created.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        CultureInfo.CurrentCulture = _previousCulture;
        CultureInfo.CurrentUICulture = _previousUiCulture;
        s_active.Value = _previousActive;
        _disposed = true;
    }
}
