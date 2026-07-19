using System.Text;
using System.Windows.Documents;

namespace UI.Wysiwyg;

/// <summary>
/// Locates the Mermaid Diagram a caret is within — a Code Block whose language is
/// <see cref="Language"/> — and returns its source so the Diagram Preview can render it. Pure and
/// view-only: it reads the Visual Document and never changes the Markdown Document (INV-047).
/// </summary>
public static class MermaidDiagram
{
    /// <summary>The Code Block language (info string) that marks a Mermaid Diagram.</summary>
    public const string Language = "mermaid";

    /// <summary>
    /// Whether <paramref name="language"/> names a Mermaid Diagram — the info string <c>mermaid</c>,
    /// compared case-insensitively and ignoring surrounding whitespace.
    /// </summary>
    /// <param name="language">The Code Block's language (info string), or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it names a Mermaid Diagram; otherwise <see langword="false"/>.</returns>
    public static bool IsMermaidLanguage(string? language) =>
        string.Equals(language?.Trim(), Language, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The source of the Mermaid Diagram the caret at <paramref name="caret"/> is within, or
    /// <see langword="null"/> when the caret is not inside a Mermaid Diagram (it sits in another Code
    /// Block, in prose, or nowhere). Reading it is view-only — it never changes the Markdown Document
    /// (INV-047).
    /// </summary>
    /// <param name="caret">The caret position to inspect, or <see langword="null"/>.</param>
    /// <returns>The diagram's source, or <see langword="null"/> when the caret is not in a Mermaid Diagram.</returns>
    public static string? SourceAt(TextPointer? caret)
    {
        if (caret?.Paragraph is { Tag: CodeBlockRole role } paragraph && IsMermaidLanguage(role.Language))
        {
            return CodeText(paragraph);
        }

        return null;
    }

    // A Code Block paragraph holds one Run per line separated by LineBreaks (see the projector); join
    // the Runs with newlines to recover the diagram's source exactly as it was projected.
    private static string CodeText(Paragraph paragraph)
    {
        var builder = new StringBuilder();
        foreach (var inline in paragraph.Inlines)
        {
            switch (inline)
            {
                case Run run:
                    builder.Append(run.Text);
                    break;
                case LineBreak:
                    builder.Append('\n');
                    break;
            }
        }

        return builder.ToString();
    }
}
