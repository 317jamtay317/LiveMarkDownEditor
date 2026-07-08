using System.Windows.Input;

namespace UI.Core;

/// <summary>
/// An asynchronous <see cref="ICommand"/> that accepts a typed parameter and runs an async callback,
/// disabling itself while the operation is in flight so it cannot be re-entered.
/// </summary>
/// <typeparam name="T">The command parameter's type.</typeparam>
public sealed class AsyncRelayCommand<T> : ICommand
{
    private readonly Func<T?, Task> _execute;
    private readonly Func<T?, bool>? _canExecute;
    private bool _isRunning;

    /// <summary>Creates a command from an async execute callback and an optional can-execute predicate.</summary>
    /// <param name="execute">The async operation run when the command executes.</param>
    /// <param name="canExecute">Optional predicate gating execution; <see langword="null"/> means always executable.</param>
    public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke((T?)parameter) ?? true);

    /// <inheritdoc />
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
        {
            return;
        }

        _isRunning = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await _execute((T?)parameter).ConfigureAwait(true);
        }
        finally
        {
            _isRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
