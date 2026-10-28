using System.Collections.Concurrent;

namespace AstroCast.Services;

/// <summary>
/// Centralised registry that owns the <see cref="CancellationTokenSource"/>
/// lifecycle for every long-running view-model operation. Each operation is
/// keyed by a stable string (e.g., <c>"transits"</c>, <c>"astrocartography"</c>)
/// so the UI can request cancellation without holding a direct reference to
/// the source.
/// </summary>
/// <remarks>
/// <para>
/// Typical usage from a view model:
/// <code>
/// CancellationToken ct = _registry.StartOperation("transits");
/// try
/// {
///     await Task.Run(() => _service.Calculate(input, ct), ct);
/// }
/// finally
/// {
///     _registry.CompleteOperation("transits");
/// }
/// </code>
/// </para>
/// <para>
/// Starting an operation that is already active cancels the previous source
/// before issuing a new one — this preserves the invariant that one key maps
/// to at most one in-flight token at a time. <see cref="CancelOperation"/> is
/// safe to call when no operation is active for the key (it simply returns
/// false). <see cref="Dispose"/> cancels and disposes every still-active source
/// so the application can shut down without leaking native handles.
/// </para>
/// </remarks>
public sealed class OperationCancellationRegistry : IDisposable
{
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _sources = new();
    private bool _disposed;

    /// <summary>
    /// Starts a new operation under <paramref name="operationKey"/>, returning the
    /// associated <see cref="CancellationToken"/>. If a previous operation with the
    /// same key is still active, it is cancelled first to enforce single-flight
    /// semantics per key.
    /// </summary>
    /// <param name="operationKey">Stable identifier for the operation (e.g., "transits").</param>
    /// <returns>The cancellation token bound to the new operation.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="operationKey"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="ObjectDisposedException">Thrown if the registry has been disposed.</exception>
    public CancellationToken StartOperation(string operationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
        ObjectDisposedException.ThrowIf(_disposed, this);

        CancellationTokenSource fresh = new();

        // AddOrUpdate semantics: cancel + dispose any existing source so a key
        // never represents two concurrent operations simultaneously.
        _sources.AddOrUpdate(
            operationKey,
            addValueFactory: _ => fresh,
            updateValueFactory: (_, existing) =>
            {
                try
                {
                    existing.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed — ignore.
                }

                existing.Dispose();
                return fresh;
            });

        return fresh.Token;
    }

    /// <summary>
    /// Requests cancellation of the operation associated with
    /// <paramref name="operationKey"/>, if one is currently active.
    /// </summary>
    /// <param name="operationKey">The operation key supplied to <see cref="StartOperation"/>.</param>
    /// <returns>
    /// <c>true</c> if an active source was found and cancellation was signalled;
    /// <c>false</c> if no operation is currently registered under that key.
    /// </returns>
    public bool CancelOperation(string operationKey)
    {
        if (string.IsNullOrWhiteSpace(operationKey) || _disposed)
        {
            return false;
        }

        if (!_sources.TryGetValue(operationKey, out CancellationTokenSource? source))
        {
            return false;
        }

        try
        {
            source.Cancel();
            return true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Marks the operation associated with <paramref name="operationKey"/> as
    /// completed: removes it from the registry and disposes its
    /// <see cref="CancellationTokenSource"/>. Safe to call from a <c>finally</c>
    /// block whether or not the operation completed successfully.
    /// </summary>
    /// <param name="operationKey">The operation key supplied to <see cref="StartOperation"/>.</param>
    /// <returns>
    /// <c>true</c> if a registered source was removed; <c>false</c> if no operation
    /// was active under that key.
    /// </returns>
    public bool CompleteOperation(string operationKey)
    {
        if (string.IsNullOrWhiteSpace(operationKey))
        {
            return false;
        }

        if (!_sources.TryRemove(operationKey, out CancellationTokenSource? source))
        {
            return false;
        }

        source.Dispose();
        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if any operation is currently registered under
    /// <paramref name="operationKey"/>.
    /// </summary>
    /// <param name="operationKey">The operation key to check.</param>
    public bool IsOperationActive(string operationKey)
    {
        if (string.IsNullOrWhiteSpace(operationKey))
        {
            return false;
        }

        return _sources.ContainsKey(operationKey);
    }

    /// <summary>
    /// Snapshot of the keys for all operations currently registered. Intended for
    /// diagnostic logging and UI status — not for control flow.
    /// </summary>
    public IReadOnlyCollection<string> ActiveOperationKeys =>
        _sources.Keys.ToArray();

    /// <summary>
    /// Cancels and disposes every registered <see cref="CancellationTokenSource"/>
    /// so the application can shut down without leaking native handles.
    /// Idempotent: safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        foreach (KeyValuePair<string, CancellationTokenSource> kvp in _sources)
        {
            try
            {
                kvp.Value.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed — ignore.
            }

            kvp.Value.Dispose();
        }

        _sources.Clear();
    }
}
