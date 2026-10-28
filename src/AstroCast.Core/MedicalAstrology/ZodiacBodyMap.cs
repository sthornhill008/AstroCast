using AstroCast.Core.Enums;

namespace AstroCast.Core.MedicalAstrology;

/// <summary>
/// Maps each zodiac sign to its traditionally associated body regions and
/// health vulnerabilities. Based on the Aries-to-Pisces head-to-toe
/// melothesia system documented by Galen, Culpeper, and the medieval
/// medical astrology tradition.
/// </summary>
/// <remarks>
/// <para>
/// These correspondences are provided for educational and traditional study
/// purposes only. They do not constitute medical advice or diagnosis.
/// </para>
/// <para>
/// Sources: Eileen Nauman (Medical Astrology), Noel Tyl (Astrological Timing
/// of Critical Illness), traditional Galenic melothesia.
/// </para>
/// </remarks>
public static class ZodiacBodyMap
{
    /// <summary>
    /// Gets the body regions traditionally associated with the specified zodiac sign.
    /// </summary>
    /// <param name="sign">The zodiac sign.</param>
    /// <returns>Read-only list of body region associations.</returns>
    public static IReadOnlyList<BodyRegion> GetBodyRegions(ZodiacSign sign) =>
        Regions.TryGetValue(sign, out var regions) ? regions : [];

    /// <summary>
    /// Gets a brief summary of the traditional health focus for the specified sign.
    /// </summary>
    /// <param name="sign">The zodiac sign.</param>
    /// <returns>A short summary string.</returns>
    public static string GetHealthSummary(ZodiacSign sign) =>
        Summaries.TryGetValue(sign, out var summary) ? summary : "No traditional health associations recorded.";

    /// <summary>
    /// Gets all zodiac signs and their associated body regions.
    /// </summary>
    public static IReadOnlyDictionary<ZodiacSign, IReadOnlyList<BodyRegion>> AllRegions => Regions;

    private static readonly Dictionary<ZodiacSign, IReadOnlyList<BodyRegion>> Regions = new()
    {
        [ZodiacSign.Aries] =
        [
            new("Head and skull", "Rules the cranium, brain, and facial bones"),
            new("Eyes and face", "Governs eyesight, facial muscles, and sinuses"),
            new("Adrenal glands", "Associated with the adrenal and suprarenal glands"),
            new("Pineal gland", "Traditional rulership of the 'third eye' region")
        ],
        [ZodiacSign.Taurus] =
        [
            new("Neck and throat", "Rules the cervical vertebrae, throat, and larynx"),
            new("Thyroid gland", "Governs thyroid function and metabolism"),
            new("Vocal cords", "Associated with voice production and tone"),
            new("Ears", "Traditional correspondence with the lower ear and Eustachian tubes")
        ],
        [ZodiacSign.Gemini] =
        [
            new("Arms and hands", "Rules the upper limbs, fingers, and dexterity"),
            new("Lungs and bronchi", "Governs respiratory passages and gas exchange"),
            new("Shoulders", "Associated with the shoulder girdle and clavicles"),
            new("Nervous system", "Traditional correspondence with peripheral nerves and reflexes")
        ],
        [ZodiacSign.Cancer] =
        [
            new("Chest and breasts", "Rules the thoracic cage and mammary tissue"),
            new("Stomach", "Governs gastric function and digestive secretions"),
            new("Alimentary canal", "Associated with the upper digestive tract"),
            new("Uterus", "Traditional correspondence with the womb and nurturing functions")
        ],
        [ZodiacSign.Leo] =
        [
            new("Heart", "Rules the cardiac muscle, pericardium, and coronary arteries"),
            new("Spine and upper back", "Governs the thoracic spine and dorsal region"),
            new("Spinal cord", "Associated with the central nervous pathway"),
            new("Circulatory vitality", "Traditional correspondence with vital force and circulation")
        ],
        [ZodiacSign.Virgo] =
        [
            new("Intestines", "Rules the small intestine and processes of assimilation"),
            new("Digestive system", "Governs enzymatic breakdown and nutrient absorption"),
            new("Spleen", "Associated with blood filtration and immune cell production"),
            new("Pancreas", "Traditional correspondence with insulin production and sugar metabolism")
        ],
        [ZodiacSign.Libra] =
        [
            new("Kidneys", "Rules renal function, fluid balance, and electrolyte regulation"),
            new("Lumbar region", "Governs the lower back and lumbar vertebrae"),
            new("Skin", "Associated with the integumentary system and complexion"),
            new("Adrenal cortex", "Traditional correspondence with cortisol and stress hormones")
        ],
        [ZodiacSign.Scorpio] =
        [
            new("Reproductive system", "Rules the generative organs and reproductive processes"),
            new("Eliminative system", "Governs the colon, rectum, and excretory functions"),
            new("Blood and blood cells", "Associated with blood formation and regeneration"),
            new("Nose", "Traditional correspondence with the nasal passages and pheromone detection")
        ],
        [ZodiacSign.Sagittarius] =
        [
            new("Hips and thighs", "Rules the pelvic girdle, femur, and upper legs"),
            new("Liver", "Governs hepatic function, bile production, and detoxification"),
            new("Sciatic nerve", "Associated with the largest nerve in the body"),
            new("Pituitary gland", "Traditional correspondence with the master endocrine gland")
        ],
        [ZodiacSign.Capricorn] =
        [
            new("Knees and joints", "Rules the patella, cartilage, and joint structures"),
            new("Skeletal system", "Governs bone density, bone marrow, and mineralisation"),
            new("Teeth", "Associated with dental health and enamel"),
            new("Skin structure", "Traditional correspondence with skin elasticity and connective tissue")
        ],
        [ZodiacSign.Aquarius] =
        [
            new("Ankles and calves", "Rules the lower legs, Achilles tendon, and shin"),
            new("Circulatory system", "Governs venous return, varicose conditions, and blood flow"),
            new("Shins and lower legs", "Associated with the tibial and fibular region"),
            new("Nervous system (higher)", "Traditional correspondence with electrical impulses and nerve conductivity")
        ],
        [ZodiacSign.Pisces] =
        [
            new("Feet and toes", "Rules the plantar surface, metatarsals, and phalanges"),
            new("Lymphatic system", "Governs lymph drainage, immunity, and fluid balance"),
            new("Immune system", "Associated with immunological resilience and sensitivity"),
            new("Pineal gland / sleep", "Traditional correspondence with melatonin and dream states")
        ]
    };

    private static readonly Dictionary<ZodiacSign, string> Summaries = new()
    {
        [ZodiacSign.Aries] = "Focus areas: head, eyes, sinuses, adrenals. Traditionally associated with headaches, migraines, and inflammation.",
        [ZodiacSign.Taurus] = "Focus areas: throat, neck, thyroid, ears. Traditionally associated with sore throats, thyroid conditions, and vocal strain.",
        [ZodiacSign.Gemini] = "Focus areas: lungs, arms, shoulders, nerves. Traditionally associated with respiratory conditions and nervous tension.",
        [ZodiacSign.Cancer] = "Focus areas: chest, stomach, breasts, digestion. Traditionally associated with digestive sensitivity and fluid retention.",
        [ZodiacSign.Leo] = "Focus areas: heart, spine, upper back, vitality. Traditionally associated with cardiovascular conditions and back strain.",
        [ZodiacSign.Virgo] = "Focus areas: intestines, digestion, pancreas, spleen. Traditionally associated with digestive disorders and dietary sensitivity.",
        [ZodiacSign.Libra] = "Focus areas: kidneys, lower back, skin, hormonal balance. Traditionally associated with renal conditions and skin sensitivity.",
        [ZodiacSign.Scorpio] = "Focus areas: reproductive organs, eliminative system, blood. Traditionally associated with regenerative processes and toxin buildup.",
        [ZodiacSign.Sagittarius] = "Focus areas: hips, thighs, liver, sciatic nerve. Traditionally associated with sports injuries, liver conditions, and sciatica.",
        [ZodiacSign.Capricorn] = "Focus areas: knees, joints, bones, teeth. Traditionally associated with joint stiffness, fractures, and dental issues.",
        [ZodiacSign.Aquarius] = "Focus areas: ankles, calves, circulation, nervous system. Traditionally associated with circulatory issues and ankle injuries.",
        [ZodiacSign.Pisces] = "Focus areas: feet, lymphatic system, immune system. Traditionally associated with foot problems, immune sensitivity, and fluid imbalances."
    };
}
