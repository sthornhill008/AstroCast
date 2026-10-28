using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AstroCast.Tests.Commands;

/// <summary>
/// Hardening tests for <see cref="AsyncRelayCommand"/> exception handling.
/// Verifies that logger failures and subscriber failures cannot escape the
/// <c>async void</c> <see cref="AsyncRelayCommand.Execute"/> method and crash
/// the WPF dispatcher.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AsyncRelayCommandTests
{
    #region Test Doubles

    /// <summary>
    /// Serilog sink that records the number of calls and optionally throws
    /// to simulate a logger that fails (e.g., disk full, IO error).
    /// </summary>
    private sealed class RecordingSink : ILogEventSink
    {
        public int CallCount { get; private set; }

        public bool ThrowOnEmit { get; set; }

        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent)
        {
            CallCount++;
            Events.Add(logEvent);

            if (ThrowOnEmit)
            {
                throw new InvalidOperationException("Simulated logger failure");
            }
        }
    }

    #endregion

    #region Success Path

    /// <summary>
    /// Execute_SuccessfulDelegate_LeavesLastExceptionNullAndIsExecutingFalse.
    /// </summary>
    [Fact]
    public async Task Execute_SuccessfulDelegate_LeavesLastExceptionNullAndIsExecutingFalse()
    {
        TaskCompletionSource<bool> tcs = new();
        AsyncRelayCommand command = new(async _ =>
        {
            await Task.Yield();
            tcs.SetResult(true);
        });

        command.Execute(null);
        await tcs.Task;
        await WaitForCompletionAsync(command);

        command.IsExecuting.Should().BeFalse();
        command.LastException.Should().BeNull();
    }

    #endregion

    #region Exception Path

    /// <summary>
    /// Execute_DelegateThrows_LogsExceptionAndRaisesErrorOccurred.
    /// </summary>
    [Fact]
    public async Task Execute_DelegateThrows_LogsExceptionAndRaisesErrorOccurred()
    {
        RecordingSink sink = new();
        ILogger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        InvalidOperationException expected = new("boom");
        AsyncRelayCommand command = new(
            async _ =>
            {
                await Task.Yield();
                throw expected;
            },
            canExecute: null,
            logger: logger);

        Exception? captured = null;
        command.ErrorOccurred += (_, ex) => captured = ex;

        command.Execute(null);
        await WaitForCompletionAsync(command);

        command.IsExecuting.Should().BeFalse();
        command.LastException.Should().BeSameAs(expected);
        captured.Should().BeSameAs(expected);
        sink.CallCount.Should().BeGreaterThan(0);
        sink.Events.Should().Contain(e => e.Level == LogEventLevel.Error);
    }

    /// <summary>
    /// Execute_DelegateThrowsOperationCanceled_DoesNotRaiseErrorOccurred.
    /// </summary>
    [Fact]
    public async Task Execute_DelegateThrowsOperationCanceled_DoesNotRaiseErrorOccurred()
    {
        AsyncRelayCommand command = new(async _ =>
        {
            await Task.Yield();
            throw new OperationCanceledException();
        });

        bool errorRaised = false;
        command.ErrorOccurred += (_, _) => errorRaised = true;

        command.Execute(null);
        await WaitForCompletionAsync(command);

        command.IsExecuting.Should().BeFalse();
        command.LastException.Should().BeNull();
        errorRaised.Should().BeFalse();
    }

    #endregion

    #region Hardened Exception Path

    /// <summary>
    /// Execute_LoggerThrows_DoesNotPropagateAndStillRaisesErrorOccurred.
    /// </summary>
    [Fact]
    public async Task Execute_LoggerThrows_DoesNotPropagateAndStillRaisesErrorOccurred()
    {
        RecordingSink sink = new() { ThrowOnEmit = true };
        ILogger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        InvalidOperationException expected = new("boom");
        AsyncRelayCommand command = new(
            async _ =>
            {
                await Task.Yield();
                throw expected;
            },
            canExecute: null,
            logger: logger);

        Exception? captured = null;
        command.ErrorOccurred += (_, ex) => captured = ex;

        // Must not throw even though the logger throws inside the catch block.
        Func<Task> act = async () =>
        {
            command.Execute(null);
            await WaitForCompletionAsync(command);
        };

        await act.Should().NotThrowAsync();

        command.IsExecuting.Should().BeFalse();
        command.LastException.Should().BeSameAs(expected);
        captured.Should().BeSameAs(expected);
    }

    /// <summary>
    /// Execute_ErrorSubscriberThrows_DoesNotPropagateAndIsExecutingReturnsToFalse.
    /// </summary>
    [Fact]
    public async Task Execute_ErrorSubscriberThrows_DoesNotPropagateAndIsExecutingReturnsToFalse()
    {
        AsyncRelayCommand command = new(async _ =>
        {
            await Task.Yield();
            throw new InvalidOperationException("boom");
        });

        command.ErrorOccurred += (_, _) =>
            throw new InvalidOperationException("Subscriber failure");

        Func<Task> act = async () =>
        {
            command.Execute(null);
            await WaitForCompletionAsync(command);
        };

        await act.Should().NotThrowAsync();

        command.IsExecuting.Should().BeFalse();
        command.LastException.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("boom");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Polls <see cref="AsyncRelayCommand.IsExecuting"/> until the async void
    /// handler finishes (or the timeout elapses). Test-only helper because the
    /// public surface cannot await the fire-and-forget dispatch directly.
    /// </summary>
    /// <param name="command">The command to observe.</param>
    /// <param name="timeoutMs">Maximum milliseconds to wait.</param>
    private static async Task WaitForCompletionAsync(AsyncRelayCommand command, int timeoutMs = 5000)
    {
        int elapsed = 0;
        const int pollMs = 10;

        while (command.IsExecuting && elapsed < timeoutMs)
        {
            await Task.Delay(pollMs);
            elapsed += pollMs;
        }

        command.IsExecuting.Should().BeFalse(
            "the async void handler should have finished within {0} ms", timeoutMs);
    }

    #endregion
}
