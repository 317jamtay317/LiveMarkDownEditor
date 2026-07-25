using Markdig.Syntax;
using MigraDoc.DocumentObjectModel;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using MdTableCell = Markdig.Extensions.Tables.TableCell;
using MdColumnAlign = Markdig.Extensions.Tables.TableColumnAlign;

namespace Infrastructure.Pdf.Blocks;

/// <summary>
/// Writes a Table as a real table on the page: its columns sharing the page width evenly, its
/// header row bold, and each column aligned the way the Table says.
/// </summary>
internal sealed class TableWriter : IBlockWriter
{
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
