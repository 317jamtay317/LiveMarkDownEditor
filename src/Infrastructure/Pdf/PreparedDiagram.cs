namespace Infrastructure.Pdf;

/// <summary>
/// A Mermaid Diagram rendered to a temporary image file, ready for the <see cref="MarkdownPdfComposer"/>
/// to place in a PDF (INV-050). The exporter renders each diagram, writes its PNG to a temp file, and
/// passes these to the composer; it deletes the files once the PDF has been rendered.
/// </summary>
/// <param name="ImagePath">The path of the temporary PNG file.</param>
/// <param name="PixelWidth">The image width in pixels, used to size it on the page.</param>
/// <param name="PixelHeight">The image height in pixels.</param>
internal sealed record PreparedDiagram(string ImagePath, int PixelWidth, int PixelHeight);
