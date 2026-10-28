using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AstroCast.Tests.Commands;

/// <summary>
/// Hardening tests for the synchronous <see cref="RelayCommand"/> and
/// <see cref="RelayCommand{T}"/> exception paths. Mirrors the safety net added to
/// <see cref="AsyncRelayCommand"/>: a throwing delegate, a throwing logger, or a
/// throwing <c>ErrorOccurred</c> subscriber must not escape Execute and tear down
/// the WPF dispatcher.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RelayCommandTests
{
    #region Test Doubles

    /// <summary>
    /// Serilog sink that records the number of calls and optionally throws to
    /// simulate a logger that fails (e.g., disk full, IO error).
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

    #region RelayCommand — Exception Path

    /// <summary>
    /// Execute on a throwing delegate must log the exception, raise
    /// <see cref="RelayCommand.ErrorOccurred"/>, and populate
    /// <see cref="RelayCommand.LastException"/>.
    /// </summary>
    [Fact]
    public void Execute_DelegateThrows_LogsExceptionAndRaisesErrorOccurred()
    {
        RecordingSink sink = new();
        ILogger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        InvalidOperationException expected = new("sync boom");
        RelayCommand command = new(
            _ => throw expected,
            canExecute: null,
            logger: logger);

        Exception? captured = null;
        command.ErrorOccurred += (_, ex) => captured = ex;

        Action act = () => command.Execute(null);

        act.Should().NotThrow("a throwing delegate must not escape Execute");
        command.LastException.Should().BeSameAs(expected);
        captured.Should().BeSameAs(expected);
        sink.CallCount.Should().BeGreaterThan(0);
        sink.Events.Should().Contain(e => e.Level == LogEventLevel.Error);
    }

    /// <summary>
    /// A logger that throws must not propagate its own failure; the exception
    /// surface (<see cref="RelayCommand.ErrorOccurred"/> and
    /// <see cref="RelayCommand.LastException"/>) must still report the original
    /// failure.
    /// </summary>
    [Fact]
    public void Execute_LoggerThrows_DoesNotPropagateAndStillRaisesErrorOccurred()
    {
        RecordingSink sink = new() { ThrowOnEmit = true };
        ILogger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        InvalidOperationException expected = new("sync boom");
        RelayCommand command = new(
            _ => throw expected,
            canExecute: null,
            logger: logger);

        Exception? captured = null;
        command.ErrorOccurred += (_, ex) => captured = ex;

        Action act = () => command.Execute(null);

        act.Should().NotThrow("a throwing logger must not escape Execute");
        command.LastException.Should().BeSameAs(expected);
        captured.Should().BeSameAs(expected);
    }

    /// <summary>
    /// A throwing <see cref="RelayCommand.ErrorOccurred"/> subscriber must not
    /// escape Execute. The original failure must still be stored in
    /// <see cref="RelayCommand.LastException"/>.
    /// </summary>
    [Fact]
    public void Execute_ErrorSubscriberThrows_DoesNotPropagate()
    {
        InvalidOperationException expected = new("sync boom");
        RelayCommand command = new(_ => throw expected);

        command.ErrorOccurred += (_, _) =>
            throw new InvalidOperationException("Subscriber failure");

        Action act = () => command.Execute(null);

        act.Should().NotThrow("a throwing subscriber must not escape Execute");
        command.LastException.Should().BeSameAs(expected);
    }

    #endregion

    #region RelayCommand{T} — Exception Path

    /// <summary>
    /// Execute on a throwing typed-parameter delegate must log the exception,
    /// raise <see cref="RelayCommand{T}.ErrorOccurred"/>, and populate
    /// <see cref="RelayCommand{T}.LastException"/>.
    /// </summary>
    [Fact]
    public void Execute_Generic_DelegateThrows_LogsAndRaisesErrorOccurred()
    {
        RecordingSink sink = new();
        ILogger logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        InvalidOperationException expected = new("typed boom");
        RelayCommand<string> command = new(
            _ => throw expected,
            canExecute: null,
            logger: logger);

        Exception? captured = null;
        command.ErrorOccurred += (_, ex) => captured = ex;

        Action act = () => command.Execute("parameter");

        act.Should().NotThrow("a throwing delegate must not escape Execute");
        command.LastException.Should().BeSameAs(expected);
        captured.Should().BeSameAs(expected);
        sink.CallCount.Should().BeGreaterThan(0);
        sink.Events.Should().Contain(e => e.Level == LogEventLevel.Error);
    }

    #endregion
}
