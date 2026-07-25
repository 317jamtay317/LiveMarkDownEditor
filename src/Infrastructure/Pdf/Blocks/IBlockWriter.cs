using Markdig.Syntax;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes one kind of Markdown block onto the page of an Export as PDF. Because a PDF cannot embed
/// the Visual Document, each construct is re-laid-out from the Markdown (INV-033), and a Block
/// Writer is where one construct's layout lives.
/// </summary>
internal interface IBlockWriter
{
    /// <summary>Writes the block at the place the context describes.</summary>
    /// <param name="block">
    /// The block to write. A writer is handed only the kind of block its <see cref="IBlockFactory"/>
    /// created it for, and writes nothing for any other.
    /// </param>
    /// <param name="context">Where on the page the block is written, and how it is nested.</param>
    void Write(Block block, BlockContext context);
}
