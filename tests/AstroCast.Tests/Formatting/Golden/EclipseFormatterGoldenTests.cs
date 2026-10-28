using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Tests.Fixtures;

namespace AstroCast.Tests.Formatting.Golden;

/// <summary>
/// Golden-file test for <see cref="EclipseFormatter"/>. Uses a hand-built
/// <see cref="EclipseSeries"/> so the test does not depend on the
/// Swiss Ephemeris: this keeps the golden a pure test of the formatter's
/// output structure (headings, tables, phase timing sections, footer) rather
/// than a test of eclipse computation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class EclipseFormatterGoldenTests : GoldenFormatterTestBase
{
    /// <summary>
    /// Verifies that <see cref="EclipseFormatter.SeriesToMarkdown"/> renders
    /// a canonical series identically to the checked-in golden.
    /// </summary>
    [Fact]
    public void SeriesToMarkdown_MatchesGolden()
    {
        // Arrange - a two-eclipse synthetic series, one solar and one lunar,
        // covering the representative shapes (phase times, totality duration,
        // positional data) that the formatter conditionally renders.
        EclipseSeries series = BuildCanonicalSeries();

        // Act
        string markdown = EclipseFormatter.SeriesToMarkdown(series);

        // Assert
        AssertGolden(markdown, "EclipseFormatter_SeriesToMarkdown_SampleSeries.golden.md");
    }

    /// <summary>
    /// Canonical two-eclipse series used as input to the golden test.
    /// Values are fabricated but plausible (they would not survive a physical
    /// sanity check, but that is not the purpose of the golden).
    /// </summary>
    private static EclipseSeries BuildCanonicalSeries()
    {
        EclipseResult solar = new()
        {
            Category = "Solar",
            IsSolar = true,
            Type = "Total",
            FullLabel = "Total Solar Eclipse",
            MaximumUtc = new DateTime(2024, 4, 8, 18, 17, 21, DateTimeKind.Utc),
            MaximumJd = 2460409.2620,
            FirstContactUtc = new DateTime(2024, 4, 8, 15, 42, 15, DateTimeKind.Utc),
            LastContactUtc = new DateTime(2024, 4, 8, 20, 52, 14, DateTimeKind.Utc),
            TotalityBeginUtc = new DateTime(2024, 4, 8, 16, 38, 45, DateTimeKind.Utc),
            TotalityEndUtc = new DateTime(2024, 4, 8, 19, 55, 35, DateTimeKind.Utc),
            TotalityDurationMinutes = 4.45,
            SunLongitude = 19.325,
            MoonLongitude = 19.325,
            Sign = "Aries",
            Degree = 19.325,
            PositionFormatted = "19\u00b020' Aries",
            Element = "Fire",
            Modality = "Cardinal",
            NodeLongitude = 16.910,
            NodeDistance = 2.415,
            RawFlags = 0x0004,
            EngineVersion = "AstroCast 1.0.206 (SwissEph 2.10.03)",
            CalculatedAt = new DateTime(2026, 4, 17, 14, 23, 15, DateTimeKind.Utc),
        };

        EclipseResult lunar = new()
        {
            Category = "Lunar",
            IsSolar = false,
            Type = "Penumbral",
            FullLabel = "Penumbral Lunar Eclipse",
            MaximumUtc = new DateTime(2024, 3, 25, 7, 12, 48, DateTimeKind.Utc),
            MaximumJd = 2460394.8005,
            FirstContactUtc = new DateTime(2024, 3, 25, 5, 52, 35, DateTimeKind.Utc),
            LastContactUtc = new DateTime(2024, 3, 25, 8, 33, 05, DateTimeKind.Utc),
            PenumbralBeginUtc = new DateTime(2024, 3, 25, 4, 53, 10, DateTimeKind.Utc),
            PenumbralEndUtc = new DateTime(2024, 3, 25, 9, 32, 40, DateTimeKind.Utc),
            TotalityDurationMinutes = null,
            SunLongitude = 5.125,
            MoonLongitude = 185.125,
            Sign = "Libra",
            Degree = 5.125,
            PositionFormatted = "5\u00b007' Libra",
            Element = "Air",
            Modality = "Cardinal",
            NodeLongitude = 15.710,
            NodeDistance = 10.585,
            RawFlags = 0x0010,
            EngineVersion = "AstroCast 1.0.206 (SwissEph 2.10.03)",
            CalculatedAt = new DateTime(2026, 4, 17, 14, 23, 15, DateTimeKind.Utc),
        };

        return new EclipseSeries
        {
            Eclipses = new List<EclipseResult> { lunar, solar }.AsReadOnly(),
            ScanStart = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ScanEnd = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            Count = 2,
            EngineVersion = "AstroCast 1.0.206 (SwissEph 2.10.03)",
            CalculatedAt = new DateTime(2026, 4, 17, 14, 23, 15, DateTimeKind.Utc),
        };
    }

    // Each golden again under de-DE, sv-SE and th-TH, compared with the same golden
    // file: the export reads the same whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="SeriesToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SeriesToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SeriesToMarkdown_MatchesGolden();
    }
}
