using Markdig.Syntax;

namespace Infrastructure.Markdown;

/// <summary>
/// Identifies the Mermaid Diagrams in a parsed Markdown document and reads their source. A Mermaid
/// Diagram is a fenced Code Block whose info string is <c>mermaid</c> (INV-047). Shared by the PDF
/// exporter (which renders each one) and the composer (which places the rendered image), so both key
/// a diagram on the identical source text.
/// </summary>
internal static class MermaidBlocks
{
    /// <summary>Whether <paramref name="info"/> is the <c>mermaid</c> info string (case-insensitive).</summary>
    /// <param name="info">A fenced Code Block's info string, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it names a Mermaid Diagram; otherwise <see langword="false"/>.</returns>
    public static bool IsMermaid(string? info) =>
        string.Equals(info?.Trim(), "mermaid", StringComparison.OrdinalIgnoreCase);

    /// <summary>The source text of a Code Block — its lines joined with newlines.</summary>
    /// <param name="code">The Code Block whose source to read.</param>
    /// <returns>The Code Block's source text.</returns>
    public static string SourceOf(CodeBlock code)
    {
        var lines = code.Lines;
        var slices = new List<string>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            slices.Add(lines.Lines[i].Slice.ToString());
        }

        return string.Join("\n", slices);
    }

    /// <summary>The distinct Mermaid Diagram sources in <paramref name="document"/>, in document order.</summary>
    /// <param name="document">The parsed Markdown document to scan.</param>
    /// <returns>Each Mermaid Diagram's source, without duplicates.</returns>
    public static IReadOnlyList<string> Find(MarkdownDocument document)
    {
        var sources = new List<string>();
        var seen = new HashSet<string>();
        foreach (var fenced in document.Descendants<FencedCodeBlock>())
        {
            if (IsMermaid(fenced.Info) && seen.Add(SourceOf(fenced)))
            {
                sources.Add(SourceOf(fenced));
            }
        }

        return sources;
    }
}
