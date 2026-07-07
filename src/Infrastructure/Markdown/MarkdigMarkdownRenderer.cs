using Domain;
using Markdig;

namespace Infrastructure.Markdown;

/// <summary>
/// Markdig-backed adapter for the <see cref="IMarkdownRenderer"/> port. Renders a
/// <see cref="MarkdownDocument"/> to HTML <see cref="RenderedOutput"/> using the shared
/// <see cref="GfmPipeline"/>.
/// </summary>
/// <remarks>
/// Satisfies INV-002: the render is a pure function of the document's source text — the pipeline
/// is fixed and holds no per-render state, so identical source always yields identical output.
/// </remarks>
public sealed class MarkdigMarkdownRenderer : IMarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline = GfmPipeline.Create();

    /// <inheritdoc />
    public RenderedOutput Render(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var html = Markdig.Markdown.ToHtml(document.Source.Text, _pipeline);
        return new RenderedOutput(html);
    }
}
