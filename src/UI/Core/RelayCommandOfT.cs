using System.Windows.Input;

namespace UI.Core;

/// <summary>
/// A synchronous, parameterized <see cref="ICommand"/> that delegates execution and can-execute
/// checks to supplied callbacks taking the command parameter — the parameterized counterpart of
/// <see cref="RelayCommand"/>.
/// </summary>
/// <typeparam name="T">The command parameter's type.</typeparam>
/// <remarks>
/// Like <see cref="RelayCommand"/> it raises <see cref="CanExecuteChanged"/> only when its owner
/// calls <see cref="RaiseCanExecuteChanged"/>, so a command whose availability changed on its own
/// is never left stale until the user's next mouse move. A parameter that is not a
/// <typeparamref name="T"/> can never execute.
/// </remarks>
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _execute;
    private readonly Predicate<T>? _canExecute;

    /// <summary>Creates a command from an execute action and an optional can-execute predicate.</summary>
    /// <param name="execute">The action run, with the command parameter, when the command executes.</param>
    /// <param name="canExecute">Optional predicate gating execution; <see langword="null"/> means always executable.</param>
    public RelayCommand(Action<T> execute, Predicate<T>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public bool CanExecute(object? parameter) =>
        parameter is T value && (_canExecute?.Invoke(value) ?? true);

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (parameter is T value && CanExecute(parameter))
        {
            _execute(value);
        }
    }

    /// <summary>
    /// Asks bound controls to requery <see cref="CanExecute"/>. Call this when the state the
    /// can-execute predicate reads has changed.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
