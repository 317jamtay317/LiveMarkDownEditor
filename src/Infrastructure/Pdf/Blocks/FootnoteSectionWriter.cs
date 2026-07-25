using System.Globalization;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;
using MdFootnote = Markdig.Extensions.Footnotes.Footnote;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes the Footnote Section as the Rendered Output shows it: a rule, then each Footnote
/// Definition beside its Footnote Number, in reference order (INV-065).
/// </summary>
/// <remarks>
/// A Footnote Definition no Footnote Reference cites is not in the section at all — Markdown omits
/// it from the Rendered Output, and so does a printout. Notes are set smaller than the prose that
/// cites them, as they are in print.
/// </remarks>
internal sealed class FootnoteSectionWriter : IBlockWriter
{
    private const double FootnoteFontSize = 9.5;

    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        if (block is not FootnoteGroup footnotes)
        {
            return;
        }

        var notes = footnotes.OfType<MdFootnote>().ToList();
        if (notes.Count == 0)
        {
            return;
        }

        context.AddRule();
        foreach (var note in notes)
        {
            WriteNote(note, note.Order.ToString(CultureInfo.InvariantCulture) + ". ", context.Indented());
        }
    }

    // One Footnote Definition, its Number written before its first block the way a List Item's marker
    // is. The back-reference the parser appended belongs to the Rendered Output's navigation, not to
    // the note, and is dropped with the other inlines it cannot print (INV-065).
    private static void WriteNote(MdFootnote note, string marker, BlockContext context)
    {
        var wroteMarker = false;
        foreach (var child in note)
        {
            if (child is ParagraphBlock paragraph && !wroteMarker)
            {
                var written = context.NewParagraph();
                written.Format.Font.Size = FootnoteFontSize;
                written.AddText(marker);
                InlineWriter.Write(paragraph.Inline, written);
                wroteMarker = true;
                continue;
            }

            context.Write(child);
        }
    }
}
