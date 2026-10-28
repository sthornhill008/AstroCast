using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.MedicalAstrology;

/// <summary>
/// Calculates the humoral temperament from a chart by AstroCast's own scoring
/// scheme (ruling S-D9). The algorithm scores elemental points from the Ascendant sign,
/// Ascendant ruler's sign, Moon sign, Moon dispositor's sign, Sun sign,
/// and each body in an angular house (1, 4, 7, 10).
/// </summary>
/// <remarks>
/// <para>
/// The scheme is the app's own, kept unchanged and named as such on the exports
/// (ruling S-D9, 2026-10-03). It is not the method of any published source; until
/// 1.3.3 these docs said it was based on named traditional authors and the Galenic
/// framework, and it follows none of them (Y5-3).
/// </para>
/// <para>
/// Scoring: Ascendant sign = 3 points, Ascendant ruler sign = 2 points,
/// Moon sign = 3 points, Moon dispositor sign = 2 points, Sun sign = 1 point,
/// each body in house 1, 4, 7 or 10 = 1 point. Each point goes to the element of
/// the sign: Fire Choleric, Earth Melancholic, Air Sanguine, Water Phlegmatic.
/// </para>
/// <para>
/// "Angular" is any of the twelve chart bodies (the Sun to Pluto, the True Node and
/// Chiron) in house 1, 4, 7 or 10, so the Sun and the Moon can score a second time.
/// Houses are by longitude (branch F6), not the 5° orb of the Angular Planets table (Y5-2).
/// </para>
/// <para>
/// A tie is printed as a tie: <see cref="TemperamentProfile.DominantTemperaments"/> and
/// <see cref="TemperamentProfile.SecondaryTemperaments"/> list every temperament at a score.
/// The secondary holds only temperaments that score: when no other element is above 0,
/// there is none.
/// </para>
/// </remarks>
public static class TemperamentCalculator
{
    /// <summary>
    /// Calculate the temperament profile from a chart result.
    /// </summary>
    /// <param name="result">The calculated chart result containing positions and house data.</param>
    /// <returns>A <see cref="TemperamentProfile"/> with elemental scores and dominant temperament.</returns>
    public static TemperamentProfile Calculate(ChartResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        int fire = 0, earth = 0, air = 0, water = 0;

        // Helper to add points to the element of a given sign
        void AddPoints(ZodiacSign sign, int points)
        {
            switch (ElementHelper.GetElement(sign))
            {
                case "Fire": fire += points; break;
                case "Earth": earth += points; break;
                case "Air": air += points; break;
                case "Water": water += points; break;
            }
        }

        // 1. Ascendant sign (3 points) -- strongest factor
        ZodiacSign ascSign = GetSignFromLongitude(result.Houses.Ascendant);
        AddPoints(ascSign, 3);

        // 2. Ascendant ruler's sign (2 points)
        CelestialBodyId ascRuler = ElementHelper.GetTraditionalRuler(ascSign);
        PlanetPosition? ascRulerPosition = result.Positions.FirstOrDefault(p => p.Body == ascRuler);
        if (ascRulerPosition is not null)
            AddPoints(ascRulerPosition.Sign, 2);

        // 3. Moon sign (3 points) -- equal weight to Ascendant
        PlanetPosition? moon = result.Positions.FirstOrDefault(p => p.Body == CelestialBodyId.Moon);
        if (moon is not null)
        {
            AddPoints(moon.Sign, 3);

            // 4. Moon dispositor's sign (2 points)
            CelestialBodyId moonDispositor = ElementHelper.GetTraditionalRuler(moon.Sign);
            PlanetPosition? dispositorPosition = result.Positions.FirstOrDefault(p => p.Body == moonDispositor);
            if (dispositorPosition is not null)
                AddPoints(dispositorPosition.Sign, 2);
        }

        // 5. Sun sign (1 point) -- seasonal indicator
        PlanetPosition? sun = result.Positions.FirstOrDefault(p => p.Body == CelestialBodyId.Sun);
        if (sun is not null)
            AddPoints(sun.Sign, 1);

        // 6. Angular planets (1 point each) -- houses 1, 4, 7, 10
        foreach (PlanetPosition position in result.Positions)
        {
            if (position.House is 1 or 4 or 7 or 10)
                AddPoints(position.Sign, 1);
        }

        // Determine dominant and secondary temperaments
        (string Element, int Score, Temperament Temperament)[] scores =
        [
            ("Fire", fire, Temperament.Choleric),
            ("Earth", earth, Temperament.Melancholic),
            ("Air", air, Temperament.Sanguine),
            ("Water", water, Temperament.Phlegmatic)
        ];

        (string Element, int Score, Temperament Temperament)[] ordered =
            scores.OrderByDescending(s => s.Score).ToArray();

        return new TemperamentProfile
        {
            FireScore = fire,
            EarthScore = earth,
            AirScore = air,
            WaterScore = water,
            DominantTemperament = ordered[0].Temperament,
            SecondaryTemperament = ordered[1].Temperament
        };
    }

    /// <summary>
    /// Derive the zodiac sign from an ecliptic longitude (0-360 degrees).
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in decimal degrees.</param>
    /// <returns>The zodiac sign at the given longitude.</returns>
    private static ZodiacSign GetSignFromLongitude(double longitude) =>
        (ZodiacSign)((int)(longitude % 360.0 / 30.0) + 1);
}
