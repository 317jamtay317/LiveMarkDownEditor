using Markdig.Syntax;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes a paragraph — and any other leaf block whose content is simply the inlines it carries —
/// as one paragraph on the page.
/// </summary>
internal sealed class ParagraphWriter : IBlockWriter
{
    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        if (block is not LeafBlock leaf)
        {
            return;
        }

        InlineWriter.Write(leaf.Inline, context.NewParagraph());
    }
}
