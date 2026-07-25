using Infrastructure.Pdf.Blocks;
using Markdig.Syntax;
using MigraDoc.DocumentObjectModel;

namespace Infrastructure.Pdf;

/// <summary>
/// Re-lays-out a parsed Markdown syntax tree into a MigraDoc <see cref="Document"/>. Because a PDF
/// cannot embed the Visual Document, an Export as PDF is composed afresh from the Markdown (INV-033);
/// the composer sets the document's styles and hands each block to the Block Writer its kind belongs
/// to, so where a construct lands on the page is that writer's business alone.
/// </summary>
/// <remarks>
/// Only the font families the built-in Windows resolver maps are used — see <see cref="PdfStyle"/> —
/// so rendering never fails to resolve a font.
/// </remarks>
internal sealed class MarkdownPdfComposer
{
    private readonly Document _document = new();
    private readonly Section _section;
    private readonly IBlockFactory _blocks;

    /// <summary>Creates a composer with the document styles the exported PDF uses.</summary>
    /// <param name="diagrams">
    /// The rendered Mermaid Diagram images, keyed by the diagram's source, to place where each
    /// diagram's Code Block is (INV-050). A `mermaid` Code Block with no entry here falls back to its
    /// source text as an ordinary Code Block. Defaults to none.
    /// </param>
    /// <param name="syntaxHighlighter">
    /// Tokenizes each Code Block's code so the PDF carries the same Syntax Highlighting the Visual
    /// Document shows (INV-064). <see langword="null"/> writes every Code Block uncolored.
    /// </param>
    public MarkdownPdfComposer(
        IReadOnlyDictionary<string, PreparedDiagram>? diagrams = null,
        Domain.ISyntaxHighlighter? syntaxHighlighter = null)
    {
        _blocks = new BlockFactory(diagrams, syntaxHighlighter);

        // "Normal" is a MigraDoc built-in style; it is always present.
        var normal = _document.Styles["Normal"]!;
        normal.Font.Name = PdfStyle.BodyFont;
        normal.Font.Size = 10.5;
        normal.ParagraphFormat.SpaceAfter = "6pt";

        _section = _document.AddSection();
    }

    /// <summary>Composes the given Markdown syntax tree into a paged MigraDoc document.</summary>
    /// <param name="ast">The parsed Markdown document.</param>
    /// <returns>The MigraDoc document ready to render.</returns>
    public Document Compose(MarkdownDocument ast)
    {
        new BlockContext(_section, _blocks).WriteBlocks(ast);

        // An empty section renders nothing; MigraDoc needs a paragraph to produce a (blank) page.
        if (_section.Elements.Count == 0)
        {
            _section.AddParagraph();
        }

        return _document;
    }
}
