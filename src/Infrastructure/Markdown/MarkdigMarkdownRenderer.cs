using Domain;
using Markdig;
using Markdig.Renderers;

namespace Infrastructure.Markdown;

/// <summary>
/// Markdig-backed adapter for the <see cref="IMarkdownRenderer"/> port. Renders a
/// <see cref="MarkdownDocument"/> to HTML <see cref="RenderedOutput"/> using the shared
/// <see cref="GfmPipeline"/>.
/// </summary>
/// <remarks>
/// Satisfies INV-002: the render is a pure function of the document's source text — the pipeline
/// is fixed and holds no per-render state, so identical source always yields identical output.
/// <para>
/// Given a tokenizer, each fenced Code Block is rendered with its Syntax Highlighting carried as
/// token markup (INV-064). The coloring adds spans around the code and never changes the code, so
/// both Export Shapes still carry the same content they always did (INV-032).
/// </para>
/// </remarks>
/// <param name="syntaxHighlighter">
/// Tokenizes each Code Block's code so the Rendered Output carries its Syntax Highlighting.
/// <see langword="null"/> renders every Code Block uncolored, exactly as before.
/// </param>
public sealed class MarkdigMarkdownRenderer(ISyntaxHighlighter? syntaxHighlighter = null) : Domain.IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = GfmPipeline.Create();

    /// <inheritdoc />
    public RenderedOutput Render(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var html = syntaxHighlighter is null
            ? Markdig.Markdown.ToHtml(document.Source.Text, _pipeline)
            : ToHighlightedHtml(document.Source.Text);

        return new RenderedOutput(html);
    }

    // Renders through a writer whose Code Block renderer has been swapped for the highlighting one.
    // The renderer is built per render rather than held, so the adapter stays free of per-render
    // state and the render stays a pure function of its input (INV-002).
    private string ToHighlightedHtml(string markdown)
    {
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        _pipeline.Setup(renderer);

        renderer.ObjectRenderers.RemoveAll(existing => existing is Markdig.Renderers.Html.CodeBlockRenderer);
        renderer.ObjectRenderers.Add(new HighlightedCodeBlockRenderer(syntaxHighlighter!));

        renderer.Render(Markdig.Markdown.Parse(markdown, _pipeline));
        writer.Flush();
        return writer.ToString();
    }
}
