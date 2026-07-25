using Markdig.Syntax;
using MigraDoc.DocumentObjectModel;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Places a rendered Mermaid Diagram where its Code Block is, so the export shows the picture the
/// Visual Document shows rather than the diagram's source (INV-050).
/// </summary>
/// <remarks>
/// The image is scaled to fit the page width while keeping its aspect ratio; a diagram narrower
/// than the page keeps its natural size. A Mermaid Diagram the renderer could not produce never
/// reaches this writer — it falls back to a <see cref="CodeBlockWriter"/> and prints its source.
/// </remarks>
/// <param name="diagram">The rendered image to place.</param>
internal sealed class MermaidDiagramWriter(PreparedDiagram diagram) : IBlockWriter
{
    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        var image = context.Section.AddImage(diagram.ImagePath);
        image.LockAspectRatio = true;
        var naturalCm = diagram.PixelWidth / 96.0 * 2.54;
        image.Width = Unit.FromCentimeter(
            naturalCm > 0 ? Math.Min(PdfStyle.UsableWidthCm, naturalCm) : PdfStyle.UsableWidthCm);
    }
}
