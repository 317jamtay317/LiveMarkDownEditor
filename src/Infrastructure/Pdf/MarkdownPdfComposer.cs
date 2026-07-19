using Infrastructure.Markdown;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MigraDoc.DocumentObjectModel;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdColumnAlign = Markdig.Extensions.Tables.TableColumnAlign;

namespace Infrastructure.Pdf;

/// <summary>
/// Re-lays-out a parsed Markdown syntax tree into a MigraDoc <see cref="Document"/>. Because a PDF
/// cannot embed the Visual Document, an Export as PDF is composed afresh from the Markdown (INV-033);
/// this is where each Markdown construct is mapped to its paged equivalent.
/// </summary>
/// <remarks>
/// Only the font families the built-in Windows resolver maps are used — <see cref="BodyFont"/> for
/// prose and headings, <see cref="CodeFont"/> for code — so rendering never fails to resolve a font.
/// Strikethrough has no MigraDoc equivalent, so struck text keeps its content without the rule.
/// </remarks>
internal sealed class MarkdownPdfComposer
{
    private const string BodyFont = "Arial";
    private const string CodeFont = "Courier New";
    private const double UsableWidthCm = 16.0;
    private const double IndentStepCm = 0.6;

    private static readonly double[] HeadingSizes = [20, 17, 15, 13, 12, 11];

    private readonly Document _document = new();
    private readonly Section _section;
    private readonly IReadOnlyDictionary<string, PreparedDiagram> _diagrams;

    /// <summary>Creates a composer with the document styles the exported PDF uses.</summary>
    /// <param name="diagrams">
    /// The rendered Mermaid Diagram images, keyed by the diagram's source, to place where each
    /// diagram's Code Block is (INV-050). A `mermaid` Code Block with no entry here falls back to its
    /// source text as an ordinary Code Block. Defaults to none.
    /// </param>
    public MarkdownPdfComposer(IReadOnlyDictionary<string, PreparedDiagram>? diagrams = null)
    {
        _diagrams = diagrams ?? new Dictionary<string, PreparedDiagram>();

        // "Normal" is a MigraDoc built-in style; it is always present.
        var normal = _document.Styles["Normal"]!;
        normal.Font.Name = BodyFont;
        normal.Font.Size = 10.5;
        normal.ParagraphFormat.SpaceAfter = "6pt";

        _section = _document.AddSection();
    }

    /// <summary>Composes the given Markdown syntax tree into a paged MigraDoc document.</summary>
    /// <param name="ast">The parsed Markdown document.</param>
    /// <returns>The MigraDoc document ready to render.</returns>
    public Document Compose(MarkdownDocument ast)
    {
        WriteBlocks(ast, indentCm: 0, quoted: false);

        // An empty section renders nothing; MigraDoc needs a paragraph to produce a (blank) page.
        if (_section.Elements.Count == 0)
        {
            _section.AddParagraph();
        }

        return _document;
    }

    private void WriteBlocks(ContainerBlock container, double indentCm, bool quoted)
    {
        foreach (var block in container)
        {
            WriteBlock(block, indentCm, quoted);
        }
    }

    private void WriteBlock(Block block, double indentCm, bool quoted)
    {
        switch (block)
        {
            case HeadingBlock heading:
                WriteHeading(heading, indentCm);
                break;
            case ParagraphBlock paragraph:
                WriteInlineParagraph(paragraph.Inline, indentCm, quoted);
                break;
            case MdTable table:
                WriteTable(table);
                break;
            case ListBlock list:
                WriteList(list, indentCm, quoted);
                break;
            case QuoteBlock quote:
                WriteBlocks(quote, indentCm + IndentStepCm, quoted: true);
                break;
            case CodeBlock code:
                WriteCodeBlock(code, indentCm);
                break;
            case ThematicBreakBlock:
                WriteThematicBreak();
                break;
            case ContainerBlock nested:
                WriteBlocks(nested, indentCm, quoted);
                break;
            case LeafBlock leaf when leaf.Inline is not null:
                WriteInlineParagraph(leaf.Inline, indentCm, quoted);
                break;
        }
    }

    private void WriteHeading(HeadingBlock heading, double indentCm)
    {
        var paragraph = NewParagraph(indentCm, quoted: false);
        var level = Math.Clamp(heading.Level, 1, 6);
        paragraph.Format.Font.Size = HeadingSizes[level - 1];
        paragraph.Format.Font.Bold = true;
        paragraph.Format.SpaceBefore = "10pt";
        paragraph.Format.SpaceAfter = "4pt";
        paragraph.Format.KeepWithNext = true;
        WriteInlines(heading.Inline, paragraph, default);
    }

    private void WriteInlineParagraph(ContainerInline? inlines, double indentCm, bool quoted)
    {
        var paragraph = NewParagraph(indentCm, quoted);
        WriteInlines(inlines, paragraph, default);
    }

    private void WriteList(ListBlock list, double indentCm, bool quoted)
    {
        var number = ParseStart(list.OrderedStart);
        foreach (var itemBlock in list)
        {
            var item = (ListItemBlock)itemBlock;
            var task = FindTask(item);
            var marker = task is not null
                ? (task.Checked ? "[x] " : "[ ] ")
                : list.IsOrdered ? $"{number}. " : "• ";

            WriteListItem(item, marker, indentCm, quoted);
            number++;
        }
    }

    private void WriteListItem(ListItemBlock item, string marker, double indentCm, bool quoted)
    {
        var wroteMarker = false;
        foreach (var child in item)
        {
            if (child is ParagraphBlock paragraph && !wroteMarker)
            {
                var p = NewParagraph(indentCm + IndentStepCm, quoted);
                p.AddText(marker);
                WriteInlines(paragraph.Inline, p, default);
                wroteMarker = true;
            }
            else
            {
                WriteBlock(child, indentCm + IndentStepCm, quoted);
            }
        }
    }

    private void WriteCodeBlock(CodeBlock code, double indentCm)
    {
        // A Mermaid Diagram we have a rendered image for shows the picture, not its source text
        // (INV-050); a `mermaid` block with no image falls through and writes the code as usual.
        if (code is FencedCodeBlock fenced && MermaidBlocks.IsMermaid(fenced.Info)
            && _diagrams.TryGetValue(MermaidBlocks.SourceOf(code), out var diagram))
        {
            WriteDiagramImage(diagram);
            return;
        }

        var paragraph = NewParagraph(indentCm + 0.2, quoted: false);
        paragraph.Format.Font.Name = CodeFont;
        paragraph.Format.Font.Size = 9.5;
        paragraph.Format.Shading.Color = Colors.WhiteSmoke;
        paragraph.Format.SpaceBefore = "4pt";
        paragraph.Format.SpaceAfter = "4pt";

        for (var i = 0; i < code.Lines.Count; i++)
        {
            if (i > 0)
            {
                paragraph.AddLineBreak();
            }

            paragraph.AddText(code.Lines.Lines[i].Slice.ToString());
        }
    }

    // Places a rendered Mermaid Diagram image, scaled to fit the page width while keeping its aspect
    // ratio. A diagram narrower than the usable width keeps its natural size (INV-050).
    private void WriteDiagramImage(PreparedDiagram diagram)
    {
        var image = _section.AddImage(diagram.ImagePath);
        image.LockAspectRatio = true;
        var naturalCm = diagram.PixelWidth / 96.0 * 2.54;
        image.Width = Unit.FromCentimeter(naturalCm > 0 ? Math.Min(UsableWidthCm, naturalCm) : UsableWidthCm);
    }

    private void WriteThematicBreak()
    {
        var paragraph = _section.AddParagraph();
        paragraph.Format.Borders.Bottom.Width = 0.75;
        paragraph.Format.Borders.Bottom.Color = Colors.Gray;
        paragraph.Format.SpaceBefore = "6pt";
        paragraph.Format.SpaceAfter = "6pt";
    }

    private void WriteTable(MdTable table)
    {
        var pdfTable = _section.AddTable();
        pdfTable.Borders.Width = 0.5;
        pdfTable.Borders.Color = Colors.LightGray;

        var columns = table.ColumnDefinitions.Count > 0 ? table.ColumnDefinitions.Count : MaxCells(table);
        columns = Math.Max(columns, 1);
        for (var c = 0; c < columns; c++)
        {
            pdfTable.AddColumn(Unit.FromCentimeter(UsableWidthCm / columns));
        }

        foreach (var rowBlock in table)
        {
            var mdRow = (MdTableRow)rowBlock;
            var row = pdfTable.AddRow();
            for (var c = 0; c < mdRow.Count && c < columns; c++)
            {
                var cellParagraph = row.Cells[c].AddParagraph();
                cellParagraph.Format.Alignment = AlignmentFor(table, c);
                if (mdRow.IsHeader)
                {
                    cellParagraph.Format.Font.Bold = true;
                }

                if (mdRow[c] is MdTableCell { Count: > 0 } cell && cell[0] is LeafBlock { Inline: { } inline })
                {
                    WriteInlines(inline, cellParagraph, default);
                }
            }
        }
    }

    private Paragraph NewParagraph(double indentCm, bool quoted)
    {
        var paragraph = _section.AddParagraph();
        if (indentCm > 0)
        {
            paragraph.Format.LeftIndent = Unit.FromCentimeter(indentCm);
        }

        if (quoted)
        {
            paragraph.Format.Borders.Left.Width = 2;
            paragraph.Format.Borders.Left.Color = Colors.LightGray;
            paragraph.Format.Borders.DistanceFromLeft = "4pt";
            paragraph.Format.Font.Color = Colors.DimGray;
        }

        return paragraph;
    }

    private static void WriteInlines(ContainerInline? inlines, Paragraph paragraph, InlineStyle style)
    {
        if (inlines is null)
        {
            return;
        }

        foreach (var inline in inlines)
        {
            WriteInline(inline, paragraph, style);
        }
    }

    private static void WriteInline(Inline inline, Paragraph paragraph, InlineStyle style)
    {
        switch (inline)
        {
            case LiteralInline literal:
                AddRun(paragraph, literal.Content.ToString(), style);
                break;
            case EmphasisInline emphasis:
                WriteInlines(emphasis, paragraph, style.WithEmphasis(emphasis));
                break;
            case CodeInline code:
                AddRun(paragraph, code.Content, style with { Code = true });
                break;
            case LinkInline { IsImage: true } image:
                WriteImage(image, paragraph, style);
                break;
            case LinkInline link:
                WriteInlines(link, paragraph, style with { Link = true });
                break;
            case AutolinkInline autolink:
                AddRun(paragraph, autolink.Url, style with { Link = true });
                break;
            case LineBreakInline lineBreak:
                if (lineBreak.IsHard)
                {
                    paragraph.AddLineBreak();
                }
                else
                {
                    AddRun(paragraph, " ", style);
                }

                break;
            case HtmlEntityInline entity:
                AddRun(paragraph, entity.Transcoded.ToString(), style);
                break;
            case TaskList:
            case HtmlInline:
                break;
            case ContainerInline container:
                WriteInlines(container, paragraph, style);
                break;
        }
    }

    private static void WriteImage(LinkInline image, Paragraph paragraph, InlineStyle style)
    {
        // No Base Directory is available here, so the alt text (the image's child inlines) is shown
        // rather than the picture — the correct fallback for an image that cannot be embedded (INV-031).
        if (image.FirstChild is not null)
        {
            WriteInlines(image, paragraph, style);
        }
        else
        {
            AddRun(paragraph, image.Url ?? string.Empty, style);
        }
    }

    private static void AddRun(Paragraph paragraph, string text, InlineStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var run = paragraph.AddFormattedText(text);
        run.Bold = style.Bold;
        run.Italic = style.Italic;
        if (style.Code)
        {
            run.Font.Name = CodeFont;
        }

        if (style.Link)
        {
            run.Font.Color = Colors.RoyalBlue;
            run.Font.Underline = Underline.Single;
        }
    }

    private static ParagraphAlignment AlignmentFor(MdTable table, int column)
    {
        if (column >= table.ColumnDefinitions.Count)
        {
            return ParagraphAlignment.Left;
        }

        return table.ColumnDefinitions[column].Alignment switch
        {
            MdColumnAlign.Center => ParagraphAlignment.Center,
            MdColumnAlign.Right => ParagraphAlignment.Right,
            _ => ParagraphAlignment.Left,
        };
    }

    private static int MaxCells(MdTable table)
    {
        var max = 0;
        foreach (var rowBlock in table)
        {
            max = Math.Max(max, ((MdTableRow)rowBlock).Count);
        }

        return max;
    }

    private static TaskList? FindTask(ListItemBlock item) =>
        item.Count > 0 && item[0] is ParagraphBlock { Inline.FirstChild: TaskList task } ? task : null;

    private static int ParseStart(string? orderedStart) =>
        int.TryParse(orderedStart, out var start) ? start : 1;

    /// <summary>The accumulated inline formatting applied to a run of text.</summary>
    private readonly record struct InlineStyle(bool Bold, bool Italic, bool Code, bool Link)
    {
        /// <summary>Returns this style with the formatting the given emphasis run adds.</summary>
        public InlineStyle WithEmphasis(EmphasisInline emphasis) => emphasis switch
        {
            // Strikethrough (~~) has no MigraDoc equivalent; its text is kept without the rule.
            { DelimiterChar: '~' } => this,
            { DelimiterCount: >= 2 } => this with { Bold = true },
            _ => this with { Italic = true },
        };
    }
}
