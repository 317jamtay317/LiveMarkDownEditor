using System.IO;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Domain;
using UI.Controls;

namespace UI.Wysiwyg;

/// <summary>
/// Renders each Mermaid Diagram in a Visual Document to its picture and fills the corresponding
/// <see cref="MermaidDiagramView"/>, so diagrams appear inline as pictures (INV-047). Rendering is
/// asynchronous — the picture arrives after the projection and changes no structure (INV-003) — and a
/// diagram the renderer cannot produce is left showing its source-text fallback. Rendered pictures are
/// cached by source, so typing elsewhere (which re-projects) never re-renders an unchanged diagram.
/// </summary>
internal sealed class MermaidRenderCoordinator
{
    private readonly Dictionary<string, ImageSource> _cache = new(StringComparer.Ordinal);

    /// <summary>Renders every Mermaid Diagram in the document that is not already rendered.</summary>
    /// <param name="document">The Visual Document to scan for Mermaid Diagrams.</param>
    /// <param name="renderer">The Mermaid image renderer, or <see langword="null"/> to leave the source fallbacks.</param>
    internal void RenderAll(FlowDocument document, IMermaidImageRenderer? renderer)
    {
        if (renderer is null)
        {
            return;
        }

        foreach (var view in DiagramViews(document))
        {
            var source = view.Source ?? string.Empty;
            if (_cache.TryGetValue(source, out var cached))
            {
                view.Rendered = cached;
            }
            else
            {
                _ = RenderOneAsync(view, source, renderer);
            }
        }
    }

    // Renders one diagram off the UI thread and, back on it, fills the view — unless the view has since
    // been re-projected to a different source. A diagram the renderer cannot produce keeps its fallback.
    private async Task RenderOneAsync(MermaidDiagramView view, string source, IMermaidImageRenderer renderer)
    {
        try
        {
            var image = await renderer.RenderAsync(source).ConfigureAwait(true);
            if (image is null)
            {
                return;
            }

            var picture = ToBitmap(image.Png);
            _cache[source] = picture;
            if (string.Equals(view.Source, source, StringComparison.Ordinal))
            {
                view.Rendered = picture;
            }
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException or ArgumentException)
        {
            // A picture that cannot be decoded leaves the source-text fallback (INV-047).
        }
    }

    private static IEnumerable<MermaidDiagramView> DiagramViews(FlowDocument document) =>
        document.Blocks.OfType<BlockUIContainer>().Select(container => container.Child).OfType<MermaidDiagramView>();

    private static ImageSource ToBitmap(byte[] png)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = new MemoryStream(png);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
