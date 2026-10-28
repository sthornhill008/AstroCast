using AstroCast.Core.Formatting;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="OffsetLabelFormatter"/>, the one offset label the chart, its
/// exports and the entry form share (card 6).
/// </summary>
/// <remarks>
/// The rule: the zone record's name leads the label when it is a word ("EST", "LMT",
/// "PMT"), and is left out when it is numeric ("+06", "+0330"); a whole or half hour
/// prints as hours ("UTC−5", "UTC+5.5"), anything else as h:mm or h:mm:ss. The offsets
/// below are IANA's, the names its zone records'.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class OffsetLabelFormatterTests
{
    /// <summary>
    /// A zone's label: the record's name when it is a word, then the offset.
    /// </summary>
    /// <param name="offsetSeconds">The offset from UTC, in seconds.</param>
    /// <param name="name">The zone record's name for it.</param>
    /// <param name="expected">The label.</param>
    [Theory]
    [InlineData(-18000, "EST", "EST (UTC−5)")]
    [InlineData(-14400, "EDT", "EDT (UTC−4)")]
    [InlineData(0, "GMT", "GMT (UTC+0)")]
    [InlineData(0, "UTC", "UTC (UTC+0)")]
    [InlineData(19800, "IST", "IST (UTC+5.5)")]
    [InlineData(-9000, "NDT", "NDT (UTC−2.5)")]
    [InlineData(21600, "+06", "UTC+6")]
    [InlineData(12600, "+0330", "UTC+3.5")]
    [InlineData(20700, "+0545", "UTC+5:45")]
    [InlineData(31500, "+0845", "UTC+8:45")]
    [InlineData(-17762, "LMT", "LMT (UTC−4:56:02)")]
    [InlineData(561, "PMT", "PMT (UTC+0:09:21)")]
    [InlineData(-2670, "MMT", "MMT (UTC−0:44:30)")]
    [InlineData(-1521, "DMT", "DMT (UTC−0:25:21)")]
    [InlineData(-18000, "", "UTC−5")]
    public void Format_Zone_Should_LeadWithAWordName_ThenTheOffset(int offsetSeconds, string name, string expected)
    {
        OffsetLabelFormatter.Format(TimeSpan.FromSeconds(offsetSeconds), name, isExplicit: false)
            .Should().Be(expected);
    }

    /// <summary>
    /// An explicit offset is labelled "(explicit)" and never named, by the same offset rule.
    /// </summary>
    /// <param name="offsetHours">The offset the user entered, in hours.</param>
    /// <param name="expected">The label.</param>
    [Theory]
    [InlineData(-5.0, "UTC−5 (explicit)")]
    [InlineData(0.0, "UTC+0 (explicit)")]
    [InlineData(5.5, "UTC+5.5 (explicit)")]
    [InlineData(5.75, "UTC+5:45 (explicit)")]
    [InlineData(-3.5, "UTC−3.5 (explicit)")]
    public void Format_Explicit_Should_LabelTheOffset_AsExplicit(double offsetHours, string expected)
    {
        OffsetLabelFormatter.Format(TimeSpan.FromHours(offsetHours), "EST", isExplicit: true)
            .Should().Be(expected);
    }

    /// <summary>
    /// An offset with a fraction of a second, as an explicit offset entered in decimal
    /// hours can carry, is labelled to the nearest second.
    /// </summary>
    [Fact]
    public void Format_Should_RoundTheOffset_ToTheNearestSecond()
    {
        OffsetLabelFormatter.Format(TimeSpan.FromHours(-4.933888), null, isExplicit: true)
            .Should().Be("UTC−4:56:02 (explicit)");
    }

    /// <summary>The label is the same under every culture (branch O-1).</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Format_Should_NotDependOnTheCulture(string culture)
    {
        using CultureScope scope = new(culture);

        OffsetLabelFormatter.Format(TimeSpan.FromSeconds(19800), "IST", isExplicit: false).Should().Be("IST (UTC+5.5)");
        OffsetLabelFormatter.Format(TimeSpan.FromSeconds(-17762), "LMT", isExplicit: false).Should().Be("LMT (UTC−4:56:02)");
        OffsetLabelFormatter.Format(TimeSpan.FromHours(-3.5), null, isExplicit: true).Should().Be("UTC−3.5 (explicit)");
    }
}
