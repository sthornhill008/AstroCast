using System.Windows.Input;

using Serilog;

namespace AstroCast.ViewModels;

/// <summary>
/// A synchronous implementation of <see cref="ICommand"/> that delegates
/// execution and can-execute logic to provided delegates.
/// </summary>
/// <remarks>
/// <para>
/// Lightweight command infrastructure for MVVM without external dependencies.
/// Call <see cref="RaiseCanExecuteChanged"/> to re-evaluate the can-execute state.
/// </para>
/// <para>
/// The Execute path is hardened so that an exception thrown by the user-supplied
/// delegate (or by a throwing <see cref="ErrorOccurred"/> subscriber) cannot tear
/// down the WPF dispatcher. Exceptions are routed through the optional
/// <see cref="ILogger"/> first, then surfaced to subscribers — each step protected
/// from re-throwing. <see cref="OperationCanceledException"/> is handled silently.
/// </para>
/// </remarks>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="RelayCommand"/>.
    /// </summary>
    /// <param name="execute">The action to invoke when the command is executed.</param>
    /// <param name="canExecute">
    /// Optional predicate that determines whether the command can execute.
    /// When null, the command is always enabled.
    /// </param>
    /// <param name="logger">
    /// Optional Serilog logger used to record unhandled exceptions thrown by the
    /// execute delegate. When null, exceptions are written to
    /// <see cref="System.Diagnostics.Debug"/> so the legacy diagnostic surface
    /// is preserved.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="execute"/> is null.
    /// </exception>
    public RelayCommand(
        Action<object?> execute,
        Func<object?, bool>? canExecute = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;
        _canExecute = canExecute;
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Raised when an unhandled exception occurs during command execution.
    /// Subscribers can use this to display error messages or log failures.
    /// </summary>
    /// <remarks>
    /// <see cref="OperationCanceledException"/> is NOT raised through this event
    /// because cancellation is an expected user-initiated action, not an error.
    /// </remarks>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Gets the last exception thrown during execution, or null if the last
    /// execution completed successfully or was cancelled.
    /// </summary>
    public Exception? LastException { get; private set; }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) =>
        _canExecute is null || _canExecute(parameter);

    /// <inheritdoc />
    /// <remarks>
    /// Wraps the user delegate so a throwing command cannot crash the WPF
    /// dispatcher. The exception is first logged (logger failures are
    /// swallowed), then the <see cref="ErrorOccurred"/> event is raised
    /// (subscriber failures are swallowed).
    /// </remarks>
    public void Execute(object? parameter)
    {
        LastException = null;

        try
        {
            _execute(parameter);
        }
        catch (OperationCanceledException)
        {
            // Expected: user-initiated cancellation — do not surface as an error.
            System.Diagnostics.Debug.WriteLine("RelayCommand: operation cancelled.");
        }
        catch (Exception ex)
        {
            LastException = ex;

            // Step 1 — log first. If the logger throws (e.g., disk full), swallow it
            // so the catch block can still fall through to the subscriber notification.
            try
            {
                if (_logger is not null)
                {
                    _logger.Error(ex, "RelayCommand unhandled exception");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"RelayCommand unhandled: {ex}");
                }
            }
            catch (Exception loggingEx)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"RelayCommand: logger threw while reporting exception: {loggingEx}");
            }

            // Step 2 — notify subscribers. A throwing subscriber must not escape.
            try
            {
                ErrorOccurred?.Invoke(this, ex);
            }
            catch (Exception subscriberEx)
            {
                try
                {
                    _logger?.Error(
                        subscriberEx,
                        "RelayCommand: ErrorOccurred subscriber threw");
                }
                catch
                {
                    // Logger already failed once or is unavailable — nothing else to do.
                }

                System.Diagnostics.Debug.WriteLine(
                    $"RelayCommand: ErrorOccurred subscriber threw: {subscriberEx}");
            }
        }
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event to signal
    /// that the return value of <see cref="CanExecute"/> may have changed.
    /// </summary>
    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// An asynchronous implementation of <see cref="ICommand"/> that delegates
/// execution to an async method and prevents concurrent re-entry.
/// </summary>
/// <remarks>
/// <para>
/// While the async operation is in progress, <see cref="CanExecute"/> returns false.
/// Exceptions are caught, logged via the optional <see cref="ILogger"/>, and surfaced
/// via the <see cref="ErrorOccurred"/> event. <see cref="OperationCanceledException"/>
/// is handled silently (expected user-initiated cancellation).
/// </para>
/// <para>
/// The exception path is hardened so that a throwing logger or a throwing
/// <see cref="ErrorOccurred"/> subscriber cannot crash the WPF dispatcher
/// from this <c>async void</c> handler.
/// </para>
/// </remarks>
public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<object?, Task> _execute;
    private readonly Func<object?, bool>? _canExecute;
    private readonly ILogger? _logger;
    private bool _isExecuting;

    /// <summary>
    /// Initialises a new instance of <see cref="AsyncRelayCommand"/>.
    /// </summary>
    /// <param name="execute">The async function to invoke when the command is executed.</param>
    /// <param name="canExecute">
    /// Optional predicate that determines whether the command can execute.
    /// When null, the command is always enabled (subject to <see cref="IsExecuting"/>).
    /// </param>
    /// <param name="logger">
    /// Optional Serilog logger used to record unhandled exceptions thrown by the
    /// async delegate. When null, exceptions are written to <see cref="System.Diagnostics.Debug"/>
    /// so the legacy diagnostic surface is preserved.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="execute"/> is null.
    /// </exception>
    public AsyncRelayCommand(
        Func<object?, Task> execute,
        Func<object?, bool>? canExecute = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;
        _canExecute = canExecute;
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Raised when an unhandled exception occurs during command execution.
    /// Subscribers can use this to display error messages or log failures.
    /// </summary>
    /// <remarks>
    /// <see cref="OperationCanceledException"/> is NOT raised through this event
    /// because cancellation is an expected user-initiated action, not an error.
    /// </remarks>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Gets a value indicating whether the command is currently executing.
    /// </summary>
    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            _isExecuting = value;
            RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Gets the last exception thrown during execution, or null if the last
    /// execution completed successfully or was cancelled.
    /// </summary>
    public Exception? LastException { get; private set; }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) =>
        !IsExecuting && (_canExecute is null || _canExecute(parameter));

    /// <inheritdoc />
    /// <remarks>
    /// This is an <c>async void</c> method (required by <see cref="ICommand"/>).
    /// Any exception that escapes here would terminate the WPF dispatcher, so the
    /// catch block records the failure to the logger first, then notifies subscribers,
    /// each step protected from re-throwing.
    /// </remarks>
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        IsExecuting = true;
        LastException = null;

        try
        {
            await _execute(parameter);
        }
        catch (OperationCanceledException)
        {
            // Expected: user-initiated cancellation — do not surface as an error.
            System.Diagnostics.Debug.WriteLine("AsyncRelayCommand: operation cancelled.");
        }
        catch (Exception ex)
        {
            LastException = ex;

            // Step 1 — log first. If the logger throws (e.g., disk full), swallow it
            // so the catch block can still fall through to the subscriber notification.
            try
            {
                if (_logger is not null)
                {
                    _logger.Error(ex, "AsyncRelayCommand unhandled exception");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"AsyncRelayCommand unhandled: {ex}");
                }
            }
            catch (Exception loggingEx)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"AsyncRelayCommand: logger threw while reporting exception: {loggingEx}");
            }

            // Step 2 — notify subscribers. A throwing subscriber must not escape
            // and tear down the WPF dispatcher from this async void method.
            try
            {
                ErrorOccurred?.Invoke(this, ex);
            }
            catch (Exception subscriberEx)
            {
                try
                {
                    _logger?.Error(
                        subscriberEx,
                        "AsyncRelayCommand: ErrorOccurred subscriber threw");
                }
                catch
                {
                    // Logger already failed once or is unavailable — nothing else to do.
                }

                System.Diagnostics.Debug.WriteLine(
                    $"AsyncRelayCommand: ErrorOccurred subscriber threw: {subscriberEx}");
            }
        }
        finally
        {
            IsExecuting = false;
        }
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event to signal
    /// that the return value of <see cref="CanExecute"/> may have changed.
    /// </summary>
    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// A strongly-typed relay command that accepts a parameter of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The command parameter type.</typeparam>
/// <remarks>
/// The Execute path is hardened so that an exception thrown by the user-supplied
/// delegate (or by a throwing <see cref="ErrorOccurred"/> subscriber) cannot tear
/// down the WPF dispatcher. Same exception-handling contract as
/// <see cref="RelayCommand"/>.
/// </remarks>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Func<T, bool>? _canExecute;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initialises a new instance of the <see cref="RelayCommand{T}"/> class.
    /// </summary>
    /// <param name="execute">The action to execute when the command is invoked.</param>
    /// <param name="canExecute">Optional predicate that determines whether the command can execute.</param>
    /// <param name="logger">
    /// Optional Serilog logger used to record unhandled exceptions thrown by the
    /// execute delegate. When null, exceptions are written to
    /// <see cref="System.Diagnostics.Debug"/>.
    /// </param>
    public RelayCommand(
        Action<T> execute,
        Func<T, bool>? canExecute = null,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        _execute = execute;
        _canExecute = canExecute;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// A plain CLR event, matching <see cref="RelayCommand"/> and
    /// <see cref="AsyncRelayCommand"/>. Re-evaluation is requested explicitly by
    /// calling <see cref="RaiseCanExecuteChanged"/>; the codebase already routes
    /// every refresh that way (see <c>MainViewModel</c>'s command-state refresh
    /// helpers and <c>ChildViewModelBase.RequestCommandStateRefresh</c>).
    /// </para>
    /// <para>
    /// <b>Why this is not wired to WPF's <c>CommandManager.RequerySuggested</c>.</b>
    /// This type previously subscribed callers to that static event, which lives
    /// in <c>PresentationCore</c> and therefore cannot exist outside WPF. Removing
    /// it lets the command family compile in a framework-neutral assembly (the
    /// planned <c>AstroCast.Presentation</c>) so the same commands can back both
    /// the WPF and the future Avalonia UI. The change is behaviour-preserving:
    /// the only instance of this type is <c>MainViewModel.NavigateToPanelCommand</c>,
    /// constructed without a <c>canExecute</c> predicate, so
    /// <see cref="CanExecute"/> is unconditionally <see langword="true"/> and no
    /// subscriber could ever observe a state change.
    /// </para>
    /// </remarks>
    public event EventHandler? CanExecuteChanged;

    /// <summary>
    /// Raised when an unhandled exception occurs during command execution.
    /// </summary>
    /// <remarks>
    /// <see cref="OperationCanceledException"/> is NOT raised through this event
    /// because cancellation is an expected user-initiated action, not an error.
    /// </remarks>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Gets the last exception thrown during execution, or null if the last
    /// execution completed successfully or was cancelled.
    /// </summary>
    public Exception? LastException { get; private set; }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) =>
        _canExecute is null || (parameter is T t && _canExecute(t));

    /// <inheritdoc />
    /// <remarks>
    /// Wraps the user delegate so a throwing command cannot crash the WPF
    /// dispatcher. The exception is first logged, then the
    /// <see cref="ErrorOccurred"/> event is raised — each step protected from
    /// re-throwing. Silently ignores invocations when <paramref name="parameter"/>
    /// is not of type <typeparamref name="T"/>.
    /// </remarks>
    public void Execute(object? parameter)
    {
        if (parameter is not T t)
            return;

        LastException = null;

        try
        {
            _execute(t);
        }
        catch (OperationCanceledException)
        {
            // Expected: user-initiated cancellation — do not surface as an error.
            System.Diagnostics.Debug.WriteLine("RelayCommand<T>: operation cancelled.");
        }
        catch (Exception ex)
        {
            LastException = ex;

            // Step 1 — log first. If the logger throws (e.g., disk full), swallow it
            // so the catch block can still fall through to the subscriber notification.
            try
            {
                if (_logger is not null)
                {
                    _logger.Error(ex, "RelayCommand<T> unhandled exception");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"RelayCommand<T> unhandled: {ex}");
                }
            }
            catch (Exception loggingEx)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"RelayCommand<T>: logger threw while reporting exception: {loggingEx}");
            }

            // Step 2 — notify subscribers. A throwing subscriber must not escape.
            try
            {
                ErrorOccurred?.Invoke(this, ex);
            }
            catch (Exception subscriberEx)
            {
                try
                {
                    _logger?.Error(
                        subscriberEx,
                        "RelayCommand<T>: ErrorOccurred subscriber threw");
                }
                catch
                {
                    // Logger already failed once or is unavailable — nothing else to do.
                }

                System.Diagnostics.Debug.WriteLine(
                    $"RelayCommand<T>: ErrorOccurred subscriber threw: {subscriberEx}");
            }
        }
    }

    /// <summary>
    /// Raises the <see cref="CanExecuteChanged"/> event to signal
    /// that the return value of <see cref="CanExecute"/> may have changed.
    /// </summary>
    public void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
