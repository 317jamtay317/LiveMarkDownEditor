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
/// cached by source <em>and</em> theme, so typing elsewhere (which re-projects) never re-renders an
/// unchanged diagram, and toggling the theme re-renders each diagram once per theme and no more.
/// </summary>
internal sealed class MermaidRenderCoordinator
{
    private readonly Dictionary<(string Source, bool Dark), ImageSource> _cache = new();
    private bool _dark;

    /// <summary>Renders every Mermaid Diagram in the document not already rendered in the given theme.</summary>
    /// <param name="document">The Visual Document to scan for Mermaid Diagrams.</param>
    /// <param name="renderer">The Mermaid image renderer, or <see langword="null"/> to leave the source fallbacks.</param>
    /// <param name="dark">Whether to render in the dark theme, so the picture matches the editor palette.</param>
    internal void RenderAll(FlowDocument document, IMermaidImageRenderer? renderer, bool dark)
    {
        _dark = dark;
        if (renderer is null)
        {
            return;
        }

        foreach (var view in DiagramViews(document))
        {
            var source = view.Source ?? string.Empty;
            if (_cache.TryGetValue((source, dark), out var cached))
            {
                view.Rendered = cached;
            }
            else
            {
                _ = RenderOneAsync(view, source, dark, renderer);
            }
        }
    }

    // Renders one diagram off the UI thread and, back on it, fills the view — unless the view has since
    // been re-projected to a different source, or the theme has since changed (a stale render must not
    // overwrite a newer one). A diagram the renderer cannot produce keeps its fallback.
    private async Task RenderOneAsync(MermaidDiagramView view, string source, bool dark, IMermaidImageRenderer renderer)
    {
        try
        {
            var image = await renderer.RenderAsync(source, dark).ConfigureAwait(true);
            if (image is null)
            {
                return;
            }

            var picture = ToBitmap(image.Png);
            _cache[(source, dark)] = picture;
            if (_dark == dark && string.Equals(view.Source, source, StringComparison.Ordinal))
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
