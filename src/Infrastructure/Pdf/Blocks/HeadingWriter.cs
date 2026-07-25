using Markdig.Syntax;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes a Heading: its text at the size its Heading Level is shown at, kept with the block that
/// follows so a Heading never strands itself at the foot of a page.
/// </summary>
/// <remarks>
/// A Heading is distinguished by size, as it is in the Visual Document. Inside a Block Quote it is
/// written unquoted — its own shape already sets it apart.
/// </remarks>
internal sealed class HeadingWriter : IBlockWriter
{
    private static readonly double[] HeadingSizes = [20, 17, 15, 13, 12, 11];

    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        if (block is not HeadingBlock heading)
        {
            return;
        }

        var paragraph = context.Unquoted().NewParagraph();
        var level = Math.Clamp(heading.Level, 1, 6);
        paragraph.Format.Font.Size = HeadingSizes[level - 1];
        paragraph.Format.Font.Bold = true;
        paragraph.Format.SpaceBefore = "10pt";
        paragraph.Format.SpaceAfter = "4pt";
        paragraph.Format.KeepWithNext = true;
        InlineWriter.Write(heading.Inline, paragraph);
    }
}
