using System.Globalization;

using AstroCast.Core.Formatting;

namespace AstroCast.Core.Models;

/// <summary>
/// A detected antiscia or contra-antiscia connection between two planets.
/// An antiscia connection acts as a hidden conjunction; a contra-antiscia
/// connection acts as a hidden opposition.
/// </summary>
public sealed class AntisciaConnection
{
    /// <summary>First planet in the connection.</summary>
    public required string Planet1 { get; init; }

    /// <summary>Second planet in the connection.</summary>
    public required string Planet2 { get; init; }

    /// <summary>
    /// Type of connection: "Antiscia" (hidden conjunction) or
    /// "ContraAntiscia" (hidden opposition).
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The orb of the connection in degrees. This is the angular distance
    /// between planet 1's antiscion (or contra-antiscion) point and
    /// planet 2's actual position. Tighter orbs are more significant.
    /// Classical maximum: 2°.
    /// </summary>
    public required double Orb { get; init; }

    /// <summary>
    /// Human-readable notation (e.g., "Mars antiscia Venus (0°45′)"), the orb from
    /// <see cref="PositionFormatter.FormatOrb"/>.
    /// </summary>
    public string Notation
    {
        get
        {
            string typeLabel = Type == "Antiscia" ? "antiscia" : "contra-antiscia";
            return string.Create(CultureInfo.InvariantCulture, $"{Planet1} {typeLabel} {Planet2} ({PositionFormatter.FormatOrb(Orb)})");
        }
    }
}
