using System.IO;

namespace Domain;

/// <summary>
/// The single source of truth for what counts as a Markdown Document file by name — a file whose
/// extension is <c>.md</c> or <c>.markdown</c> (compared case-insensitively). Both the pure
/// <see cref="FolderWorkspace.From"/> builder and the Infrastructure folder reader consult this rule,
/// so "which files appear in a Folder Tree" is decided in exactly one place (INV-042).
/// </summary>
public static class MarkdownFile
{
    /// <summary>The recognised Markdown file extensions, lower-case and including the leading dot.</summary>
    public static readonly IReadOnlyList<string> Extensions = [".md", ".markdown"];

    /// <summary>
    /// Whether <paramref name="fileName"/> names a Markdown Document — its extension is one of
    /// <see cref="Extensions"/>, compared case-insensitively. A blank name is not Markdown.
    /// </summary>
    /// <param name="fileName">The file name (or path) to test.</param>
    /// <returns><see langword="true"/> if the name ends in a recognised Markdown extension.</returns>
    public static bool IsMarkdown(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return Extensions.Any(known => extension.Equals(known, StringComparison.OrdinalIgnoreCase));
    }
}
