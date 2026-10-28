using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Unit tests for <see cref="TimezoneProvider"/> verifying the IANA timezone
/// list is complete, properly formatted, sorted, and deduplicated.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TimezoneProviderTests
{
    [Fact]
    public void GetTimezoneIds_Should_ReturnNonEmptyList()
    {
        IReadOnlyList<string> ids = TimezoneProvider.GetTimezoneIds();

        ids.Should().NotBeEmpty(
            "the system should provide at least one timezone identifier");
        ids.Count.Should().BeGreaterThan(100,
            "a typical system has hundreds of IANA timezone identifiers");
    }

    [Fact]
    public void GetTimezoneIds_Should_ContainAmericaNewYork()
    {
        IReadOnlyList<string> ids = TimezoneProvider.GetTimezoneIds();

        ids.Should().Contain("America/New_York",
            "America/New_York is a standard IANA timezone that should be present");
    }

    [Fact]
    public void GetTimezoneIds_Should_ContainEuropeLondon()
    {
        IReadOnlyList<string> ids = TimezoneProvider.GetTimezoneIds();

        ids.Should().Contain("Europe/London",
            "Europe/London is a standard IANA timezone that should be present");
    }

    [Fact]
    public void GetTimezoneIds_Should_ContainAsiaTokyo()
    {
        IReadOnlyList<string> ids = TimezoneProvider.GetTimezoneIds();

        ids.Should().Contain("Asia/Tokyo",
            "Asia/Tokyo is a standard IANA timezone that should be present");
    }

    [Fact]
    public void GetTimezoneIds_Should_ContainOnlyIanaFormat()
    {
        IReadOnlyList<string> ids = TimezoneProvider.GetTimezoneIds();

        foreach (string id in ids)
        {
            id.Should().Contain("/",
                $"every timezone ID should be in IANA Area/City format, but found '{id}'");
        }
    }

    [Fact]
    public void GetTimezoneIds_Should_BeSortedAlphabetically()
    {
        IReadOnlyList<string> ids = TimezoneProvider.GetTimezoneIds();

        List<string> sorted = [.. ids.OrderBy(id => id, StringComparer.Ordinal)];

        ids.Should().BeEquivalentTo(sorted,
            options => options.WithStrictOrdering(),
            "timezone IDs should be sorted in ordinal alphabetical order");
    }

    [Fact]
    public void GetTimezoneIds_Should_ContainNoDuplicates()
    {
        IReadOnlyList<string> ids = TimezoneProvider.GetTimezoneIds();

        ids.Should().OnlyHaveUniqueItems(
            "the timezone list should not contain duplicate entries");
    }

    #region The app's own zones (cards 3, 5, 7)

    /// <summary>
    /// The zones offered are the app's own: zone.tab's 418 in tzdb 2026e, the same on every
    /// platform, America/Vancouver among them. They came from the operating system, which
    /// gave 418 on macOS but 131 on Windows, without Vancouver.
    /// </summary>
    [Fact]
    public void GetTimezoneIds_Should_BeTheAppsOwnZoneTab_The418WithVancouver()
    {
        IReadOnlyList<string> ids = TimezoneProvider.GetTimezoneIds();

        ids.Should().HaveCount(418).And.Contain("America/Vancouver");
        ids.Should().Equal(new TzdbTimeZoneRules().ZoneIds);
    }

    /// <summary>
    /// A zone identifier from elsewhere (a place lookup, the machine) becomes an offered
    /// zone: kept if it is offered, in the list's own spelling; else its canonical zone if
    /// that is offered (an IANA alias, or a Windows identifier through CLDR); else none.
    /// </summary>
    /// <param name="zoneId">The identifier.</param>
    /// <param name="expected">The offered zone, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("America/New_York", "America/New_York")]
    [InlineData("america/new_york", "America/New_York")]
    [InlineData("Europe/Oslo", "Europe/Oslo")]
    [InlineData("US/Eastern", "America/New_York")]
    [InlineData("Asia/Calcutta", "Asia/Kolkata")]
    [InlineData("Europe/Kiev", "Europe/Kyiv")]
    [InlineData("Eastern Standard Time", "America/New_York")]
    [InlineData("Etc/UTC", null)]
    [InlineData("EST5EDT", null)]
    [InlineData("Not/A/Zone", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void FindOfferedZone_Should_KeepAnOfferedZone_ElseTakeItsCanonicalZone(string? zoneId, string? expected)
    {
        TimezoneProvider.FindOfferedZone(zoneId, new TzdbTimeZoneRules()).Should().Be(expected);
    }

    /// <summary>
    /// The machine's zone, for a new chart's default (card 7): on macOS and Linux the zone
    /// <c>/etc/localtime</c> links to, else the platform's local zone, each mapped into the
    /// offered list; blank if neither is offered. This Mac's local zone reads "EST5EDT",
    /// which is not offered and, since 2026d, is not New York before 1966 (TZ-3), while its
    /// <c>/etc/localtime</c> links to America/New_York.
    /// </summary>
    /// <param name="linkTarget">Where <c>/etc/localtime</c> links, or <see langword="null"/>.</param>
    /// <param name="localZoneId">The platform's local zone identifier.</param>
    /// <param name="expected">The default zone.</param>
    [Theory]
    [InlineData("/var/db/timezone/zoneinfo/America/New_York", "EST5EDT", "America/New_York")]
    [InlineData("../usr/share/zoneinfo/Europe/London", "Europe/London", "Europe/London")]
    [InlineData("/usr/share/zoneinfo/America/Argentina/Buenos_Aires", "UTC", "America/Argentina/Buenos_Aires")]
    [InlineData(null, "Eastern Standard Time", "America/New_York")]
    [InlineData(null, "America/Chicago", "America/Chicago")]
    [InlineData("/usr/share/zoneinfo/Etc/UTC", "Etc/UTC", "")]
    [InlineData(null, "EST5EDT", "")]
    public void FindMachineZone_Should_MapTheMachinesZone_IntoTheOfferedList(
        string? linkTarget, string localZoneId, string expected)
    {
        TimezoneProvider.FindMachineZone(linkTarget, localZoneId, new TzdbTimeZoneRules()).Should().Be(expected);
    }

    #endregion
}
