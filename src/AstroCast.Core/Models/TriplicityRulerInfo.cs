namespace AstroCast.Core.Models;

/// <summary>
/// Triplicity rulers for a zodiac sign's element, using the Dorothean system.
/// Each element has a day ruler, night ruler, and participating ruler.
/// </summary>
/// <remarks>
/// <para>
/// The Dorothean triplicity rulers are:
/// </para>
/// <list type="table">
///   <listheader><term>Element</term><description>Day / Night / Participating</description></listheader>
///   <item><term>Fire (Aries, Leo, Sagittarius)</term><description>Sun / Jupiter / Saturn</description></item>
///   <item><term>Earth (Taurus, Virgo, Capricorn)</term><description>Venus / Moon / Mars</description></item>
///   <item><term>Air (Gemini, Libra, Aquarius)</term><description>Saturn / Mercury / Jupiter</description></item>
///   <item><term>Water (Cancer, Scorpio, Pisces)</term><description>Venus / Mars / Moon</description></item>
/// </list>
/// </remarks>
public sealed class TriplicityRulerInfo
{
    /// <summary>
    /// The element of the sign: "Fire", "Earth", "Air", or "Water".
    /// </summary>
    public required string Element { get; init; }

    /// <summary>
    /// The triplicity ruler active in a day chart (Sun above horizon).
    /// </summary>
    public required string DayRuler { get; init; }

    /// <summary>
    /// The triplicity ruler active in a night chart (Sun below horizon).
    /// </summary>
    public required string NightRuler { get; init; }

    /// <summary>
    /// The participating ruler, active in both day and night charts
    /// as a secondary influence.
    /// </summary>
    public required string ParticipatingRuler { get; init; }

    /// <summary>
    /// The sect-appropriate triplicity ruler for this chart: <see cref="DayRuler"/>
    /// in a day chart, <see cref="NightRuler"/> in a night chart.
    /// </summary>
    public required string ActiveRuler { get; init; }
}
