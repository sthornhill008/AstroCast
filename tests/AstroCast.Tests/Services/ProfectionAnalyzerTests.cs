using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="ProfectionAnalyzer"/> covering age computation, the profected house and its sign,
/// one sign a year from the Ascendant's sign with the cusps unread (T-D9), the lord of the year, natal
/// conditions, aspects, the profection table, the refusal of a reference before the birth day (T6-6) and
/// the narrative summary. The real-chart values are Tier A, calendar arithmetic (h_truth.out).
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProfectionAnalyzerTests
{
    #region Age Computation

    [Fact]
    public void Analyze_BornJan15_2000_ReferenceMarc30_2026_Age26()
    {
        // Birthday Jan 15 has already passed by March 30 → age 26
        ChartResult chart = CreateChart(birthDate: new DateTime(2000, 1, 15));
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(2026, 3, 30));
        result.Age.Should().Be(26);
    }

    [Fact]
    public void Analyze_BornJun15_2000_ReferenceMar30_2026_Age25()
    {
        // Birthday June 15 has NOT passed by March 30 → age 25
        ChartResult chart = CreateChart(birthDate: new DateTime(2000, 6, 15));
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(2026, 3, 30));
        result.Age.Should().Be(25);
    }

    [Fact]
    public void Analyze_BornMar30_2000_ReferenceMar30_2026_Age26()
    {
        // Birthday is today → age 26
        ChartResult chart = CreateChart(birthDate: new DateTime(2000, 3, 30));
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(2026, 3, 30));
        result.Age.Should().Be(26);
    }

    /// <summary>A reference date before the birth day is refused (T6-6).</summary>
    [Fact]
    public void Analyze_ReferenceBeforeTheBirthDay_IsRefused()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1));
        Action act = () => ProfectionAnalyzer.Analyze(chart, new DateTime(1979, 12, 31));

        ArgumentOutOfRangeException refusal = act.Should().Throw<ArgumentOutOfRangeException>().Which;
        refusal.ParamName.Should().Be("referenceDate");
        refusal.Message.Should().StartWith("The reference date 1979-12-31 is before the birth day 1980-01-01.");
    }

    #endregion

    #region Profected House

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 2)]
    [InlineData(11, 12)]
    [InlineData(12, 1)]   // Cycle restarts
    [InlineData(25, 2)]   // 25 mod 12 = 1, +1 = house 2
    [InlineData(30, 7)]   // 30 mod 12 = 6, +1 = house 7
    public void Analyze_Age_MapsToCorrectProfectedHouse(int age, int expectedHouse)
    {
        // Use a birth date and reference that will produce the given age
        DateTime birthDate = new DateTime(1980, 1, 1);
        DateTime refDate = birthDate.AddYears(age);
        ChartResult chart = CreateChart(birthDate: birthDate);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, refDate);
        result.ProfectedHouse.Should().Be(expectedHouse);
    }

    #endregion

    #region Lord of the Year

    [Fact]
    public void Analyze_AscendantAries_Age0_ProfectsToAries_LordMars()
    {
        // Ascendant 15° Aries, age 0: the 1st, the Ascendant's own sign, Aries → Mars
        ChartResult chart = CreateChart(
            birthDate: new DateTime(1980, 1, 1),
            ascendant: 15.0);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.ProfectedHouse.Should().Be(1);
        result.ProfectedSign.Should().Be("Aries");
        result.LordOfTheYear.Should().Be("Mars");
    }

    /// <summary>
    /// The profected sign is one sign a year from the Ascendant's sign, whatever sign the house's cusp
    /// carries (T-D9): with a quadrant-like 2nd cusp at 5° Gemini, age 1 still profects to Taurus and Venus.
    /// Until 1.3.4 the sign was the cusp's, so a quadrant chart skipped and repeated signs.
    /// </summary>
    [Fact]
    public void Analyze_AscendantAries_Age1_ProfectsToTaurus_WhateverTheSecondCusp()
    {
        ChartResult chart = CreateChart(
            birthDate: new DateTime(1980, 1, 1),
            ascendant: 25.0,
            cusps: SkewedCusps);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1981, 1, 1));
        result.ProfectedHouse.Should().Be(2);
        result.ProfectedSign.Should().Be("Taurus", "the 2nd sign from Aries, not the 2nd cusp's Gemini");
        result.LordOfTheYear.Should().Be("Venus");
    }

    [Fact]
    public void Analyze_AscendantAries_Age9_ProfectsToCapricorn_LordSaturn()
    {
        // Ascendant 0° Aries, age 9: the 10th sign from Aries is Capricorn → Saturn
        ChartResult chart = CreateChart(
            birthDate: new DateTime(1980, 1, 1),
            ascendant: 0.0);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1989, 1, 1));
        result.ProfectedHouse.Should().Be(10);
        result.ProfectedSign.Should().Be("Capricorn");
        result.LordOfTheYear.Should().Be("Saturn");
    }

    #endregion

    #region Lord's Natal Condition

    [Fact]
    public void Analyze_LordWithDomicileDignity_LordDignityScoreFive()
    {
        // Mars in Aries = domicile, score +5
        ChartResult chart = CreateChart(
            birthDate: new DateTime(1980, 1, 1),
            ascendant: 15.0, // Aries ASC → age 0 = house 1, lord = Mars
            dignities: [
                CreateDignity("Mars", ZodiacSign.Aries, isDomicile: true),
            ]);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.LordDignityScore.Should().Be(5);
        result.LordDignity.Should().Contain("Domicile");
    }

    [Fact]
    public void Analyze_LordCombust_ConditionsContainCombust()
    {
        ChartResult chart = CreateChart(
            birthDate: new DateTime(1980, 1, 1),
            ascendant: 15.0, // Aries ASC, lord = Mars at age 0
            planetConditions: [
                CreateCondition("Mars", isCombust: true),
            ]);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.LordConditions.Should().Contain(s => s.Contains("Combust") || s.Contains("combust"));
    }

    [Fact]
    public void Analyze_LordRetrograde_LordRetrogradeIsTrue()
    {
        ChartResult chart = CreateChart(
            birthDate: new DateTime(1980, 1, 1),
            ascendant: 15.0,
            positions: [
                CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: -0.3), // retrograde
                CreatePosition(CelestialBodyId.Sun, 280.0, 10),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10),
                CreatePosition(CelestialBodyId.Venus, 165.0, 7),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 1),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1),
            ]);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.LordRetrograde.Should().BeTrue();
    }

    #endregion

    #region Lord's Natal Aspects

    [Fact]
    public void Analyze_LordHasAspects_LordNatalAspectsPopulated()
    {
        ChartResult chart = CreateChart(
            birthDate: new DateTime(1980, 1, 1),
            ascendant: 15.0, // lord = Mars
            aspects: [
                CreateAspect("Mars", 45.0, "Jupiter", 165.0, AspectType.Trine, 2.0, isApplying: true),
                CreateAspect("Mars", 45.0, "Saturn", 330.0, AspectType.Square, 4.0, isApplying: false),
            ]);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.LordNatalAspects.Should().HaveCount(2);
        result.LordNatalAspects.Should().Contain(s => s.Contains("Jupiter"));
        result.LordNatalAspects.Should().Contain(s => s.Contains("Saturn"));
    }

    [Fact]
    public void Analyze_LordHasNoAspects_LordNatalAspectsEmpty()
    {
        ChartResult chart = CreateChart(
            birthDate: new DateTime(1980, 1, 1),
            ascendant: 15.0);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.LordNatalAspects.Should().BeEmpty();
    }

    #endregion

    #region Profection Table

    [Fact]
    public void Analyze_ProfectionTable_HasExactly12Entries()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1));
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));
        result.ProfectionTable.Should().HaveCount(12);
    }

    [Fact]
    public void Analyze_ProfectionTable_ExactlyOneCurrentEntry()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1));
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));
        result.ProfectionTable.Count(e => e.IsCurrent).Should().Be(1);
    }

    [Fact]
    public void Analyze_ProfectionTable_CurrentEntryMatchesProfectedHouse()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1));
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));
        ProfectionTableEntry? currentEntry = result.ProfectionTable.FirstOrDefault(e => e.IsCurrent);
        currentEntry.Should().NotBeNull();
        currentEntry!.House.Should().Be(result.ProfectedHouse);
    }

    [Fact]
    public void Analyze_ProfectionTable_AgesContainCurrentAgeMarked()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1));
        // Age 30 at reference date
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));
        result.Age.Should().Be(30);
        ProfectionTableEntry? currentEntry = result.ProfectionTable.First(e => e.IsCurrent);
        currentEntry.Ages.Should().Contain("30*");
    }

    /// <summary>
    /// Row h of the table carries the sign h − 1 after the Ascendant's and its traditional ruler, whatever the
    /// cusps carry (T-D9): twelve signs, each once, from Aries.
    /// </summary>
    [Fact]
    public void Analyze_ProfectionTable_RowsAreTheSignsFromTheAscendant_WhateverTheCusps()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1), ascendant: 25.0, cusps: SkewedCusps);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));

        result.ProfectionTable.Select(e => (e.House, e.Sign, e.Lord)).Should().Equal(
            (1, "Aries", "Mars"), (2, "Taurus", "Venus"), (3, "Gemini", "Mercury"), (4, "Cancer", "Moon"),
            (5, "Leo", "Sun"), (6, "Virgo", "Mercury"), (7, "Libra", "Venus"), (8, "Scorpio", "Mars"),
            (9, "Sagittarius", "Jupiter"), (10, "Capricorn", "Saturn"), (11, "Aquarius", "Saturn"),
            (12, "Pisces", "Jupiter"));
    }

    #endregion

    #region Formatter — Birth Chart Only

    [Fact]
    public void ToMarkdown_BirthChart_ContainsProfectionSection()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1990, 3, 15), eventType: "Birth");
        string md = ChartMarkdownFormatter.ToMarkdown(chart);
        md.Should().Contain("## Annual Profection");
    }

    [Fact]
    public void ToMarkdown_EventChart_DoesNotContainProfectionSection()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1990, 3, 15), eventType: "Event");
        string md = ChartMarkdownFormatter.ToMarkdown(chart);
        md.Should().NotContain("## Annual Profection");
    }

    [Fact]
    public void ToMarkdown_HoraryChart_DoesNotContainProfectionSection()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1990, 3, 15), eventType: "Horary");
        string md = ChartMarkdownFormatter.ToMarkdown(chart);
        md.Should().NotContain("## Annual Profection");
    }

    #endregion

    #region Summary

    [Fact]
    public void Analyze_Summary_ContainsLordName()
    {
        ChartResult chart = CreateChart(
            birthDate: new DateTime(1980, 1, 1),
            ascendant: 15.0); // Mars as lord at age 0
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.Summary.Should().Contain("Mars");
    }

    /// <summary>The summary names the profected sign alone: the sign is the whole sign's, not a cusp's.</summary>
    [Fact]
    public void Analyze_Summary_NamesTheProfectedSign()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1), ascendant: 15.0);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.Summary.Should().Contain("The profected house is the 1st (Aries), making Mars the lord of the year.");
        result.Summary.Should().NotContain("on the cusp");
    }

    [Fact]
    public void Analyze_Summary_ContainsProfectedHouseNumber()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1));
        // Age 6 → house 7
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1986, 1, 1));
        result.Summary.Should().Contain("7th");
    }

    [Fact]
    public void Analyze_Summary_ContainsActivatedThemes()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1), ascendant: 15.0);
        // Age 0 → house 1 → Self, body, identity...
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1980, 1, 1));
        result.Summary.Should().Contain("Self");
    }

    #endregion

    #region Result Fields

    [Fact]
    public void Analyze_ReturnsCorrectBirthDateAndReferenceDate()
    {
        DateTime birth = new DateTime(1990, 6, 21);
        DateTime reference = new DateTime(2026, 3, 30);
        ChartResult chart = CreateChart(birthDate: birth);
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, reference);
        result.BirthDate.Should().Be(birth.Date);
        result.ReferenceDate.Should().Be(reference.Date);
    }

    [Fact]
    public void Analyze_ReturnsCorrectName()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1), name: "Test Person");
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(2010, 1, 1));
        result.Name.Should().Be("Test Person");
    }

    [Fact]
    public void Analyze_ProfectionCycleYear_IsZeroForAge12()
    {
        // Age 12 → mod 12 = 0 = cycle year 0 = house 1
        ChartResult chart = CreateChart(birthDate: new DateTime(1980, 1, 1));
        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, new DateTime(1992, 1, 1));
        result.ProfectionCycleYear.Should().Be(0);
        result.ProfectedHouse.Should().Be(1);
    }

    #endregion

    #region AnalyzeForReturn (SR Age Semantics)

    /// <summary>
    /// Reproduces person A's SR scenario (invented; the same shape as the live
    /// case this test once reproduced): an SR ingressing one day before the
    /// calendar birthday. The natal Analyze decrements because the reference
    /// date is one day before the anniversary; AnalyzeForReturn must give the
    /// year-of-life the SR governs.
    /// </summary>
    [Fact]
    public void AnalyzeForReturn_PreBirthdayIngress_AdvancesAge()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(2000, 10, 5));
        DateTime ingress = new(2025, 10, 4, 18, 7, 36, DateTimeKind.Utc);

        ProfectionYearResult result = ProfectionAnalyzer.AnalyzeForReturn(chart, ingress);

        result.Age.Should().Be(25,
            "the SR ingress one day before the 25th calendar birthday governs the year-of-life entered at age 25");
    }

    [Fact]
    public void AnalyzeForReturn_OnBirthdayIngress_GivesCorrectAge()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(2000, 10, 5));
        DateTime ingress = new(2025, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        ProfectionYearResult result = ProfectionAnalyzer.AnalyzeForReturn(chart, ingress);

        result.Age.Should().Be(25,
            "an SR ingressing on the natal anniversary governs the year-of-life entered at age 25");
    }

    [Fact]
    public void AnalyzeForReturn_PostBirthdayIngress_GivesCorrectAge()
    {
        ChartResult chart = CreateChart(birthDate: new DateTime(2000, 10, 5));
        DateTime ingress = new(2025, 10, 6, 6, 0, 0, DateTimeKind.Utc);

        ProfectionYearResult result = ProfectionAnalyzer.AnalyzeForReturn(chart, ingress);

        result.Age.Should().Be(25,
            "an SR ingressing one day after the natal anniversary still governs the same year-of-life as the on-birthday case — the SR ingress always governs one whole year-of-life regardless of which side of the calendar birthday it falls on");
    }

    /// <summary>
    /// Reproduces the lord-of-the-year correctness check on person A's dates:
    /// Capricorn rising, age 78 → 78 % 12 = 6 → profected house 7th, sign
    /// Cancer (7th from Capricorn ASC), lord Moon.
    /// </summary>
    [Fact]
    public void AnalyzeForReturn_ProfectedHouseAndLordMatchAge()
    {
        // Capricorn ASC at 0° gives houses Capricorn (1) → Aquarius (2) → … → Leo (8)
        ChartResult chart = CreateChart(
            birthDate: new DateTime(1947, 10, 5),
            ascendant: 270.0); // 270° = 0° Capricorn
        DateTime ingress = new(2025, 10, 4, 18, 7, 36, DateTimeKind.Utc);

        ProfectionYearResult result = ProfectionAnalyzer.AnalyzeForReturn(chart, ingress);

        result.Age.Should().Be(78);
        result.ProfectedHouse.Should().Be(7,
            "78 mod 12 = 6, profected house = 6 + 1 = 7");
        result.ProfectedSign.Should().Be("Cancer",
            "7th from Capricorn ASC equals Cancer");
        result.LordOfTheYear.Should().Be("Moon",
            "the Moon is the traditional ruler of Cancer");
    }

    [Fact]
    public void Analyze_PreBirthday_PreservesNatalSemantic()
    {
        // Locks in that the original Analyze method still gives the
        // pre-birthday natal-decrement age — the refactor must not break
        // natal call-site behaviour.
        ChartResult chart = CreateChart(birthDate: new DateTime(2000, 10, 5));
        DateTime referenceDate = new(2025, 10, 4);

        ProfectionYearResult result = ProfectionAnalyzer.Analyze(chart, referenceDate);

        result.Age.Should().Be(24,
            "natal Analyze decrements when reference date is before the calendar anniversary in the same year — preserves pre-refactor natal semantic");
    }

    [Fact]
    public void AnalyzeForReturn_AndAnalyze_DivergeOnPreBirthday()
    {
        // Locks in the semantic distinction between the two methods: for a
        // reference date one day before the natal anniversary, the SR-context
        // age is exactly one greater than the natal-context age.
        ChartResult chart = CreateChart(birthDate: new DateTime(2000, 10, 5));
        DateTime preBirthday = new(2025, 10, 4, 18, 7, 36, DateTimeKind.Utc);

        ProfectionYearResult natal = ProfectionAnalyzer.Analyze(chart, preBirthday);
        ProfectionYearResult forReturn = ProfectionAnalyzer.AnalyzeForReturn(chart, preBirthday);

        forReturn.Age.Should().Be(natal.Age + 1,
            "SR-context age advances past the calendar boundary; natal-context age stays at the pre-anniversary year");
    }

    #endregion

    #region Real charts

    /// <summary>
    /// The profections of real charts (Tier A): J2000 NYC at 2026-10-04 (h_truth.out), Ascendant Capricorn;
    /// person A (invented), Ascendant Scorpio, whose Placidus cusps skip and repeat signs
    /// (fixtures/truth/persons.py; the profection arithmetic is the rule itself).
    /// </summary>
    [Collection("Ephemeris")]
    [Trait("Category", "Integration")]
    public sealed class IntegrationTests
    {
        private static readonly DateTime Reference = new(2026, 10, 4);

        private readonly EphemerisFixture _fixture;
        private readonly ChartResult _j2000;

        /// <summary>Casts J2000 NYC.</summary>
        /// <param name="fixture">The shared ephemeris fixture.</param>
        public IntegrationTests(EphemerisFixture fixture)
        {
            _fixture = fixture;
            _j2000 = GoldenFixtures.J2000Nyc(fixture);
        }

        /// <summary>J2000 NYC at 2026-10-04: age 26, the 3rd, Pisces, Jupiter (the same under either rule).</summary>
        [Fact]
        public void Analyze_J2000Nyc_Age26_ThirdHouse_Pisces_Jupiter()
        {
            ProfectionYearResult result = ProfectionAnalyzer.Analyze(_j2000, Reference);

            result.Age.Should().Be(26);
            result.ProfectedHouse.Should().Be(3);
            result.ProfectedSign.Should().Be("Pisces");
            result.LordOfTheYear.Should().Be("Jupiter");
        }

        /// <summary>
        /// Person A at 2023-01-15: age 75, the 4th, the fourth sign from Scorpio, Aquarius, and Saturn its lord;
        /// the table's 4th row Aquarius and Saturn, its 5th Pisces and Jupiter. Until 1.3.4 the sign was the
        /// 4th Placidus cusp's, Pisces, and the lord Jupiter (T-D9): person A's quadrant cusps skip Aquarius.
        /// </summary>
        [Fact]
        public void Analyze_Hartwell_Age75_FourthHouse_Aquarius_Saturn()
        {
            ChartResult hartwell = GoldenFixtures.CreateChartEngine(_fixture).Calculate(DailyHoroscopeServiceTests.HartwellInput());
            ProfectionYearResult result = ProfectionAnalyzer.Analyze(hartwell, new DateTime(2023, 1, 15));

            result.Age.Should().Be(75);
            result.ProfectedHouse.Should().Be(4);
            result.ProfectedSign.Should().Be("Aquarius");
            result.LordOfTheYear.Should().Be("Saturn");
            result.ProfectionTable[3].Should().Match<ProfectionTableEntry>(e => e.House == 4 && e.Sign == "Aquarius" && e.Lord == "Saturn");
            result.ProfectionTable[4].Should().Match<ProfectionTableEntry>(e => e.House == 5 && e.Sign == "Pisces" && e.Lord == "Jupiter");
            result.Summary.Should().Contain("the 4th (Aquarius), making Saturn the lord of the year");
        }

        /// <summary>The 2026 solar return of J2000 NYC, 2025-12-31 19:17:48 UT, rounds to age 26 from the birth instant.</summary>
        [Fact]
        public void AnalyzeForReturn_J2000Nyc_SolarReturn2026_Age26()
        {
            ProfectionYearResult result = ProfectionAnalyzer.AnalyzeForReturn(
                _j2000, new DateTime(2025, 12, 31, 19, 17, 48, DateTimeKind.Utc));

            result.Age.Should().Be(26);
            result.ProfectedSign.Should().Be("Pisces");
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Quadrant-like cusps for an Ascendant at 25° Aries: the 2nd cusp in Gemini and the 12th in Aries, so a
    /// cusp-read profection would skip Taurus and read Aries twice.
    /// </summary>
    private static readonly double[] SkewedCusps =
        [25.0, 65.0, 95.0, 130.0, 160.0, 190.0, 205.0, 245.0, 275.0, 310.0, 340.0, 10.0];

    private static ChartResult CreateChart(
        DateTime? birthDate = null,
        double ascendant = 15.0,
        string name = "Test Person",
        string eventType = "Birth",
        IReadOnlyList<PlanetPosition>? positions = null,
        IReadOnlyList<AspectResult>? aspects = null,
        IReadOnlyList<DignityInfo>? dignities = null,
        IReadOnlyList<PlanetCondition>? planetConditions = null,
        IReadOnlyList<double>? cusps = null)
    {
        DateTime birth = birthDate ?? new DateTime(1980, 1, 1);
        positions ??= CreateDefaultPositions();
        aspects ??= [];
        dignities ??= [];
        planetConditions ??= [];

        return new ChartResult
        {
            Input = new ChartInput
            {
                Name = name,
                LocalDateTime = birth,
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York",
                EventType = eventType,
            },
            JulianDay = 2444239.5,
            Obliquity = 23.4367,
            UtcDateTime = birth.ToUniversalTime(),
            Positions = positions,
            Houses = new HouseData
            {
                Cusps = cusps?.ToList() ?? Enumerable.Range(0, 12)
                    .Select(i => (ascendant + i * 30.0) % 360.0).ToList(),
                Ascendant = ascendant,
                Midheaven = (ascendant + 270.0) % 360.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = aspects,
            Midpoints = [],
            MidpointActivations = [],
            Dignities = dignities,
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            PlanetConditions = planetConditions,
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
        CreatePosition(CelestialBodyId.Uranus, 50.0, 2),
        CreatePosition(CelestialBodyId.Neptune, 355.0, 12),
        CreatePosition(CelestialBodyId.Pluto, 300.0, 10),
        CreatePosition(CelestialBodyId.TrueNode, 10.0, 1),
        CreatePosition(CelestialBodyId.Chiron, 20.0, 1),
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

    private static AspectResult CreateAspect(
        string body1, double lon1, string body2, double lon2,
        AspectType type, double orb, bool isApplying)
    {
        AspectDefinition def = AspectDefinition.All.First(a => a.Type == type);
        return new AspectResult
        {
            Body1Name = body1,
            Body1Longitude = lon1,
            Body2Name = body2,
            Body2Longitude = lon2,
            Aspect = def,
            Orb = orb,
            IsApplying = isApplying,
        };
    }

    private static DignityInfo CreateDignity(
        string planetName, ZodiacSign sign,
        bool isDomicile = false, bool isExalted = false,
        bool isInDetriment = false, bool isInFall = false)
    {
        return new DignityInfo
        {
            PlanetName = planetName,
            Sign = sign,
            IsDomicile = isDomicile,
            IsExalted = isExalted,
            IsInDetriment = isInDetriment,
            IsInFall = isInFall,
            Dispositor = "Mars",
        };
    }

    private static PlanetCondition CreateCondition(
        string planetName, bool isCombust = false, bool isBesieged = false)
    {
        List<string> conditions = [];
        if (isCombust) conditions.Add("Combust");
        if (isBesieged) conditions.Add("Besieged");

        return new PlanetCondition
        {
            PlanetName = planetName,
            IsCazimi = false,
            IsCombust = isCombust,
            IsUnderTheBeams = false,
            IsBesieged = isBesieged,
            IsConjunctBenefic = false,
            IsConjunctMalefic = false,
            HasApplyingBeneficAspect = false,
            HasApplyingMaleficAspect = false,
            AccidentalScore = isCombust ? -5 : isBesieged ? -5 : 0,
            Conditions = conditions.AsReadOnly(),
        };
    }

    #endregion
}
