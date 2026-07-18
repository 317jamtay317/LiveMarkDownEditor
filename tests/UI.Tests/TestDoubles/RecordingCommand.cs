using System.Windows.Input;

namespace UI.Tests.TestDoubles;

/// <summary>
/// An <see cref="ICommand"/> for tests that records the last parameter it was executed with, so a
/// test can assert what a control asked to be done without wiring a real ViewModel.
/// </summary>
public sealed class RecordingCommand : ICommand
{
    /// <summary>The parameter of the most recent <see cref="Execute"/>, or <see langword="null"/>.</summary>
    public object? LastParameter { get; private set; }

    /// <summary>How many times <see cref="Execute"/> was called.</summary>
    public int ExecuteCount { get; private set; }

    /// <inheritdoc />
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        LastParameter = parameter;
        ExecuteCount++;
    }

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
}
