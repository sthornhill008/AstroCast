using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace AstroCast.ViewModels;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> whose contents can be replaced
/// wholesale with a single <see cref="NotifyCollectionChangedAction.Reset"/>
/// notification.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> The timezone filter properties
/// (<c>MainViewModel.FilteredTimezones</c>,
/// <c>ElectionalViewModel.FilteredTimezones</c>) feed editable ComboBoxes whose
/// <c>SelectedItem</c> binds two-way. A UI framework's selector preserves the
/// selected item across an in-place <see cref="NotifyCollectionChangedAction.Reset"/>
/// on a stable <c>ItemsSource</c> instance — the notification shape WPF's
/// <c>ICollectionView.Refresh()</c> produced before the Phase 3 port work — but
/// treats a <i>replaced</i> <c>ItemsSource</c> instance as a hard reset and
/// drops the selection, pushing <see langword="null"/> through the two-way
/// binding. Replacing the instance per keystroke is exactly the regression this
/// type repairs: the view models keep ONE instance of this collection alive for
/// their lifetime and refresh it via <see cref="ReplaceAll"/>.
/// </para>
/// <para>
/// <b>Why a single <c>Reset</c> and not per-item events.</b> Clearing and
/// re-adding through the public <see cref="Collection{T}.Clear"/> /
/// <see cref="Collection{T}.Add"/> surface would raise one
/// <see cref="INotifyCollectionChanged.CollectionChanged"/> per item — up to
/// several hundred per keystroke over the ~600-entry zone list — where
/// <c>Refresh()</c> raised exactly one. <see cref="ReplaceAll"/> mutates the
/// protected <see cref="Collection{T}.Items"/> list directly (which raises
/// nothing) and then notifies once, reproducing the original shape: one
/// <see cref="PropertyChangedEventArgs"/> for <c>Count</c>, one for the
/// indexer, one <c>Reset</c>.
/// </para>
/// <para>
/// This type is pure BCL (<c>System.ObjectModel</c> /
/// <c>System.Collections.Specialized</c>) and carries no UI-framework
/// dependency, so it moves to the cross-platform presentation assembly with the
/// view models in Phase 3e.
/// </para>
/// </remarks>
internal sealed class ObservableRangeCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Cached event args, mirroring the singletons WPF's own collection types
    /// reuse for the same notifications.
    /// </summary>
    private static readonly PropertyChangedEventArgs s_countChanged = new(nameof(Count));

    /// <summary>
    /// Indexer-changed args. <c>"Item[]"</c> is the binding indexer name
    /// (<c>Binding.IndexerName</c>) that <see cref="ObservableCollection{T}"/>
    /// itself raises from its mutators.
    /// </summary>
    private static readonly PropertyChangedEventArgs s_indexerChanged = new("Item[]");

    /// <summary>
    /// Reset args, raised once per <see cref="ReplaceAll"/>.
    /// </summary>
    private static readonly NotifyCollectionChangedEventArgs s_reset =
        new(NotifyCollectionChangedAction.Reset);

    /// <summary>
    /// Initialises the collection with <paramref name="items"/>.
    /// </summary>
    /// <param name="items">The initial contents.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="items"/> is <see langword="null"/>.
    /// </exception>
    public ObservableRangeCollection(IEnumerable<T> items)
        : base(items)
    {
    }

    /// <summary>
    /// Replaces the entire contents with <paramref name="items"/>, raising a
    /// single <see cref="NotifyCollectionChangedAction.Reset"/>.
    /// </summary>
    /// <param name="items">
    /// The new contents. Must not be this collection itself (the contents are
    /// cleared before <paramref name="items"/> is enumerated).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="items"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called from inside one of this collection's own
    /// <see cref="INotifyCollectionChanged.CollectionChanged"/> handlers
    /// (matching the reentrancy contract of the base mutators).
    /// </exception>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (ReferenceEquals(items, this))
        {
            throw new ArgumentException(
                "Cannot replace a collection with itself: the contents are cleared before the replacement is enumerated.",
                nameof(items));
        }

        CheckReentrancy();

        // Mutate the protected Items list directly — this raises no events —
        // then notify once. This is the same pattern ObservableCollection's own
        // ClearItems uses, extended to cover the re-population.
        Items.Clear();
        foreach (T item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(s_countChanged);
        OnPropertyChanged(s_indexerChanged);
        OnCollectionChanged(s_reset);
    }
}
