using Markdig.Syntax;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Creates the <see cref="IBlockWriter"/> that writes a given Markdown block. It is the single
/// place that knows which writer a block belongs to, so an Export as PDF gains a construct by
/// gaining a writer rather than by growing a branch wherever blocks are written.
/// </summary>
internal interface IBlockFactory
{
    /// <summary>Creates the Block Writer for the given block.</summary>
    /// <param name="block">The block about to be written.</param>
    /// <returns>
    /// The writer for that block's kind — never <see langword="null"/>. A block a PDF carries
    /// nothing for gets a writer that writes nothing.
    /// </returns>
    IBlockWriter Create(Block block);
}
