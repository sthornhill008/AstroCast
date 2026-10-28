using System.ComponentModel;
using System.Runtime.CompilerServices;

using AstroCast.Core.Interfaces;

namespace AstroCast.ViewModels;

/// <summary>
/// Base class for child view models composed into <see cref="MainViewModel"/>.
/// Part of the H-1 decomposition project: subtypes own a single feature area
/// (chart entry, location search, synastry, transits, electional, returns,
/// cycles, mundane, return/ingress timeline, relocation, astrocartography)
/// previously housed inline in <see cref="MainViewModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cross-VM communication rules.</b>
/// Child VMs are siblings composed by <see cref="MainViewModel"/>. They must
/// not reference each other directly — any cross-VM signal goes through the
/// parent. The <see cref="Parent"/> back-reference is a <i>last resort</i>
/// for concerns that genuinely cannot be expressed via input properties
/// (e.g. reading the current natal <c>ChartResult</c> in Transits, which is
/// owned by <c>ChartEntry</c>). Every use of <see cref="Parent"/> should be
/// justifiable in a code comment.
/// </para>
/// <para>
/// <b>Command-state refresh.</b>
/// When a child VM's state changes in a way that should re-evaluate
/// <c>CanExecute</c> on export or calculate commands owned by
/// <see cref="MainViewModel"/>, call <see cref="RequestCommandStateRefresh"/>.
/// The parent subscribes to <see cref="CommandStateRefreshRequested"/> in its
/// constructor and routes the signal to its private
/// <c>RefreshExportCommandStates</c>, so child VMs never reach into the
/// parent's private command table.
/// </para>
/// <para>
/// <b>UI-thread marshalling (H-2).</b>
/// <see cref="OnPropertyChanged"/> replicates the marshalling behaviour from
/// <see cref="MainViewModel"/>'s private <c>OnPropertyChanged</c>:
/// off-UI-thread invocations are posted through <see cref="IUiDispatcher"/>,
/// and raised inline when no dispatcher is available (e.g. xUnit runs, where
/// no UI framework is bootstrapped). The dispatcher is read from the parent
/// rather than injected here, so the whole view-model tree shares one instance.
/// Losing this marshalling during decomposition would be a H-2 regression —
/// any future edit to this method must preserve both the marshalling and the
/// inline fallback.
/// </para>
/// </remarks>
public abstract class ChildViewModelBase : INotifyPropertyChanged
{
    /// <summary>
    /// Weak back-reference to the composing parent. Used only when cross-VM
    /// state cannot be expressed via input properties. Prefer input
    /// properties over <c>Parent.Whatever</c>.
    /// </summary>
    protected MainViewModel Parent { get; }

    /// <summary>
    /// Initialises the base with the parent back-reference.
    /// </summary>
    /// <param name="parent">The <see cref="MainViewModel"/> that composes this child.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    protected ChildViewModelBase(MainViewModel parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        Parent = parent;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when this child VM's state has changed in a way that may affect
    /// command <c>CanExecute</c> evaluation on <see cref="MainViewModel"/>.
    /// The parent subscribes in its constructor and routes to its private
    /// <c>RefreshExportCommandStates</c>.
    /// </summary>
    public event EventHandler? CommandStateRefreshRequested;

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> for the specified property,
    /// dispatcher-marshalling the event to the WPF UI thread if invoked from
    /// a background context.
    /// </summary>
    /// <param name="propertyName">
    /// Name of the changed property, supplied automatically via
    /// <see cref="CallerMemberNameAttribute"/>.
    /// </param>
    /// <remarks>
    /// Mirrors <see cref="MainViewModel"/>'s private <c>OnPropertyChanged</c>
    /// (H-2). When no <see cref="IUiDispatcher"/> was injected into the parent,
    /// or the injected one reports no UI thread (unit-test context), the event
    /// is raised inline.
    /// </remarks>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChangedEventHandler? handler = PropertyChanged;
        if (handler is null)
        {
            return;
        }

        PropertyChangedEventArgs args = new(propertyName);
        IUiDispatcher? dispatcher = Parent.UiDispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            handler(this, args);
        }
        else
        {
            dispatcher.Post(() => handler(this, args));
        }
    }

    /// <summary>
    /// Assigns <paramref name="value"/> to <paramref name="field"/> and raises
    /// <see cref="PropertyChanged"/> when the value changes. Matches
    /// <see cref="MainViewModel"/>'s private <c>SetProperty</c> signature so
    /// code lifted from the parent needs no rewriting.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">
    /// Name of the changed property, supplied automatically via
    /// <see cref="CallerMemberNameAttribute"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the value was changed; <see langword="false"/>
    /// if it was already equal under
    /// <see cref="EqualityComparer{T}.Default"/>.
    /// </returns>
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Signals that a command-state refresh on <see cref="MainViewModel"/> is
    /// required. The parent subscribes to
    /// <see cref="CommandStateRefreshRequested"/> in its constructor and
    /// invokes its private command-state refresh in response.
    /// </summary>
    protected void RequestCommandStateRefresh()
        => CommandStateRefreshRequested?.Invoke(this, EventArgs.Empty);
}
