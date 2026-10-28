using AstroCast.Core.Models;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="MoonMansionService"/> verifying correct mansion
/// identification across the ecliptic and at boundary conditions.
/// </summary>
public sealed class MoonMansionServiceTests
{
    private readonly MoonMansionService _service = new();

    #region Catalog Validation

    [Fact]
    public void Catalog_Has28Mansions()
    {
        MoonMansionInfo.Catalog.Should().HaveCount(28);
    }

    [Fact]
    public void Catalog_NumbersAre1Through28()
    {
        IReadOnlyList<MoonMansionInfo> catalog = MoonMansionInfo.Catalog;
        for (int i = 0; i < 28; i++)
        {
            catalog[i].Number.Should().Be(i + 1);
        }
    }

    [Fact]
    public void Catalog_AllMansionsHaveNonEmptyNames()
    {
        foreach (MoonMansionInfo mansion in MoonMansionInfo.Catalog)
        {
            mansion.Name.Should().NotBeNullOrWhiteSpace(
                $"Mansion {mansion.Number} should have a name");
            mansion.Meaning.Should().NotBeNullOrWhiteSpace(
                $"Mansion {mansion.Number} should have a meaning");
            mansion.Keywords.Should().NotBeNullOrWhiteSpace(
                $"Mansion {mansion.Number} should have keywords");
        }
    }

    [Fact]
    public void Catalog_AllMansionsHaveValidNature()
    {
        string[] validNatures = ["Benefic", "Malefic", "Neutral"];
        foreach (MoonMansionInfo mansion in MoonMansionInfo.Catalog)
        {
            validNatures.Should().Contain(mansion.Nature,
                $"Mansion {mansion.Number} ({mansion.Name}) should have a valid nature");
        }
    }

    [Fact]
    public void Catalog_BeneficMansionsAre3_7_14_24()
    {
        int[] expected = [3, 7, 14, 24];
        IEnumerable<int> actual = MoonMansionInfo.Catalog
            .Where(m => m.IsBenefic)
            .Select(m => m.Number);
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Catalog_MaleficMansionsAre4_5_10_12_16_18_19()
    {
        int[] expected = [4, 5, 10, 12, 16, 18, 19];
        IEnumerable<int> actual = MoonMansionInfo.Catalog
            .Where(m => m.IsMalefic)
            .Select(m => m.Number);
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Catalog_StartAndEndLongitudesAreContinuous()
    {
        IReadOnlyList<MoonMansionInfo> catalog = MoonMansionInfo.Catalog;
        for (int i = 0; i < 27; i++)
        {
            catalog[i].EndLongitude.Should().BeApproximately(
                catalog[i + 1].StartLongitude, 0.0001,
                $"End of mansion {i + 1} should match start of mansion {i + 2}");
        }

        catalog[0].StartLongitude.Should().Be(0.0);
        catalog[27].EndLongitude.Should().BeApproximately(360.0, 0.0001);
    }

    #endregion

    #region Mansion Identification

    [Theory]
    [InlineData(0.0, 1, "Al-Sharatain")]
    [InlineData(6.0, 1, "Al-Sharatain")]
    [InlineData(12.86, 2, "Al-Butain")]
    [InlineData(25.72, 3, "Al-Thurayya")]
    [InlineData(90.0, 8, "Al-Nathrah")]
    [InlineData(180.0, 15, "Al-Ghafr")]
    [InlineData(270.0, 22, "Sa'd al-Dhabih")]
    public void GetMansion_ReturnsCorrectMansion(double longitude, int expectedNumber, string expectedName)
    {
        MoonMansionInfo result = _service.GetMansion(longitude);

        result.Number.Should().Be(expectedNumber);
        result.Name.Should().Be(expectedName);
    }

    [Fact]
    public void GetMansion_JustPastMansionWidth_ReturnsMansion2()
    {
        // 360/28 = 12.857142... → just past boundary should be mansion 2
        double mansionWidth = 360.0 / 28.0;
        MoonMansionInfo result = _service.GetMansion(mansionWidth + 0.001);

        result.Number.Should().Be(2);
    }

    [Fact]
    public void GetMansion_JustBeforeMansionBoundary_ReturnsCurrentMansion()
    {
        double mansionWidth = 360.0 / 28.0;
        double justBefore = mansionWidth - 0.0001;
        MoonMansionInfo result = _service.GetMansion(justBefore);

        result.Number.Should().Be(1);
    }

    [Fact]
    public void GetMansion_LastMansion_ReturnsMansion28()
    {
        // 27 * (360/28) = 347.142... → start of mansion 28
        double mansion28Start = 27.0 * (360.0 / 28.0);
        MoonMansionInfo result = _service.GetMansion(mansion28Start + 5.0);

        result.Number.Should().Be(28);
        result.Name.Should().Be("Batn al-Hut");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GetMansion_At360_ReturnsMansion1()
    {
        MoonMansionInfo result = _service.GetMansion(360.0);

        result.Number.Should().Be(1);
    }

    [Fact]
    public void GetMansion_NegativeLongitude_NormalisesCorrectly()
    {
        // -10 degrees → 350 degrees → should be mansion 28
        MoonMansionInfo result = _service.GetMansion(-10.0);

        result.Number.Should().Be(28);
    }

    [Fact]
    public void GetMansion_LargeLongitude_NormalisesCorrectly()
    {
        // 370 degrees → 10 degrees → mansion 1
        MoonMansionInfo result = _service.GetMansion(370.0);

        result.Number.Should().Be(1);
    }

    [Fact]
    public void GetMansion_Zero_ReturnsMansion1()
    {
        MoonMansionInfo result = _service.GetMansion(0.0);

        result.Number.Should().Be(1);
        result.Name.Should().Be("Al-Sharatain");
    }

    [Fact]
    public void GetMansion_359Point9_ReturnsMansion28()
    {
        MoonMansionInfo result = _service.GetMansion(359.9);

        result.Number.Should().Be(28);
    }

    #endregion

    #region Nature Properties

    [Fact]
    public void IsBenefic_TrueForBeneficMansion()
    {
        // Mansion 3 (Al-Thurayya) is Benefic
        MoonMansionInfo result = _service.GetMansion(26.0);

        result.IsBenefic.Should().BeTrue();
        result.IsMalefic.Should().BeFalse();
    }

    [Fact]
    public void IsMalefic_TrueForMaleficMansion()
    {
        // Mansion 4 (Al-Dabaran) is Malefic: starts at 3 * (360/28) ≈ 38.57
        MoonMansionInfo result = _service.GetMansion(40.0);

        result.IsMalefic.Should().BeTrue();
        result.IsBenefic.Should().BeFalse();
    }

    [Fact]
    public void NeutralMansion_IsBeneficAndIsMaleficBothFalse()
    {
        // Mansion 1 (Al-Sharatain) is Neutral
        MoonMansionInfo result = _service.GetMansion(5.0);

        result.IsBenefic.Should().BeFalse();
        result.IsMalefic.Should().BeFalse();
        result.Nature.Should().Be("Neutral");
    }

    #endregion

    #region All 28 Mansions Coverage

    [Fact]
    public void GetMansion_AllMansionsReachable()
    {
        double mansionWidth = 360.0 / 28.0;
        HashSet<int> found = [];

        for (int i = 0; i < 28; i++)
        {
            double longitude = (i * mansionWidth) + (mansionWidth / 2.0);
            MoonMansionInfo result = _service.GetMansion(longitude);
            found.Add(result.Number);
        }

        found.Should().HaveCount(28);
        found.Should().BeEquivalentTo(Enumerable.Range(1, 28));
    }

    #endregion
}
