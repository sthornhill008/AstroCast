namespace AstroCast.Core.Models.PrimaryDirections;

/// <summary>
/// Why a primary direction has no range, or no months per minute (branch DirectionNotes, 1.7.2; finding A5): what
/// happens to the direction at one of the two birth instants the figure is taken from. The values combine, since
/// the two instants may each have a cause of their own.
/// </summary>
/// <remarks>
/// The service takes each cause from what it holds when it leaves the figure null
/// (<see cref="Services.PrimaryDirections.PrimaryDirectionService"/>): the arc the direction has at the moved
/// birth instant, and the birth's own. Nothing here says how an arc becomes a date.
/// </remarks>
[Flags]
public enum PrimaryDirectionGapReasons
{
    /// <summary>The figure is there, or was never sought.</summary>
    None = 0,

    /// <summary>
    /// At that birth instant the direction falls before birth: its arc has passed zero, so the two points had
    /// already met when the life began.
    /// </summary>
    BeforeBirth = 1,

    /// <summary>At that birth instant the direction falls beyond the age limit of the run.</summary>
    BeyondAgeLimit = 2,

    /// <summary>
    /// The direction is to the Part of Fortune, and at that birth instant the chart is a day chart where the
    /// birth's is a night chart, or the reverse: the Lot is made by its other formula there.
    /// </summary>
    SectChange = 4,

    /// <summary>
    /// At that birth instant a point of the direction does not rise or set at the place, so it has no semi-arc
    /// and the direction no arc.
    /// </summary>
    NoSemiArc = 8,
}
