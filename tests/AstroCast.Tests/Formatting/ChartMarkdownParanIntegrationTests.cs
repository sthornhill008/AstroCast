using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// End-to-end integration tests validating the full pipeline: chart calculation
/// with integrated paran calculation, and combined Markdown export. Verifies
/// section ordering, content presence, and file write/read round-trip.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartMarkdownParanIntegrationTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all required services.
    /// </summary>
    public ChartMarkdownParanIntegrationTests(EphemerisFixture fixture)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(
            fixture.EphemerisService, aspects, midpoints,
            new DignityService(), new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    #region Full Pipeline — Natal Chart with Parans

    /// <summary>
    /// Full pipeline test: calculate a real chart (parans computed automatically),
    /// export to Markdown, and verify all sections are present in the correct order.
    /// </summary>
    [Fact]
    public void FullPipeline_Should_ContainAllSectionsAndParans_When_CombinedExport()
    {
        // Arrange — London, 2026-01-15 noon (parans computed by engine)
        ChartResult chartResult = _engine.Calculate(new ChartInput
        {
            Name = "Integration Test Chart",
            LocalDateTime = new DateTime(2026, 1, 15, 12, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London",
        });

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(chartResult);

        // Assert — standard sections present
        markdown.Should().Contain("## Planetary Positions",
            "Output should contain Planetary Positions section");
        markdown.Should().Contain("## Aspects",
            "Output should contain Aspects section");
        markdown.Should().Contain("## Midpoint Activations",
            "Output should contain Midpoint Activations section");
        markdown.Should().Contain("## Fixed Stars",
            "Output should contain Fixed Stars section");

        // Assert — paran section present (engine-calculated)
        chartResult.Parans.Should().NotBeNull("engine should compute parans for London");
        markdown.Should().Contain("## Parans (Paranatellonta)",
            "Output should contain the Parans section");

        // Assert — section ordering
        int fixedStarsIndex = markdown.IndexOf("## Fixed Stars", StringComparison.Ordinal);
        int paranIndex = markdown.IndexOf("## Parans (Paranatellonta)", StringComparison.Ordinal);
        int footerIndex = markdown.IndexOf("---", paranIndex, StringComparison.Ordinal);

        paranIndex.Should().BeGreaterThan(fixedStarsIndex,
            "Paran section should appear after Fixed Stars");
        footerIndex.Should().BeGreaterThan(paranIndex,
            "Footer should appear after Paran section");

        // Assert — at least one paran event (statistically very likely at mid-latitude)
        chartResult.Parans!.Parans.Count.Should().BeGreaterThan(0,
            "London at noon should produce at least one paran event");
    }

    #endregion

    #region File Write and Read Back

    /// <summary>
    /// Export chart with engine-calculated parans to a temp file, read it back,
    /// and verify the paran section is present in the written file.
    /// </summary>
    [Fact]
    public async Task SaveAsync_Should_WriteParanSection_When_ParansInChartResult()
    {
        // Arrange
        ChartResult chartResult = _engine.Calculate(new ChartInput
        {
            Name = "File Write Test",
            LocalDateTime = new DateTime(2026, 1, 15, 12, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London",
        });

        string tempPath = Path.Combine(
            Path.GetTempPath(), $"paran_export_test_{Guid.NewGuid()}.md");

        try
        {
            // Act
            await ChartMarkdownFormatter.SaveAsync(chartResult, tempPath);

            // Assert
            File.Exists(tempPath).Should().BeTrue("the Markdown file should exist after export");
            string content = await File.ReadAllTextAsync(tempPath);
            content.Should().Contain("## Parans (Paranatellonta)",
                "the written file should contain the paran section");
            content.Should().Contain("File Write Test",
                "the written file should contain the chart name");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    #endregion

    #region Engine-Calculated Parans in Standard Output

    /// <summary>
    /// The standard ToMarkdown call should now include parans automatically
    /// since they are part of ChartResult.
    /// </summary>
    [Fact]
    public void ToMarkdown_Should_ContainParans_When_EngineCalculated()
    {
        // Arrange — same chart as the full pipeline test
        ChartResult chartResult = _engine.Calculate(new ChartInput
        {
            Name = "Engine Paran Test",
            LocalDateTime = new DateTime(2026, 1, 15, 12, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London",
        });

        // Act
        string markdown = ChartMarkdownFormatter.ToMarkdown(chartResult);

        // Assert — parans are now part of every chart result
        chartResult.Parans.Should().NotBeNull("engine should compute parans");
        markdown.Should().Contain("## Parans (Paranatellonta)",
            "Parans section should appear automatically in Markdown output");
        markdown.Should().Contain("## Planetary Positions",
            "Standard sections should still be present");
        markdown.Should().Contain("*Generated by",
            "Footer should still be present");
    }

    #endregion
}
