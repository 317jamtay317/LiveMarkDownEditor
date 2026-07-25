using Markdig.Syntax;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes nothing — the writer for a block a PDF carries no equivalent of, such as raw HTML or a
/// link reference definition, which is content the Rendered Output resolves rather than prints.
/// </summary>
/// <remarks>
/// Having a writer for these keeps <see cref="IBlockFactory.Create"/> total: every block has a
/// writer, so no caller has to ask whether one exists.
/// </remarks>
internal sealed class SkippedBlockWriter : IBlockWriter
{
    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        // A block with nothing to say on paper says nothing.
    }
}
