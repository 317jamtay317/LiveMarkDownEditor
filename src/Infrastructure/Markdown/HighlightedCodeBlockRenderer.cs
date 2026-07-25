using System.Collections.Frozen;
using Domain;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace Infrastructure.Markdown;

/// <summary>
/// Renders a fenced Code Block to HTML with its Syntax Highlighting carried as token markup: each
/// Code Token becomes a <c>&lt;span&gt;</c> classed by its Code Token Kind (INV-064).
/// </summary>
/// <remarks>
/// A Code Block with no Highlighting Language — and a Mermaid Diagram, whose block a Standalone Page
/// reads the source of to render the picture (INV-049) — falls through to Markdig's own renderer, so
/// its HTML is byte-for-byte what it always was. The code itself is HTML-escaped exactly as before;
/// the spans are the only markup the coloring adds, so the text a reader copies out of the page is
/// unchanged (INV-032).
/// </remarks>
internal sealed class HighlightedCodeBlockRenderer(ISyntaxHighlighter highlighter) : HtmlObjectRenderer<CodeBlock>
{
    /// <summary>The CSS class each Code Token Kind is marked with in an exported page.</summary>
    internal static readonly FrozenDictionary<CodeTokenKind, string> ClassNames =
        new Dictionary<CodeTokenKind, string>
        {
            [CodeTokenKind.Comment] = "tok-comment",
            [CodeTokenKind.String] = "tok-string",
            [CodeTokenKind.Number] = "tok-number",
            [CodeTokenKind.Keyword] = "tok-keyword",
            [CodeTokenKind.Type] = "tok-type",
            [CodeTokenKind.Function] = "tok-function",
            [CodeTokenKind.Operator] = "tok-operator",
        }.ToFrozenDictionary();

    private readonly CodeBlockRenderer _uncolored = new();

    /// <inheritdoc />
    protected override void Write(HtmlRenderer renderer, CodeBlock obj)
    {
        var language = (obj as FencedCodeBlock)?.Info;
        var tokens = obj is FencedCodeBlock
            ? highlighter.Highlight(CodeOf(obj), language)
            : [];

        if (tokens.Count == 0)
        {
            // No Highlighting Language. Markdig's own renderer produces exactly the HTML this block
            // has always produced — including the language-mermaid class and untouched source a
            // Standalone Page needs to render the diagram.
            _uncolored.Write(renderer, obj);
            return;
        }

        renderer.Write("<pre><code");
        if (!string.IsNullOrEmpty(language))
        {
            renderer.Write(" class=\"language-");
            renderer.WriteEscape(language);
            renderer.Write('"');
        }

        renderer.Write('>');

        foreach (var token in tokens)
        {
            if (ClassNames.TryGetValue(token.Kind, out var className))
            {
                renderer.Write("<span class=\"").Write(className).Write("\">");
                renderer.WriteEscape(token.Text);
                renderer.Write("</span>");
            }
            else
            {
                // A Plain Code Token gets no span, mirroring the palette: plain code is the ordinary
                // code color, so wrapping it would add markup that styles nothing.
                renderer.WriteEscape(token.Text);
            }
        }

        // Markdig ends a code block's content with a newline; keeping it means the highlighted block
        // lays out exactly as an uncolored one.
        renderer.Write('\n');
        renderer.Write("</code></pre>");
        renderer.EnsureLine();
    }

    // The block's code, lines joined by '\n' — the same text the Visual Document holds and the same
    // text the tokenizer is handed everywhere else, so every surface colors identically.
    private static string CodeOf(LeafBlock block)
    {
        var lines = block.Lines;
        var slices = new List<string>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            slices.Add(lines.Lines[i].Slice.ToString());
        }

        return string.Join("\n", slices);
    }
}
