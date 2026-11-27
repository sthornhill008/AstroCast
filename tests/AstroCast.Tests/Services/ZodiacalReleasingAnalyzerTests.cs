using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="ZodiacalReleasingAnalyzer"/> as Valens has it (T-D10, T6-4): Level 1 periods of
/// 360-day years from the birth day, Level 2 sub-periods of each sign's years as 30-day months with the
/// loosing of the bond after one round of 211 months, the last clamped to the Level 1 end, angularity from
/// the Lot of Fortune's sign, the refusal of a reference before the birth day (T6-6), graceful handling of
/// a missing Lot of Spirit, and summaries. The real-chart values are Tier A, calendar arithmetic
/// (h_truth.out).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ZodiacalReleasingAnalyzerTests
{
    #region Level 1 — Starting Sign

    [Fact]
    public void Analyze_SpiritInAries_FirstL1IsAries15Years()
    {
        // Lot of Spirit at 10° Aries (longitude 10)
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.CurrentL1Sign.Should().Be("Aries");
        result.CurrentL1Years.Should().Be(15);
    }

    [Fact]
    public void Analyze_SpiritInPisces_FirstL1IsPisces12Years()
    {
        // Lot of Spirit at 340° (Pisces)
        ChartResult chart = CreateChart(spiritLongitude: 340.0, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.CurrentL1Sign.Should().Be("Pisces");
        result.CurrentL1Years.Should().Be(12);
    }

    [Fact]
    public void Analyze_L1AdvancesInZodiacOrder()
    {
        // Spirit in Aries → first period Aries(15), second Taurus(8), third Gemini(20)
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));

        result.Level1Periods.Should().HaveCountGreaterThan(2);
        result.Level1Periods[0].Sign.Should().Be("Aries");
        result.Level1Periods[1].Sign.Should().Be("Taurus");
        result.Level1Periods[2].Sign.Should().Be("Gemini");
    }

    /// <summary>
    /// A Level 1 period runs its sign's years of 360 days from the birth day (Valens): Aries's 15 years are
    /// 5,400 days, 1980-01-01 to 1994-10-14. Until 1.3.4 a year was 365.25 days (T-D10).
    /// </summary>
    [Fact]
    public void Analyze_L1PeriodsRun360DayYears()
    {
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));

        result.Level1Periods[0].StartDate.Should().Be(new DateTime(1980, 1, 1));
        result.Level1Periods[0].EndDate.Should().Be(new DateTime(1994, 10, 14));
        result.Level1Periods[1].EndDate.Should().Be(new DateTime(1994, 10, 14).AddDays(8 * 360));
        result.Level1Periods[2].EndDate.Should().Be(new DateTime(1994, 10, 14).AddDays((8 + 20) * 360));
    }

    /// <summary>A reference date before the birth day is refused (T6-6).</summary>
    [Fact]
    public void Analyze_ReferenceBeforeTheBirthDay_IsRefused()
    {
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 0.0);
        Action act = () => ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1979, 12, 31));

        ArgumentOutOfRangeException refusal = act.Should().Throw<ArgumentOutOfRangeException>().Which;
        refusal.ParamName.Should().Be("referenceDate");
        refusal.Message.Should().StartWith("The reference date 1979-12-31 is before the birth day 1980-01-01.");
    }

    #endregion

    #region Peak Detection (Angularity to Fortune)

    [Fact]
    public void Analyze_FortuneInAries_PeriodInAries_IsAngularPeak()
    {
        // Spirit and Fortune both in Aries → first period is Angular
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 10.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.CurrentL1Angularity.Should().Be("Angular");
        result.CurrentL1IsPeak.Should().BeTrue();
    }

    [Fact]
    public void Analyze_FortuneInAries_PeriodInCancer_IsAngularSquare()
    {
        // Fortune in Aries, Spirit in Cancer → Cancer is 3 signs from Aries = Angular (square)
        ChartResult chart = CreateChart(spiritLongitude: 100.0, fortuneLongitude: 10.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.CurrentL1Sign.Should().Be("Cancer");
        result.CurrentL1Angularity.Should().Be("Angular");
        result.CurrentL1IsPeak.Should().BeTrue();
    }

    [Fact]
    public void Analyze_FortuneInAries_PeriodInTaurus_IsSuccedent()
    {
        // Fortune in Aries, period in Taurus → 1 sign apart = Succedent
        ChartResult chart = CreateChart(spiritLongitude: 40.0, fortuneLongitude: 10.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.CurrentL1Sign.Should().Be("Taurus");
        result.CurrentL1Angularity.Should().Be("Succedent");
        result.CurrentL1IsPeak.Should().BeFalse();
    }

    [Fact]
    public void Analyze_FortuneInAries_PeriodInGemini_IsCadent()
    {
        // Fortune in Aries, period in Gemini → 2 signs apart = Cadent
        ChartResult chart = CreateChart(spiritLongitude: 70.0, fortuneLongitude: 10.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.CurrentL1Sign.Should().Be("Gemini");
        result.CurrentL1Angularity.Should().Be("Cadent");
        result.CurrentL1IsPeak.Should().BeFalse();
    }

    /// <summary>
    /// From Fortune's sign the 1st, 4th, 7th and 10th places are angular, the 2nd, 5th, 8th and 11th
    /// succedent and the 3rd, 6th, 9th and 12th cadent (T6-4). Until 1.3.4 the 5th and 11th were called
    /// cadent and the 6th and 12th succedent.
    /// </summary>
    [Theory]
    [InlineData(ZodiacSign.Aries, ZodiacSign.Aries, "Angular")]
    [InlineData(ZodiacSign.Cancer, ZodiacSign.Aries, "Angular")]   // Square
    [InlineData(ZodiacSign.Libra, ZodiacSign.Aries, "Angular")]    // Opposition
    [InlineData(ZodiacSign.Capricorn, ZodiacSign.Aries, "Angular")] // Square
    [InlineData(ZodiacSign.Taurus, ZodiacSign.Aries, "Succedent")]
    [InlineData(ZodiacSign.Leo, ZodiacSign.Aries, "Succedent")]    // the 5th place
    [InlineData(ZodiacSign.Scorpio, ZodiacSign.Aries, "Succedent")] // the 8th
    [InlineData(ZodiacSign.Aquarius, ZodiacSign.Aries, "Succedent")] // the 11th
    [InlineData(ZodiacSign.Gemini, ZodiacSign.Aries, "Cadent")]
    [InlineData(ZodiacSign.Virgo, ZodiacSign.Aries, "Cadent")]     // the 6th place
    [InlineData(ZodiacSign.Sagittarius, ZodiacSign.Aries, "Cadent")] // the 9th
    [InlineData(ZodiacSign.Pisces, ZodiacSign.Aries, "Cadent")]    // the 12th
    public void GetAngularity_ReturnsCorrectCategory(
        ZodiacSign periodSign, ZodiacSign fortuneSign, string expected)
    {
        string result = ZodiacalReleasingAnalyzer.GetAngularity(periodSign, fortuneSign);
        result.Should().Be(expected);
    }

    #endregion

    #region Level 2 Sub-Periods

    [Fact]
    public void Analyze_L2SubPeriodsStartFromL1Sign()
    {
        // Spirit in Aries → first L1 is Aries → first L2 should also be Aries
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1980, 6, 1));
        result.CurrentL2Periods.Should().NotBeEmpty();
        result.CurrentL2Periods[0].Sign.Should().Be("Aries");
    }

    [Fact]
    public void Analyze_L2DurationsTotalEqualsL1Duration()
    {
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1985, 6, 1));

        if (result.CurrentL2Periods.Count == 0)
            return;

        DateTime l2First = result.CurrentL2Periods[0].StartDate;
        DateTime l2Last = result.CurrentL2Periods[^1].EndDate;
        double l2TotalDays = (l2Last - l2First).TotalDays;
        double l1TotalDays = (result.CurrentL1End - result.CurrentL1Start).TotalDays;

        l2TotalDays.Should().BeApproximately(l1TotalDays, 1.0); // Within 1 day tolerance
    }

    /// <summary>
    /// Within a Level 1 period each sign in zodiacal order from the Level 1 sign runs its years as 30-day
    /// months, the last clamped to the Level 1 end (Valens): in Aries's 180 months, Aries 15 months
    /// (1980-01-01 to 1981-03-26), Taurus 8, Gemini 20, Cancer 25 (to 1985-08-02) ... Capricorn 27, then
    /// Aquarius's 30 cut to the 11 that remain. A row's Years is its months over 12. Until 1.3.4 the Level 1
    /// span was shared out in proportion to the years (T-D10).
    /// </summary>
    [Fact]
    public void Analyze_L2SubPeriodsRunTheirSignsYearsAs30DayMonths()
    {
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1985, 6, 1));

        result.CurrentL1Sign.Should().Be("Aries");
        result.CurrentL2Periods.Select(p => (p.Sign, (p.EndDate - p.StartDate).TotalDays / 30.0)).Should().Equal(
            ("Aries", 15.0), ("Taurus", 8.0), ("Gemini", 20.0), ("Cancer", 25.0), ("Leo", 19.0), ("Virgo", 20.0),
            ("Libra", 8.0), ("Scorpio", 15.0), ("Sagittarius", 12.0), ("Capricorn", 27.0), ("Aquarius", 11.0));
        result.CurrentL2Periods.Select(p => (p.StartDate, p.EndDate)).Take(4).Should().Equal(
            (new DateTime(1980, 1, 1), new DateTime(1981, 3, 26)),
            (new DateTime(1981, 3, 26), new DateTime(1981, 11, 21)),
            (new DateTime(1981, 11, 21), new DateTime(1983, 7, 14)),
            (new DateTime(1983, 7, 14), new DateTime(1985, 8, 2)));
        result.CurrentL2Periods.Select(p => p.Years).Should().Equal(
            15 / 12.0, 8 / 12.0, 20 / 12.0, 25 / 12.0, 19 / 12.0, 20 / 12.0,
            8 / 12.0, 15 / 12.0, 12 / 12.0, 27 / 12.0, 11 / 12.0);
        result.CurrentL2Periods[^1].EndDate.Should().Be(result.CurrentL1End);
        result.CurrentL2Sign.Should().Be("Cancer");
    }

    /// <summary>
    /// After one round of the twelve signs, 211 months, the sequence passes to the sign opposite the Level 1
    /// sign, the loosing of the bond (Valens): in Aquarius's 360 months the round ends with Capricorn on the
    /// 6,330th day, 1997-05-01, Leo follows, and Aries's 15 months are cut to the 6 that remain.
    /// </summary>
    [Fact]
    public void Analyze_L2AfterOneRound_LoosesTheBondToTheSignOppositeTheL1Sign()
    {
        ChartResult chart = CreateChart(spiritLongitude: 310.0, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(2000, 1, 1));

        result.CurrentL1Sign.Should().Be("Aquarius");
        result.CurrentL2Periods.Should().HaveCount(21);
        result.CurrentL2Periods.Select(p => p.Sign).Should().Equal(
            "Aquarius", "Pisces", "Aries", "Taurus", "Gemini", "Cancer", "Leo", "Virgo", "Libra", "Scorpio",
            "Sagittarius", "Capricorn",
            "Leo", "Virgo", "Libra", "Scorpio", "Sagittarius", "Capricorn", "Aquarius", "Pisces", "Aries");
        result.CurrentL2Periods[11].EndDate.Should().Be(new DateTime(1997, 5, 1));
        result.CurrentL2Periods[12].StartDate.Should().Be(new DateTime(1997, 5, 1));
        (result.CurrentL2Periods[^1].EndDate - result.CurrentL2Periods[^1].StartDate).TotalDays.Should().Be(180.0);
        result.CurrentL2Periods[^1].EndDate.Should().Be(result.CurrentL1End);
    }

    #endregion

    #region Missing Lot of Spirit

    [Fact]
    public void Analyze_MissingLotOfSpirit_ReturnsEmptyResult()
    {
        ChartResult chart = CreateChart(spiritLongitude: null, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(2026, 1, 1));
        result.Level1Periods.Should().BeEmpty();
        result.CurrentL2Periods.Should().BeEmpty();
        result.Summary.Should().Contain("not available");
    }

    #endregion

    #region Summary

    [Fact]
    public void Analyze_Summary_ContainsL1AndL2Signs()
    {
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1985, 6, 1));
        result.Summary.Should().Contain("Aries"); // L1 sign (Spirit in Aries)
        result.Summary.Should().Contain("Spirit");
    }

    [Fact]
    public void Analyze_Summary_ContainsAngularityInfo()
    {
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 10.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1985, 6, 1));
        result.Summary.Should().Contain("Angular");
    }

    /// <summary>
    /// The article before the Level 1 sign follows its first letter: "an Aries" and "an Aquarius", "a" before
    /// the other ten (branch ExportText, 1.7.2; finding N12). Until 1.7.2 every sign read "a": "a Aries".
    /// </summary>
    /// <param name="spiritLongitude">The Lot of Spirit's longitude, whose sign is the first Level 1 period's.</param>
    /// <param name="expected">The article and the sign the summary prints.</param>
    [Theory]
    [InlineData(10.0, "an Aries")]
    [InlineData(310.0, "an Aquarius")]
    [InlineData(40.0, "a Taurus")]
    [InlineData(280.0, "a Capricorn")]
    public void Analyze_Summary_ArticleFollowsTheSign(double spiritLongitude, string expected)
    {
        ChartResult chart = CreateChart(spiritLongitude: spiritLongitude, fortuneLongitude: 10.0);

        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));

        result.Summary.Should().Contain($"Currently in {expected} Level 1 period (");
    }

    #endregion

    #region Fields

    [Fact]
    public void Analyze_ReturnsCorrectLotSigns()
    {
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 100.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(1985, 6, 1));
        result.LotOfSpiritSign.Should().Be("Aries");
        result.LotOfFortuneSign.Should().Be("Cancer");
    }

    [Fact]
    public void Analyze_NullChart_ThrowsArgumentNullException()
    {
        Action act = () => ZodiacalReleasingAnalyzer.Analyze(null!, DateTime.Today);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Analyze_L1PeriodsAreContiguous()
    {
        ChartResult chart = CreateChart(spiritLongitude: 10.0, fortuneLongitude: 0.0);
        ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));

        for (int i = 1; i < result.Level1Periods.Count; i++)
        {
            result.Level1Periods[i].StartDate.Should().Be(result.Level1Periods[i - 1].EndDate);
        }
    }

    #endregion

    #region Real charts

    /// <summary>
    /// The releasing of J2000 NYC, Spirit Scorpio and Fortune Pisces, born 2000-01-01 (Tier A, h_truth.out).
    /// </summary>
    [Collection("Ephemeris")]
    [Trait("Category", "Integration")]
    public sealed class IntegrationTests
    {
        private readonly ChartResult _j2000;

        /// <summary>Casts J2000 NYC.</summary>
        /// <param name="fixture">The shared ephemeris fixture.</param>
        public IntegrationTests(EphemerisFixture fixture)
        {
            _j2000 = GoldenFixtures.J2000Nyc(fixture);
        }

        /// <summary>
        /// At 2026-10-04: Scorpio's 15 years of 360 days to 2014-10-14, Sagittarius to 2026-08-12, Capricorn
        /// current to 2053-03-23 and Aquarius, the first period after it, closing the timeline; the angularity
        /// from Pisces. Until 1.3.4 the years were 365.25 days (Sagittarius to 2026-12-31) and Capricorn, the
        /// 11th from Pisces, read Cadent.
        /// </summary>
        [Fact]
        public void Analyze_J2000Nyc_Level1_FourPeriodsOf360DayYears()
        {
            ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(_j2000, new DateTime(2026, 10, 4));

            result.LotOfSpiritSign.Should().Be("Scorpio");
            result.LotOfFortuneSign.Should().Be("Pisces");
            result.Level1Periods.Select(p => (p.Sign, (int)p.Years, p.StartDate, p.EndDate, p.Angularity)).Should().Equal(
                ("Scorpio", 15, new DateTime(2000, 1, 1), new DateTime(2014, 10, 14), "Cadent"),
                ("Sagittarius", 12, new DateTime(2014, 10, 14), new DateTime(2026, 8, 12), "Angular"),
                ("Capricorn", 27, new DateTime(2026, 8, 12), new DateTime(2053, 3, 23), "Succedent"),
                ("Aquarius", 30, new DateTime(2053, 3, 23), new DateTime(2082, 10, 17), "Cadent"));
            result.Level1Periods.Single(p => p.IsCurrent).Sign.Should().Be("Capricorn");
            result.CurrentL1Sign.Should().Be("Capricorn");
            result.CurrentL1Angularity.Should().Be("Succedent");
            result.CurrentL1IsPeak.Should().BeFalse();
        }

        /// <summary>
        /// At 2026-10-04, Capricorn's 324 months: the twelve signs from Capricorn, 211 months to 2043-12-11,
        /// then Cancer, the loosing of the bond, and the six after it, then Capricorn's 27 cut to the 14 that
        /// remain; nineteen rows, Capricorn current.
        /// </summary>
        [Fact]
        public void Analyze_J2000Nyc_Level2_NineteenMonthsRowsWithTheLoosingOfTheBond()
        {
            ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(_j2000, new DateTime(2026, 10, 4));

            result.CurrentL2Periods.Select(p => (p.Sign, (int)Math.Round((p.EndDate - p.StartDate).TotalDays / 30.0), p.StartDate, p.EndDate, p.Angularity)).Should().Equal(
                ("Capricorn", 27, new DateTime(2026, 8, 12), new DateTime(2028, 10, 30), "Succedent"),
                ("Aquarius", 30, new DateTime(2028, 10, 30), new DateTime(2031, 4, 18), "Cadent"),
                ("Pisces", 12, new DateTime(2031, 4, 18), new DateTime(2032, 4, 12), "Angular"),
                ("Aries", 15, new DateTime(2032, 4, 12), new DateTime(2033, 7, 6), "Succedent"),
                ("Taurus", 8, new DateTime(2033, 7, 6), new DateTime(2034, 3, 3), "Cadent"),
                ("Gemini", 20, new DateTime(2034, 3, 3), new DateTime(2035, 10, 24), "Angular"),
                ("Cancer", 25, new DateTime(2035, 10, 24), new DateTime(2037, 11, 12), "Succedent"),
                ("Leo", 19, new DateTime(2037, 11, 12), new DateTime(2039, 6, 5), "Cadent"),
                ("Virgo", 20, new DateTime(2039, 6, 5), new DateTime(2041, 1, 25), "Angular"),
                ("Libra", 8, new DateTime(2041, 1, 25), new DateTime(2041, 9, 22), "Succedent"),
                ("Scorpio", 15, new DateTime(2041, 9, 22), new DateTime(2042, 12, 16), "Cadent"),
                ("Sagittarius", 12, new DateTime(2042, 12, 16), new DateTime(2043, 12, 11), "Angular"),
                ("Cancer", 25, new DateTime(2043, 12, 11), new DateTime(2045, 12, 30), "Succedent"),
                ("Leo", 19, new DateTime(2045, 12, 30), new DateTime(2047, 7, 23), "Cadent"),
                ("Virgo", 20, new DateTime(2047, 7, 23), new DateTime(2049, 3, 14), "Angular"),
                ("Libra", 8, new DateTime(2049, 3, 14), new DateTime(2049, 11, 9), "Succedent"),
                ("Scorpio", 15, new DateTime(2049, 11, 9), new DateTime(2051, 2, 2), "Cadent"),
                ("Sagittarius", 12, new DateTime(2051, 2, 2), new DateTime(2052, 1, 28), "Angular"),
                ("Capricorn", 14, new DateTime(2052, 1, 28), new DateTime(2053, 3, 23), "Succedent"));
            result.CurrentL2Periods.Single(p => p.IsCurrent).Sign.Should().Be("Capricorn");
            result.CurrentL2Sign.Should().Be("Capricorn");
            result.CurrentL2Start.Should().Be(new DateTime(2026, 8, 12));
            result.CurrentL2End.Should().Be(new DateTime(2028, 10, 30));
            result.CurrentL2Angularity.Should().Be("Succedent");
        }

        /// <summary>
        /// At 2025-12-31 the current Level 1 is Sagittarius and the current Level 2 Cancer, its 25 months cut to
        /// the 20 that remain, 2024-12-20 to 2026-08-12, the eighth and last row.
        /// </summary>
        [Fact]
        public void Analyze_J2000Nyc_At20251231_SagittariusAndTheClampedCancer()
        {
            ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(_j2000, new DateTime(2025, 12, 31));

            result.CurrentL1Sign.Should().Be("Sagittarius");
            result.CurrentL1Start.Should().Be(new DateTime(2014, 10, 14));
            result.CurrentL1End.Should().Be(new DateTime(2026, 8, 12));
            result.Level1Periods.Should().HaveCount(3);
            result.CurrentL2Periods.Should().HaveCount(8);
            result.CurrentL2Sign.Should().Be("Cancer");
            result.CurrentL2Start.Should().Be(new DateTime(2024, 12, 20));
            result.CurrentL2End.Should().Be(new DateTime(2026, 8, 12));
            (result.CurrentL2End - result.CurrentL2Start).TotalDays.Should().Be(600.0, "20 months of 30 days");
            result.CurrentL2Periods[^1].Sign.Should().Be("Cancer");
        }
    }

    #endregion

    #region Helpers

    private static ChartResult CreateChart(
        double? spiritLongitude = 10.0,
        double fortuneLongitude = 0.0,
        DateTime? birthDate = null)
    {
        DateTime birth = birthDate ?? new DateTime(1980, 1, 1);

        List<ArabicPart> parts = [];

        if (spiritLongitude.HasValue)
        {
            parts.Add(new ArabicPart
            {
                Name = "Lot of Spirit",
                Group = "Arabic",
                Longitude = spiritLongitude.Value,
                House = 1,
                Formula = "Asc + Sun - Moon",
                Domain = "Career, actions, vocation",
                Ruler = "Mars",
            });
        }

        parts.Add(new ArabicPart
        {
            Name = "Lot of Fortune",
            Group = "Arabic",
            Longitude = fortuneLongitude,
            House = 1,
            Formula = "Asc + Moon - Sun",
            Domain = "General wellbeing, material fortune",
            Ruler = "Mars",
        });

        return new ChartResult
        {
            Input = new ChartInput
            {
                Name = "Test Person",
                LocalDateTime = birth,
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York",
                EventType = "Birth",
            },
            JulianDay = 2444239.5,
            Obliquity = 23.4367,
            UtcDateTime = birth.ToUniversalTime(),
            Positions = CreateDefaultPositions(),
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12)
                    .Select(i => (15.0 + i * 30.0) % 360.0).ToList(),
                Ascendant = 15.0,
                Midheaven = 285.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ArabicParts = parts,
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    private static IReadOnlyList<PlanetPosition> CreateDefaultPositions() =>
    [
        CreatePosition(CelestialBodyId.Sun, 280.0, 10),
        CreatePosition(CelestialBodyId.Moon, 100.0, 4),
        CreatePosition(CelestialBodyId.Mercury, 290.0, 10),
        CreatePosition(CelestialBodyId.Venus, 165.0, 7),
        CreatePosition(CelestialBodyId.Mars, 45.0, 2),
        CreatePosition(CelestialBodyId.Jupiter, 60.0, 3),
        CreatePosition(CelestialBodyId.Saturn, 330.0, 12),
    ];

    private static PlanetPosition CreatePosition(
        CelestialBodyId body, double longitude, int house, double speed = 1.0)
    {
        return new PlanetPosition
        {
            Body = body,
            Name = body.ToString(),
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = speed,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
            House = house,
        };
    }

    #endregion
}
