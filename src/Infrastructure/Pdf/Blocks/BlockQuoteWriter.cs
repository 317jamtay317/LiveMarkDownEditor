using Markdig.Syntax;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes a Block Quote: its blocks indented one step and set off with a left rule and muted text,
/// as the Visual Document shows them.
/// </summary>
internal sealed class BlockQuoteWriter : IBlockWriter
{
    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        if (block is QuoteBlock quote)
        {
            context.Indented().InsideBlockQuote().WriteBlocks(quote);
        }
    }
}
