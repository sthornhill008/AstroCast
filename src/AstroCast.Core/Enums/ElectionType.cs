namespace AstroCast.Core.Enums;

/// <summary>
/// Categories of electional events. Each type carries its own weight profile
/// that emphasises different scoring dimensions (e.g., weddings weight Venus
/// and the Moon heavily, contracts weight Mercury).
/// </summary>
public enum ElectionType
{
    /// <summary>Launching a new business, product, or venture.</summary>
    BusinessLaunch,

    /// <summary>Signing a contract, lease, or binding agreement.</summary>
    Contract,

    /// <summary>Wedding ceremony or formal partnership commitment.</summary>
    Wedding,

    /// <summary>Scheduled surgical procedure.</summary>
    Surgery,

    /// <summary>Listing a property for sale or lease.</summary>
    RealEstateListing,

    /// <summary>Closing on a property purchase or sale.</summary>
    RealEstateClosing,

    /// <summary>Launching a creative project, album, book, or artwork.</summary>
    CreativeLaunch,

    /// <summary>Departure for a journey or relocation.</summary>
    Travel,

    /// <summary>General-purpose election with balanced weights.</summary>
    Other,
}
