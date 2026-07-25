using Markdig.Syntax;
using MigraDoc.DocumentObjectModel;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Where a block is written: the section it goes into, how far it is indented, and whether it sits
/// inside a Block Quote. A Block Writer that nests other blocks — a List, a Block Quote, a Footnote
/// Definition — writes them through the context it derives, so nesting is expressed by the context
/// rather than threaded through every writer.
/// </summary>
/// <param name="Section">The MigraDoc section the composed document is written into.</param>
/// <param name="Factory">Creates the writer for each block written through this context.</param>
/// <param name="IndentCm">How far, in centimetres, blocks written here are indented.</param>
/// <param name="Quoted">Whether blocks written here are inside a Block Quote.</param>
internal sealed record BlockContext(
    Section Section,
    IBlockFactory Factory,
    double IndentCm = 0,
    bool Quoted = false)
{
    /// <summary>Returns this context indented by one further step of nesting.</summary>
    /// <param name="stepCm">How far to indent, in centimetres. Defaults to one nesting step.</param>
    /// <returns>The context nested blocks are written with.</returns>
    public BlockContext Indented(double stepCm = PdfStyle.IndentStepCm) =>
        this with { IndentCm = IndentCm + stepCm };

    /// <summary>Returns this context marked as being inside a Block Quote.</summary>
    /// <returns>The context a Block Quote's own blocks are written with.</returns>
    public BlockContext InsideBlockQuote() => this with { Quoted = true };

    /// <summary>
    /// Returns this context with any Block Quote styling dropped — for a block distinguished by its
    /// own shape, which the quote rule beside it would say nothing more about.
    /// </summary>
    /// <returns>The context an unquoted block is written with.</returns>
    public BlockContext Unquoted() => this with { Quoted = false };

    /// <summary>Writes a block here, through the writer its kind belongs to.</summary>
    /// <param name="block">The block to write.</param>
    public void Write(Block block) => Factory.Create(block).Write(block, this);

    /// <summary>Writes every block a container holds, in order.</summary>
    /// <param name="container">The container whose blocks are written.</param>
    public void WriteBlocks(ContainerBlock container)
    {
        foreach (var block in container)
        {
            Write(block);
        }
    }

    /// <summary>Starts a new paragraph here, indented and set off as this context describes.</summary>
    /// <returns>The paragraph a Block Writer fills.</returns>
    public Paragraph NewParagraph()
    {
        var paragraph = Section.AddParagraph();
        if (IndentCm > 0)
        {
            paragraph.Format.LeftIndent = Unit.FromCentimeter(IndentCm);
        }

        if (Quoted)
        {
            paragraph.Format.Borders.Left.Width = 2;
            paragraph.Format.Borders.Left.Color = Colors.LightGray;
            paragraph.Format.Borders.DistanceFromLeft = "4pt";
            paragraph.Format.Font.Color = Colors.DimGray;
        }

        return paragraph;
    }

    /// <summary>
    /// Draws a horizontal rule across the page — what a Thematic Break is, and what sets the
    /// Footnote Section off from the prose above it (INV-065).
    /// </summary>
    public void AddRule()
    {
        var paragraph = Section.AddParagraph();
        paragraph.Format.Borders.Bottom.Width = 0.75;
        paragraph.Format.Borders.Bottom.Color = Colors.Gray;
        paragraph.Format.SpaceBefore = "6pt";
        paragraph.Format.SpaceAfter = "6pt";
    }
}
