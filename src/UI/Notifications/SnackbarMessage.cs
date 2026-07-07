namespace UI.Notifications;

/// <summary>
/// A single transient notification surfaced to the user through the snackbar. Immutable and always
/// carries non-empty content.
/// </summary>
public sealed record SnackbarMessage
{
    /// <summary>Creates a snackbar message.</summary>
    /// <param name="content">The text shown to the user. Must not be null, empty, or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> is null, empty, or whitespace.</exception>
    public SnackbarMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Snackbar message content must not be empty.", nameof(content));
        }

        Content = content;
    }

    /// <summary>The text shown to the user.</summary>
    public string Content { get; }
}
