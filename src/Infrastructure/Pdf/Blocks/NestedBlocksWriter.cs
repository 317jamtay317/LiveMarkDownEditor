using Markdig.Syntax;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes a container that is nothing itself but the blocks it holds — each of them written where
/// the container stands, at the same indent.
/// </summary>
internal sealed class NestedBlocksWriter : IBlockWriter
{
    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        if (block is ContainerBlock container)
        {
            context.WriteBlocks(container);
        }
    }
}
