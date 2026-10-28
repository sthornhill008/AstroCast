using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="HoraryPerfectionAnalyzer"/> covering category mapping,
/// significator identification, perfection mechanisms, denial mechanisms,
/// reception quality, Moon testimony, and overall judgment.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HoraryPerfectionAnalyzerTests
{
    #region Category Mapping

    [Theory]
    [InlineData("Career", 10)]
    [InlineData("Relationship", 7)]
    [InlineData("Money", 2)]
    [InlineData("Home", 4)]
    [InlineData("Health", 6)]
    [InlineData("Travel", 9)]
    [InlineData("Education", 9)]
    [InlineData("Legal", 7)]
    [InlineData("Children", 5)]
    [InlineData("Love", 5)]
    [InlineData("Siblings", 3)]
    [InlineData("Death / Inheritance", 8)]
    [InlineData("Friends", 11)]
    [InlineData("Hidden Matters", 12)]
    [InlineData("Lost Objects", 2)]
    [InlineData("General", 7)]
    public void Analyze_CategoryMapping_MapsToCorrectHouse(string category, int expectedHouse)
    {
        ChartResult chart = CreateBaseChart(category: category);
        HoraryPerfectionResult result = Analyze(chart, category);
        result.QuesitedHouseNumber.Should().Be(expectedHouse);
    }

    [Fact]
    public void Analyze_UnknownCategory_DefaultsToHouse7()
    {
        // An unrecognised category still takes the 7th house, and the result says so.
        ChartResult chart = CreateBaseChart(category: "Aliens");
        HoraryPerfectionResult result = Analyze(chart, "Aliens");
        result.QuesitedHouseNumber.Should().Be(7);
        result.Notes.Should().ContainSingle().Which.Should().Be(
            "Category 'Aliens' is not recognised; the 7th house is assumed.");
    }

    #endregion

    #region Significator Identification

    [Fact]
    public void Analyze_ScorpioAscendant_CareerQuestion_L1IsMars()
    {
        // Scorpio ASC → L1 = Mars (traditional ruler)
        // Career → 10th house cusp
        ChartResult chart = CreateBaseChart(
            ascendant: 215.0, // Scorpio
            category: "Career");

        HoraryPerfectionResult result = Analyze(chart, "Career");
        result.QuerentSignificator.Should().Be("Mars");
    }

    [Fact]
    public void Analyze_SameRulerBothHouses_L1FallsBackToMoon()
    {
        // Pisces ASC (345°) → L1 = Jupiter
        // Career → house 10. Cusp 10 = (345 + 270) % 360 = 255° → Sagittarius → Jupiter. Same ruler!
        // When L1 == Lx, the analyzer should fall back to Moon as the querent significator.
        double ascendant = 345.0; // Pisces → Jupiter
        ChartResult chart = CreateBaseChart(ascendant: ascendant, category: "Career");

        HoraryPerfectionResult result = Analyze(chart, "Career");
        // L1 falls back to Moon since both houses (1 and 10) are ruled by Jupiter
        result.QuerentSignificator.Should().Be("Moon");
        result.QuesitedSignificator.Should().Be("Jupiter");
    }

    /// <summary>
    /// When the Moon rules both the 1st and the quesited house (the Ascendant and the 2nd cusp both in Cancer, as
    /// unequal houses allow), the Moon signifies the querent and Jupiter, exalted in Cancer, the quesited, and the
    /// notes say so (QI-1, ruling Q15). Until 1.3.3 the Moon was both significators.
    /// </summary>
    [Fact]
    public void SameRuler_Moon_QuesitedIsJupiter()
    {
        // Ascendant 5° Cancer and 2nd cusp 28° Cancer: the Moon rules both; Money → 2nd house.
        ChartResult chart = CreateBaseChart(
            ascendant: 95.0,
            category: "Money",
            cusps: [95.0, 118.0, 150.0, 185.0, 215.0, 245.0, 275.0, 298.0, 330.0, 5.0, 35.0, 65.0]);

        HoraryPerfectionResult result = Analyze(chart, "Money");

        result.QuerentSignificator.Should().Be("Moon");
        result.QuesitedSignificator.Should().Be("Jupiter");
        result.Notes.Should().Contain(
            "The Moon rules both the 1st and the 2nd house; the Moon signifies the querent and Jupiter, exalted in Cancer, the quesited.");
    }

    #endregion

    #region Direct Perfection

    [Fact]
    public void Analyze_ApplyingTrineBetweenSignificators_IsPerfected()
    {
        // Aries ASC → L1 = Mars, Relationship → 7th house cusp Libra → Lx = Venus
        double ascendant = 15.0; // Aries
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            aspects: [
                CreateAspect("Mars", 45.0, "Venus", 165.0, AspectType.Trine, 0.5, isApplying: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.IsPerfected.Should().BeTrue();
        result.PerfectionMechanism.Should().Be("Direct Aspect");
        result.PerfectionAspect.Should().Be("Trine");
    }

    [Fact]
    public void Analyze_SeparatingAspect_NotPerfectedByDirectAspect()
    {
        double ascendant = 15.0; // Aries → Mars
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            aspects: [
                CreateAspect("Mars", 45.0, "Venus", 165.0, AspectType.Trine, 0.5, isApplying: false),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        // Direct aspect is separating, so no perfection by that mechanism
        result.PerfectionMechanism.Should().NotBe("Direct Aspect");
    }

    [Fact]
    public void Analyze_MinorAspectApplying_NotPerfectedByDirectAspect()
    {
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            aspects: [
                CreateAspect("Mars", 45.0, "Venus", 75.0, AspectType.SemiSextile, 0.5, isApplying: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.PerfectionMechanism.Should().NotBe("Direct Aspect");
    }

    [Fact]
    public void Analyze_DirectAspectPerfectsOutOfSign_WithReception_StillPerfects()
    {
        // Mars (L1) at 27.3° Aquarius (+1.5°/day) closes on a sextile to Venus (Lx) at 29°
        // Aries (+1.25°/day), 1.7° wide: exact after 6.8 days, after Venus enters Taurus
        // (19.2 h) and Mars enters Pisces (43.2 h). Venus in Aries is in Mars's domicile, so
        // reception is present and the aspect still perfects.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: DefaultPositionsWith(
                (CelestialBodyId.Mars, 327.3, 11, 1.5),
                (CelestialBodyId.Venus, 29.0, 1, 1.25)),
            aspects: [
                CreateAspect("Mars", 327.3, "Venus", 29.0, AspectType.Sextile, 1.7, isApplying: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");

        result.IsPerfected.Should().BeTrue("reception lets an aspect that completes out of sign perfect");
        result.PerfectionMechanism.Should().Be("Direct Aspect");
        result.PerfectionTimeUtc.Should().BeCloseTo(FixtureInstantUtc.AddDays(6.8), TimeSpan.FromDays(0.001));
    }

    [Fact]
    public void Analyze_DirectAspectPerfectsOutOfSign_WithoutReception_DoesNotPerfect()
    {
        // The same sextile 60° further on: Mars at 27.3° Aries, Venus at 29° Gemini. Venus enters
        // Cancer after 19.2 h and Mars enters Taurus after 43.2 h, both before the aspect is exact
        // after 6.8 days, and neither is in the other's domicile or exaltation. It does not perfect.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: DefaultPositionsWith(
                (CelestialBodyId.Mars, 27.3, 1, 1.5),
                (CelestialBodyId.Venus, 89.0, 3, 1.25)),
            aspects: [
                CreateAspect("Mars", 27.3, "Venus", 89.0, AspectType.Sextile, 1.7, isApplying: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");

        result.IsPerfected.Should().BeFalse("the aspect completes only after both significators change sign");
        result.PerfectionTimeUtc.Should().BeNull();
        string note = result.Notes.Should().ContainSingle().Subject;
        note.Should().StartWith("The Mars\u2013Venus sextile is exact on 2025-04-03 ");
        note.Should().EndWith(
            "only after Venus enters Cancer on 2025-03-28 15:12 (America/New_York) and Mars enters Taurus on " +
            "2025-03-29 15:12 (America/New_York); with no reception between the significators it does not perfect.");
        result.Judgment.Should().Be("Uncertain");
        result.JudgmentNote.Should().Be(
            "The applying Mars\u2013Venus sextile perfects only after a significator leaves its sign, and there is no " +
            "clear denial \u2014 the matter may not come to fruition, or the outcome is unclear.");
    }

    #endregion

    #region Mutual Reception Perfection

    [Fact]
    public void Analyze_MutualReception_IsPerfected()
    {
        // Aries → L1 = Mars, Relationship → Lx = Venus. Mars in Taurus and Venus in Aries,
        // each in the other's domicile: a domicile reception perfects, and the Reception line
        // names its type.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: DefaultPositionsWith((CelestialBodyId.Venus, 10.0, 1, 0.8)),
            mutualReceptions: [
                new MutualReception
                {
                    Planet1 = "Mars",
                    Planet1Sign = ZodiacSign.Taurus,
                    Planet2 = "Venus",
                    Planet2Sign = ZodiacSign.Aries,
                    ReceptionType = "Domicile",
                },
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.IsPerfected.Should().BeTrue();
        result.PerfectionMechanism.Should().Be("Mutual Reception");
        result.ReceptionQuality.Should().Be("Mutual by domicile");
        result.ReceptionDetail.Should().Be("Mars in Venus's domicile (Taurus); Venus in Mars's domicile (Aries).");
    }

    [Fact]
    public void Analyze_TriplicityMutualReception_IsNotPerfected()
    {
        // A mutual reception by triplicity alone is not one of the types that perfect —
        // domicile, exaltation, or mixed domicile–exaltation — and nothing else perfects.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            mutualReceptions: [
                new MutualReception
                {
                    Planet1 = "Mars",
                    Planet1Sign = ZodiacSign.Taurus,
                    Planet2 = "Venus",
                    Planet2Sign = ZodiacSign.Virgo,
                    ReceptionType = "Triplicity",
                },
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.IsPerfected.Should().BeFalse("a triplicity reception does not perfect");
        result.PerfectionMechanism.Should().BeNull();
    }

    #endregion

    #region Translation of Light

    [Fact]
    public void Analyze_TranslationOfLight_IsPerfected()
    {
        // Aries ASC → L1 = Mars, Relationship → Lx = Venus
        // Moon (fast) separates from Mars, applies to Venus → Translation
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.5),
                CreatePosition(CelestialBodyId.Venus, 165.0, 8, speed: 0.8),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0), // Fast
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ],
            aspects: [
                // Moon separating from Mars
                CreateAspect("Moon", 100.0, "Mars", 45.0, AspectType.Sextile, 2.0, isApplying: false),
                // Moon applying to Venus
                CreateAspect("Moon", 100.0, "Venus", 165.0, AspectType.Sextile, 3.0, isApplying: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.IsPerfected.Should().BeTrue();
        result.PerfectionMechanism.Should().Be("Translation of Light");
        result.Translator.Should().Be("Moon");
    }

    #endregion

    #region Collection of Light

    [Fact]
    public void Analyze_CollectionOfLight_IsPerfected()
    {
        // Aries ASC → L1 = Mars, Relationship → Lx = Venus
        // Both Mars and Venus apply to Saturn (slow) → Collection
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.5),
                CreatePosition(CelestialBodyId.Venus, 165.0, 8, speed: 0.8),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.03),
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ],
            aspects: [
                // Mars applies to Saturn
                CreateAspect("Mars", 45.0, "Saturn", 330.0, AspectType.Square, 3.0, isApplying: true),
                // Venus applies to Saturn
                CreateAspect("Venus", 165.0, "Saturn", 330.0, AspectType.Trine, 3.0, isApplying: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.IsPerfected.Should().BeTrue();
        result.PerfectionMechanism.Should().Be("Collection of Light");
        result.Collector.Should().Be("Saturn");
    }

    #endregion

    #region Intermediaries — Traditional, Completing, Unprohibited

    [Fact]
    public void Analyze_OnlyNonTraditionalBodiesReceiveBothApplications_IsNotCollection()
    {
        // Mars (L1, +0.5°/day) and Venus (Lx, +0.8°/day) both apply to Uranus at 290° and to
        // the True Node at 50°, each slower than either significator, and to no traditional
        // planet. Neither is one of the seven, so neither collects.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.5),
                CreatePosition(CelestialBodyId.Venus, 165.0, 8, speed: 0.8),
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
                CreatePosition(CelestialBodyId.Uranus, 290.0, 10, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 50.0, 2, speed: -0.05),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ],
            aspects: [
                CreateAspect("Mars", 45.0, "Uranus", 290.0, AspectType.Trine, 5.0, isApplying: true),
                CreateAspect("Venus", 165.0, "Uranus", 290.0, AspectType.Trine, 5.0, isApplying: true),
                CreateAspect("Mars", 45.0, "TrueNode", 50.0, AspectType.Conjunction, 5.0, isApplying: true),
                CreateAspect("Venus", 165.0, "TrueNode", 50.0, AspectType.Trine, 5.0, isApplying: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");

        result.IsPerfected.Should().BeFalse("only the traditional seven may collect");
        result.PerfectionMechanism.Should().BeNull();
        result.Collector.Should().BeNull();
        result.Notes.Should().BeEmpty();
    }

    [Fact]
    public void Analyze_TranslatorsApplicationTurnsAround_TranslationDoesNotPerfect()
    {
        // Mercury (+1.2°/day, faster than Mars and Venus) separates from Mars's sextile and
        // applies to Venus's, 3° wide. It slows by 0.1°/day², so it gains on Venus at
        // 0.4 − 0.1t°/day: the orb 3 − 0.4t + 0.05t² bottoms out at 2.20° after 4 days and the
        // translation never completes. Nothing else perfects.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 39.0, 1, speed: 0.5),
                CreatePosition(CelestialBodyId.Venus, 165.0, 8, speed: 0.8),
                CreatePosition(CelestialBodyId.Mercury, 102.0, 4, speed: 1.2),
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ],
            aspects: [
                CreateAspect("Mercury", 102.0, "Mars", 39.0, AspectType.Sextile, 3.0, isApplying: false),
                CreateAspect("Mercury", 102.0, "Venus", 165.0, AspectType.Sextile, 3.0, isApplying: true),
            ]);
        SyntheticSpeedEphemeris ephemeris = EphemerisFor(chart)
            .WithBody(CelestialBodyId.Mercury, 1.2, -0.1, 102.0);

        HoraryPerfectionResult result = HoraryPerfectionAnalyzer.Analyze(chart, "Relationship", chart.JulianDay, ephemeris);

        result.IsPerfected.Should().BeFalse("the translator's application turns around before exact");
        result.Translator.Should().BeNull();
        result.Notes.Should().ContainSingle().Which.Should().Be(
            "The translation of light via Mercury does not complete: Mercury's sextile to Venus turns around at " +
            "2\u00b012\u2032 on 2025-03-31 20:00 (America/New_York).");
        result.Judgment.Should().Be("Uncertain");
    }

    [Fact]
    public void Analyze_MarsExactToSignificatorBeforeCollectionCompletes_IsProhibition()
    {
        // Libra rising → L1 Venus; Career → 10th in Cancer → Lx Moon. Both apply to Saturn at
        // 350°: the Moon's trine is exact after 0.39 days, Venus's opposition after 6.67, so the
        // collection completes 2025-04-03 16:00 UT. Mars (+1.3°/day) reaches the square to Venus
        // after 2 days, before that — a prohibition. Saturn, the collector, does not prohibit.
        double ascendant = 195.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Career",
            positions: [
                CreatePosition(CelestialBodyId.Venus, 165.0, 12, speed: 0.8),
                CreatePosition(CelestialBodyId.Moon, 105.0, 10, speed: 13.0),
                CreatePosition(CelestialBodyId.Saturn, 350.0, 6, speed: 0.05),
                CreatePosition(CelestialBodyId.Mars, 74.0, 9, speed: 1.3),
                CreatePosition(CelestialBodyId.Sun, 280.0, 4, speed: 1.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 4, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 8, speed: 0.1),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 8, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 6, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 4, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 7, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 7, speed: 0.05),
            ],
            aspects: [
                CreateAspect("Venus", 165.0, "Saturn", 350.0, AspectType.Opposition, 5.0, isApplying: true),
                CreateAspect("Moon", 105.0, "Saturn", 350.0, AspectType.Trine, 5.0, isApplying: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Career");

        result.QuerentSignificator.Should().Be("Venus");
        result.QuesitedSignificator.Should().Be("Moon");
        result.IsPerfected.Should().BeTrue();
        result.PerfectionMechanism.Should().Be("Collection of Light");
        result.Collector.Should().Be("Saturn");
        result.DenialReasons.Should().ContainSingle().Which.Should().Be(
            "Prohibition by Mars — Mars's square to Venus (L1) is exact on 2025-03-29 20:00 (America/New_York), " +
            "before the collection of light by Saturn completes on 2025-04-03 12:00 (America/New_York).");
        HoraryProhibitionEvent prohibition = result.ProhibitionEvents.Should().ContainSingle().Subject;
        prohibition.Malefic.Should().Be("Mars");
        prohibition.Target.Should().Be("Venus");
        (prohibition.ExactTimeUtc - FixtureInstantUtc).TotalDays.Should().BeApproximately(2.0, 0.001);
        result.Judgment.Should().Be("Uncertain");
    }

    #endregion

    #region Antiscia Perfection

    [Fact]
    public void Analyze_AntisciaConnection_IsPerfected()
    {
        // Mars at 45° and Venus at 134.5°: the longitudes sum to 179.5°, half a degree short of
        // the antiscion's 180°, and their speeds (+0.5 and +0.8°/day) carry the sum toward it.
        // An applying antiscion perfects.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: DefaultPositionsWith((CelestialBodyId.Venus, 134.5, 5, 0.8)),
            antisciaConnections: [
                new AntisciaConnection
                {
                    Planet1 = "Mars",
                    Planet2 = "Venus",
                    Type = "Antiscia",
                    Orb = 0.5,
                },
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.IsPerfected.Should().BeTrue();
        result.PerfectionMechanism.Should().Be("Antiscia");
    }

    [Fact]
    public void Analyze_SeparatingAntiscion_IsTestimonyNotPerfection()
    {
        // The twin of the applying case: Venus at 135.5° puts the sum at 180.5°, past the
        // antiscion, and the same speeds carry it further away. A separating antiscion is
        // testimony, not perfection.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: DefaultPositionsWith((CelestialBodyId.Venus, 135.5, 5, 0.8)),
            antisciaConnections: [
                new AntisciaConnection
                {
                    Planet1 = "Mars",
                    Planet2 = "Venus",
                    Type = "Antiscia",
                    Orb = 0.5,
                },
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.IsPerfected.Should().BeFalse("the antiscion is separating");
        result.PerfectionMechanism.Should().BeNull();
        result.Notes.Should().ContainSingle().Which.Should().Be(
            "The Mars\u2013Venus antiscion (orb 0\u00b030\u2032) is separating: testimony, not perfection.");
    }

    #endregion

    #region Moon VoC Denial

    [Fact]
    public void Analyze_MoonVoC_IsDenied()
    {
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Career",
            moonCondition: new MoonConditionInfo
            {
                Phase = MoonPhaseType.FirstQuarter,
                PhaseAngle = 90.0,
                IsVoidOfCourse = true,
                FirstApplyingPlanet = null,
                FirstApplyingAspectType = null,
                FirstApplyingOrb = null,
                PhasePosition = "mid",
                IsViaCombusta = false,
                MoonSign = ZodiacSign.Cancer,
                MoonLongitude = 100.0,
            });

        HoraryPerfectionResult result = Analyze(chart, "Career");
        result.IsDenied.Should().BeTrue();
        result.DenialReasons.Should().Contain(r => r.Contains("Moon Void of Course"));
    }

    #endregion

    #region Combustion Denial

    [Fact]
    public void Analyze_L1Combust_IsDenied()
    {
        // Aries ASC → L1 = Mars, Career → house 10 (Capricorn cusp → Saturn)
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Career",
            planetConditions: [
                CreateCondition("Mars", isCombust: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Career");
        result.IsDenied.Should().BeTrue();
        result.DenialReasons.Should().Contain(r => r.Contains("combust"));
    }

    #endregion

    #region Besiegement Denial

    [Fact]
    public void Analyze_LxBesieged_IsDenied()
    {
        // Aries ASC → L1 = Mars, Career → 10th house cusp Capricorn → Lx = Saturn
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Career",
            planetConditions: [
                CreateCondition("Saturn", isBesieged: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Career");
        result.IsDenied.Should().BeTrue();
        result.DenialReasons.Should().Contain(r => r.Contains("besieged"));
    }

    #endregion

    #region Prohibition

    [Fact]
    public void Analyze_ProhibitionByMalefic_AddsDenialReason()
    {
        // Aries ASC → L1 = Mars, Relationship → Lx = Venus. Linear motion stated here:
        // Mars at 45° (+0.5°/day) applies to a trine with Venus at 170° (+0.1°/day),
        // orb 5°, closing at 0.4°/day → exact after 12.5 days. Saturn at 137°
        // (+0.05°/day, a real Saturn speed) is 92° from Mars: Mars reaches the exact
        // square to Saturn after 2/0.45 = 4.4 days, before its trine to Venus perfects.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.4),
                CreatePosition(CelestialBodyId.Venus, 170.0, 8, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 137.0, 7, speed: 0.05),
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ],
            aspects: [
                CreateAspect("Mars", 45.0, "Venus", 170.0, AspectType.Trine, 5.0, isApplying: true),
            ]);
        SyntheticSpeedEphemeris ephemeris = EphemerisFor(chart)
            .WithBody(CelestialBodyId.Mars, 0.5, 0.0, 45.0); // Mars moves at +0.5°/day in the sky

        HoraryPerfectionResult result = HoraryPerfectionAnalyzer.Analyze(chart, "Relationship", chart.JulianDay, ephemeris);

        result.IsPerfected.Should().BeTrue();
        result.PerfectionMechanism.Should().Be("Direct Aspect");
        (result.PerfectionTimeUtc!.Value - FixtureInstantUtc).TotalDays.Should().BeApproximately(12.5, 0.01);
        string reason = result.DenialReasons.Should().ContainSingle().Subject;
        reason.Should().StartWith("Prohibition by Saturn").And.Contain("square to Mars (L1)");
        HoraryProhibitionEvent evt = result.ProhibitionEvents.Should().ContainSingle().Subject;
        evt.Malefic.Should().Be("Saturn");
        evt.Target.Should().Be("Mars");
        evt.AspectType.Should().Be("Square");
        (evt.ExactTimeUtc - FixtureInstantUtc).TotalDays.Should().BeApproximately(2.0 / 0.45, 0.01);
        result.Judgment.Should().Be("Uncertain");
        result.JudgmentNote.Should().Contain("Prohibition by Saturn complicates the outcome");
    }

    #endregion

    #region Refranation

    [Fact]
    public void Analyze_L1StationsAndTheApplicationFails_IsRefranationAndJudgedNo()
    {
        // Aries ASC → L1 = Mars, Relationship → Lx = Venus. Mars at 45° applies to a
        // trine with Venus at 167° (orb 2°). In the sky Mars moves +0.10°/day and
        // decelerates 0.05°/day², so it stations retrograde after 2 days having
        // advanced only 0.1°; Venus drifts back toward it at −0.01°/day. The orb
        // 2 − 0.11t + 0.025t² bottoms out at 1.879° after 2.2 days and never reaches
        // zero, so Mars's station falls inside the failing application's window.
        // Nothing else perfects and nothing else denies.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.10),
                CreatePosition(CelestialBodyId.Venus, 167.0, 8, speed: -0.01),
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ],
            aspects: [
                CreateAspect("Mars", 45.0, "Venus", 167.0, AspectType.Trine, 2.0, isApplying: true),
            ]);
        SyntheticSpeedEphemeris ephemeris = EphemerisFor(chart)
            .WithBody(CelestialBodyId.Mars, 0.10, -0.05, 45.0);

        HoraryPerfectionResult result = HoraryPerfectionAnalyzer.Analyze(chart, "Relationship", chart.JulianDay, ephemeris);

        result.IsPerfected.Should().BeFalse("the application turns around at 1.88° before exact");
        result.PerfectionTimeUtc.Should().BeNull();
        result.ApplicationClosestOrb.Should().BeApproximately(1.879, 0.002);
        (result.ApplicationFailsTimeUtc!.Value - FixtureInstantUtc).TotalDays.Should().BeApproximately(2.2, 1.0 / 24.0);
        HorarySignificatorStation station = result.SignificatorStations.Should().ContainSingle().Subject;
        station.Significator.Should().Be("Mars");
        station.Label.Should().Be("L1");
        station.IsRetrogradeStation.Should().BeTrue();
        (station.TimeUtc - FixtureInstantUtc).TotalDays.Should().BeApproximately(2.0, 0.001);
        string reason = result.DenialReasons.Should().ContainSingle().Subject;
        reason.Should().StartWith("Refranation \u2014 Mars (L1) stations retrograde on ");
        reason.Should().Contain("the Mars\u2013Venus trine then fails, closest 1\u00b052\u2032 on ");
        result.Notes.Should().BeEmpty();
        result.Judgment.Should().Be("No");
        result.JudgmentNote.Should().StartWith("Refranation \u2014 the chart does not show");
    }

    [Fact]
    public void Analyze_L1StationsButTheAspectStillPerfects_IsANoteNotADenial()
    {
        // Same geometry, but Venus retrogrades toward Mars at −0.5°/day: Mars stations
        // retrograde after 2 days, yet the trine still perfects (the orb shrinks at
        // ~0.6°/day, reaching zero after about 3.5 days).
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.10),
                CreatePosition(CelestialBodyId.Venus, 167.0, 8, speed: -0.5),
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ],
            aspects: [
                CreateAspect("Mars", 45.0, "Venus", 167.0, AspectType.Trine, 2.0, isApplying: true),
            ]);
        SyntheticSpeedEphemeris ephemeris = EphemerisFor(chart)
            .WithBody(CelestialBodyId.Mars, 0.10, -0.05, 45.0);

        HoraryPerfectionResult result = HoraryPerfectionAnalyzer.Analyze(chart, "Relationship", chart.JulianDay, ephemeris);

        result.IsPerfected.Should().BeTrue();
        result.PerfectionMechanism.Should().Be("Direct Aspect");
        result.DenialReasons.Should().BeEmpty();
        result.SignificatorStations.Should().ContainSingle(st => st.Significator == "Mars" && st.IsRetrogradeStation);
        string note = result.Notes.Should().ContainSingle().Subject;
        note.Should().StartWith("Mars (L1) stations retrograde on ").And.Contain("the Mars\u2013Venus trine still perfects on ");
        result.Judgment.Should().Be("Yes");
    }

    #endregion

    #region Application unresolved (the 120-day cap)

    /// <summary>
    /// Relationship \u2192 L1 Mars, Lx Venus. Mars at 45\u00b0 (+0.50\u00b0/day) closes on a trine to
    /// Venus at 167\u00b0 (+0.49\u00b0/day), 2\u00b0 wide, at a steady 0.01\u00b0/day: the orb is still 0.80\u00b0
    /// when the finder's 120-day cap is reached, neither exact nor turned around. Neither
    /// significator stations, and nothing else perfects or denies.
    /// </summary>
    private static ChartResult CreateChartStillApplyingAtTheCap() => CreateBaseChart(
        ascendant: 15.0,
        category: "Relationship",
        positions: [
            CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.5),
            CreatePosition(CelestialBodyId.Venus, 167.0, 8, speed: 0.49),
            CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
            CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
            CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
            CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
            CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
            CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
            CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
            CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
            CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
            CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
        ],
        aspects: [
            CreateAspect("Mars", 45.0, "Venus", 167.0, AspectType.Trine, 2.0, isApplying: true),
        ]);

    [Fact]
    public void Analyze_ApplicationStillApplyingAtTheCap_IsUnresolvedAndJudgedUncertain()
    {
        HoraryPerfectionResult result = Analyze(CreateChartStillApplyingAtTheCap(), "Relationship");

        result.ApplicationCapped.Should().BeTrue();
        result.IsPerfected.Should().BeFalse();
        result.PerfectionTimeUtc.Should().BeNull();
        result.ApplicationFailsTimeUtc.Should().BeNull("the application did not turn around");
        result.ApplicationClosestOrb.Should().BeNull();
        result.SignificatorStations.Should().BeEmpty();
        result.DenialReasons.Should().BeEmpty();
        // The closest orb at the cap, 2 − 0.01 × 120 = 0.8°, comes out a hair below 0.8 in the
        // analyzer's arithmetic; FormatOrb's guard on its floor prints the constructed 0.8° as
        // 0°48′ (branch O-D3 R1a, 1.3.4), where the plain floor printed 0°47′.
        result.Notes.Should().ContainSingle().Which.Should().Be(
            "The Mars\u2013Venus trine is still applying 120 days after the chart " +
            "(closest 0\u00b048\u2032 on 2025-07-25 20:00 (America/New_York)); neither perfection nor refranation can be judged.");
        result.Judgment.Should().Be("Uncertain");
        result.JudgmentNote.Should().Be(
            "The Mars\u2013Venus trine is still applying 120 days after the chart, and there is no clear denial " +
            "\u2014 the matter may not come to fruition, or the outcome is unclear.");
    }

    [Fact]
    public void ToMarkdown_ApplicationStillApplyingAtTheCap_PrintsApplicationUnresolved()
    {
        ChartResult chart = CreateChartStillApplyingAtTheCap();
        chart.HoraryPerfection = Analyze(chart, "Relationship");

        string markdown = ChartMarkdownFormatter.ToMarkdown(chart);

        markdown.Should().Contain("**Application unresolved:** Mars\u2013Venus trine still applying 120 days after the chart  ");
        markdown.Should().NotContain("**Application fails:**");
    }

    #endregion

    #region Reception Quality

    [Fact]
    public void Analyze_MutualDomicileReception_ReceptionIsMutual()
    {
        // L1 = Mars in Taurus (Venus' domicile), Lx = Venus in Aries (Mars' domicile)
        double ascendant = 15.0; // Aries → Mars
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 40.0, 2, speed: 0.5),     // Taurus (Venus' domicile)
                CreatePosition(CelestialBodyId.Venus, 10.0, 1, speed: 0.8),    // Aries (Mars' domicile)
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 200.0, 7, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.ReceptionQuality.Should().Be("Mutual by domicile");
    }

    [Fact]
    public void Analyze_NoDignityRelationship_ReceptionIsNeutral()
    {
        // L1 = Mars in Leo (Sun's domicile), Lx = Venus in Virgo (Mercury's domicile)
        double ascendant = 15.0; // Aries → Mars
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 130.0, 5, speed: 0.5),    // Leo
                CreatePosition(CelestialBodyId.Venus, 170.0, 6, speed: 0.8),   // Virgo
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 200.0, 7, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.ReceptionQuality.Should().Be("Neutral");
    }

    /// <summary>
    /// A mutual reception between L1 and Lx is named by its type whatever the perfection (QI-2: E1-1, E1-5). In each
    /// case a direct aspect perfects, within sign, and the pair's mutual reception is the chart context's. The expected
    /// lines come from the dignity tables (Tier A): domicile rulers and exaltations, the kinds named from the type, so
    /// Venus in Virgo with Mercury in Pisces is a reception by exaltation (each in the other's exaltation) even though
    /// Venus is also in Mercury's domicile. Until 1.3.3 an exaltation or mixed reception under a direct aspect printed
    /// "Neutral" with no Detail, a domicile one "Mutual", and a mechanism Detail named each side domicile-first.
    /// </summary>
    /// <param name="reception">The case: "exaltation", "mixed", "domicile" or "venus-mercury".</param>
    /// <param name="quality">The expected Reception quality.</param>
    /// <param name="detail">The expected Reception detail.</param>
    [Theory]
    [InlineData("exaltation", "Mutual by exaltation",
        "Mars in Jupiter's exaltation (Cancer); Jupiter in Mars's exaltation (Capricorn).")]
    [InlineData("mixed", "Mutual by domicile and exaltation",
        "Mars in Venus's domicile (Taurus); Venus in Mars's exaltation (Capricorn).")]
    [InlineData("domicile", "Mutual by domicile",
        "Mars in Venus's domicile (Libra); Venus in Mars's domicile (Aries).")]
    [InlineData("venus-mercury", "Mutual by exaltation",
        "Venus in Mercury's exaltation (Virgo); Mercury in Venus's exaltation (Pisces).")]
    public void Reception_IsNamed_WhateverThePerfection(string reception, string quality, string detail)
    {
        ChartResult chart = reception switch
        {
            // Aries → L1 Mars; Travel → 9th cusp Sagittarius → Lx Jupiter. Mars in Cancer (Jupiter's exaltation)
            // opposite Jupiter in Capricorn (Mars's exaltation), 1° applying: exact in 2.5 days, both in sign.
            "exaltation" => CreateBaseChart(
                ascendant: 15.0,
                category: "Travel",
                positions: DefaultPositionsWith((CelestialBodyId.Mars, 100.0, 4, 0.5), (CelestialBodyId.Jupiter, 281.0, 10, 0.1)),
                aspects: [CreateAspect("Mars", 100.0, "Jupiter", 281.0, AspectType.Opposition, 1.0, isApplying: true)],
                mutualReceptions: [Reception("Mars", ZodiacSign.Cancer, "Jupiter", ZodiacSign.Capricorn, "Exaltation")]),

            // Aries → L1 Mars; Relationship → 7th cusp Libra → Lx Venus. Mars in Taurus (Venus's domicile) trine Venus
            // in Capricorn (Mars's exaltation), 1° applying: exact in 3.3 days, both in sign.
            "mixed" => CreateBaseChart(
                ascendant: 15.0,
                category: "Relationship",
                positions: DefaultPositionsWith((CelestialBodyId.Venus, 284.0, 10, 0.8)),
                aspects: [CreateAspect("Mars", 45.0, "Venus", 284.0, AspectType.Trine, 1.0, isApplying: true)],
                mutualReceptions: [Reception("Mars", ZodiacSign.Taurus, "Venus", ZodiacSign.Capricorn, "Mixed (Domicile-Exaltation)")]),

            // Aries → L1 Mars; Relationship → Lx Venus. Mars in Libra (Venus's domicile) opposite Venus in Aries (Mars's
            // domicile), 1° applying: exact in 3.3 days, both in sign.
            "domicile" => CreateBaseChart(
                ascendant: 15.0,
                category: "Relationship",
                positions: DefaultPositionsWith((CelestialBodyId.Mars, 195.0, 7, 0.5), (CelestialBodyId.Venus, 14.0, 1, 0.8)),
                aspects: [CreateAspect("Mars", 195.0, "Venus", 14.0, AspectType.Opposition, 1.0, isApplying: true)],
                mutualReceptions: [Reception("Mars", ZodiacSign.Libra, "Venus", ZodiacSign.Aries, "Domicile")]),

            // Taurus → L1 Venus; Money → 2nd cusp Gemini → Lx Mercury. Venus in Virgo (Mercury's domicile and
            // exaltation) opposite Mercury in Pisces (Venus's exaltation), 1° applying: exact in 2.5 days, both in sign.
            _ => CreateBaseChart(
                ascendant: 45.0,
                category: "Money",
                positions: DefaultPositionsWith((CelestialBodyId.Venus, 170.0, 5, 0.8), (CelestialBodyId.Mercury, 349.0, 11, 1.2)),
                aspects: [CreateAspect("Venus", 170.0, "Mercury", 349.0, AspectType.Opposition, 1.0, isApplying: true)],
                mutualReceptions: [Reception("Venus", ZodiacSign.Virgo, "Mercury", ZodiacSign.Pisces, "Exaltation")]),
        };

        HoraryPerfectionResult result = Analyze(chart, chart.Input.HoraryCategory ?? "General");

        result.PerfectionMechanism.Should().Be("Direct Aspect");
        result.ReceptionQuality.Should().Be(quality);
        result.ReceptionDetail.Should().Be(detail);
    }

    /// <summary>A mutual reception as the chart context lists it.</summary>
    /// <param name="planet1">The first planet.</param>
    /// <param name="sign1">The sign it occupies.</param>
    /// <param name="planet2">The second planet.</param>
    /// <param name="sign2">The sign it occupies.</param>
    /// <param name="type">The reception's type, as <c>ChartContextService</c> names it.</param>
    /// <returns>The reception.</returns>
    private static MutualReception Reception(string planet1, ZodiacSign sign1, string planet2, ZodiacSign sign2, string type) =>
        new()
        {
            Planet1 = planet1,
            Planet1Sign = sign1,
            Planet2 = planet2,
            Planet2Sign = sign2,
            ReceptionType = type,
        };

    #endregion

    #region Moon Testimony

    [Fact]
    public void Analyze_MoonAppliesToQuesitedSignificator_TestimonyAligns()
    {
        // Aries ASC → L1 = Mars, Relationship → Lx = Venus
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            moonCondition: new MoonConditionInfo
            {
                Phase = MoonPhaseType.WaxingCrescent,
                PhaseAngle = 45.0,
                IsVoidOfCourse = false,
                FirstApplyingPlanet = "Venus",
                FirstApplyingAspectType = AspectType.Trine,
                FirstApplyingOrb = 3.0,
                PhasePosition = "early",
                IsViaCombusta = false,
                MoonSign = ZodiacSign.Cancer,
                MoonLongitude = 100.0,
            });

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.MoonTestimony.Should().Contain("querent's instincts align");
    }

    [Fact]
    public void Analyze_MoonVoidOfCourse_TestimonyStandstill()
    {
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Career",
            moonCondition: new MoonConditionInfo
            {
                Phase = MoonPhaseType.FullMoon,
                PhaseAngle = 180.0,
                IsVoidOfCourse = true,
                FirstApplyingPlanet = null,
                FirstApplyingAspectType = null,
                FirstApplyingOrb = null,
                PhasePosition = "mid",
                IsViaCombusta = false,
                MoonSign = ZodiacSign.Libra,
                MoonLongitude = 190.0,
            });

        HoraryPerfectionResult result = Analyze(chart, "Career");
        result.MoonTestimony.Should().Contain("standstill");
        result.MoonVoC.Should().BeTrue();
    }

    [Fact]
    public void Analyze_MoonAppliesToBenefic_SupportiveTestimony()
    {
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Career",
            moonCondition: new MoonConditionInfo
            {
                Phase = MoonPhaseType.WaxingCrescent,
                PhaseAngle = 45.0,
                IsVoidOfCourse = false,
                FirstApplyingPlanet = "Jupiter",
                FirstApplyingAspectType = AspectType.Trine,
                FirstApplyingOrb = 2.0,
                PhasePosition = "early",
                IsViaCombusta = false,
                MoonSign = ZodiacSign.Cancer,
                MoonLongitude = 100.0,
            });

        HoraryPerfectionResult result = Analyze(chart, "Career");
        result.MoonTestimony.Should().Contain("supportive conditions");
    }

    [Fact]
    public void Analyze_MoonAppliesToMalefic_ObstaclesTestimony()
    {
        // Use Relationship category so Lx = Venus (not Saturn).
        // Moon first applies to Saturn (malefic, not Lx) → obstacles testimony.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            moonCondition: new MoonConditionInfo
            {
                Phase = MoonPhaseType.WaningGibbous,
                PhaseAngle = 270.0,
                IsVoidOfCourse = false,
                FirstApplyingPlanet = "Saturn",
                FirstApplyingAspectType = AspectType.Square,
                FirstApplyingOrb = 4.0,
                PhasePosition = "late",
                IsViaCombusta = false,
                MoonSign = ZodiacSign.Libra,
                MoonLongitude = 190.0,
            });

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.MoonTestimony.Should().Contain("obstacles");
    }

    #endregion

    #region Judgment

    [Fact]
    public void Analyze_PerfectedNoDenial_JudgmentYes()
    {
        // Mars at 45° (+0.5°/day) and Venus at 163° (+0.8°/day): 118° apart, so Venus
        // pulling ahead opens the pair to the exact trine after 2/0.3 = 6.67 days.
        // Saturn at 330° (+0.05°/day) makes no exact aspect to either in that time.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.5),
                CreatePosition(CelestialBodyId.Venus, 163.0, 8, speed: 0.8),
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ],
            aspects: [
                CreateAspect("Mars", 45.0, "Venus", 163.0, AspectType.Trine, 2.0, isApplying: true),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Relationship");

        result.IsPerfected.Should().BeTrue();
        (result.PerfectionTimeUtc!.Value - FixtureInstantUtc).TotalDays.Should().BeApproximately(2.0 / 0.3, 0.01);
        result.DenialReasons.Should().BeEmpty();
        result.Judgment.Should().Be("Yes");
    }

    [Fact]
    public void Analyze_NotPerfectedWithVoC_JudgmentNo()
    {
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Career",
            moonCondition: new MoonConditionInfo
            {
                Phase = MoonPhaseType.WaningCrescent,
                PhaseAngle = 315.0,
                IsVoidOfCourse = true,
                FirstApplyingPlanet = null,
                FirstApplyingAspectType = null,
                FirstApplyingOrb = null,
                PhasePosition = "late",
                IsViaCombusta = false,
                MoonSign = ZodiacSign.Gemini,
                MoonLongitude = 80.0,
            });

        HoraryPerfectionResult result = Analyze(chart, "Career");
        result.Judgment.Should().Be("No");
    }

    [Fact]
    public void Analyze_PerfectedButDenied_JudgmentUncertain()
    {
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            aspects: [
                CreateAspect("Mars", 45.0, "Venus", 165.0, AspectType.Trine, 2.0, isApplying: true),
            ],
            moonCondition: new MoonConditionInfo
            {
                Phase = MoonPhaseType.WaningCrescent,
                PhaseAngle = 315.0,
                IsVoidOfCourse = true,
                FirstApplyingPlanet = null,
                FirstApplyingAspectType = null,
                FirstApplyingOrb = null,
                PhasePosition = "late",
                IsViaCombusta = false,
                MoonSign = ZodiacSign.Gemini,
                MoonLongitude = 80.0,
            });

        HoraryPerfectionResult result = Analyze(chart, "Relationship");
        result.Judgment.Should().Be("Uncertain");
    }

    [Fact]
    public void Analyze_NoPerfectionNoDenial_JudgmentUncertain()
    {
        // Career → Lx = Saturn, here crawling at +0.005°/day on the eve of a station.
        // With no applying aspect between Mars and Saturn there is nothing a station
        // could refranate, so the slow Saturn is not a denial and the judgment stays
        // Uncertain — the case the old speed rule turned into "No".
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Career",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.5),
                CreatePosition(CelestialBodyId.Venus, 165.0, 8, speed: 0.8),
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ]);

        HoraryPerfectionResult result = Analyze(chart, "Career");

        result.IsPerfected.Should().BeFalse();
        result.DenialReasons.Should().BeEmpty();
        result.Judgment.Should().Be("Uncertain");
        result.JudgmentNote.Should().Contain("No applying aspect");
    }

    [Fact]
    public void Analyze_ApplicationFailsNoDenial_JudgmentNoteSaysItTurnsAround()
    {
        // Relationship → L1 Mars, Lx Venus. Mars at 45° (+0.6°/day, slowing by 0.05°/day²)
        // closes on a trine to Venus at 167° (+0.41°/day), 2° wide. The orb
        // 2 − 0.19t + 0.025t² bottoms out at 1.639° after 3.8 days, between hourly samples,
        // and never reaches zero. Mars does not station until day 12 — after the 10.5 days
        // the chart's 0.19°/day would take to close the 2° — and Venus never does, so the
        // failure is a note, not a refranation, and nothing else perfects or denies.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: [
                CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.6),
                CreatePosition(CelestialBodyId.Venus, 167.0, 8, speed: 0.41),
                CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
                CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
                CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
                CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
                CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
                CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
                CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
                CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
                CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
                CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
            ],
            aspects: [
                CreateAspect("Mars", 45.0, "Venus", 167.0, AspectType.Trine, 2.0, isApplying: true),
            ]);
        SyntheticSpeedEphemeris ephemeris = EphemerisFor(chart)
            .WithBody(CelestialBodyId.Mars, 0.6, -0.05, 45.0);

        HoraryPerfectionResult result = HoraryPerfectionAnalyzer.Analyze(chart, "Relationship", chart.JulianDay, ephemeris);

        result.IsPerfected.Should().BeFalse("the application turns around at 1.639° before exact");
        result.ApplicationClosestOrb.Should().BeApproximately(1.639, 0.0001);
        result.ApplicationFailsTimeUtc.Should().BeCloseTo(FixtureInstantUtc.AddDays(3.8), TimeSpan.FromMinutes(1));
        result.SignificatorStations.Should().BeEmpty();
        result.DenialReasons.Should().BeEmpty();
        result.Notes.Should().ContainSingle(n => n.Contains("with neither significator stationing"));
        result.Judgment.Should().Be("Uncertain");
        result.JudgmentNote.Should().Be(
            "The applying Mars–Venus trine turns around without completing, and there is no clear denial " +
            "— the matter may not come to fruition, or the outcome is unclear.");
    }

    [Fact]
    public void Analyze_StationAfterTurnaroundBeforeLinearEstimate_IsRefranation()
    {
        // The same trine with Mars slowing faster (0.097°/day²). The orb 2 − 0.19t + 0.0485t²
        // turns around at 1.81° after 1.9588 days (03-29 23:00:37 UT) and Mars stations
        // retrograde at day 6.1856 (04-03 04:27:13 UT) — after the turnaround, but before the
        // linear perfection estimate: 2° closed at the chart's 0.19°/day takes 10.5263 days
        // (04-07 12:37:54 UT). The failure is refranation, naming the station.
        double ascendant = 15.0;
        ChartResult chart = CreateBaseChart(
            ascendant: ascendant,
            category: "Relationship",
            positions: DefaultPositionsWith(
                (CelestialBodyId.Mars, 45.0, 2, 0.6),
                (CelestialBodyId.Venus, 167.0, 8, 0.41)),
            aspects: [
                CreateAspect("Mars", 45.0, "Venus", 167.0, AspectType.Trine, 2.0, isApplying: true),
            ]);
        SyntheticSpeedEphemeris ephemeris = EphemerisFor(chart)
            .WithBody(CelestialBodyId.Mars, 0.6, -0.097, 45.0);

        HoraryPerfectionResult result = HoraryPerfectionAnalyzer.Analyze(chart, "Relationship", chart.JulianDay, ephemeris);

        result.IsPerfected.Should().BeFalse("the application turns around at 1.81° before exact");
        result.DenialReasons.Should().ContainSingle().Which.Should().Be(
            "Refranation \u2014 Mars (L1) stations retrograde on 2025-04-03 00:27 (America/New_York), after the " +
            "Mars\u2013Venus trine turns around at 1\u00b048\u2032 on 2025-03-29 19:00 (America/New_York) but before its " +
            "linear perfection estimate of 2025-04-07 08:37 (America/New_York).");
        result.Notes.Should().BeEmpty("the turnaround note is retired once a station is found");
        HorarySignificatorStation station = result.SignificatorStations.Should().ContainSingle().Subject;
        station.Significator.Should().Be("Mars");
        station.IsRetrogradeStation.Should().BeTrue();
        station.TimeUtc.Should().BeCloseTo(FixtureInstantUtc.AddDays(0.6 / 0.097), TimeSpan.FromSeconds(2));
        result.Judgment.Should().Be("No");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// The UTC instant of <see cref="CreateBaseChart"/>'s <c>JulianDay</c>
    /// (2460762.5 = 2025-03-28 00:00 UT). Every time field of the fixture names
    /// this one instant: 20:00 EDT (UTC−4) on 2025-03-27 in America/New_York.
    /// Timing assertions anchor on it because the analyzer and the synthetic
    /// ephemeris work from the Julian Day.
    /// </summary>
    private static readonly DateTime FixtureInstantUtc = new(2025, 3, 28, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Runs the analyzer on a hand-built chart with a synthetic ephemeris in
    /// which every body moves in a straight line from the longitude and speed
    /// the chart gives it, so the chart's own positions are what the ephemeris
    /// returns at the chart instant.
    /// </summary>
    private static HoraryPerfectionResult Analyze(ChartResult chart, string category) =>
        HoraryPerfectionAnalyzer.Analyze(chart, category, chart.JulianDay, EphemerisFor(chart));

    /// <summary>Builds a synthetic ephemeris that reproduces the chart's positions at its instant.</summary>
    private static SyntheticSpeedEphemeris EphemerisFor(ChartResult chart)
    {
        SyntheticSpeedEphemeris ephemeris = new(chart.JulianDay);
        foreach (PlanetPosition p in chart.Positions)
            ephemeris.WithBody(p.Body, p.SpeedInLongitude, 0.0, p.Longitude);
        return ephemeris;
    }

    /// <summary>
    /// Builds a horary chart with the given positions, aspects and chart context, and equal houses from
    /// <paramref name="ascendant"/>, or the given <paramref name="cusps"/> when they are supplied.
    /// </summary>
    private static ChartResult CreateBaseChart(
        double ascendant = 15.0,
        string category = "Career",
        IReadOnlyList<PlanetPosition>? positions = null,
        IReadOnlyList<AspectResult>? aspects = null,
        IReadOnlyList<MutualReception>? mutualReceptions = null,
        IReadOnlyList<AntisciaConnection>? antisciaConnections = null,
        IReadOnlyList<PlanetCondition>? planetConditions = null,
        MoonConditionInfo? moonCondition = null,
        IReadOnlyList<double>? cusps = null)
    {
        positions ??= CreateDefaultPositions();
        aspects ??= [];
        mutualReceptions ??= [];
        antisciaConnections ??= [];
        planetConditions ??= [];

        ZodiacSign ascSign = (ZodiacSign)((int)(ascendant % 360.0 / 30.0) + 1);

        ChartContextInfo? context = new ChartContextInfo
        {
            ChartRuler = Core.MedicalAstrology.ElementHelper.GetTraditionalRuler(ascSign).ToString(),
            AscendantSign = ascSign,
            ChartRulerDignity = new DignityInfo
            {
                PlanetName = Core.MedicalAstrology.ElementHelper.GetTraditionalRuler(ascSign).ToString(),
                Sign = ZodiacSign.Aries,
                IsDomicile = false,
                IsExalted = false,
                IsInDetriment = false,
                IsInFall = false,
                Dispositor = "Mars",
            },
            ChartRulerHouse = 1,
            IsChartRulerRetrograde = false,
            IsDayChart = true,
            EventSignificator = null,
            EventSignificatorDignity = null,
            KeyHouses = [],
            AngularPlanets = [],
            MutualReceptions = mutualReceptions,
        };

        return new ChartResult
        {
            Input = new ChartInput
            {
                Name = "Test Horary",
                LocalDateTime = new DateTime(2025, 3, 27, 20, 0, 0),
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York",
                EventType = "Horary",
                HoraryQuestion = "Will I get the job?",
                HoraryCategory = category,
            },
            JulianDay = 2460762.5,
            Obliquity = 23.4367,
            UtcDateTime = new DateTime(2025, 3, 28, 0, 0, 0, DateTimeKind.Utc),
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
            Dignities = [],
            MoonCondition = moonCondition,
            ChartContext = context,
            FixedStarConjunctions = [],
            PlanetConditions = planetConditions,
            AntisciaConnections = antisciaConnections,
            ResolvedUtcOffsetHours = -4.0,
            ResolvedOffsetLabel = "EDT (UTC−4)",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>The default positions with the given bodies moved to new longitudes, houses and speeds.</summary>
    private static IReadOnlyList<PlanetPosition> DefaultPositionsWith(
        params (CelestialBodyId Body, double Longitude, int House, double Speed)[] moved)
    {
        List<PlanetPosition> positions = [.. CreateDefaultPositions()];
        foreach ((CelestialBodyId body, double longitude, int house, double speed) in moved)
        {
            int index = positions.FindIndex(p => p.Body == body);
            positions[index] = CreatePosition(body, longitude, house, speed);
        }

        return positions;
    }

    private static IReadOnlyList<PlanetPosition> CreateDefaultPositions()
    {
        return
        [
            CreatePosition(CelestialBodyId.Sun, 280.0, 10, speed: 1.0),
            CreatePosition(CelestialBodyId.Moon, 100.0, 4, speed: 13.0),
            CreatePosition(CelestialBodyId.Mercury, 290.0, 10, speed: 1.2),
            CreatePosition(CelestialBodyId.Venus, 165.0, 8, speed: 0.8),
            CreatePosition(CelestialBodyId.Mars, 45.0, 2, speed: 0.5),
            CreatePosition(CelestialBodyId.Jupiter, 60.0, 3, speed: 0.1),
            CreatePosition(CelestialBodyId.Saturn, 330.0, 12, speed: 0.05),
            CreatePosition(CelestialBodyId.Uranus, 50.0, 2, speed: 0.01),
            CreatePosition(CelestialBodyId.Neptune, 355.0, 12, speed: 0.005),
            CreatePosition(CelestialBodyId.Pluto, 300.0, 10, speed: 0.003),
            CreatePosition(CelestialBodyId.TrueNode, 10.0, 1, speed: 0.0),
            CreatePosition(CelestialBodyId.Chiron, 20.0, 1, speed: 0.05),
        ];
    }

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

    private static PlanetCondition CreateCondition(
        string planetName,
        bool isCombust = false,
        bool isBesieged = false,
        bool isCazimi = false)
    {
        return new PlanetCondition
        {
            PlanetName = planetName,
            IsCazimi = isCazimi,
            IsCombust = isCombust,
            IsUnderTheBeams = false,
            IsBesieged = isBesieged,
            IsConjunctBenefic = false,
            IsConjunctMalefic = false,
            HasApplyingBeneficAspect = false,
            HasApplyingMaleficAspect = false,
            AccidentalScore = isCombust ? -5 : isBesieged ? -5 : 0,
            Conditions = [],
        };
    }

    #endregion

    #region Culture independence

    // The notes and denial reasons are built with the invariant culture, so their
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="Analyze_DirectAspectPerfectsOutOfSign_WithoutReception_DoesNotPerfect"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Analyze_DirectAspectPerfectsOutOfSign_WithoutReception_DoesNotPerfect_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        Analyze_DirectAspectPerfectsOutOfSign_WithoutReception_DoesNotPerfect();
    }

    /// <summary>
    /// <see cref="Analyze_TranslatorsApplicationTurnsAround_TranslationDoesNotPerfect"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Analyze_TranslatorsApplicationTurnsAround_TranslationDoesNotPerfect_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        Analyze_TranslatorsApplicationTurnsAround_TranslationDoesNotPerfect();
    }

    /// <summary>
    /// <see cref="Analyze_MarsExactToSignificatorBeforeCollectionCompletes_IsProhibition"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Analyze_MarsExactToSignificatorBeforeCollectionCompletes_IsProhibition_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        Analyze_MarsExactToSignificatorBeforeCollectionCompletes_IsProhibition();
    }

    /// <summary>
    /// <see cref="Analyze_SeparatingAntiscion_IsTestimonyNotPerfection"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Analyze_SeparatingAntiscion_IsTestimonyNotPerfection_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        Analyze_SeparatingAntiscion_IsTestimonyNotPerfection();
    }

    /// <summary>
    /// <see cref="Analyze_L1StationsAndTheApplicationFails_IsRefranationAndJudgedNo"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Analyze_L1StationsAndTheApplicationFails_IsRefranationAndJudgedNo_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        Analyze_L1StationsAndTheApplicationFails_IsRefranationAndJudgedNo();
    }

    /// <summary>
    /// <see cref="Analyze_ApplicationStillApplyingAtTheCap_IsUnresolvedAndJudgedUncertain"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Analyze_ApplicationStillApplyingAtTheCap_IsUnresolvedAndJudgedUncertain_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        Analyze_ApplicationStillApplyingAtTheCap_IsUnresolvedAndJudgedUncertain();
    }

    /// <summary>
    /// <see cref="Analyze_StationAfterTurnaroundBeforeLinearEstimate_IsRefranation"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Analyze_StationAfterTurnaroundBeforeLinearEstimate_IsRefranation_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        Analyze_StationAfterTurnaroundBeforeLinearEstimate_IsRefranation();
    }

    #endregion
}
