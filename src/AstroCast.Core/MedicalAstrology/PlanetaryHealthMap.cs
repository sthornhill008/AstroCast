using AstroCast.Core.Enums;

namespace AstroCast.Core.MedicalAstrology;

/// <summary>
/// Maps each planet to its traditionally associated organ systems, physiological
/// processes, and health tendencies. Based on Reinhold Ebertin's biological
/// correspondences (COSI), Eileen Nauman's Med-Scan system, and the
/// traditional Galenic-Ptolemaic framework.
/// </summary>
/// <remarks>
/// These correspondences are provided for educational and traditional study
/// purposes only. They do not constitute medical advice or diagnosis.
/// </remarks>
public static class PlanetaryHealthMap
{
    /// <summary>
    /// Gets the organ/system associations for the specified planet.
    /// </summary>
    /// <param name="body">The celestial body.</param>
    /// <returns>Read-only list of body region associations.</returns>
    public static IReadOnlyList<BodyRegion> GetBodyRegions(CelestialBodyId body) =>
        Regions.TryGetValue(body, out var regions) ? regions : [];

    /// <summary>
    /// Gets a brief description of the planet's traditional health role.
    /// </summary>
    /// <param name="body">The celestial body.</param>
    /// <returns>Summary string or empty if unknown.</returns>
    public static string GetHealthRole(CelestialBodyId body) =>
        Roles.TryGetValue(body, out var role) ? role : string.Empty;

    private static readonly Dictionary<CelestialBodyId, IReadOnlyList<BodyRegion>> Regions = new()
    {
        [CelestialBodyId.Sun] =
        [
            new("Heart and circulatory centre", "Rules the cardiac muscle and arterial vitality"),
            new("Thymus gland", "Associated with immune maturation in youth"),
            new("Right eye (male) / Left eye (female)", "Traditional solar rulership of the dominant eye"),
            new("Vital force", "Governs overall constitutional strength and recovery power")
        ],
        [CelestialBodyId.Moon] =
        [
            new("Stomach and gastric function", "Rules digestive secretions and appetite rhythms"),
            new("Breasts and uterus", "Governs reproductive fluids and nurturing organs"),
            new("Lymphatic system", "Associated with fluid balance and lymph drainage"),
            new("Left eye (male) / Right eye (female)", "Traditional lunar rulership of the receptive eye")
        ],
        [CelestialBodyId.Mercury] =
        [
            new("Nervous system", "Rules nerve impulse transmission and reflexes"),
            new("Lungs and respiratory passages", "Governs breathing, bronchi, and gas exchange"),
            new("Thyroid gland", "Associated with metabolic rate and hormonal communication"),
            new("Hands and dexterity", "Traditional correspondence with fine motor coordination")
        ],
        [CelestialBodyId.Venus] =
        [
            new("Kidneys and renal system", "Rules fluid filtration and electrolyte balance"),
            new("Throat and vocal cords", "Governs voice quality, tonsils, and palate"),
            new("Venous circulation", "Associated with venous return and vascular tone"),
            new("Sugar metabolism", "Ebertin/Tyl: correspondence with insulin and glucose regulation")
        ],
        [CelestialBodyId.Mars] =
        [
            new("Muscles and muscular system", "Rules voluntary muscle function and physical exertion"),
            new("Blood and iron metabolism", "Governs red blood cell production and haemoglobin"),
            new("Adrenal glands", "Associated with adrenaline, cortisol, and stress response"),
            new("Inflammatory response", "Traditional correspondence with fever, infection, and acute illness")
        ],
        [CelestialBodyId.Jupiter] =
        [
            new("Liver and hepatic function", "Rules bile production, glycogen storage, and detoxification"),
            new("Arterial system", "Governs arterial expansion, blood pressure regulation"),
            new("Pituitary gland", "Associated with growth hormone and endocrine orchestration"),
            new("Fat metabolism", "Traditional correspondence with lipid storage and weight regulation")
        ],
        [CelestialBodyId.Saturn] =
        [
            new("Bones and skeletal system", "Rules bone density, mineralisation, and structural integrity"),
            new("Teeth and dental health", "Governs enamel, dental roots, and jaw structure"),
            new("Joints and cartilage", "Associated with joint lubrication, arthritis, and stiffness"),
            new("Skin and connective tissue", "Traditional correspondence with skin aging and chronic conditions")
        ],
        [CelestialBodyId.Uranus] =
        [
            new("Nervous system (higher)", "Rules electrical impulses, nerve conductivity, and sudden spasms"),
            new("Ankles and lower legs", "Governs ankle stability and Achilles tendon"),
            new("Pituitary gland (electrical)", "Associated with sudden hormonal shifts"),
            new("Accidents and sudden onset", "Traditional correspondence with shock, spasm, and trauma")
        ],
        [CelestialBodyId.Neptune] =
        [
            new("Pineal gland", "Rules melatonin production, sleep-wake cycles, and dream states"),
            new("Lymphatic and immune system", "Governs immune sensitivity, allergic response"),
            new("Feet", "Associated with plantar health and gait"),
            new("Drug sensitivity", "Traditional correspondence with medication reactions and toxin sensitivity")
        ],
        [CelestialBodyId.Pluto] =
        [
            new("Reproductive organs (generative)", "Rules cellular regeneration and reproductive transformation"),
            new("Eliminative system", "Governs deep detoxification and waste removal"),
            new("Cellular regeneration", "Associated with DNA repair and cell death/renewal cycles"),
            new("Endocrine system (deep)", "Traditional correspondence with hormonal transformation and puberty/menopause")
        ]
    };

    private static readonly Dictionary<CelestialBodyId, string> Roles = new()
    {
        [CelestialBodyId.Sun] = "Constitutional vitality, core life force, and recovery power. When afflicted by hard aspects, may indicate vulnerability in heart function, circulation, or overall vitality.",
        [CelestialBodyId.Moon] = "Emotional health, fluid balance, and nurturing functions. When afflicted, may indicate sensitivity in digestion, fluid retention, or emotional wellbeing.",
        [CelestialBodyId.Mercury] = "Nervous communication, coordination, and respiratory function. When afflicted, may indicate areas of focus in nerves, lungs, or thyroid.",
        [CelestialBodyId.Venus] = "Balance, harmony, and venous circulation. When afflicted, may indicate areas of focus in kidneys, sugar metabolism, or skin.",
        [CelestialBodyId.Mars] = "Physical energy, inflammation, and surgical response. When afflicted, may indicate areas of focus in muscles, blood, or acute conditions.",
        [CelestialBodyId.Jupiter] = "Growth, expansion, and liver function. When afflicted, may indicate areas of focus in liver, arterial system, or weight.",
        [CelestialBodyId.Saturn] = "Structure, restriction, and chronic conditions. When afflicted, may indicate areas of focus in bones, joints, teeth, or skin.",
        [CelestialBodyId.Uranus] = "Sudden onset, electrical impulses, and nervous tension. When afflicted, may indicate areas of focus in nervous system, spasms, or accidents.",
        [CelestialBodyId.Neptune] = "Sensitivity, immunity, and drug response. When afflicted, may indicate areas of focus in immune system, sleep, or medication sensitivity.",
        [CelestialBodyId.Pluto] = "Regeneration, transformation, and deep healing. When afflicted, may indicate areas of focus in reproductive system, elimination, or cellular renewal."
    };
}
