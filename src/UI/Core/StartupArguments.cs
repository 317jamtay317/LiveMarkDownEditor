namespace UI.Core;

/// <summary>
/// Reads the Startup Document out of the process's command-line arguments: the Markdown file path
/// handed to the editor by the command line, or by the operating system shell when the user opens
/// an <c>.md</c> file with the editor (INV-020).
/// </summary>
public static class StartupArguments
{
    /// <summary>
    /// The Startup Document path among <paramref name="args"/>, or <see langword="null"/> when none
    /// was given. Only a Markdown file path qualifies, so host configuration switches and their
    /// values (e.g. <c>--environment Development</c>) are never mistaken for a document.
    /// </summary>
    /// <param name="args">The command-line arguments, as passed to <c>Main</c>.</param>
    /// <returns>The first Markdown file path, or <see langword="null"/>.</returns>
    public static string? DocumentPath(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.FirstOrDefault(argument =>
            argument.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            || argument.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase));
    }
}
