namespace Domain;

/// <summary>
/// A Mermaid Diagram rendered to a raster image, for embedding in an Export as PDF (INV-050). It
/// carries the PNG-encoded bytes and the image's pixel dimensions, so the exporter can lay the
/// picture out at a sensible size in the diagram's place.
/// </summary>
/// <param name="Png">The PNG-encoded image bytes.</param>
/// <param name="PixelWidth">The rendered image's width in pixels.</param>
/// <param name="PixelHeight">The rendered image's height in pixels.</param>
public sealed record DiagramImage(byte[] Png, int PixelWidth, int PixelHeight);
