using Microsoft.Extensions.Logging;
using UI.Notifications;

namespace UI.Diagnostics;

/// <summary>
/// Default <see cref="IGlobalExceptionHandler"/>. Logs the exception at error level and surfaces a
/// user-facing snackbar message. Holds no WPF dependency so it is fully unit-testable.
/// </summary>
/// <param name="logger">The logger used to record the exception.</param>
/// <param name="snackbar">The snackbar used to notify the user.</param>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    ISnackbarService snackbar) : IGlobalExceptionHandler
{
    /// <inheritdoc />
    public void Handle(Exception exception, string source)
    {
        ArgumentNullException.ThrowIfNull(exception);

        logger.LogError(exception, "Unhandled exception surfaced from {Source}.", source);
        snackbar.Show("An unexpected error occurred. The details have been logged.");
    }
}
