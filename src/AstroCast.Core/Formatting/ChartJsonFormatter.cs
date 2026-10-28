using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Serializes a <see cref="ChartResult"/> to JSON for AI consumption: the file both heads' Export JSON writes.
/// Uses camelCase naming and indented formatting for readability.
/// </summary>
public static class ChartJsonFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Serialize a <see cref="ChartResult"/> to a JSON string.
    /// </summary>
    /// <param name="result">The chart calculation result to serialize.</param>
    /// <returns>A JSON string with camelCase properties and indented formatting.</returns>
    public static string ToJson(ChartResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var output = new
        {
            chart = new
            {
                name = result.Input.Name,
                dateTime = result.Input.LocalDateTime.ToString("o"),
                timeZone = result.Input.TimeZone,
                utcDateTime = CalendarDateFormatter.Iso(result.UtcDateTime),
                calendar = CalendarDateFormatter.IsJulian(result.UtcDateTime) ? "Julian" : null,
                julianDay = result.JulianDay,
                location = new
                {
                    name = result.Input.LocationName,
                    latitude = result.Input.Latitude,
                    longitude = result.Input.Longitude,
                },
                gender = result.Input.Gender.ToString(),
                sourceNotes = result.Input.SourceNotes,
                eventType = result.Input.EventType,
                eventDescription = result.Input.EventDescription,
                notes = result.Input.Notes,
                useExplicitOffset = result.Input.UseExplicitOffset ? true : (bool?)null,
                utcOffsetHours = result.Input.UseExplicitOffset ? result.Input.UtcOffsetHours : null,
                resolvedUtcOffsetHours = result.ResolvedUtcOffsetHours,
                resolvedOffsetLabel = result.ResolvedOffsetLabel,
                timeNote = result.TimeNote,
            },
            positions = result.Positions.Select(p => new
            {
                body = p.Name,
                longitude = Math.Round(p.Longitude, 4),
                latitude = Math.Round(p.Latitude, 4),
                sign = p.Sign.ToString(),
                formattedPosition = PositionFormatter.FormatPosition(p),
                house = p.House,
                houseExact = p.HouseExact.HasValue
                    ? Math.Round(p.HouseExact.Value, 2)
                    : (double?)null,
                speed = Math.Round(p.SpeedInLongitude, 4),
                direction = p.DirectionCode,
                isRetrograde = p.IsRetrograde,
                rightAscension = p.RightAscension.HasValue
                    ? Math.Round(p.RightAscension.Value, 4)
                    : (double?)null,
                declination = p.Declination.HasValue
                    ? Math.Round(p.Declination.Value, 4)
                    : (double?)null,
            }),
            // The five minor points, directly after the positions and never among them (R3; scoping decision
            // 4; branch AsteroidsExport, 1.6.0): null, so omitted, when the chart carries none, else one
            // element per point with the positions' keys, out of bounds against the chart's obliquity, and its
            // aspects at 2° in the chart's order, each with the row's own orb, never the definition's.
            minorPoints = result.MinorPoints.Count == 0 ? null : result.MinorPoints.Select(p => new
            {
                body = p.Name,
                longitude = Math.Round(p.Longitude, 4),
                latitude = Math.Round(p.Latitude, 4),
                sign = p.Sign.ToString(),
                formattedPosition = PositionFormatter.FormatPosition(p),
                house = p.House,
                houseExact = p.HouseExact.HasValue
                    ? Math.Round(p.HouseExact.Value, 2)
                    : (double?)null,
                speed = Math.Round(p.SpeedInLongitude, 4),
                direction = p.DirectionCode,
                isRetrograde = p.IsRetrograde,
                rightAscension = p.RightAscension.HasValue
                    ? Math.Round(p.RightAscension.Value, 4)
                    : (double?)null,
                declination = p.Declination.HasValue
                    ? Math.Round(p.Declination.Value, 4)
                    : (double?)null,
                isOutOfBounds = PositionFormatter.IsOutOfBounds(p, result.Obliquity),
                aspects = result.MinorPointAspects
                    .Where(a => string.Equals(a.Body1Name, p.Name, StringComparison.Ordinal))
                    .Select(a => new
                    {
                        body = a.Body2Name,
                        aspect = a.Aspect.Type.ToString(),
                        angle = a.Aspect.Angle,
                        orb = Math.Round(a.Orb, 4),
                        isApplying = a.IsApplying,
                    }),
            }),
            houses = new
            {
                system = result.HouseSystemName,
                cusps = result.Houses.Cusps.Select((c, i) => new
                {
                    house = i + 1,
                    longitude = Math.Round(c, 4),
                    formatted = PositionFormatter.FormatLongitude(c),
                }),
                angles = new
                {
                    ascendant = Math.Round(result.Houses.Ascendant, 4),
                    midheaven = Math.Round(result.Houses.Midheaven, 4),
                    descendant = Math.Round(result.Houses.Descendant, 4),
                    ic = Math.Round(result.Houses.ImumCoeli, 4),
                    vertex = Math.Round(result.Houses.Vertex, 4),
                    armc = Math.Round(result.Houses.Armc, 4),
                },
            },
            aspects = result.Aspects.Select(a => new
            {
                body1 = a.Body1Name,
                body2 = a.Body2Name,
                aspect = a.Aspect.Type.ToString(),
                angle = a.Aspect.Angle,
                orb = Math.Round(a.Orb, 4),
                isApplying = a.IsApplying,
                category = a.Aspect.Category.ToString(),
            }),
            dignities = result.Dignities.Select(d => new
            {
                planet = d.PlanetName,
                sign = d.Sign.ToString(),
                dignity = d.Label,
                score = d.Score,
                dispositor = d.Dispositor,
                isDomicile = d.IsDomicile,
                isExalted = d.IsExalted,
                isInDetriment = d.IsInDetriment,
                isInFall = d.IsInFall,
                isPeregrine = d.IsPeregrine,
                triplicityRuler = d.TriplicityRuler,
                isTriplicityRuler = d.IsTriplicityRuler,
                decanRuler = d.DecanRuler,
                isInOwnDecan = d.IsInOwnDecan,
            }),
            moonCondition = result.MoonCondition is not null ? new
            {
                phase = result.MoonCondition.Phase.ToString(),
                phaseLabel = result.MoonCondition.PhaseLabel,
                phaseAngle = Math.Round(result.MoonCondition.PhaseAngle, 4),
                phasePosition = result.MoonCondition.PhasePosition,
                isWaxing = result.MoonCondition.IsWaxing,
                isVoidOfCourse = result.MoonCondition.IsVoidOfCourse,
                firstApplyingPlanet = result.MoonCondition.FirstApplyingPlanet,
                firstApplyingAspect = result.MoonCondition.FirstApplyingAspectType?.ToString(),
                firstApplyingOrb = result.MoonCondition.FirstApplyingOrb.HasValue
                    ? Math.Round(result.MoonCondition.FirstApplyingOrb.Value, 4)
                    : (double?)null,
                isViaCombusta = result.MoonCondition.IsViaCombusta,
                moonSign = result.MoonCondition.MoonSign.ToString(),
                moonLongitude = Math.Round(result.MoonCondition.MoonLongitude, 4),
            } : null,
            chartContext = result.ChartContext is not null ? new
            {
                chartRuler = result.ChartContext.ChartRuler,
                ascendantSign = result.ChartContext.AscendantSign.ToString(),
                chartRulerDignity = result.ChartContext.ChartRulerDignity.Label,
                chartRulerScore = result.ChartContext.ChartRulerDignity.Score,
                chartRulerHouse = result.ChartContext.ChartRulerHouse,
                isChartRulerRetrograde = result.ChartContext.IsChartRulerRetrograde,
                isDayChart = result.ChartContext.IsDayChart,
                sect = result.ChartContext.SectLabel,
                eventSignificator = result.ChartContext.EventSignificator,
                eventSignificatorDignity = result.ChartContext.EventSignificatorDignity?.Label,
                eventSignificatorScore = result.ChartContext.EventSignificatorDignity?.Score,
                keyHouses = result.ChartContext.KeyHouses,
                angularPlanets = result.ChartContext.AngularPlanets.Select(ap => new
                {
                    planet = ap.PlanetName,
                    angle = ap.Angle,
                    orb = Math.Round(ap.Orb, 4),
                    isBenefic = ap.IsBenefic,
                    isMalefic = ap.IsMalefic,
                }),
                mutualReceptions = result.ChartContext.MutualReceptions.Select(mr => new
                {
                    planet1 = mr.Planet1,
                    planet1Sign = mr.Planet1Sign.ToString(),
                    planet2 = mr.Planet2,
                    planet2Sign = mr.Planet2Sign.ToString(),
                    receptionType = mr.ReceptionType,
                    strength = mr.Strength,
                    notation = mr.Notation,
                }),
                planetarySect = result.ChartContext.PlanetarySect.Select(s => new
                {
                    planet = s.PlanetName,
                    naturalSect = s.NaturalSect,
                    isInSect = s.IsInSect,
                    isBenefic = s.IsBenefic,
                    isMalefic = s.IsMalefic,
                    isInSectMalefic = s.IsInSectMalefic,
                    isOutOfSectMalefic = s.IsOutOfSectMalefic,
                    isInSectBenefic = s.IsInSectBenefic,
                    isHayz = s.IsHayz,
                    houseOfJoy = s.HouseOfJoy,
                }),
                ascendantTriplicityRulers = result.ChartContext.AscendantTriplicityRulers is not null ? new
                {
                    element = result.ChartContext.AscendantTriplicityRulers.Element,
                    dayRuler = result.ChartContext.AscendantTriplicityRulers.DayRuler,
                    nightRuler = result.ChartContext.AscendantTriplicityRulers.NightRuler,
                    participatingRuler = result.ChartContext.AscendantTriplicityRulers.ParticipatingRuler,
                    activeRuler = result.ChartContext.AscendantTriplicityRulers.ActiveRuler,
                } : null,
            } : null,
            midpointActivations = result.MidpointActivations.Select(ma => new
            {
                midpoint = ma.Midpoint.Notation,
                midpointLongitude = Math.Round(ma.Midpoint.Longitude, 4),
                activator = ma.ActivatorName,
                activatorLongitude = Math.Round(ma.ActivatorLongitude, 4),
                harmonicAngle = ma.HarmonicAngle,
                orb = Math.Round(ma.Orb, 4),
                notation = ma.Notation,
            }),
            fixedStarConjunctions = result.FixedStarConjunctions.Select(fsc => new
            {
                star = fsc.StarName,
                constellation = fsc.Constellation,
                magnitude = fsc.Magnitude,
                starLongitude = Math.Round(fsc.StarLongitude, 4),
                formattedPosition = PositionFormatter.FormatLongitude(fsc.StarLongitude),
                conjunct = fsc.ChartPointName,
                chartPointLongitude = Math.Round(fsc.ChartPointLongitude, 4),
                orb = Math.Round(fsc.Orb, 4),
                nature = fsc.Nature,
                keywords = fsc.Keywords,
                isRoyalStar = fsc.IsRoyalStar ? true : (bool?)null,
                isBehenianStar = fsc.IsBehenianStar ? true : (bool?)null,
            }),
            planetConditions = result.PlanetConditions.Select(pc => new
            {
                planet = pc.PlanetName,
                isCazimi = pc.IsCazimi,
                isCombust = pc.IsCombust,
                isUnderTheBeams = pc.IsUnderTheBeams,
                isBesieged = pc.IsBesieged,
                isConjunctBenefic = pc.IsConjunctBenefic,
                isConjunctMalefic = pc.IsConjunctMalefic,
                hasApplyingBeneficAspect = pc.HasApplyingBeneficAspect,
                hasApplyingMaleficAspect = pc.HasApplyingMaleficAspect,
                accidentalScore = pc.AccidentalScore,
                conditions = pc.Conditions,
            }),
            moonMansion = result.MoonMansion is not null ? new
            {
                number = result.MoonMansion.Number,
                name = result.MoonMansion.Name,
                meaning = result.MoonMansion.Meaning,
                nature = result.MoonMansion.Nature,
                keywords = result.MoonMansion.Keywords,
                isBenefic = result.MoonMansion.IsBenefic ? true : (bool?)null,
                isMalefic = result.MoonMansion.IsMalefic ? true : (bool?)null,
            } : null,
            planetaryHour = result.PlanetaryHour is not null ? new
            {
                rulingPlanet = result.PlanetaryHour.RulingPlanet,
                hourNumber = result.PlanetaryHour.Hour.HourNumber,
                isDaytime = result.PlanetaryHour.Hour.IsDaytime,
                hourStart = result.PlanetaryHour.Hour.HourStart.ToString("HH:mm:ss", Inv),
                hourEnd = result.PlanetaryHour.Hour.HourEnd.ToString("HH:mm:ss", Inv),
                // The bounds as instants, so a bound in a repeated or skipped wall hour is one instant.
                hourStartUtc = result.PlanetaryHour.Hour.HourStartUtc is DateTime startUtc ? CalendarDateFormatter.Iso(startUtc) : null,
                hourEndUtc = result.PlanetaryHour.Hour.HourEndUtc is DateTime endUtc ? CalendarDateFormatter.Iso(endUtc) : null,
                // A chart cast at an explicit offset reads its hour on that offset's clock, not a zone's.
                hourTimeZone = IsExplicitOffset(result) ? null : result.Input.TimeZone,
                hourOffsetLabel = IsExplicitOffset(result) ? result.ResolvedOffsetLabel : null,
                isRadical = result.PlanetaryHour.IsRadical,
                radicalityLevel = result.PlanetaryHour.RadicalityLevel,
                radicalityNote = result.PlanetaryHour.RadicalityNote,
            } : null,
            arabicParts = result.ArabicParts.Select(ap => new
            {
                name = ap.Name,
                group = ap.Group,
                // 4-decimal policy like every other angular field in this
                // document; the Lot itself keeps its full longitude (F8-2).
                longitude = Math.Round(ap.Longitude, 4),
                sign = ap.Sign.ToString(),
                signDegrees = ap.SignDegrees,
                signMinutes = ap.SignMinutes,
                house = ap.House,
                formula = ap.Formula,
                domain = ap.Domain,
                ruler = ap.Ruler,
            }),
            antisciaPoints = result.AntisciaPoints.Select(ap => new
            {
                planet = ap.PlanetName,
                // 4-decimal policy like every other angular field in this
                // document; full precision here was the lone platform-unstable
                // value (arm64 vs x64 libm in the raw ephemeris longitude).
                longitude = Math.Round(ap.Longitude, 4),
                antisciaLongitude = ap.AntisciaLongitude,
                antisciaSign = ap.AntisciaSign.ToString(),
                contraAntisciaLongitude = ap.ContraAntisciaLongitude,
                contraAntisciaSign = ap.ContraAntisciaSign.ToString(),
            }),
            antisciaConnections = result.AntisciaConnections.Select(ac => new
            {
                planet1 = ac.Planet1,
                planet2 = ac.Planet2,
                type = ac.Type,
                orb = ac.Orb,
                notation = ac.Notation,
            }),
            stations = result.Stations.Select(s => new
            {
                planet = s.PlanetName,
                stationType = s.StationType,
                isStationing = s.IsStationing,
                dailySpeed = s.DailySpeed,
                stationThreshold = s.StationThreshold,
                stationProximity = s.StationProximity,
                phase = s.Phase?.ToString(),
                stationTimeUtc = s.StationTimeUtc is DateTime stationUtc ? CalendarDateFormatter.Iso(stationUtc) : null,
                stationJulianDay = s.StationJulianDay,
                stationLongitude = s.StationLongitude.HasValue
                    ? Math.Round(s.StationLongitude.Value, 4)
                    : (double?)null,
                stationFormattedPosition = s.StationLongitude.HasValue
                    ? PositionFormatter.FormatLongitude(s.StationLongitude.Value)
                    : null,
                daysToStation = s.DaysToStation.HasValue
                    ? Math.Round(s.DaysToStation.Value, 4)
                    : (double?)null,
            }),
            parans = result.Parans is not null ? new
            {
                latitude = result.Parans.Latitude,
                longitude = result.Parans.Longitude,
                orbMinutes = result.Parans.OrbMinutes,
                bodyCount = result.Parans.BodyCount,
                totalCrossings = result.Parans.Crossings.Count,
                events = result.Parans.Parans.Select(p => new
                {
                    bodyA = p.BodyAName,
                    angleA = p.AngleA.ToString(),
                    bodyB = p.BodyBName,
                    angleB = p.AngleB.ToString(),
                    orbMinutes = Math.Round(p.OrbMinutes, 2),
                    description = p.Description,
                }),
            } : null,
            metadata = new
            {
                engineVersion = result.EngineVersion,
                calculatedAt = result.CalculatedAt.ToString("o"),
                totalPositions = result.Positions.Count,
                totalAspects = result.Aspects.Count,
                totalMidpoints = result.Midpoints.Count,
                totalActivations = result.MidpointActivations.Count,
                totalFixedStarConjunctions = result.FixedStarConjunctions.Count,
                totalPlanetConditions = result.PlanetConditions.Count,
                totalArabicParts = result.ArabicParts.Count,
                totalAntisciaConnections = result.AntisciaConnections.Count,
                totalStations = result.Stations.Count,
                totalParans = result.Parans?.Parans.Count,
            },
        };

        return JsonSerializer.Serialize(output, Options);
    }

    /// <summary>
    /// Save a chart result as JSON to a file.
    /// </summary>
    /// <param name="result">The chart calculation result to save.</param>
    /// <param name="filePath">Destination file path for the JSON output.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        ChartResult result, string filePath, CancellationToken ct = default)
    {
        string json = ToJson(result);
        await File.WriteAllTextAsync(filePath, json, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the chart was cast at an explicit offset, the condition the chart engine casts with: then its
    /// planetary hour is on that offset's clock, which the JSON names by its label instead of a zone.
    /// </summary>
    /// <param name="result">The chart.</param>
    /// <returns>True when the input uses an explicit offset and gives one.</returns>
    private static bool IsExplicitOffset(ChartResult result) =>
        result.Input.UseExplicitOffset && result.Input.UtcOffsetHours.HasValue;
}
