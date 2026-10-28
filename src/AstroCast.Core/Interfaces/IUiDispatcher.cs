using System.Collections;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Abstraction for UI-thread affinity: marshalling work onto the UI thread, and
/// declaring which bound collections may be mutated off it. Allows a view model
/// to raise change notifications and mutate bound collections from a background
/// context without coupling to a specific UI framework (e.g., WPF's
/// <c>System.Windows.Threading.Dispatcher</c> and
/// <c>System.Windows.Data.BindingOperations</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Data-binding frameworks require that property-changed
/// notifications reach the UI on the thread that owns the visual tree. AstroCast's
/// view models raise <c>PropertyChanged</c> from background work (<c>Task.Run</c>
/// calculations, async continuations that escaped the UI context), so the raise
/// has to be marshalled. Doing that directly against WPF's dispatcher pins the
/// view-model layer to <c>WindowsBase</c>/<c>PresentationFramework</c> and blocks
/// it from compiling in a framework-neutral assembly. This interface is the
/// cross-platform seam: the WPF head implements it today with
/// <c>Application.Current.Dispatcher</c>, and an Avalonia head will implement it
/// with <c>Avalonia.Threading.Dispatcher.UIThread</c> without the view models
/// changing at all.
/// </para>
/// <para>
/// <b>Headless contract — load-bearing, do not tighten.</b> Implementations must
/// behave sensibly when no UI thread exists at all: <see cref="CheckAccess"/>
/// returns <see langword="true"/> and <see cref="Post"/> invokes inline. Unit
/// tests construct view models with no UI framework bootstrapped, and the whole
/// suite depends on notifications being raised synchronously in that case.
/// Callers may also hold a <see langword="null"/> reference to this interface,
/// which carries the same meaning: run inline. Neither case is an error, and an
/// implementation must never throw because a dispatcher is unavailable.
/// </para>
/// </remarks>
public interface IUiDispatcher
{
    /// <summary>
    /// Determines whether the calling thread is the UI thread, and therefore
    /// whether work may be executed directly rather than marshalled.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the caller is already on the UI thread, or if no
    /// UI thread exists (headless/unit-test context) so that inline execution is
    /// correct; otherwise <see langword="false"/>.
    /// </returns>
    bool CheckAccess();

    /// <summary>
    /// Schedules <paramref name="action"/> for execution on the UI thread.
    /// </summary>
    /// <param name="action">The work to run on the UI thread.</param>
    /// <remarks>
    /// The call is asynchronous where a real dispatcher exists — it posts and
    /// returns without waiting, matching WPF's <c>BeginInvoke</c> semantics — and
    /// falls back to invoking <paramref name="action"/> inline when there is no
    /// UI thread to post to.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="action"/> is <see langword="null"/>.
    /// </exception>
    void Post(Action action);

    /// <summary>
    /// Declares that <paramref name="collection"/> is bound to the UI but may be
    /// mutated from a background thread, and that access must be serialised on
    /// <paramref name="lockObject"/>.
    /// </summary>
    /// <param name="collection">The bound collection to register.</param>
    /// <param name="lockObject">
    /// The monitor a background writer takes before mutating
    /// <paramref name="collection"/>, and which the binding layer takes before
    /// reading it.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Why this lives on the UI-thread abstraction.</b> It is a UI-thread
    /// affinity concern in the same family as <see cref="CheckAccess"/> and
    /// <see cref="Post"/>: it exists so the binding layer can safely read a
    /// collection on the UI thread while another thread writes it. WPF
    /// implements it with
    /// <c>BindingOperations.EnableCollectionSynchronization</c>; frameworks with
    /// no equivalent concept implement it as a no-op.
    /// </para>
    /// <para>
    /// <b>Registration is advisory, not enforcement.</b> This call does not take
    /// the lock and does not make mutation thread-safe on its own — a background
    /// writer must still take <paramref name="lockObject"/> itself. It only tells
    /// the binding layer which monitor to use.
    /// </para>
    /// <para>
    /// <b>Must never throw for want of a UI framework.</b> Like the rest of this
    /// interface, a headless implementation — or a <see langword="null"/>
    /// reference held by the caller — means "nothing to register", not an error.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="collection"/> or <paramref name="lockObject"/>
    /// is <see langword="null"/>.
    /// </exception>
    void EnableCollectionSynchronization(IEnumerable collection, object lockObject);
}
