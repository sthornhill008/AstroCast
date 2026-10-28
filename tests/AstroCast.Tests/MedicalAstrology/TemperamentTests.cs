using AstroCast.Core.MedicalAstrology;
using FluentAssertions;
using Xunit;

namespace AstroCast.Tests.MedicalAstrology;

/// <summary>
/// Tests for the <see cref="Temperament"/> enum and <see cref="TemperamentProfile"/>.
/// </summary>
public sealed class TemperamentTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Temperament_HasFourValues()
    {
        Enum.GetValues<Temperament>().Should().HaveCount(4);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TemperamentProfile_TotalPoints_SumsAllScores()
    {
        var profile = new TemperamentProfile
        {
            FireScore = 3, EarthScore = 2, AirScore = 4, WaterScore = 1,
            DominantTemperament = Temperament.Sanguine,
            SecondaryTemperament = Temperament.Choleric
        };
        profile.TotalPoints.Should().Be(10);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(Temperament.Sanguine, "Hot & Moist")]
    [InlineData(Temperament.Choleric, "Hot & Dry")]
    [InlineData(Temperament.Melancholic, "Cold & Dry")]
    [InlineData(Temperament.Phlegmatic, "Cold & Moist")]
    public void TemperamentProfile_Description_ContainsQualities(Temperament temperament, string expectedQualities)
    {
        var profile = new TemperamentProfile
        {
            FireScore = 1, EarthScore = 1, AirScore = 1, WaterScore = 1,
            DominantTemperament = temperament,
            SecondaryTemperament = temperament
        };
        profile.Description.Should().Contain(expectedQualities);
    }

    /// <summary>
    /// The dominant and secondary temperaments read from the four scores, a tie printed as a tie (Y5-1, ruling S-D9):
    /// every temperament at the highest score, then every one at the highest score below it, each in the order Fire
    /// (Choleric), Earth (Melancholic), Air (Sanguine), Water (Phlegmatic); one prints alone, two or more joined and
    /// marked "(tied, N each)", and an empty secondary prints "none". The secondary holds only temperaments that
    /// score: with no other element above 0 it is empty (Steve's ruling of 2026-10-12). The expected texts are
    /// counted by hand from the scores (Tier A), and the expected lists are the temperaments each text names, in its
    /// order. The two required members are set to one value in every case: the lists and the texts read the scores
    /// alone.
    /// </summary>
    /// <param name="fire">The Fire (Choleric) score.</param>
    /// <param name="earth">The Earth (Melancholic) score.</param>
    /// <param name="air">The Air (Sanguine) score.</param>
    /// <param name="water">The Water (Phlegmatic) score.</param>
    /// <param name="dominant">The expected dominant text.</param>
    /// <param name="secondary">The expected secondary text.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(2, 3, 5, 5, "Sanguine and Phlegmatic (tied, 5 each)", "Melancholic")]
    [InlineData(4, 4, 2, 7, "Phlegmatic", "Choleric and Melancholic (tied, 4 each)")]
    [InlineData(1, 8, 3, 4, "Melancholic", "Phlegmatic")]
    [InlineData(4, 4, 4, 1, "Choleric, Melancholic and Sanguine (tied, 4 each)", "Phlegmatic")]
    [InlineData(3, 3, 3, 3, "Choleric, Melancholic, Sanguine and Phlegmatic (tied, 3 each)", "none")]
    [InlineData(14, 0, 0, 0, "Choleric", "none")]
    public void Texts_FromScores(int fire, int earth, int air, int water, string dominant, string secondary)
    {
        var profile = new TemperamentProfile
        {
            FireScore = fire, EarthScore = earth, AirScore = air, WaterScore = water,
            DominantTemperament = Temperament.Choleric,
            SecondaryTemperament = Temperament.Choleric
        };

        profile.DominantText.Should().Be(dominant);
        profile.SecondaryText.Should().Be(secondary);
        profile.DominantTemperaments.Should().Equal(Named(dominant));
        profile.SecondaryTemperaments.Should().Equal(Named(secondary));
    }

    /// <summary>
    /// The temperaments a printed text names, in the order it names them: none for "none", else the names before any
    /// " (tied, N each)", separated by ", " and " and ".
    /// </summary>
    /// <param name="text">A dominant or secondary text.</param>
    /// <returns>The temperaments the text names.</returns>
    private static Temperament[] Named(string text)
    {
        if (text == "none")
            return [];

        int tied = text.IndexOf(" (tied", StringComparison.Ordinal);
        string names = tied < 0 ? text : text[..tied];
        return [.. names.Replace(" and ", ", ", StringComparison.Ordinal).Split(", ").Select(n => Enum.Parse<Temperament>(n))];
    }
}
