using System.Windows.Documents;

namespace UI.Wysiwyg;

/// <summary>
/// A Code Region of a Visual Document: a span that receives Code Shading, either a whole Code Block
/// or one inline Code Span. It is presentation-only — it names a range to shade and carries nothing
/// that feeds back into Capture (INV-017).
/// </summary>
/// <param name="Start">The start of the region in the Visual Document.</param>
/// <param name="End">The end of the region in the Visual Document.</param>
/// <param name="IsBlock">
/// <see langword="true"/> for a Code Block (shaded as a full-width panel); <see langword="false"/>
/// for an inline Code Span (shaded as a snug box hugging the text).
/// </param>
public sealed record CodeRegion(TextPointer Start, TextPointer End, bool IsBlock);

/// <summary>
/// Finds the Code Regions of a Visual Document — one per Code Block and one per inline Code Span — so
/// the <c>CodeShadingAdorner</c> knows what to shade. Pure and view-only: it reads the document and
/// returns ranges, never changing the Markdown Document (INV-017).
/// </summary>
public static class CodeShadingScanner
{
    /// <summary>Scans <paramref name="document"/> for its Code Regions, in document order.</summary>
    /// <param name="document">The Visual Document to scan. <see langword="null"/> yields no regions.</param>
    /// <returns>The Code Blocks and Code Spans to shade, in document order.</returns>
    public static IReadOnlyList<CodeRegion> Scan(FlowDocument? document)
    {
        var regions = new List<CodeRegion>();
        if (document is not null)
        {
            CollectBlocks(document.Blocks, regions);
        }

        return regions;
    }

    // Walks the block containers the projector produces — sections (block quotes), lists, and tables —
    // collecting a block Region for each Code Block and descending into ordinary paragraphs for their
    // inline Code Spans. A Code Block's own inline Runs are *not* descended into: the block is one
    // Region, not one per line.
    private static void CollectBlocks(IEnumerable<Block> blocks, List<CodeRegion> regions)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case Paragraph paragraph when paragraph.Tag is CodeBlockRole:
                    regions.Add(new CodeRegion(paragraph.ContentStart, paragraph.ContentEnd, IsBlock: true));
                    break;

                case Paragraph paragraph:
                    CollectInlines(paragraph.Inlines, regions);
                    break;

                case Section section:
                    CollectBlocks(section.Blocks, regions);
                    break;

                case List list:
                    foreach (var item in list.ListItems)
                    {
                        CollectBlocks(item.Blocks, regions);
                    }

                    break;

                case Table table:
                    foreach (var cell in table.RowGroups
                        .SelectMany(group => group.Rows)
                        .SelectMany(row => row.Cells))
                    {
                        CollectBlocks(cell.Blocks, regions);
                    }

                    break;
            }
        }
    }

    // Collects a span Region for each inline Code Span, descending into inline containers (bold,
    // italic, hyperlinks, …) so a Code Span nested inside emphasis is still found.
    private static void CollectInlines(IEnumerable<Inline> inlines, List<CodeRegion> regions)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run when run.Tag is InlineSemantic.Code:
                    regions.Add(new CodeRegion(run.ContentStart, run.ContentEnd, IsBlock: false));
                    break;

                case Span span:
                    CollectInlines(span.Inlines, regions);
                    break;
            }
        }
    }
}
