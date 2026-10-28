using System.Globalization;

using FluentAssertions;

namespace AstroCast.Tests.Fixtures;

/// <summary>
/// Tests for <see cref="CultureScope"/>: it sets and restores the culture of the
/// calling flow only, so culture-independence tests can run beside every other
/// test under xUnit's parallel execution.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CultureScopeTests
{
    /// <summary>The scope sets the named culture and UI culture, and disposing it restores both.</summary>
    [Fact]
    public void Scope_Should_SetTheNamedCulture_AndRestoreThePreviousOne()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        CultureInfo beforeUi = CultureInfo.CurrentUICulture;
        string? activeBefore = CultureScope.ActiveCulture;

        using (new CultureScope("th-TH"))
        {
            CultureInfo.CurrentCulture.Name.Should().Be("th-TH");
            CultureInfo.CurrentUICulture.Name.Should().Be("th-TH");
            CultureScope.ActiveCulture.Should().Be("th-TH");
        }

        CultureInfo.CurrentCulture.Should().BeSameAs(before);
        CultureInfo.CurrentUICulture.Should().BeSameAs(beforeUi);
        CultureScope.ActiveCulture.Should().Be(activeBefore);
    }

    /// <summary>Disposing an inner scope returns to the outer scope, not to the culture before both.</summary>
    [Fact]
    public void Scope_Should_RestoreTheOuterScope_When_Nested()
    {
        using (new CultureScope("de-DE"))
        {
            using (new CultureScope("sv-SE"))
            {
                CultureInfo.CurrentCulture.Name.Should().Be("sv-SE");
            }

            CultureInfo.CurrentCulture.Name.Should().Be("de-DE");
            CultureScope.ActiveCulture.Should().Be("de-DE");
        }
    }

    /// <summary>
    /// A thread running beside the scope keeps its own culture: the scope is safe under
    /// xUnit's parallel execution, which a process-wide default would not be.
    /// </summary>
    [Fact]
    public void Scope_Should_NotChangeTheCulture_OfAThreadRunningAtTheSameTime()
    {
        string outside = CultureInfo.CurrentCulture.Name;
        string? seen = null;
        string? seenActive = "unset";
        using ManualResetEventSlim scopeEntered = new();
        using ManualResetEventSlim observed = new();

        // Started before the scope exists, as a test on another xUnit worker would be.
        Thread other = new(() =>
        {
            scopeEntered.Wait(TimeSpan.FromSeconds(30));
            seen = CultureInfo.CurrentCulture.Name;
            seenActive = CultureScope.ActiveCulture;
            observed.Set();
        });
        other.Start();

        using (new CultureScope("th-TH"))
        {
            scopeEntered.Set();
            observed.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue();
            CultureInfo.CurrentCulture.Name.Should().Be("th-TH");
        }

        other.Join();
        seen.Should().Be(outside, "a scope changes only its own flow, never a process-wide default");
        seenActive.Should().BeNull();
    }

    /// <summary>Work the test starts inside the scope runs under the scope's culture.</summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task Scope_Should_FlowIntoWorkTheTestStarts()
    {
        using CultureScope scope = new("de-DE");

        string inTask = await Task.Run(() => CultureInfo.CurrentCulture.Name);

        inTask.Should().Be("de-DE");
    }

    /// <summary>
    /// Each of the three cultures changes the text it is chosen for on this platform, so a
    /// culture-independence test that passes is not passing because every culture is invariant.
    /// </summary>
    [Fact]
    public void OtherCultures_Should_EachChangeTheTextTheyAreChosenFor()
    {
        // Guards against a vacuous culture run: under InvariantGlobalization, or with
        // missing culture data, every culture formats as the invariant one does.
        using (new CultureScope("de-DE"))
            1.5.ToString().Should().Be("1,5");

        using (new CultureScope("sv-SE"))
            (-5).ToString().Should().Be("−5");

        using (new CultureScope("th-TH"))
            new DateTime(2026, 1, 1).ToString("yyyy").Should().Be("2569");

        CultureScope.OtherCultures.Cast<object[]>().Select(row => (string)row[0])
            .Should().Equal("de-DE", "sv-SE", "th-TH");
    }
}
