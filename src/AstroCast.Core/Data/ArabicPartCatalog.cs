using AstroCast.Core.Models;

namespace AstroCast.Core.Data;

/// <summary>
/// Complete catalog of Arabic Part and Hellenistic Lot definitions.
/// Contains 15 entries: 8 traditional Arabic Parts and 7 Hermetic Lots.
/// </summary>
/// <remarks>
/// The Lot of Fortune and Lot of Spirit in the Hermetic group use the same
/// formulas as Part of Fortune and Part of Spirit in the Arabic group.
/// Both are included with their respective group labels.
/// Fortune and Spirit have <see cref="ArabicPartDefinition.ComputeOrder"/> = 1
/// so they are computed before the Hermetic Lots that reference them.
/// </remarks>
public static class ArabicPartCatalog
{
    /// <summary>
    /// All 15 Part/Lot definitions, ordered by computation priority then by group.
    /// </summary>
    public static IReadOnlyList<ArabicPartDefinition> All { get; } = BuildCatalog();

    private static List<ArabicPartDefinition> BuildCatalog()
    {
        return
        [
            // --- Arabic Parts (8) ---
            // Fortune and Spirit computed first (order 1) — other Lots reference them
            new()
            {
                Name = "Part of Fortune", Group = "Arabic",
                FactorA = "Moon", FactorB = "Sun", ReversesAtNight = true,
                Domain = "General wellbeing, material fortune",
                ComputeOrder = 1,
            },
            new()
            {
                Name = "Part of Spirit", Group = "Arabic",
                FactorA = "Sun", FactorB = "Moon", ReversesAtNight = true,
                Domain = "Conscious will, intention, career",
                ComputeOrder = 1,
            },
            new()
            {
                Name = "Part of Marriage", Group = "Arabic",
                FactorA = "Desc", FactorB = "Venus", ReversesAtNight = false,
                Domain = "Relationships, partnership",
            },
            new()
            {
                Name = "Part of Children", Group = "Arabic",
                FactorA = "Jupiter", FactorB = "Saturn", ReversesAtNight = true,
                Domain = "Offspring, creativity",
            },
            new()
            {
                Name = "Part of Death", Group = "Arabic",
                FactorA = "Cusp8", FactorB = "Moon", ReversesAtNight = false,
                Domain = "Mortality, endings, major loss",
            },
            new()
            {
                Name = "Part of Illness", Group = "Arabic",
                FactorA = "Mars", FactorB = "Saturn", ReversesAtNight = true,
                Domain = "Health crises",
            },
            new()
            {
                Name = "Part of Vocation", Group = "Arabic",
                Base = "MC",
                FactorA = "Moon", FactorB = "Sun", ReversesAtNight = false,
                Domain = "Career, public role",
            },
            new()
            {
                Name = "Part of Exaltation", Group = "Arabic",
                FactorA = "19Ari", FactorB = "Sun", ReversesAtNight = true,
                Domain = "Honours, recognition",
            },

            // --- Hermetic Lots (7) ---
            // Fortune and Spirit duplicated here with Hermetic labels (same formula, order 1)
            new()
            {
                Name = "Lot of Fortune", Group = "Hermetic",
                FactorA = "Moon", FactorB = "Sun", ReversesAtNight = true,
                Domain = "Body, material circumstance",
                ComputeOrder = 1,
            },
            new()
            {
                Name = "Lot of Spirit", Group = "Hermetic",
                FactorA = "Sun", FactorB = "Moon", ReversesAtNight = true,
                Domain = "Soul, intention, action",
                ComputeOrder = 1,
            },
            new()
            {
                Name = "Lot of Eros", Group = "Hermetic",
                FactorA = "Venus", FactorB = "Spirit", ReversesAtNight = true,
                Domain = "Desire, love, pleasure",
            },
            // Paulus Alexandrinus, ch. 23 (ruling L1): by day Asc + Fortune − Mercury,
            // reversed by night to Asc + Mercury − Fortune
            new()
            {
                Name = "Lot of Necessity", Group = "Hermetic",
                FactorA = "Fortune", FactorB = "Mercury", ReversesAtNight = true,
                Domain = "Compulsion, obligation, enemies",
            },
            // Paulus Alexandrinus, ch. 23 (ruling L1): by day Asc + Fortune − Mars,
            // reversed by night to Asc + Mars − Fortune
            new()
            {
                Name = "Lot of Courage", Group = "Hermetic",
                FactorA = "Fortune", FactorB = "Mars", ReversesAtNight = true,
                Domain = "Daring, conflict, initiative",
            },
            new()
            {
                Name = "Lot of Victory", Group = "Hermetic",
                FactorA = "Jupiter", FactorB = "Spirit", ReversesAtNight = true,
                Domain = "Success, allies, recognition",
            },
            new()
            {
                Name = "Lot of Nemesis", Group = "Hermetic",
                FactorA = "Fortune", FactorB = "Saturn", ReversesAtNight = true,
                Domain = "Hidden enemies, fate, retribution",
            },
        ];
    }
}
