namespace AstroCast.Core.Models.PrimaryDirections;

/// <summary>
/// What a primary direction's promissor is taken from (branch Primary, 1.5.0, rule R4).
/// </summary>
public enum PrimaryDirectionPromissorKind
{
    /// <summary>One of the twelve chart bodies, the Sun to Pluto, the true node and Chiron: its place, an aspect point or an antiscion.</summary>
    Body,

    /// <summary>One of the 55 catalog stars, at its own place.</summary>
    FixedStar,
}
