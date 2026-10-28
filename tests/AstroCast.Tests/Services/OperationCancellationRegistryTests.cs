using AstroCast.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Unit tests for <see cref="OperationCancellationRegistry"/> verifying
/// single-flight semantics, lifecycle bookkeeping, idempotent disposal,
/// and post-disposal protection.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OperationCancellationRegistryTests
{
    #region StartOperation — Argument Validation

    /// <summary>
    /// Test 1: <see cref="OperationCancellationRegistry.StartOperation"/> with a
    /// null key must throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void StartOperation_NullKey_ThrowsArgumentException()
    {
        using OperationCancellationRegistry registry = new();

        Action act = () => registry.StartOperation(null!);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Test 2: <see cref="OperationCancellationRegistry.StartOperation"/> with an
    /// empty-string key must throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void StartOperation_EmptyKey_ThrowsArgumentException()
    {
        using OperationCancellationRegistry registry = new();

        Action act = () => registry.StartOperation(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Test 3: <see cref="OperationCancellationRegistry.StartOperation"/> with a
    /// whitespace-only key must throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void StartOperation_WhitespaceKey_ThrowsArgumentException()
    {
        using OperationCancellationRegistry registry = new();

        Action act = () => registry.StartOperation("   ");

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region StartOperation — Token Behaviour

    /// <summary>
    /// Test 4: A freshly started operation must return a token that is not yet
    /// signalled.
    /// </summary>
    [Fact]
    public void StartOperation_ValidKey_ReturnsNonCancelledToken()
    {
        using OperationCancellationRegistry registry = new();

        CancellationToken token = registry.StartOperation("op-a");

        token.IsCancellationRequested.Should().BeFalse();
    }

    /// <summary>
    /// Test 5: Starting an operation must mark its key as active so that
    /// <see cref="OperationCancellationRegistry.IsOperationActive"/> returns true
    /// and <see cref="OperationCancellationRegistry.ActiveOperationKeys"/> contains it.
    /// </summary>
    [Fact]
    public void StartOperation_ValidKey_MarksOperationActive()
    {
        using OperationCancellationRegistry registry = new();

        registry.StartOperation("op-a");

        registry.IsOperationActive("op-a").Should().BeTrue();
        registry.ActiveOperationKeys.Should().ContainSingle().Which.Should().Be("op-a");
    }

    /// <summary>
    /// Test 6: Starting a second operation under the same key must cancel the
    /// previously issued token (single-flight semantics).
    /// </summary>
    [Fact]
    public void StartOperation_SameKeyTwice_CancelsPreviousToken()
    {
        using OperationCancellationRegistry registry = new();

        CancellationToken first = registry.StartOperation("op-a");
        CancellationToken second = registry.StartOperation("op-a");

        first.IsCancellationRequested.Should().BeTrue(
            "starting the same key again must cancel the prior source");
        second.IsCancellationRequested.Should().BeFalse(
            "the new source must remain live until cancelled or completed");
    }

    #endregion

    #region CancelOperation

    /// <summary>
    /// Test 7: Cancelling an unknown key must return <c>false</c> and not throw.
    /// </summary>
    [Fact]
    public void CancelOperation_InactiveKey_ReturnsFalse()
    {
        using OperationCancellationRegistry registry = new();

        bool result = registry.CancelOperation("never-started");

        result.Should().BeFalse();
    }

    /// <summary>
    /// Test 8: Cancelling an active operation must signal its token and return
    /// <c>true</c>. The key remains registered until <see cref="OperationCancellationRegistry.CompleteOperation"/>
    /// is called from the operation's finally block.
    /// </summary>
    [Fact]
    public void CancelOperation_ActiveKey_CancelsTokenAndReturnsTrue()
    {
        using OperationCancellationRegistry registry = new();
        CancellationToken token = registry.StartOperation("op-a");

        bool result = registry.CancelOperation("op-a");

        result.Should().BeTrue();
        token.IsCancellationRequested.Should().BeTrue();
    }

    #endregion

    #region CompleteOperation

    /// <summary>
    /// Test 9: <see cref="OperationCancellationRegistry.CompleteOperation"/> on an
    /// unknown key must return <c>false</c> without throwing.
    /// </summary>
    [Fact]
    public void CompleteOperation_InactiveKey_ReturnsFalse()
    {
        using OperationCancellationRegistry registry = new();

        bool result = registry.CompleteOperation("never-started");

        result.Should().BeFalse();
    }

    /// <summary>
    /// Test 10: <see cref="OperationCancellationRegistry.CompleteOperation"/> on an
    /// active key must remove the entry, dispose its source, and return <c>true</c>.
    /// After completion the key must no longer report as active.
    /// </summary>
    [Fact]
    public void CompleteOperation_ActiveKey_RemovesEntryAndReturnsTrue()
    {
        using OperationCancellationRegistry registry = new();
        registry.StartOperation("op-a");

        bool result = registry.CompleteOperation("op-a");

        result.Should().BeTrue();
        registry.IsOperationActive("op-a").Should().BeFalse();
        registry.ActiveOperationKeys.Should().BeEmpty();
    }

    #endregion

    #region Snapshots and Disposal

    /// <summary>
    /// Test 11: <see cref="OperationCancellationRegistry.ActiveOperationKeys"/>
    /// must return every distinct key that currently has an active operation.
    /// </summary>
    [Fact]
    public void ActiveOperationKeys_ReturnsAllRegisteredKeys()
    {
        using OperationCancellationRegistry registry = new();
        registry.StartOperation("op-a");
        registry.StartOperation("op-b");
        registry.StartOperation("op-c");

        IReadOnlyCollection<string> keys = registry.ActiveOperationKeys;

        keys.Should().BeEquivalentTo(["op-a", "op-b", "op-c"]);
    }

    /// <summary>
    /// Test 12: <see cref="OperationCancellationRegistry.Dispose"/> must cancel
    /// every still-active token, clear the registry, and reject subsequent
    /// <see cref="OperationCancellationRegistry.StartOperation"/> calls with
    /// <see cref="ObjectDisposedException"/>.
    /// </summary>
    [Fact]
    public void Dispose_CancelsAllActiveOperationsAndPreventsNewStarts()
    {
        OperationCancellationRegistry registry = new();
        CancellationToken tokenA = registry.StartOperation("op-a");
        CancellationToken tokenB = registry.StartOperation("op-b");

        registry.Dispose();

        tokenA.IsCancellationRequested.Should().BeTrue();
        tokenB.IsCancellationRequested.Should().BeTrue();
        registry.ActiveOperationKeys.Should().BeEmpty();

        Action startAfterDispose = () => registry.StartOperation("op-c");
        startAfterDispose.Should().Throw<ObjectDisposedException>();
    }

    #endregion
}
