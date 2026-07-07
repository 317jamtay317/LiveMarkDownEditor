using System.Collections.ObjectModel;

namespace UI.Notifications;

/// <summary>
/// Default <see cref="ISnackbarService"/>. Holds the shown messages in an observable collection a
/// view can bind to and raises <see cref="MessageShown"/> for each. Free of WPF dependencies so it
/// is fully unit-testable; marshalling to the UI thread is the view's concern.
/// </summary>
public sealed class SnackbarService : ISnackbarService
{
    private readonly ObservableCollection<SnackbarMessage> _messages = new();

    /// <summary>Creates a snackbar service with an empty message list.</summary>
    public SnackbarService()
    {
        Messages = new ReadOnlyObservableCollection<SnackbarMessage>(_messages);
    }

    /// <inheritdoc />
    public ReadOnlyObservableCollection<SnackbarMessage> Messages { get; }

    /// <inheritdoc />
    public event EventHandler<SnackbarMessage>? MessageShown;

    /// <inheritdoc />
    public void Show(string content)
    {
        var message = new SnackbarMessage(content);
        _messages.Add(message);
        MessageShown?.Invoke(this, message);
    }
}
