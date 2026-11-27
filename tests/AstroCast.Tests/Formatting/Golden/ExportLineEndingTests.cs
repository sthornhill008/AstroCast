using System.Reflection;
using System.Runtime.ExceptionServices;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting.Golden;

/// <summary>
/// An export carries the system's own line ending throughout, CR LF on Windows and LF on macOS (branch
/// ExportText, 1.7.2; finding A3 of the 1.7.2 code audit, Steve's ruling). Until 1.7.2 the ten formatters that
/// embed a chart joined its lines with a bare line feed, so on Windows a solar return held 166 CR LF endings
/// and 359 bare LF.
/// </summary>
/// <remarks>
/// The golden comparison cannot see this: it normalises line endings. So this test makes every kind of export
/// the golden tests make, by running each of their facts inside a
/// <see cref="GoldenFormatterTestBase.CollectCandidates"/> scope, and reads each export as its formatter
/// returned it.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ExportLineEndingTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public ExportLineEndingTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Every export the golden tests make holds one kind of line ending, <see cref="Environment.NewLine"/>:
    /// with those taken out, no carriage return and no line feed is left.
    /// </summary>
    [Fact]
    public void EveryExport_HoldsOnlyTheSystemsLineEnding()
    {
        List<(string GoldenFileName, string Text)> exports = MakeEveryExport();

        exports.Count.Should().BeGreaterThanOrEqualTo(40, "the golden tests make every kind of export");
        exports.Select(e => e.GoldenFileName).Should().Contain(
        [
            "ChartMarkdownFormatter_ToMarkdown_J2000NYC.golden.md",
            "SolarReturnMarkdownFormatter_J2000NYC_2026.golden.md",
            "LunarReturnMarkdownFormatter_J2000NYC_2026April.golden.md",
            "PlanetaryReturnMarkdownFormatter_J2000NYC_Saturn1.golden.md",
            "TransitMarkdownFormatter_J2000NYC_20260415.golden.md",
            "TransitTimelineMarkdownFormatter_J2000NYC_2026April.golden.md",
            "SynastryMarkdownFormatter_J2000_Secondary.golden.md",
            "CompositeMarkdownFormatter_J2000_Secondary.golden.md",
            "DavisonMarkdownFormatter_J2000_Secondary.golden.md",
            "MedicalMarkdownFormatter_J2000NYC.golden.md",
            "PrimaryDirectionMarkdownFormatter_J2000NYC.golden.md",
        ]);

        List<string> mixed = [];
        foreach ((string goldenFileName, string text) in exports)
        {
            string rest = text.Replace(Environment.NewLine, string.Empty, StringComparison.Ordinal);
            int bareLineFeeds = rest.Count(c => c == '\n');
            int bareCarriageReturns = rest.Count(c => c == '\r');
            if (bareLineFeeds + bareCarriageReturns > 0)
                mixed.Add($"{goldenFileName}: {bareLineFeeds} bare LF, {bareCarriageReturns} bare CR");
        }

        // joined, so a failure names every export and not the first alone
        string.Join("; ", mixed.Distinct()).Should().BeEmpty(
            "an export carries the system's own line ending throughout, CR LF on Windows and LF on macOS");
    }

    /// <summary>
    /// Runs every fact of every golden test class inside a collecting scope and returns the exports they
    /// handed to the golden comparison, each as its formatter returned it. The culture theories are left out:
    /// each runs a fact again under another culture.
    /// </summary>
    /// <returns>The exports, each with its golden file's name.</returns>
    private List<(string GoldenFileName, string Text)> MakeEveryExport()
    {
        List<(string GoldenFileName, string Text)> exports = [];

        using (GoldenFormatterTestBase.CollectCandidates(exports))
        {
            IEnumerable<Type> goldenTestClasses = typeof(GoldenFormatterTestBase).Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(GoldenFormatterTestBase)) && !t.IsAbstract)
                .OrderBy(t => t.FullName, StringComparer.Ordinal);

            foreach (Type type in goldenTestClasses)
            {
                ConstructorInfo constructor = type.GetConstructors().Single();
                object instance = constructor.GetParameters().Length == 0
                    ? constructor.Invoke(null)
                    : constructor.Invoke([_fixture]);

                IEnumerable<MethodInfo> facts = type
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttribute<FactAttribute>() is not null
                        && m.GetCustomAttribute<TheoryAttribute>() is null
                        && m.GetParameters().Length == 0)
                    .OrderBy(m => m.Name, StringComparer.Ordinal);

                foreach (MethodInfo fact in facts)
                {
                    try
                    {
                        object? returned = fact.Invoke(instance, null);
                        returned.Should().BeNull("the golden facts are synchronous, so each has made its export on return ({0})", fact.Name);
                    }
                    catch (TargetInvocationException ex) when (ex.InnerException is not null)
                    {
                        ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                    }
                }
            }
        }

        return exports;
    }
}
