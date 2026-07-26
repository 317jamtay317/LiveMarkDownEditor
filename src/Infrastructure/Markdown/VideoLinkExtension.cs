using Domain;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html.Inlines;
using Markdig.Syntax.Inlines;

namespace Infrastructure.Markdown;

/// <summary>
/// Renders a Video as a <c>&lt;video controls&gt;</c> element rather than an <c>&lt;img&gt;</c> that
/// could never play (INV-069).
/// </summary>
/// <remarks>
/// A Video is authored with the Image's own syntax — <c>![alt](clip.mp4)</c> — so Markdig parses it as
/// an image link and would render it as one. This extension changes only the rendering: the parse is
/// untouched, so the Visual Document and the Rendered Output still read the identical AST, and both ask
/// the same <see cref="VideoSource"/> which construct it is. A Link to a video (<c>[text](clip.mp4)</c>)
/// stays a link — the reader is being sent to the file, not shown it.
/// </remarks>
public sealed class VideoLinkExtension : IMarkdownExtension
{
    /// <summary>No parser changes: which construct it is, is decided from the Media Source (INV-069).</summary>
    /// <param name="pipeline">The pipeline builder being configured.</param>
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
    }

    /// <summary>Swaps Markdig's link renderer for the one that knows a Video from an Image.</summary>
    /// <param name="pipeline">The built pipeline.</param>
    /// <param name="renderer">The renderer being configured.</param>
    public void Setup(MarkdownPipeline pipeline, Markdig.Renderers.IMarkdownRenderer renderer)
    {
        if (renderer is not HtmlRenderer html)
        {
            return;
        }

        html.ObjectRenderers.RemoveAll(existing => existing is LinkInlineRenderer);
        html.ObjectRenderers.Add(new VideoLinkInlineRenderer());
    }
}

/// <summary>
/// The link renderer that writes a Video as a video element and defers everything else — every Link,
/// and every Image whose Media Source is not a video — to Markdig's own rendering (INV-069).
/// </summary>
internal sealed class VideoLinkInlineRenderer : LinkInlineRenderer
{
    /// <inheritdoc />
    protected override void Write(HtmlRenderer renderer, LinkInline link)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(link);

        if (!link.IsImage || !VideoSource.IsVideo(link.Url))
        {
            base.Write(renderer, link);
            return;
        }

        if (!renderer.EnableHtmlForInline)
        {
            // Somewhere HTML is not allowed (an image's own alt attribute, say) only the words can go.
            renderer.WriteChildren(link);
            return;
        }

        renderer.Write("<video controls src=\"");
        renderer.WriteEscapeUrl(link.Url);
        renderer.Write('"');
        if (!string.IsNullOrEmpty(link.Title))
        {
            renderer.Write(" title=\"");
            renderer.WriteEscape(link.Title);
            renderer.Write('"');
        }

        renderer.Write('>');

        // The alt text becomes the element's fallback content — what a browser that cannot play the
        // Video shows, which is the same words the editor shows when it cannot (INV-031/069).
        renderer.WriteChildren(link);
        renderer.Write("</video>");
    }
}
