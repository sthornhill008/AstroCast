using System.Collections;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

using AstroCast.Core.Interfaces;

namespace AstroCast.Services;

/// <summary>
/// WPF implementation of <see cref="IUiDispatcher"/> backed by
/// <see cref="Application.Current"/>'s <see cref="Dispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the WPF half of the UI-thread seam described on
/// <see cref="IUiDispatcher"/>. It reproduces exactly the marshalling logic that
/// previously lived inline in <c>MainViewModel.OnPropertyChanged</c> and
/// <c>ChildViewModelBase.OnPropertyChanged</c>, so moving the view models onto the
/// abstraction is behaviour-preserving on Windows.
/// </para>
/// <para>
/// <b>The null-<see cref="Application"/> path is deliberate.</b> Both members read
/// <c>Application.Current?.Dispatcher</c> on each call rather than caching it, and
/// treat a missing application as "no UI thread exists, run inline" — never as an
/// error. That mirrors the original code and preserves the H-2 behaviour the unit
/// suite relies on when no WPF application is bootstrapped.
/// </para>
/// </remarks>
public sealed class WpfUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    /// <remarks>
    /// Returns <see langword="true"/> when no <see cref="Application"/> is running,
    /// so that callers execute inline instead of attempting to marshal.
    /// </remarks>
    public bool CheckAccess()
    {
        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        return dispatcher is null || dispatcher.CheckAccess();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Posts via <see cref="Dispatcher.BeginInvoke(Delegate, object[])"/> when a
    /// dispatcher is available; invokes <paramref name="action"/> inline when none
    /// is (headless/unit-test context).
    /// </remarks>
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Dispatcher? dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delegates verbatim to
    /// <see cref="BindingOperations.EnableCollectionSynchronization(IEnumerable, object)"/>,
    /// so the registration WPF sees is byte-for-byte the one the view models
    /// performed inline before this seam existed. Unlike the other members this
    /// one needs no null-<see cref="Application"/> guard: the call is a static
    /// registration against WPF's binding infrastructure and is harmless with no
    /// application running.
    /// </remarks>
    public void EnableCollectionSynchronization(IEnumerable collection, object lockObject)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(lockObject);

        BindingOperations.EnableCollectionSynchronization(collection, lockObject);
    }
}
