using AstroCast.Core.Formatting;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="TransitReportNaming"/>: the title, file stem and dialog subject of the fifteen
/// combinations of techniques (branch Modes, 1.4.0 item 2; scoping C1f; the fourth technique since branch
/// PrimaryExport, 1.5.0).
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransitReportNamingTests
{
    /// <summary>The fifteen combinations and their titles: with the transits, always the transit report.</summary>
    /// <param name="transits">Whether the transit scan ran.</param>
    /// <param name="solarArcs">Whether the solar arcs are in.</param>
    /// <param name="progressions">Whether the secondary progressions are in.</param>
    /// <param name="primaryDirections">Whether the primary directions are in.</param>
    /// <param name="expected">The title.</param>
    [Theory]
    [InlineData(true, true, true, true, "Transit Report")]
    [InlineData(true, true, true, false, "Transit Report")]
    [InlineData(true, true, false, false, "Transit Report")]
    [InlineData(true, false, true, false, "Transit Report")]
    [InlineData(true, false, false, false, "Transit Report")]
    [InlineData(true, false, false, true, "Transit Report")]
    [InlineData(true, true, false, true, "Transit Report")]
    [InlineData(true, false, true, true, "Transit Report")]
    [InlineData(false, true, false, false, "Solar Arc Report")]
    [InlineData(false, false, true, false, "Secondary Progressions Report")]
    [InlineData(false, true, true, false, "Solar Arc and Progressions Report")]
    [InlineData(false, false, false, true, "Primary Directions Report")]
    [InlineData(false, true, false, true, "Solar Arc and Primary Directions Report")]
    [InlineData(false, false, true, true, "Progressions and Primary Directions Report")]
    [InlineData(false, true, true, true, "Solar Arc, Progressions and Primary Directions Report")]
    public void Title(bool transits, bool solarArcs, bool progressions, bool primaryDirections, string expected)
    {
        TransitReportNaming.Title(transits, solarArcs, progressions, primaryDirections).Should().Be(expected);
    }

    /// <summary>The fifteen combinations and their file stems.</summary>
    /// <param name="transits">Whether the transit scan ran.</param>
    /// <param name="solarArcs">Whether the solar arcs are in.</param>
    /// <param name="progressions">Whether the secondary progressions are in.</param>
    /// <param name="primaryDirections">Whether the primary directions are in.</param>
    /// <param name="expected">The stem between the chart's name and the dates.</param>
    [Theory]
    [InlineData(true, true, true, true, "Transits")]
    [InlineData(true, true, true, false, "Transits")]
    [InlineData(true, true, false, false, "Transits")]
    [InlineData(true, false, true, false, "Transits")]
    [InlineData(true, false, false, false, "Transits")]
    [InlineData(true, false, false, true, "Transits")]
    [InlineData(true, true, false, true, "Transits")]
    [InlineData(true, false, true, true, "Transits")]
    [InlineData(false, true, false, false, "SolarArcs")]
    [InlineData(false, false, true, false, "Progressions")]
    [InlineData(false, true, true, false, "SolarArcsProgressions")]
    [InlineData(false, false, false, true, "PrimaryDirections")]
    [InlineData(false, true, false, true, "SolarArcsPrimaryDirections")]
    [InlineData(false, false, true, true, "ProgressionsPrimaryDirections")]
    [InlineData(false, true, true, true, "SolarArcsProgressionsPrimaryDirections")]
    public void FileStem(bool transits, bool solarArcs, bool progressions, bool primaryDirections, string expected)
    {
        TransitReportNaming.FileStem(transits, solarArcs, progressions, primaryDirections).Should().Be(expected);
    }

    /// <summary>The fifteen combinations and the save dialog's subject.</summary>
    /// <param name="transits">Whether the transit scan ran.</param>
    /// <param name="solarArcs">Whether the solar arcs are in.</param>
    /// <param name="progressions">Whether the secondary progressions are in.</param>
    /// <param name="primaryDirections">Whether the primary directions are in.</param>
    /// <param name="expected">The subject of "Export {Subject} as Markdown".</param>
    [Theory]
    [InlineData(true, true, true, true, "Transits")]
    [InlineData(true, true, true, false, "Transits")]
    [InlineData(true, true, false, false, "Transits")]
    [InlineData(true, false, true, false, "Transits")]
    [InlineData(true, false, false, false, "Transits")]
    [InlineData(true, false, false, true, "Transits")]
    [InlineData(true, true, false, true, "Transits")]
    [InlineData(true, false, true, true, "Transits")]
    [InlineData(false, true, false, false, "Solar Arcs")]
    [InlineData(false, false, true, false, "Progressions")]
    [InlineData(false, true, true, false, "Solar Arcs and Progressions")]
    [InlineData(false, false, false, true, "Primary Directions")]
    [InlineData(false, true, false, true, "Solar Arcs and Primary Directions")]
    [InlineData(false, false, true, true, "Progressions and Primary Directions")]
    [InlineData(false, true, true, true, "Solar Arcs, Progressions and Primary Directions")]
    public void DialogSubject(bool transits, bool solarArcs, bool progressions, bool primaryDirections, string expected)
    {
        TransitReportNaming.DialogSubject(transits, solarArcs, progressions, primaryDirections).Should().Be(expected);
    }

    /// <summary>No technique has no name: every function refuses it, naming the four.</summary>
    [Fact]
    public void NoTechnique_IsRefused()
    {
        Action title = () => TransitReportNaming.Title(false, false, false, false);
        Action stem = () => TransitReportNaming.FileStem(false, false, false, false);
        Action subject = () => TransitReportNaming.DialogSubject(false, false, false, false);

        title.Should().Throw<ArgumentException>().WithMessage("*transits*solar arcs*secondary progressions*primary directions*");
        stem.Should().Throw<ArgumentException>();
        subject.Should().Throw<ArgumentException>();
    }
}
