using System.ComponentModel;

namespace AstroCast.Core.Enums;

/// <summary>
/// Types of events for chart calculation, electional analysis, and AI interpretation.
/// Each event type maps to a natural planetary significator and key houses
/// via <see cref="Services.ChartContextService"/>.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="ChartInput"/> model stores the event type as a <c>string</c>
/// for backward compatibility with the SQLite database. Use <see cref="EventTypeHelper"/>
/// to convert between enum values and display-friendly strings.
/// </para>
/// <para>
/// Birth and Horary charts have special UI behavior (horary question field,
/// electional settings panel). Electional charts use the separate
/// <see cref="ElectionType"/> enum for scorer-specific weight profiles.
/// </para>
/// </remarks>
public enum EventType
{
    // --- Core Chart Types ---

    /// <summary>Natal birth chart. No specific significator.</summary>
    [Description("Birth")]
    Birth,

    /// <summary>General event chart. No specific significator.</summary>
    [Description("Event")]
    Event,

    /// <summary>Horary chart (question-based). Special UI behavior for question entry.</summary>
    [Description("Horary")]
    Horary,

    /// <summary>Electional chart. Uses electional settings panel.</summary>
    [Description("Electional")]
    Electional,

    // --- Life Events ---

    /// <summary>Wedding or marriage ceremony. Significator: Venus. Houses: 1, 7, 5.</summary>
    [Description("Wedding / Marriage")]
    Wedding,

    /// <summary>Starting a new job or career role. Significator: Saturn. Houses: 1, 10, 6.</summary>
    [Description("Job / Career Start")]
    JobStart,

    /// <summary>Moving to a new residence. Significator: Moon. Houses: 1, 4, 7.</summary>
    [Description("Relocation / Move")]
    Relocation,

    /// <summary>Surgical procedure. Significator: Mars. Houses: 1, 6, 8.</summary>
    [Description("Surgery")]
    Surgery,

    /// <summary>Significant journey. Significator: Jupiter. Houses: 1, 9, 3.</summary>
    [Description("Travel / Trip")]
    Travel,

    /// <summary>Educational enrollment or program start. Significator: Mercury. Houses: 1, 9, 3.</summary>
    [Description("Education")]
    Education,

    // --- Business and Legal ---

    /// <summary>Business launch or incorporation. Significator: Jupiter. Houses: 1, 10, 2.</summary>
    [Description("Business Launch")]
    BusinessLaunch,

    /// <summary>Signing a contract or agreement. Significator: Mercury. Houses: 1, 7, 3.</summary>
    [Description("Contract Signing")]
    Contract,

    /// <summary>Real estate purchase or sale. Significator: Moon. Houses: 1, 4, 7.</summary>
    [Description("Real Estate")]
    RealEstate,

    /// <summary>Filing a legal action or lawsuit. Significator: Jupiter. Houses: 1, 7, 9.</summary>
    [Description("Legal Filing")]
    LegalFiling,

    /// <summary>Investment or financial commitment. Significator: Jupiter. Houses: 1, 2, 8.</summary>
    [Description("Investment")]
    Investment,

    /// <summary>Founding an organization, club, or group. Significator: Saturn. Houses: 1, 10, 11.</summary>
    [Description("Organization / Group")]
    Organization,

    // --- Creative and Technology ---

    /// <summary>Launching a creative work (book, film, album, art). Significator: Venus. Houses: 1, 5, 10.</summary>
    [Description("Creative Project")]
    CreativeProject,

    /// <summary>Technology or software launch. Significator: Mercury. Houses: 1, 10, 11.</summary>
    [Description("Technology Launch")]
    TechnologyLaunch,

    // --- Political and Mundane ---

    /// <summary>Political event (inauguration, election filing, campaign launch). Significator: Sun. Houses: 1, 10, 7.</summary>
    [Description("Political Event")]
    Political,

    // --- General ---

    /// <summary>General or unclassified event type. No specific significator.</summary>
    [Description("Other")]
    Other,
}
