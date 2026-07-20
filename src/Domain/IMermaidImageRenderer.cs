namespace Domain;

/// <summary>
/// Port for rendering a Mermaid Diagram's source to a raster image, so an Export as PDF can embed the
/// diagram as a picture where its Code Block was (INV-050). The Domain owns this contract; an adapter
/// realises it — the WebView2-backed adapter runs Mermaid.js — so the Domain stays free of the
/// browser.
/// </summary>
/// <remarks>
/// A diagram the renderer cannot produce — an unavailable renderer, or source Mermaid rejects —
/// yields <see langword="null"/> rather than throwing, so the exporter falls back to the diagram's
/// source text as an ordinary Code Block (the Image fallback of INV-031, reached from export).
/// </remarks>
public interface IMermaidImageRenderer
{
    /// <summary>Renders the given Mermaid Diagram source to a PNG image.</summary>
    /// <param name="source">The Mermaid Diagram source to render.</param>
    /// <returns>The rendered image, or <see langword="null"/> when it cannot be produced.</returns>
    Task<DiagramImage?> RenderAsync(string source);
}
