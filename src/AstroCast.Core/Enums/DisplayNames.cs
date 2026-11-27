using AstroCast.Core.Models.PrimaryDirections;

namespace AstroCast.Core.Enums;

/// <summary>
/// The display names of the enums the exports and the screen print in words: one place for each name
/// (1.4.0, E3-6).
/// </summary>
/// <remarks>
/// <para>
/// The house-system names were <c>ChartEngine.GetHouseSystemName</c>'s private table, which sets
/// <c>ChartResult.HouseSystemName</c> for every export, and the relationship names were
/// <c>SynastryMarkdownFormatter.FormatRelationshipType</c>'s; both moved here unchanged, so no export moves.
/// The election-type names were new in 1.4.0 and used on screen only; the electional export prints them since
/// branch ExportText (1.7.2; finding N11), where until then it printed the enum name, "BusinessLaunch". The
/// primary-direction key's names (branch Primary, 1.5.0) are for the Directions panel and the exports' Key
/// line. The Moon's phases and the aspects have theirs since branch ExportText.
/// </para>
/// <para>
/// Names are English, the same under every culture, as Core's text is.
/// </para>
/// </remarks>
public static class DisplayNames
{
    /// <summary>Display names for <see cref="HouseSystem"/>.</summary>
    /// <param name="system">The house system.</param>
    extension(HouseSystem system)
    {
        /// <summary>
        /// Gets the house system's name as the exports print it: "Whole Sign", "Equal (from ASC)". A value
        /// outside the enum reads "Placidus", the default, as it always has.
        /// </summary>
        public string DisplayName => system switch
        {
            HouseSystem.Placidus => "Placidus",
            HouseSystem.Koch => "Koch",
            HouseSystem.Equal => "Equal (from ASC)",
            HouseSystem.WholeSign => "Whole Sign",
            HouseSystem.Regiomontanus => "Regiomontanus",
            HouseSystem.Campanus => "Campanus",
            HouseSystem.Alcabitius => "Alcabitius",
            HouseSystem.Porphyry => "Porphyry",
            HouseSystem.Morinus => "Morinus",
            _ => "Placidus",
        };
    }

    /// <summary>Display names for <see cref="RelationshipType"/>.</summary>
    /// <param name="type">The relationship type.</param>
    extension(RelationshipType type)
    {
        /// <summary>
        /// Gets the relationship type's name as the synastry export prints it: "Romantic Partnership",
        /// "Parent &amp; Child". <see cref="RelationshipType.Unspecified"/>, which the export leaves out, reads
        /// "Unspecified".
        /// </summary>
        public string DisplayName => type switch
        {
            RelationshipType.Romantic => "Romantic Partnership",
            RelationshipType.Marriage => "Marriage",
            RelationshipType.ParentChild => "Parent & Child",
            RelationshipType.Siblings => "Siblings",
            RelationshipType.Friendship => "Friendship",
            RelationshipType.Business => "Business Partnership",
            RelationshipType.Colleagues => "Colleagues",
            RelationshipType.TeacherStudent => "Teacher & Student",
            RelationshipType.TherapistClient => "Therapist & Client",
            RelationshipType.Adversarial => "Adversarial",
            RelationshipType.Other => "Other",
            _ => type.ToString(),
        };
    }

    /// <summary>Display names for <see cref="ElectionType"/>.</summary>
    /// <param name="type">The election type.</param>
    extension(ElectionType type)
    {
        /// <summary>
        /// Gets the election type's name in words, as the screen shows it: "Business Launch", "Real Estate
        /// Closing". The electional export prints the enum name instead.
        /// </summary>
        public string DisplayName => type switch
        {
            ElectionType.BusinessLaunch => "Business Launch",
            ElectionType.Contract => "Contract",
            ElectionType.Wedding => "Wedding",
            ElectionType.Surgery => "Surgery",
            ElectionType.RealEstateListing => "Real Estate Listing",
            ElectionType.RealEstateClosing => "Real Estate Closing",
            ElectionType.CreativeLaunch => "Creative Launch",
            ElectionType.Travel => "Travel",
            ElectionType.Other => "Other",
            _ => type.ToString(),
        };
    }

    /// <summary>Display names for <see cref="PrimaryDirectionKey"/>.</summary>
    /// <param name="key">The key.</param>
    extension(PrimaryDirectionKey key)
    {
        /// <summary>
        /// Gets the key's name in words, with its rate: "Naibod (0°59′08″ a year)", "Ptolemy (1° a year)", "True solar
        /// arc in right ascension".
        /// </summary>
        public string DisplayName => key switch
        {
            PrimaryDirectionKey.Naibod => "Naibod (0\u00b059\u203208\u2033 a year)",
            PrimaryDirectionKey.Ptolemy => "Ptolemy (1\u00b0 a year)",
            PrimaryDirectionKey.SolarArcRightAscension => "True solar arc in right ascension",
            _ => key.ToString(),
        };
    }

    /// <summary>Display names for <see cref="MoonPhaseType"/>.</summary>
    /// <param name="phase">The Moon's phase.</param>
    extension(MoonPhaseType phase)
    {
        /// <summary>
        /// Gets the phase's name in words, "Waning Crescent", as the chart export's Moon Conditions print it
        /// (the names <c>MoonConditionInfo.PhaseLabel</c> held, moved here unchanged). The electional export
        /// prints it too since branch ExportText (1.7.2; finding N11); until then a candidate's Moon line and
        /// its strengths printed the enum name, "WaningCrescent".
        /// </summary>
        public string DisplayName => phase switch
        {
            MoonPhaseType.NewMoon => "New Moon",
            MoonPhaseType.WaxingCrescent => "Waxing Crescent",
            MoonPhaseType.FirstQuarter => "First Quarter",
            MoonPhaseType.WaxingGibbous => "Waxing Gibbous",
            MoonPhaseType.FullMoon => "Full Moon",
            MoonPhaseType.WaningGibbous => "Waning Gibbous",
            MoonPhaseType.LastQuarter => "Last Quarter",
            MoonPhaseType.WaningCrescent => "Waning Crescent",
            _ => phase.ToString(),
        };
    }

    /// <summary>Display names for <see cref="AspectType"/>.</summary>
    /// <param name="aspect">The aspect.</param>
    extension(AspectType aspect)
    {
        /// <summary>
        /// Gets the aspect's name in words, "Semi-Sextile" and "Semi-Square" where the enum's names run two
        /// words together; the one-word names are the enum's own. The Annual Profection's Lord's Natal Aspects
        /// print it (branch ExportText, 1.7.2; finding N11): until then "Venus SemiSextile Mercury". The JSON
        /// keeps the enum name.
        /// </summary>
        public string DisplayName => aspect switch
        {
            AspectType.SemiSquare => "Semi-Square",
            AspectType.SemiSextile => "Semi-Sextile",
            _ => aspect.ToString(),
        };
    }
}
