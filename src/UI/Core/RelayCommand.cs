using System.Windows.Input;

namespace UI.Core;

/// <summary>
/// A synchronous <see cref="ICommand"/> that delegates execution and can-execute checks to
/// supplied callbacks.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    /// <summary>Creates a command from an execute action and an optional can-execute predicate.</summary>
    /// <param name="execute">The action run when the command executes.</param>
    /// <param name="canExecute">Optional predicate gating execution; <see langword="null"/> means always executable.</param>
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
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
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute();
}
