namespace UI.Core;

/// <summary>
/// Abstraction over the UI thread's dispatcher, so ViewModels can marshal work (such as reacting to
/// an External Change raised on a background thread) onto the UI thread without depending on WPF.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>Queues <paramref name="action"/> to run on the UI thread.</summary>
    /// <param name="action">The work to run on the UI thread.</param>
    void Post(Action action);
}
