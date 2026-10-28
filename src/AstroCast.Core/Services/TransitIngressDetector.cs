using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;

namespace AstroCast.Core.Services;

/// <summary>
/// Detects sign ingresses (planet changing zodiac sign) within a date range.
/// </summary>
/// <remarks>
/// <para>
/// A sign ingress occurs when a planet moves from one zodiac sign to another.
/// This naturally handles both direct and retrograde ingresses — a planet
/// retrograding from Aries back into Pisces is detected as a valid ingress.
/// </para>
/// <para>
/// An ingress is dated by its instant (branch D, 1.3.4): the sign change between two consecutive
/// 12:00 UT samples is bisected to the second, kept as <see cref="TransitIngress.DateTimeUtc"/>,
/// and <see cref="TransitIngress.Date"/> is the UT date of that instant. Until 1.3.4 the date was
/// the first 12:00 UT sample in the new sign, the day after the crossing's UT date for about half of
/// all ingresses (audit finding T1-5): Neptune's into Aries, 2026-01-26 17:34:29 UT, read 2026-01-27.
/// </para>
/// </remarks>
public sealed class TransitIngressDetector
{
    /// <summary>
    /// Number of binary search iterations for the ingress instant.
    /// 24 hours / 2^20 ≈ 0.08 seconds precision.
    /// </summary>
    private const int BisectionIterations = 20;

    private readonly TransitEphemerisHelper _ephemeris;

    /// <summary>
    /// Initialises a new instance of <see cref="TransitIngressDetector"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris helper for position lookups.</param>
    public TransitIngressDetector(TransitEphemerisHelper ephemeris)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
    }

    /// <summary>
    /// Finds all sign ingresses for a given transit body within the date range.
    /// </summary>
    /// <param name="body">The transiting body to scan.</param>
    /// <param name="startDate">Start of scan range.</param>
    /// <param name="endDate">End of scan range.</param>
    /// <returns>
    /// List of ingresses found, chronologically ordered, each dated by its instant: a sign change
    /// between two 12:00 UT samples of the range, bisected to the second.
    /// </returns>
    public IReadOnlyList<TransitIngress> FindIngresses(
        TransitBody body, DateOnly startDate, DateOnly endDate)
    {
        List<TransitIngress> ingresses = [];

        DateOnly currentDate = startDate;
        double previousJd = _ephemeris.DateToJulianDay(currentDate);
        string previousSign = TransitEphemerisHelper.LongitudeToSign(_ephemeris.GetLongitude(body, previousJd));

        currentDate = currentDate.AddDays(1);

        while (currentDate <= endDate)
        {
            double jd = _ephemeris.DateToJulianDay(currentDate);
            string currentSign = TransitEphemerisHelper.LongitudeToSign(_ephemeris.GetLongitude(body, jd));

            if (currentSign != previousSign)
            {
                DateTime instant = RefineIngressTime(body, previousJd, previousSign, jd);
                ingresses.Add(new TransitIngress
                {
                    Planet = body,
                    FromSign = previousSign,
                    ToSign = currentSign,
                    Date = DateOnly.FromDateTime(instant),
                    DateTimeUtc = instant,
                });
            }

            previousSign = currentSign;
            previousJd = jd;
            currentDate = currentDate.AddDays(1);
        }

        return ingresses.AsReadOnly();
    }

    /// <summary>
    /// Refines an ingress to the second by binary search between the last 12:00 UT sample in the old
    /// sign and the first in the new: the sign is a step of the longitude, so the half that still
    /// reads the old sign keeps the crossing ahead of it.
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="jdBefore">Julian Day of the last sample in the old sign.</param>
    /// <param name="signBefore">The old sign.</param>
    /// <param name="jdAfter">Julian Day of the first sample in the new sign.</param>
    /// <returns>The ingress instant in UT, to the whole second.</returns>
    private DateTime RefineIngressTime(TransitBody body, double jdBefore, string signBefore, double jdAfter)
    {
        for (int i = 0; i < BisectionIterations; i++)
        {
            double jdMid = (jdBefore + jdAfter) / 2.0;
            if (TransitEphemerisHelper.LongitudeToSign(_ephemeris.GetLongitude(body, jdMid)) == signBefore)
                jdBefore = jdMid;
            else
                jdAfter = jdMid;
        }

        return _ephemeris.JulianDayToDateTime((jdBefore + jdAfter) / 2.0);
    }
}
