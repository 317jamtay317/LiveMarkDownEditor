using System.Windows.Input;

namespace UI.Core;

/// <summary>
/// A synchronous <see cref="ICommand"/> that delegates execution and can-execute checks to
/// supplied callbacks.
/// </summary>
/// <remarks>
/// The command raises <see cref="CanExecuteChanged"/> only when its owner calls
/// <see cref="RaiseCanExecuteChanged"/>. It deliberately does not delegate to
/// <see cref="CommandManager.RequerySuggested"/>, which fires on user input and so would leave a
/// command whose state changed on its own — a Conflict raised by the file watcher, say — rendered
/// stale until the user's next mouse move. An owner whose can-execute predicate reads mutable
/// state must therefore requery the command when that state changes.
/// </remarks>
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
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    /// <inheritdoc />
    public void Execute(object? parameter) => _execute();

    /// <summary>
    /// Asks bound controls to requery <see cref="CanExecute"/>. Call this when the state the
    /// can-execute predicate reads has changed.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
