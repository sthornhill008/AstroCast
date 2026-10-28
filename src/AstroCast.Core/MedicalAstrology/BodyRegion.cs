namespace AstroCast.Core.MedicalAstrology;

/// <summary>
/// Represents a region or system of the human body associated with a zodiac sign
/// or planet in traditional medical astrology. This is a data record used for
/// correspondence lookups — not a medical diagnosis.
/// </summary>
/// <param name="Name">The body region or organ name (e.g., "Head", "Liver", "Nervous system").</param>
/// <param name="Description">Brief description of the traditional association.</param>
public sealed record BodyRegion(string Name, string Description);
