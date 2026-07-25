using Markdig.Extensions.DefinitionLists;
using Markdig.Syntax;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes a Definition List: each Definition Term flush, each Definition Description indented
/// beneath it — the shape the Visual Document and the Rendered Output both show (INV-066).
/// </summary>
/// <remarks>
/// A Definition Term is kept with what follows it, so a Term never strands itself at the foot of a
/// page apart from the Descriptions it introduces.
/// </remarks>
internal sealed class DefinitionListWriter : IBlockWriter
{
    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        if (block is not DefinitionList definitions)
        {
            return;
        }

        foreach (var itemBlock in definitions)
        {
            if (itemBlock is DefinitionItem item)
            {
                WriteItem(item, context);
            }
        }
    }

    private static void WriteItem(DefinitionItem item, BlockContext context)
    {
        foreach (var child in item)
        {
            if (child is DefinitionTerm term)
            {
                var paragraph = context.NewParagraph();
                paragraph.Format.KeepWithNext = true;
                InlineWriter.Write(term.Inline, paragraph);
                continue;
            }

            context.Indented().Write(child);
        }
    }
}
