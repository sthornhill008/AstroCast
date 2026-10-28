using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Unit tests for natal harmony aspect filtering in
/// <see cref="ElectionalMarkdownFormatter.FilterNatalHarmonyAspects"/>
/// and the <see cref="AspectTypeExtensions.IsMajor"/> helper.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ElectionalNatalHarmonyFilterTests
{
    #region Helper — Build InterChartAspect stubs

    /// <summary>
    /// Creates a minimal <see cref="InterChartAspect"/> stub with the given
    /// aspect type and orb, suitable for filter unit tests.
    /// </summary>
    /// <param name="type">The aspect type.</param>
    /// <param name="orb">The orb in degrees.</param>
    /// <returns>A stub InterChartAspect.</returns>
    private static InterChartAspect MakeAspect(AspectType type, double orb)
    {
        AspectDefinition definition = AspectDefinition.All.First(d => d.Type == type);

        return new InterChartAspect
        {
            Body1Name = "E Sun",
            Body1Longitude = 15.0,
            Body2Name = "N Moon",
            Body2Longitude = 15.0 + definition.Angle + orb,
            Aspect = definition,
            Orb = orb,
        };
    }

    #endregion

    #region Test 1: IsMajor Correctly Classifies All Aspect Types

    /// <summary>
    /// Verifies that <see cref="AspectTypeExtensions.IsMajor"/> returns true
    /// for the five major Ptolemaic aspects and false for the four Ebertin aspects.
    /// </summary>
    [Theory]
    [InlineData(AspectType.Conjunction, true)]
    [InlineData(AspectType.Opposition, true)]
    [InlineData(AspectType.Trine, true)]
    [InlineData(AspectType.Square, true)]
    [InlineData(AspectType.Sextile, true)]
    [InlineData(AspectType.SemiSquare, false)]
    [InlineData(AspectType.Sesquiquadrate, false)]
    [InlineData(AspectType.SemiSextile, false)]
    [InlineData(AspectType.Quincunx, false)]
    public void IsMajor_Should_ClassifyCorrectly(AspectType type, bool expected)
    {
        // Act
        bool result = type.IsMajor();

        // Assert
        result.Should().Be(expected,
            $"{type} should be {(expected ? "major" : "minor/Ebertin")}");
    }

    #endregion

    #region Test 2: Minor Aspects Excluded

    /// <summary>
    /// Verifies that minor aspects (sesquiquadrate, quincunx, semi-sextile)
    /// are excluded from the filtered list.
    /// </summary>
    [Fact]
    public void FilterNatalHarmonyAspects_Should_ExcludeMinorAspects()
    {
        // Arrange
        List<InterChartAspect> aspects =
        [
            MakeAspect(AspectType.Conjunction, 1.0),
            MakeAspect(AspectType.Sesquiquadrate, 0.5),
            MakeAspect(AspectType.Quincunx, 0.8),
            MakeAspect(AspectType.SemiSextile, 0.3),
            MakeAspect(AspectType.SemiSquare, 0.7),
            MakeAspect(AspectType.Trine, 2.0),
        ];

        // Act
        List<InterChartAspect> result = ElectionalMarkdownFormatter.FilterNatalHarmonyAspects(aspects);

        // Assert
        result.Should().HaveCount(2, "only conjunction and trine are major");
        result.Should().OnlyContain(a => a.Aspect.Type.IsMajor(),
            "all returned aspects should be major");
    }

    #endregion

    #region Test 3: Major Aspects Retained

    /// <summary>
    /// Verifies that all five major aspect types are retained when present.
    /// </summary>
    [Fact]
    public void FilterNatalHarmonyAspects_Should_RetainAllMajorAspects()
    {
        // Arrange
        List<InterChartAspect> aspects =
        [
            MakeAspect(AspectType.Conjunction, 1.0),
            MakeAspect(AspectType.Opposition, 2.0),
            MakeAspect(AspectType.Trine, 3.0),
            MakeAspect(AspectType.Square, 4.0),
            MakeAspect(AspectType.Sextile, 1.5),
        ];

        // Act
        List<InterChartAspect> result = ElectionalMarkdownFormatter.FilterNatalHarmonyAspects(aspects);

        // Assert
        result.Should().HaveCount(5, "all 5 major aspects should be retained");
    }

    #endregion

    #region Test 4: Cap at 15 by Tightest Orb

    /// <summary>
    /// Verifies that when more than 15 major aspects exist, only the 15
    /// with the tightest orbs are kept.
    /// </summary>
    [Fact]
    public void FilterNatalHarmonyAspects_MoreThan15_Should_CapAt15ByTightestOrb()
    {
        // Arrange — 20 major aspects with orbs 0.5, 1.0, 1.5, ..., 10.0
        List<InterChartAspect> aspects = [];
        AspectType[] majorTypes =
            [AspectType.Conjunction, AspectType.Opposition, AspectType.Trine, AspectType.Square, AspectType.Sextile];

        for (int i = 0; i < 20; i++)
        {
            double orb = 0.5 + (i * 0.5);
            AspectType type = majorTypes[i % majorTypes.Length];
            aspects.Add(MakeAspect(type, orb));
        }

        // Act
        List<InterChartAspect> result = ElectionalMarkdownFormatter.FilterNatalHarmonyAspects(aspects);

        // Assert
        result.Should().HaveCount(15, "should cap at 15 aspects");
        result.Should().BeInAscendingOrder(a => a.Orb,
            "should be sorted by tightest orb first");
        result.Last().Orb.Should().BeLessThanOrEqualTo(8.0,
            "the 15th tightest orb should be <= 8.0 (orbs are 0.5 to 10.0, top 15 = 0.5..7.5)");
    }

    #endregion

    #region Test 5: Empty Input

    /// <summary>
    /// Verifies that an empty aspect list returns an empty result.
    /// </summary>
    [Fact]
    public void FilterNatalHarmonyAspects_EmptyList_Should_ReturnEmpty()
    {
        // Act
        List<InterChartAspect> result =
            ElectionalMarkdownFormatter.FilterNatalHarmonyAspects([]);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Test 6: All Minor Aspects Returns Empty

    /// <summary>
    /// Verifies that a list containing only minor aspects returns empty.
    /// </summary>
    [Fact]
    public void FilterNatalHarmonyAspects_AllMinor_Should_ReturnEmpty()
    {
        // Arrange
        List<InterChartAspect> aspects =
        [
            MakeAspect(AspectType.SemiSquare, 0.5),
            MakeAspect(AspectType.Sesquiquadrate, 1.0),
            MakeAspect(AspectType.SemiSextile, 0.3),
            MakeAspect(AspectType.Quincunx, 0.8),
        ];

        // Act
        List<InterChartAspect> result = ElectionalMarkdownFormatter.FilterNatalHarmonyAspects(aspects);

        // Assert
        result.Should().BeEmpty("no major aspects exist in the input");
    }

    #endregion

    #region Test 7: Sorted by Orb Ascending

    /// <summary>
    /// Verifies that the filtered result is sorted by orb ascending (tightest first).
    /// </summary>
    [Fact]
    public void FilterNatalHarmonyAspects_Should_SortByOrbAscending()
    {
        // Arrange — add major aspects with descending orbs
        List<InterChartAspect> aspects =
        [
            MakeAspect(AspectType.Square, 5.0),
            MakeAspect(AspectType.Conjunction, 0.2),
            MakeAspect(AspectType.Trine, 3.5),
            MakeAspect(AspectType.Opposition, 1.0),
        ];

        // Act
        List<InterChartAspect> result = ElectionalMarkdownFormatter.FilterNatalHarmonyAspects(aspects);

        // Assert
        result.Should().HaveCount(4);
        result.Should().BeInAscendingOrder(a => a.Orb, "result should be sorted by orb ascending");
    }

    #endregion
}
