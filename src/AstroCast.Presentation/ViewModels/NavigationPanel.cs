namespace AstroCast.ViewModels;

/// <summary>
/// Identifies which content panel is displayed in the sidebar navigation layout.
/// Each value corresponds to a sidebar icon and a switchable content area
/// in the main window.
/// </summary>
public enum NavigationPanel
{
    /// <summary>Natal chart input: subject, date/time, location, notes, horary, calculate/save/clear.</summary>
    Chart,

    /// <summary>Synastry and composite chart analysis (Person B selection, relationship context).</summary>
    Synastry,

    /// <summary>Transit and progression report generation.</summary>
    Transits,

    /// <summary>Solar return and lunar return calculations.</summary>
    Returns,

    /// <summary>Electional astrology time-finding engine.</summary>
    Electional,

    /// <summary>Medical astrology health profile and decumbiture (future feature placeholder).</summary>
    Medical,

    /// <summary>Astrocartography interactive map with angular lines and crossing report.</summary>
    Map,

    /// <summary>Consolidated export panel: the chart's PDF, Markdown and JSON, the database backup (Export Backup, Import Backup), all feature exports.</summary>
    Export,

    /// <summary>Relocation chart: recompute the natal chart for a new geographic location.</summary>
    Relocation,

    /// <summary>Primary directions: the whole-life list of a chart's primary directions, its settings and its export.</summary>
    Directions
}
