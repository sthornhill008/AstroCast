using System.Reflection;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// <see cref="ChartInput.WithIncludeMinorPoints"/> copies every property and sets the switch (branch AsteroidsPanel,
/// 1.6.0): the input the Chart panel's chart is recast from when the Points box changes.
/// </summary>
/// <remarks>
/// <see cref="ChartInput"/> is a sealed class whose properties are init-only, so the copy is written by hand, one
/// property at a time; the second test holds the class to the eighteen properties the copy knows, so a property
/// added later fails here until the copy carries it too.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ChartInputTests
{
    /// <summary>The names of <see cref="ChartInput"/>'s eighteen public instance properties, in ordinal order.</summary>
    private static readonly string[] KnownProperties =
    [
        "EventDescription", "EventType", "Gender", "HoraryCategory", "HoraryQuestion", "HouseSystem",
        "IncludeMinorPoints", "Keywords", "Latitude", "LocalDateTime", "LocationName", "Longitude", "Name",
        "Notes", "SourceNotes", "TimeZone", "UseExplicitOffset", "UtcOffsetHours",
    ];

    /// <summary>An input with every one of the eighteen properties off its default.</summary>
    /// <returns>The input.</returns>
    private static ChartInput OffTheDefaults() => new()
    {
        Name = "Copy",
        LocalDateTime = new DateTime(1990, 5, 17, 8, 15, 30),
        TimeZone = "Europe/London",
        Latitude = 51.5074,
        Longitude = -0.1278,
        LocationName = "London",
        EventType = "Event",
        EventDescription = "A description",
        Gender = Gender.Female,
        SourceNotes = "A source",
        Notes = "Some notes",
        Keywords = "a, b",
        UtcOffsetHours = 1.0,
        UseExplicitOffset = true,
        HouseSystem = HouseSystem.Koch,
        IncludeMinorPoints = true,
        HoraryQuestion = "Will it rain?",
        HoraryCategory = "Weather",
    };

    /// <summary>
    /// The copy is a new instance, equal to the original in the other seventeen properties, with the switch set
    /// as asked; the original is unchanged, and a copy with the switch set back equals the original in every
    /// property.
    /// </summary>
    [Fact]
    public void WithIncludeMinorPoints_CopiesEveryPropertyAndSetsTheSwitch()
    {
        ChartInput original = OffTheDefaults();

        ChartInput off = original.WithIncludeMinorPoints(false);

        off.Should().NotBeSameAs(original, "the copy is a new instance");
        off.IncludeMinorPoints.Should().BeFalse("the switch is set as asked");
        off.Should().BeEquivalentTo(
            original, o => o.Excluding(i => i.IncludeMinorPoints), "the other seventeen properties are copied");
        original.IncludeMinorPoints.Should().BeTrue("the original is unchanged");

        ChartInput on = off.WithIncludeMinorPoints(true);

        on.Should().NotBeSameAs(off);
        on.Should().BeEquivalentTo(original, "a copy with the switch set back equals the original in every property");
    }

    /// <summary>
    /// <see cref="ChartInput"/> has exactly the eighteen public instance properties the copy knows, so a property
    /// added to the class fails here until <see cref="ChartInput.WithIncludeMinorPoints"/> copies it too.
    /// </summary>
    [Fact]
    public void ChartInput_HasTheEighteenPropertiesTheCopyKnows()
    {
        string[] actual = typeof(ChartInput)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        actual.Should().Equal(KnownProperties, "a property added to ChartInput must be copied by WithIncludeMinorPoints too");
    }
}
