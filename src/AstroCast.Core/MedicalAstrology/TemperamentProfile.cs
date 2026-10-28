using System.Globalization;

namespace AstroCast.Core.MedicalAstrology;

/// <summary>
/// The result of a temperament calculation, showing the elemental balance
/// and dominant temperament derived from a chart. Scored by AstroCast's own
/// scheme (ruling S-D9) from the Ascendant, Ascendant ruler, Moon, Moon
/// dispositor, Sun sign, and each body in house 1, 4, 7 or 10.
/// </summary>
public sealed class TemperamentProfile
{
    /// <summary>Fire element score (Hot + Dry -> Choleric).</summary>
    public required int FireScore { get; init; }

    /// <summary>Earth element score (Cold + Dry -> Melancholic).</summary>
    public required int EarthScore { get; init; }

    /// <summary>Air element score (Hot + Moist -> Sanguine).</summary>
    public required int AirScore { get; init; }

    /// <summary>Water element score (Cold + Moist -> Phlegmatic).</summary>
    public required int WaterScore { get; init; }

    /// <summary>
    /// The dominant temperament as <see cref="TemperamentCalculator"/> sets it: the first of the four elements
    /// ordered by score, highest first, equal scores in the order Fire, Earth, Air, Water. That is the first of
    /// <see cref="DominantTemperaments"/>.
    /// </summary>
    /// <remarks>
    /// A tie for first does not show here: <see cref="DominantTemperaments"/> lists every temperament at the highest
    /// score, and the exports print that list (ruling S-D9: a tie is printed as a tie).
    /// </remarks>
    public required Temperament DominantTemperament { get; init; }

    /// <summary>
    /// The secondary temperament as <see cref="TemperamentCalculator"/> sets it: the second of the four elements
    /// ordered by score, highest first, equal scores in the order Fire, Earth, Air, Water. Without a tie for first,
    /// that is the first of <see cref="SecondaryTemperaments"/> when another element scores; when none does, that
    /// list is empty and this still holds the second in score order (chart 21: Melancholic). With a tie for first,
    /// it is the second of the temperaments tied for first (Fire 2, Earth 3, Air 5, Water 5: Phlegmatic).
    /// </summary>
    /// <remarks>
    /// <see cref="SecondaryTemperaments"/> lists every temperament at the highest score below the dominant's, and
    /// the exports print that list (ruling S-D9).
    /// </remarks>
    public required Temperament SecondaryTemperament { get; init; }

    /// <summary>
    /// Every temperament whose element has the highest score, in the order Fire (Choleric), Earth (Melancholic),
    /// Air (Sanguine), Water (Phlegmatic). More than one is a tie for first.
    /// </summary>
    /// <remarks>
    /// Read from <see cref="FireScore"/>, <see cref="EarthScore"/>, <see cref="AirScore"/> and
    /// <see cref="WaterScore"/> alone (ruling S-D9: a tie is printed as a tie, Y5-1). Until 1.3.3 only
    /// <see cref="DominantTemperament"/> existed, and a tie for first printed as a ranking.
    /// </remarks>
    public IReadOnlyList<Temperament> DominantTemperaments => AtScore(HighestScore);

    /// <summary>
    /// Every temperament whose element has the highest score below the dominant's, provided that score is above 0, in
    /// the order Fire (Choleric), Earth (Melancholic), Air (Sanguine), Water (Phlegmatic). More than one is a tie for
    /// second; empty when all four scores are equal, or when no other element scores.
    /// </summary>
    /// <remarks>
    /// Read from the four scores alone (ruling S-D9, Y5-1). A temperament with no points is not a secondary (Steve's
    /// ruling of 2026-10-12): Fire 14 and the rest 0 has none.
    /// </remarks>
    public IReadOnlyList<Temperament> SecondaryTemperaments => SecondScore is int second ? AtScore(second) : [];

    /// <summary>
    /// <see cref="DominantTemperaments"/> as the exports print it: one temperament alone ("Sanguine"), two or more
    /// joined and marked with their score ("Sanguine and Phlegmatic (tied, 5 each)").
    /// </summary>
    internal string DominantText => Describe(DominantTemperaments, HighestScore);

    /// <summary>
    /// <see cref="SecondaryTemperaments"/> as the exports print it: as <see cref="DominantText"/>
    /// ("Choleric and Melancholic (tied, 4 each)"), or "none" when the list is empty: all four scores equal, or no
    /// other element scoring.
    /// </summary>
    internal string SecondaryText => SecondScore is int second ? Describe(SecondaryTemperaments, second) : "none";

    /// <summary>Total points across all four elements.</summary>
    public int TotalPoints => FireScore + EarthScore + AirScore + WaterScore;

    /// <summary>
    /// Brief traditional description of the dominant temperament.
    /// </summary>
    public string Description => DominantTemperament switch
    {
        Temperament.Sanguine => "Sanguine (Hot & Moist): Warm constitution, sociable nature, strong circulation. Humor: Blood. Season: Spring. The sanguine temperament tends toward warmth and moisture, favouring the blood and respiratory system.",
        Temperament.Choleric => "Choleric (Hot & Dry): Energetic constitution, decisive nature, strong metabolism. Humor: Yellow Bile. Season: Summer. The choleric temperament tends toward heat and dryness, favouring the muscular and hepatic systems.",
        Temperament.Melancholic => "Melancholic (Cold & Dry): Careful constitution, persistent nature, strong structure. Humor: Black Bile. Season: Autumn. The melancholic temperament tends toward cold and dryness, favouring the skeletal and nervous systems.",
        Temperament.Phlegmatic => "Phlegmatic (Cold & Moist): Calm constitution, empathic nature, strong fluid balance. Humor: Phlegm. Season: Winter. The phlegmatic temperament tends toward cold and moisture, favouring the lymphatic and reproductive systems.",
        _ => "Unknown temperament."
    };

    /// <summary>The four element scores with their temperaments, in the order Fire, Earth, Air, Water.</summary>
    private (int Score, Temperament Temperament)[] ByElement =>
    [
        (FireScore, Temperament.Choleric),
        (EarthScore, Temperament.Melancholic),
        (AirScore, Temperament.Sanguine),
        (WaterScore, Temperament.Phlegmatic)
    ];

    /// <summary>The highest of the four element scores.</summary>
    private int HighestScore => ByElement.Max(e => e.Score);

    /// <summary>
    /// The highest element score below <see cref="HighestScore"/>, or null when all four are equal or that score is not
    /// above 0.
    /// </summary>
    private int? SecondScore
    {
        get
        {
            int highest = HighestScore;
            int[] below = [.. ByElement.Select(e => e.Score).Where(s => s < highest)];
            int? second = below.Length == 0 ? null : below.Max();
            return second > 0 ? second : null;
        }
    }

    /// <summary>The temperaments whose element has the given score, in the order Fire, Earth, Air, Water.</summary>
    /// <param name="score">The score to match.</param>
    /// <returns>The matching temperaments.</returns>
    private Temperament[] AtScore(int score) => [.. ByElement.Where(e => e.Score == score).Select(e => e.Temperament)];

    /// <summary>
    /// One temperament alone; two or more joined with ", " and a final " and ", then " (tied, N each)".
    /// </summary>
    /// <param name="temperaments">The temperaments at one score, at least one.</param>
    /// <param name="score">Their score.</param>
    /// <returns>The printed text, culture-independent.</returns>
    private static string Describe(IReadOnlyList<Temperament> temperaments, int score) =>
        temperaments.Count == 1
            ? temperaments[0].ToString()
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{string.Join(", ", temperaments.Take(temperaments.Count - 1))} and {temperaments[^1]} (tied, {score} each)");
}
