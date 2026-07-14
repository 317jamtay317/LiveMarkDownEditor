using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfTable = System.Windows.Documents.Table;

namespace UI.Wysiwyg;

/// <summary>
/// The Table Formatting Actions: Insert Table places a new three-column Table at the caret (only
/// outside a Table — a Table is never nested inside another Table); Add Row and Add Column grow the
/// Table containing the caret, keeping it rectangular (INV-019). Cells are composed exactly as the
/// Projector composes them, so Capture treats a user-built Table like a loaded one (INV-018).
/// </summary>
internal static class TableEditing
{
    /// <summary>Whether <paramref name="position"/> sits inside a Table.</summary>
    /// <param name="position">The position to classify (typically the caret).</param>
    internal static bool IsInTable(TextPointer? position) =>
        VisualDocumentTraversal.AncestorOf<TableCell>(position) is not null;

    /// <summary>
    /// The Insert Table Formatting Action: inserts a new Table — three columns, a header row, and
    /// two empty body rows, unaligned — after the caret's block (or in place of an empty one), and
    /// selects the first header cell for immediate typing. No-op when the caret is inside a Table.
    /// </summary>
    /// <param name="editor">The editor receiving the Table.</param>
    internal static void InsertTable(RichTextBox editor)
    {
        if (IsInTable(editor.CaretPosition))
        {
            return;
        }

        editor.BeginChange();
        try
        {
            var table = CreateTable(columns: 3, bodyRows: 2);
            PlaceTable(editor.Document, table, VisualDocumentTraversal.TopLevelBlockOf(editor.CaretPosition));

            var firstHeaderParagraph = (Paragraph)table.RowGroups[0].Rows[0].Cells[0].Blocks.FirstBlock!;
            editor.Selection.Select(firstHeaderParagraph.ContentStart, firstHeaderParagraph.ContentEnd);
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// The Add Row Formatting Action: inserts a new empty row immediately below the caret's row,
    /// minted at the Table's column count (INV-019), and moves the caret into its first cell.
    /// No-op when the caret is not inside a Table.
    /// </summary>
    /// <param name="editor">The editor whose Table grows.</param>
    internal static void AddRow(RichTextBox editor)
    {
        var caretRow = VisualDocumentTraversal.AncestorOf<TableRow>(editor.CaretPosition);
        if (caretRow?.Parent is not TableRowGroup group)
        {
            return;
        }

        editor.BeginChange();
        try
        {
            var newRow = CreateRow(caretRow.Cells.Count, isHeader: false);
            group.Rows.Insert(group.Rows.IndexOf(caretRow) + 1, newRow);

            var firstCellParagraph = (Paragraph)newRow.Cells[0].Blocks.FirstBlock!;
            editor.Selection.Select(firstCellParagraph.ContentStart, firstCellParagraph.ContentStart);
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// The Add Column Formatting Action: inserts a new empty, unaligned column immediately to the
    /// right of the caret's column, extending the header row and every body row so the Table stays
    /// rectangular (INV-019). No-op when the caret is not inside a Table.
    /// </summary>
    /// <param name="editor">The editor whose Table grows.</param>
    internal static void AddColumn(RichTextBox editor)
    {
        var caretCell = VisualDocumentTraversal.AncestorOf<TableCell>(editor.CaretPosition);
        var caretRow = VisualDocumentTraversal.AncestorOf<TableRow>(editor.CaretPosition);
        var table = VisualDocumentTraversal.AncestorOf<WpfTable>(editor.CaretPosition);
        if (caretCell is null || caretRow is null || table is null)
        {
            return;
        }

        editor.BeginChange();
        try
        {
            var insertAt = caretRow.Cells.IndexOf(caretCell) + 1;
            table.Columns.Insert(Math.Min(insertAt, table.Columns.Count), new TableColumn());

            var headerRow = table.RowGroups.Count > 0 && table.RowGroups[0].Rows.Count > 0
                ? table.RowGroups[0].Rows[0]
                : null;
            foreach (var row in table.RowGroups.SelectMany(rowGroup => rowGroup.Rows))
            {
                var cell = CreateCell(ReferenceEquals(row, headerRow), string.Empty);
                row.Cells.Insert(Math.Min(insertAt, row.Cells.Count), cell);
            }

            // One alignment per column (INV-019): the new column slots in unaligned.
            var alignments = ((table.Tag as TableRole)?.Alignments ?? []).ToList();
            alignments.Insert(Math.Min(insertAt, alignments.Count), ColumnAlignment.None);
            table.Tag = new TableRole(alignments);
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// Dresses a cell paragraph as a Table cell the way the Projector renders one — header cells
    /// bold (a header convention Capture suppresses), thin borders, comfortable padding.
    /// </summary>
    /// <param name="paragraph">The paragraph carrying the cell's content.</param>
    /// <param name="isHeader">Whether the cell belongs to the header row.</param>
    /// <returns>The composed cell.</returns>
    internal static TableCell WrapCell(Paragraph paragraph, bool isHeader)
    {
        if (isHeader)
        {
            paragraph.FontWeight = FontWeights.Bold;
        }

        var cell = new TableCell(paragraph)
        {
            BorderThickness = new Thickness(0.5),
            Padding = new Thickness(6, 3, 6, 3),
        };
        cell.SetResourceReference(Block.BorderBrushProperty, "BorderBrush");
        return cell;
    }

    // A fresh, rectangular Table (INV-019): per-column None alignments, placeholder header texts, and
    // empty body rows, composed exactly as the Projector composes a loaded Table.
    private static WpfTable CreateTable(int columns, int bodyRows)
    {
        var table = new WpfTable
        {
            Tag = new TableRole([.. Enumerable.Repeat(ColumnAlignment.None, columns)]),
            CellSpacing = 0,
            Margin = new Thickness(0, 0, 0, 6),
        };
        for (var i = 0; i < columns; i++)
        {
            table.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        group.Rows.Add(CreateRow(columns, isHeader: true));
        for (var i = 0; i < bodyRows; i++)
        {
            group.Rows.Add(CreateRow(columns, isHeader: false));
        }

        table.RowGroups.Add(group);
        return table;
    }

    private static TableRow CreateRow(int columns, bool isHeader)
    {
        var row = new TableRow();
        for (var i = 0; i < columns; i++)
        {
            row.Cells.Add(CreateCell(isHeader, isHeader ? $"Column {i + 1}" : string.Empty));
        }

        return row;
    }

    private static TableCell CreateCell(bool isHeader, string text) =>
        WrapCell(new Paragraph(new Run(text)) { Margin = new Thickness(0) }, isHeader);

    // Inserts the Table relative to the caret's block: in place of an empty paragraph (which stays,
    // as the line below the Table), after a non-empty block, or at the end of a block-less document.
    // A paragraph always follows the Table so the caret can reach the line below it.
    private static void PlaceTable(FlowDocument document, WpfTable table, Block? anchor)
    {
        if (anchor is null)
        {
            document.Blocks.Add(table);
        }
        else if (anchor is Paragraph paragraph
                 && new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text.Length == 0)
        {
            document.Blocks.InsertBefore(anchor, table);
        }
        else
        {
            document.Blocks.InsertAfter(anchor, table);
        }

        if (document.Blocks.LastBlock == table)
        {
            document.Blocks.Add(new Paragraph { Margin = new Thickness(0, 0, 0, 6) });
        }
    }
}
