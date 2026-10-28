using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Transit;

/// <summary>
/// A sign ingress (planet changing sign) during the report period.
/// </summary>
public sealed class TransitIngress
{
    /// <summary>The transiting planet.</summary>
    public required TransitBody Planet { get; init; }

    /// <summary>The sign the planet is leaving.</summary>
    public required string FromSign { get; init; }

    /// <summary>The sign the planet is entering.</summary>
    public required string ToSign { get; init; }

    /// <summary>The UT date of the ingress: the date of <see cref="DateTimeUtc"/>.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>
    /// The instant of the ingress in UT, the sign change bisected to the second between the two
    /// 12:00 UT samples around it (<see cref="Services.TransitIngressDetector"/>, branch D, 1.3.4);
    /// null for an ingress built without one. Until 1.3.4 an ingress carried only its first 12:00 UT
    /// sample in the new sign as its date, the day after the UT date of the crossing for about half
    /// of them (audit finding T1-5).
    /// </summary>
    public DateTime? DateTimeUtc { get; init; }
}
