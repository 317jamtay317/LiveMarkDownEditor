namespace UI.Diagnostics;

/// <summary>
/// Handles exceptions that escape the application's normal flow. A single testable entry point that
/// the process-wide unhandled-exception events delegate to.
/// </summary>
public interface IGlobalExceptionHandler
{
    /// <summary>Handles an unhandled exception by logging it and notifying the user.</summary>
    /// <param name="exception">The exception that was not handled.</param>
    /// <param name="source">A short description of where the exception surfaced (for example, <c>Dispatcher</c>).</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    void Handle(Exception exception, string source);
}
