using System.Collections.ObjectModel;

namespace UI.Notifications;

/// <summary>
/// Surfaces transient notifications to the user through the snackbar. The abstraction carries no
/// WPF dependency so it can be consumed and asserted on in tests, and bound to by a view.
/// </summary>
public interface ISnackbarService
{
    /// <summary>The messages that have been shown, in the order they were shown.</summary>
    ReadOnlyObservableCollection<SnackbarMessage> Messages { get; }

    /// <summary>Raised after a message has been shown.</summary>
    event EventHandler<SnackbarMessage>? MessageShown;

    /// <summary>Shows a message to the user.</summary>
    /// <param name="content">The text to show. Must not be null, empty, or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> is null, empty, or whitespace.</exception>
    void Show(string content);
}
