using System.Collections;

using Avalonia;
using Avalonia.Threading;

using AstroCast.Core.Interfaces;

namespace AstroCast.Avalonia.Services;

/// <summary>
/// Avalonia implementation of <see cref="IUiDispatcher"/>, backed by
/// <see cref="Dispatcher.UIThread"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the Avalonia half of the UI-thread seam described on
/// <see cref="IUiDispatcher"/>. It is deliberately <i>not</i> a straight
/// pass-through to <see cref="Dispatcher.UIThread"/>: the naive mapping compiles,
/// throws nothing, and silently violates the interface's documented headless
/// contract in two separate ways. Both were established empirically against
/// Avalonia 12.1.0; neither is theoretical.
/// </para>
/// <para>
/// <b>Violation 1 — <see cref="CheckAccess"/> without a guard.</b> Unlike WPF,
/// where <c>Application.Current?.Dispatcher</c> is <see langword="null"/> when no
/// application is running, Avalonia's <see cref="Dispatcher.UIThread"/> is never
/// null: reading it lazily manufactures a dispatcher latched to whichever thread
/// touched it first, process-wide. Its <c>CheckAccess()</c> then returns
/// <see langword="true"/> only for that one thread and <see langword="false"/>
/// for every other, so an unguarded implementation reports "not the UI thread" in
/// most unit tests — nondeterministically, depending on which test ran first.
/// The contract requires <see langword="true"/> when no UI exists.
/// </para>
/// <para>
/// <b>Violation 2 — <see cref="Post"/> without a guard, the more serious one.</b>
/// Avalonia's <c>Post</c> has no inline fast path; it always enqueues. With no
/// application there is no run loop draining that queue, so the action is
/// accepted and then <i>never executed</i> — no exception, no diagnostic, just
/// silently lost work. The contract requires inline invocation, and the interface
/// documents why: callers depend on notifications being raised synchronously in
/// headless contexts. Silent work-loss is worse than an exception, because tests
/// go green while the mutation never happened.
/// </para>
/// <para>
/// <b>The guard is <see cref="Application.Current"/>, and nothing else.</b> Three
/// plausible alternatives are all wrong:
/// <c>Dispatcher.SupportsRunLoops</c> returns <see langword="true"/> even with no
/// application, because the fallback managed dispatcher implements the controlled
/// dispatcher interface; <c>Dispatcher.TryGetUIThread()</c> is <c>internal</c>;
/// and merely <i>reading</i> <see cref="Dispatcher.UIThread"/> has the side
/// effect of latching the UI thread to the calling thread, so the guard must
/// never probe the dispatcher in order to decide. Checking
/// <see cref="Application.Current"/> first avoids touching the dispatcher at all
/// on the headless path.
/// </para>
/// <para>
/// <b>Priority is passed explicitly, and must stay that way.</b> Avalonia's
/// <c>Post(Action)</c> defaults to <see cref="DispatcherPriority.Default"/>,
/// which is <c>0</c> — <i>below</i> <c>Render</c> and <c>DataBind</c> on
/// Avalonia's ascending priority scale. WPF's <c>Dispatcher.BeginInvoke(Action)</c>,
/// which this replaces, defaults to <c>Normal</c>. Omitting the argument would
/// therefore demote every property-changed notification in the application below
/// rendering, app-wide and invisibly.
/// </para>
/// </remarks>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    /// <summary>
    /// Gets a value indicating whether the process is running without an Avalonia
    /// application — the headless case, in which callers must execute inline.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="Application.Current"/> only. It must not consult
    /// <see cref="Dispatcher.UIThread"/>, because reading that property latches
    /// the process-wide UI thread to the caller as a side effect.
    /// </remarks>
    private static bool NoUiThread => Application.Current is null;

    /// <inheritdoc />
    /// <remarks>
    /// Returns <see langword="true"/> when no Avalonia application is running, so
    /// that callers execute inline rather than attempting to marshal onto a
    /// dispatcher that will never be pumped.
    /// </remarks>
    public bool CheckAccess() => NoUiThread || Dispatcher.UIThread.CheckAccess();

    /// <inheritdoc />
    /// <remarks>
    /// Invokes <paramref name="action"/> inline when no application is running;
    /// otherwise posts it at <see cref="DispatcherPriority.Normal"/>, matching
    /// WPF's <c>BeginInvoke</c> default rather than Avalonia's lower one.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="action"/> is <see langword="null"/>.
    /// </exception>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (NoUiThread)
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>A deliberate no-op: Avalonia has no equivalent.</b> WPF implements this
    /// with <c>BindingOperations.EnableCollectionSynchronization</c>, which hands
    /// the binding layer a monitor so a background thread may mutate a bound
    /// collection safely. Avalonia has no such concept anywhere in the framework
    /// — verified by scanning every shipped assembly and the full source tree —
    /// and the maintainers' position is that callers must marshal instead. There
    /// is therefore nothing to call, and the interface explicitly permits a no-op
    /// for frameworks in this position.
    /// </para>
    /// <para>
    /// <b>This does not make off-thread mutation safe — it removes a guarantee.</b>
    /// Under WPF this registration is what <i>made</i> background mutation legal.
    /// Without an equivalent, any code path that relied on it becomes an
    /// unsynchronised race. In Avalonia that race does not throw at the mutation
    /// site: a quiescent mutation appears to succeed, while a mutation concurrent
    /// with a layout pass throws <see cref="ArgumentOutOfRangeException"/> on the
    /// UI thread from deep inside the virtualising panel — a stack trace nowhere
    /// near the offending code. Items may also simply be dropped.
    /// </para>
    /// <para>
    /// <b>Consequence for the port.</b> Every site that mutates a bound collection
    /// off the UI thread must instead compute on the background thread and publish
    /// once on the UI thread, or route each mutation through <see cref="Post"/>.
    /// That audit is a later slice of the Avalonia port and is tracked there. Do
    /// not read this no-op as evidence that the concern was handled — it is the
    /// marker that it has not been.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="collection"/> or <paramref name="lockObject"/>
    /// is <see langword="null"/>. The arguments are validated even though they are
    /// unused, so that a caller passing null fails here rather than appearing to
    /// register successfully.
    /// </exception>
    public void EnableCollectionSynchronization(IEnumerable collection, object lockObject)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(lockObject);

        // Intentionally empty — see the remarks above.
    }
}
