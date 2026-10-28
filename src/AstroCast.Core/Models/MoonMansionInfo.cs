namespace AstroCast.Core.Models;

/// <summary>
/// Information about the Moon's current lunar mansion (Arabic: manazil al-qamar).
/// The 28 mansions divide the ecliptic into equal segments of ~12 degrees 51 arcminutes each,
/// with each mansion having a traditional nature and keywords relevant to
/// horary, electional, and event interpretation.
/// </summary>
/// <remarks>
/// <para>
/// The mansion system is one of the oldest astrological frameworks, appearing
/// in Arabic, Indian (naksatras), and Chinese (xiu) traditions. In AstroCast,
/// the tropical zodiac is used: mansion boundaries are fixed divisions of the
/// 360 degree ecliptic, not tied to sidereal star positions.
/// </para>
/// <para>
/// Mansion nature (Benefic, Malefic, Neutral) describes the general quality
/// of the mansion. In horary, the Moon's mansion at the question time
/// qualifies the judgment -- a benefic mansion supports a positive outcome,
/// a malefic mansion cautions against it. In electional work, mansion
/// selection is a primary criterion for timing.
/// </para>
/// </remarks>
public sealed class MoonMansionInfo
{
    /// <summary>Mansion number (1-28).</summary>
    public required int Number { get; init; }

    /// <summary>Traditional Arabic name of the mansion (e.g., "Al-Sharatain").</summary>
    public required string Name { get; init; }

    /// <summary>
    /// English translation or meaning of the Arabic name
    /// (e.g., "The Two Signs").
    /// </summary>
    public required string Meaning { get; init; }

    /// <summary>
    /// Nature of the mansion: "Benefic", "Malefic", or "Neutral".
    /// </summary>
    public required string Nature { get; init; }

    /// <summary>
    /// Interpretive keywords describing the mansion's qualities
    /// (e.g., "Beginnings, journeys, new ventures").
    /// </summary>
    public required string Keywords { get; init; }

    /// <summary>
    /// The starting ecliptic longitude of this mansion in degrees.
    /// Mansion 1 starts at 0 degrees 00 arcminutes Aries (0.0 degrees).
    /// </summary>
    public double StartLongitude => (Number - 1) * (360.0 / 28.0);

    /// <summary>
    /// The ending ecliptic longitude of this mansion in degrees.
    /// </summary>
    public double EndLongitude => Number * (360.0 / 28.0);

    /// <summary>
    /// True if this mansion has a benefic nature.
    /// </summary>
    public bool IsBenefic => Nature == "Benefic";

    /// <summary>
    /// True if this mansion has a malefic nature.
    /// </summary>
    public bool IsMalefic => Nature == "Malefic";

    /// <summary>
    /// The complete catalog of all 28 lunar mansions with traditional
    /// Arabic names, natures, and interpretive keywords.
    /// </summary>
    public static IReadOnlyList<MoonMansionInfo> Catalog { get; } = BuildCatalog();

    /// <summary>
    /// Builds the complete 28-mansion catalog.
    /// </summary>
    private static IReadOnlyList<MoonMansionInfo> BuildCatalog()
    {
        return
        [
            new() { Number = 1,  Name = "Al-Sharatain",      Meaning = "The Two Signs",              Nature = "Neutral",  Keywords = "Beginnings, journeys, new ventures" },
            new() { Number = 2,  Name = "Al-Butain",         Meaning = "The Little Belly",            Nature = "Neutral",  Keywords = "Finding treasure, business contracts, reconciliation" },
            new() { Number = 3,  Name = "Al-Thurayya",       Meaning = "The Many Little Ones",        Nature = "Benefic",  Keywords = "Prosperity, favorable for all undertakings, alchemy" },
            new() { Number = 4,  Name = "Al-Dabaran",        Meaning = "The Follower",                Nature = "Malefic",  Keywords = "Discord, separation, war; building and agriculture favorable" },
            new() { Number = 5,  Name = "Al-Haqa",           Meaning = "The White Spot",              Nature = "Malefic",  Keywords = "Discord, students fail, health endangered" },
            new() { Number = 6,  Name = "Al-Hana",           Meaning = "The Mark",                    Nature = "Neutral",  Keywords = "Love and friendship favorable; hunting and sieges" },
            new() { Number = 7,  Name = "Al-Dhira",          Meaning = "The Forearm",                 Nature = "Benefic",  Keywords = "Gain, love, friendship, voyages, harvests" },
            new() { Number = 8,  Name = "Al-Nathrah",        Meaning = "The Gap",                     Nature = "Neutral",  Keywords = "Love, friendship; victory over enemies; travel mixed" },
            new() { Number = 9,  Name = "Al-Tarf",           Meaning = "The Glance",                  Nature = "Neutral",  Keywords = "Caution for journeys; favorable for crops and sowing" },
            new() { Number = 10, Name = "Al-Jabhah",         Meaning = "The Forehead",                Nature = "Malefic",  Keywords = "Strengthening love; weakening enemies; cures" },
            new() { Number = 11, Name = "Al-Zubrah",         Meaning = "The Mane",                    Nature = "Neutral",  Keywords = "Trade, travel, harvests favorable; besieging and vengeance" },
            new() { Number = 12, Name = "Al-Sarfah",         Meaning = "The Changer",                 Nature = "Malefic",  Keywords = "Separation, discord, lost things not recovered" },
            new() { Number = 13, Name = "Al-Awwa",           Meaning = "The Barker",                  Nature = "Neutral",  Keywords = "Trade and harvests favorable; voyages unfortunate" },
            new() { Number = 14, Name = "Al-Simak",          Meaning = "The Unarmed One",             Nature = "Benefic",  Keywords = "Love, healing, voyages, all good; Spica region" },
            new() { Number = 15, Name = "Al-Ghafr",          Meaning = "The Covering",                Nature = "Neutral",  Keywords = "Hidden things, digging, treasure; unfavorable for journeys" },
            new() { Number = 16, Name = "Al-Zubana",         Meaning = "The Claws",                   Nature = "Malefic",  Keywords = "Impediment to trade and travel; discord in marriage" },
            new() { Number = 17, Name = "Al-Iklil",          Meaning = "The Crown",                   Nature = "Neutral",  Keywords = "Fortifying, building, possible victory; love strengthened" },
            new() { Number = 18, Name = "Al-Qalb",           Meaning = "The Heart",                   Nature = "Malefic",  Keywords = "Treachery, conspiracies, disease; Antares region" },
            new() { Number = 19, Name = "Al-Shaulah",        Meaning = "The Sting",                   Nature = "Malefic",  Keywords = "Sieges favorable; all else unfavorable, especially marriage" },
            new() { Number = 20, Name = "Al-Na'am",          Meaning = "The Ostriches",               Nature = "Neutral",  Keywords = "Taming beasts; strengthening prisons; voyages swift" },
            new() { Number = 21, Name = "Al-Baldah",         Meaning = "The Empty Place",             Nature = "Neutral",  Keywords = "Harvests, buildings; recovery of lost things; waiting" },
            new() { Number = 22, Name = "Sa'd al-Dhabih",    Meaning = "Lucky Star of Slaughter",     Nature = "Neutral",  Keywords = "Healing, escaping imprisonment, fugitives; cures" },
            new() { Number = 23, Name = "Sa'd Bula",         Meaning = "Lucky Star of the Swallower", Nature = "Neutral",  Keywords = "Cures, separation of married couples; freedom" },
            new() { Number = 24, Name = "Sa'd al-Su'ud",     Meaning = "Luckiest of the Lucky",       Nature = "Benefic",  Keywords = "Great good fortune, marital harmony, all endeavors prosper" },
            new() { Number = 25, Name = "Sa'd al-Akhbiyah",  Meaning = "Lucky Star of Hidden Things", Nature = "Neutral",  Keywords = "Besieging, vengeance, recovery; protection of allies" },
            new() { Number = 26, Name = "Al-Fargh al-Awwal", Meaning = "The First Spout",             Nature = "Neutral",  Keywords = "Union, love; destructive to enemies; voyages return" },
            new() { Number = 27, Name = "Al-Fargh al-Thani", Meaning = "The Second Spout",            Nature = "Neutral",  Keywords = "Trade, harvests, gain; cures; good for building" },
            new() { Number = 28, Name = "Batn al-Hut",       Meaning = "Belly of the Fish",           Nature = "Neutral",  Keywords = "Trade favorable; marriage unfavorable; journeys dangerous" },
        ];
    }
}
