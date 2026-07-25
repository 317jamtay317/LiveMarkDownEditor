using Markdig.Syntax;

namespace Infrastructure.Pdf.Blocks;

/// <summary>Writes a Thematic Break as the horizontal rule it is.</summary>
internal sealed class ThematicBreakWriter : IBlockWriter
{
    /// <inheritdoc />
    public void Write(Block block, BlockContext context) => context.AddRule();
}
