using AstroCast.Tests.Fixtures;
using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// The one check every latitude and longitude box goes through (1.4.0, port plan §9 item 9; scoping decision 3):
/// <see cref="ChartEntryViewModel.TryParseLatitude"/> and <see cref="ChartEntryViewModel.TryParseLongitude"/>.
/// </summary>
/// <remarks>
/// Decimal degrees with a point, in any culture, within ±90 and ±180, with the Natal panel's four messages. A comma
/// is never a decimal or a grouping separator: the Natal and Electional panels read their boxes with
/// <c>double.TryParse(s, InvariantCulture, out)</c>, whose style allows thousands separators, so "-7,5" was −75,
/// the defect O-D4 removed from the offset box (E3-1). The Relocation box parses on every change through the same
/// check.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class CoordinateEntryTests
{
    private const string LatitudeInvalid = "Invalid latitude. Use decimal degrees (e.g., 37.4380).";
    private const string LatitudeRange = "Latitude must be between -90 and 90 degrees.";
    private const string LongitudeInvalid = "Invalid longitude. Use decimal degrees (e.g., -79.1877).";
    private const string LongitudeRange = "Longitude must be between -180 and 180 degrees.";

    /// <summary>A latitude is decimal degrees with a point, within ±90; anything else is refused with its message.</summary>
    /// <param name="text">The box's text.</param>
    /// <param name="valid">Whether the text is accepted.</param>
    /// <param name="degrees">The degrees read, when accepted.</param>
    /// <param name="error">The message, when refused.</param>
    [Theory]
    [InlineData("51.5074", true, 51.5074, null)]
    [InlineData("-90", true, -90.0, null)]
    [InlineData("90.5", false, 0.0, LatitudeRange)]
    [InlineData("-7,5", false, 0.0, LatitudeInvalid)]
    [InlineData("abc", false, 0.0, LatitudeInvalid)]
    [InlineData("", false, 0.0, LatitudeInvalid)]
    public void TryParseLatitude_ReadsDecimalDegreesWithinNinety(string text, bool valid, double degrees, string? error)
    {
        bool ok = ChartEntryViewModel.TryParseLatitude(text, out double read, out string? message);

        ok.Should().Be(valid);
        read.Should().Be(degrees);
        message.Should().Be(error);
    }

    /// <summary>A longitude is decimal degrees with a point, within ±180; anything else is refused with its message.</summary>
    /// <param name="text">The box's text.</param>
    /// <param name="valid">Whether the text is accepted.</param>
    /// <param name="degrees">The degrees read, when accepted.</param>
    /// <param name="error">The message, when refused.</param>
    [Theory]
    [InlineData("-0.1278", true, -0.1278, null)]
    [InlineData("180", true, 180.0, null)]
    [InlineData("180.01", false, 0.0, LongitudeRange)]
    [InlineData("-7,5", false, 0.0, LongitudeInvalid)]
    [InlineData("abc", false, 0.0, LongitudeInvalid)]
    public void TryParseLongitude_ReadsDecimalDegreesWithinOneEighty(string text, bool valid, double degrees, string? error)
    {
        bool ok = ChartEntryViewModel.TryParseLongitude(text, out double read, out string? message);

        ok.Should().Be(valid);
        read.Should().Be(degrees);
        message.Should().Be(error);
    }

    /// <summary>
    /// The machine's culture changes nothing: a point is the decimal separator and a comma is refused, under cultures
    /// whose own separator is a comma (de-DE, sv-SE) and one that writes another calendar (th-TH).
    /// </summary>
    /// <param name="culture">The culture the form runs under.</param>
    [Theory]
    [InlineData("de-DE")]
    [InlineData("sv-SE")]
    [InlineData("th-TH")]
    public void TryParse_IgnoresTheMachineCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ChartEntryViewModel.TryParseLatitude("-7.5", out double latitude, out _).Should().BeTrue();
        latitude.Should().Be(-7.5);
        ChartEntryViewModel.TryParseLongitude("-7,5", out _, out string? error).Should().BeFalse();
        error.Should().Be(LongitudeInvalid);
    }
}
