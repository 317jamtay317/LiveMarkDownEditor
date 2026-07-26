using Markdig.Syntax;
using MigraDoc.DocumentObjectModel;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdColumnAlign = Markdig.Extensions.Tables.TableColumnAlign;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes a Table as a real table on the page: its columns sharing the page width evenly, its
/// header row bold, each column aligned the way the Table says, and every other Body Row carrying
/// Row Banding's shade so a wide row can be followed across (INV-068).
/// </summary>
internal sealed class TableWriter : IBlockWriter
{
    // Row Banding's shade on paper: a light grey, chosen to read on a printed page rather than to
    // match the editor's translucent tint, which would be invisible against white (INV-068).
    private static readonly Color BandingColor = new(245, 245, 247);

    /// <inheritdoc />
    public void Write(Block block, BlockContext context)
    {
        if (block is not MdTable table)
        {
            return;
        }

        var pdfTable = context.Section.AddTable();
        pdfTable.Borders.Width = 0.5;
        pdfTable.Borders.Color = Colors.LightGray;

        var columns = table.ColumnDefinitions.Count > 0 ? table.ColumnDefinitions.Count : MaxCells(table);
        columns = Math.Max(columns, 1);
        for (var c = 0; c < columns; c++)
        {
            pdfTable.AddColumn(Unit.FromCentimeter(PdfStyle.UsableWidthCm / columns));
        }

        // Row Banding (INV-068): a banded Table stays banded wherever it is shown, so the printed page
        // shades the rows the Visual Document shades — every other Body Row, counted below the header.
        var bodyPosition = 0;

        foreach (var rowBlock in table)
        {
            var mdRow = (MdTableRow)rowBlock;
            var row = pdfTable.AddRow();
            if (!mdRow.IsHeader && ++bodyPosition % 2 == 0)
            {
                row.Shading.Color = BandingColor;
            }

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
                    InlineWriter.Write(inline, cellParagraph);
                }
            }
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

    // A Table with no column definitions is as wide as its widest row.
    private static int MaxCells(MdTable table)
    {
        var max = 0;
        foreach (var rowBlock in table)
        {
            max = Math.Max(max, ((MdTableRow)rowBlock).Count);
        }

        return max;
    }
}
