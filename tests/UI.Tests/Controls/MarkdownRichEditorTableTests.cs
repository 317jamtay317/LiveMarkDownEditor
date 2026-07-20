using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Table Formatting Actions on the <see cref="MarkdownRichEditor"/>: Insert Table
/// places a new three-column Table at the caret (only outside a Table), Add Row and Add Column grow
/// the Table containing the caret (only inside one), and Remove Row and Remove Column shrink it —
/// stopping short of removing its header row or its last column, which would leave source that no
/// longer parses as a Table. Every operation keeps the Table rectangular (INV-019) and Captures to
/// canonical Markdown (INV-018).
/// </summary>
public sealed class MarkdownRichEditorTableTests
{
    private const string TwoColumnTable =
        "| A | B |\n| --- | --- |\n| a1 | b1 |\n| a2 | b2 |";

    [Fact]
    public void InsertTable_AtCaret_InsertsThreeColumnTableWithHeaderAndTwoBodyRows_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "before text\n\nafter text" };
            VisualDocumentText.PlaceCaretIn(editor, "before text");

            MarkdownEditingCommands.InsertTable.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe(
                "before text\n\n"
                + "| Column 1 | Column 2 | Column 3 |\n"
                + "| --- | --- | --- |\n"
                + "|  |  |  |\n"
                + "|  |  |  |\n\n"
                + "after text");
        });
    }

    [Fact]
    public void InsertTable_ResultRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "before text\n\nafter text" };
            VisualDocumentText.PlaceCaretIn(editor, "before text");
            MarkdownEditingCommands.InsertTable.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            // INV-018: a fresh Project of the captured source Captures back to the identical text.
            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }

    [Fact]
    public void InsertTable_CannotExecute_WhenCaretIsInsideTable()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.InsertTable.CanExecute(parameter: null, target: editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void InsertTable_CanExecute_WhenCaretIsOutsideTable()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "just prose" };
            VisualDocumentText.PlaceCaretIn(editor, "prose");

            MarkdownEditingCommands.InsertTable.CanExecute(parameter: null, target: editor).ShouldBeTrue();
        });
    }

    [Fact]
    public void AddRow_InsertsEmptyRowBelowTheCaretRow_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.AddTableRow.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe(
                "| A | B |\n| --- | --- |\n| a1 | b1 |\n|  |  |\n| a2 | b2 |");
        });
    }

    [Fact]
    public void AddRow_WithCaretInHeaderRow_InsertsFirstBodyRow_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "A");

            MarkdownEditingCommands.AddTableRow.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe(
                "| A | B |\n| --- | --- |\n|  |  |\n| a1 | b1 |\n| a2 | b2 |");
        });
    }

    [Fact]
    public void AddRow_MatchesColumnCount_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.AddTableRow.Execute(parameter: null, target: editor);

            // INV-019: every captured row still has exactly one cell per column.
            var lines = editor.Markdown.Split('\n');
            lines.ShouldAllBe(line => line.Count(c => c == '|') == 3);
        });
    }

    [Fact]
    public void AddRow_CannotExecute_WhenCaretIsOutsideTable()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "just prose" };
            VisualDocumentText.PlaceCaretIn(editor, "prose");

            MarkdownEditingCommands.AddTableRow.CanExecute(parameter: null, target: editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void AddColumn_InsertsEmptyColumnRightOfTheCaretColumn_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "| A | B |\n| --- | --- |\n| a1 | b1 |" };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.AddTableColumn.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("| A |  | B |\n| --- | --- | --- |\n| a1 |  | b1 |");
        });
    }

    [Fact]
    public void AddColumn_PreservesColumnAlignments_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "| A | B |\n| :--- | ---: |\n| a1 | b1 |" };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.AddTableColumn.Execute(parameter: null, target: editor);

            // The new, unaligned column slots between the existing columns without disturbing their
            // declared alignments (INV-019: one alignment per column).
            editor.Markdown.ShouldBe("| A |  | B |\n| :--- | --- | ---: |\n| a1 |  | b1 |");
        });
    }

    [Fact]
    public void AddColumn_ExtendsEveryRow_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "b2");

            MarkdownEditingCommands.AddTableColumn.Execute(parameter: null, target: editor);

            // INV-019: adding a column extends the header row, the delimiter row, and every body row.
            var lines = editor.Markdown.Split('\n');
            lines.ShouldAllBe(line => line.Count(c => c == '|') == 4);
        });
    }

    [Fact]
    public void AddColumn_CannotExecute_WhenCaretIsOutsideTable()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "just prose" };
            VisualDocumentText.PlaceCaretIn(editor, "prose");

            MarkdownEditingCommands.AddTableColumn.CanExecute(parameter: null, target: editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void AddColumn_ResultRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "a1");
            MarkdownEditingCommands.AddTableColumn.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }

    [Fact]
    public void RemoveRow_DeletesTheCaretRow_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.RemoveTableRow.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("| A | B |\n| --- | --- |\n| a2 | b2 |");
        });
    }

    [Fact]
    public void RemoveRow_LeavesEveryRowRectangular_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "b2");

            MarkdownEditingCommands.RemoveTableRow.Execute(parameter: null, target: editor);

            var lines = editor.Markdown.Split('\n');
            lines.ShouldAllBe(line => line.Count(c => c == '|') == 3);
        });
    }

    [Fact]
    public void RemoveRow_CannotExecute_WhenCaretIsInTheHeaderRow_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "A");

            // A pipe table is nothing without its header row (INV-019).
            MarkdownEditingCommands.RemoveTableRow
                .CanExecute(parameter: null, target: editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void RemoveRow_CanExecute_WhenCaretIsInABodyRow_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.RemoveTableRow
                .CanExecute(parameter: null, target: editor).ShouldBeTrue();
        });
    }

    [Fact]
    public void RemoveRow_CannotExecute_WhenCaretIsOutsideTable()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "just prose" };
            VisualDocumentText.PlaceCaretIn(editor, "prose");

            MarkdownEditingCommands.RemoveTableRow
                .CanExecute(parameter: null, target: editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void RemoveRow_OfTheLastBodyRow_LeavesTheHeaderAlone_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "| A | B |\n| --- | --- |\n| a1 | b1 |" };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.RemoveTableRow.Execute(parameter: null, target: editor);

            // A header-only Table is still a valid pipe table.
            editor.Markdown.ShouldBe("| A | B |\n| --- | --- |");
        });
    }

    [Fact]
    public void RemoveColumn_DeletesTheCaretColumn_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.RemoveTableColumn.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("| B |\n| --- |\n| b1 |\n| b2 |");
        });
    }

    [Fact]
    public void RemoveColumn_ShrinksEveryRow_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "b2");

            MarkdownEditingCommands.RemoveTableColumn.Execute(parameter: null, target: editor);

            // INV-019: removing a column drops its cell from the header, delimiter, and every body row.
            var lines = editor.Markdown.Split('\n');
            lines.ShouldAllBe(line => line.Count(c => c == '|') == 2);
        });
    }

    [Fact]
    public void RemoveColumn_PreservesRemainingColumnAlignments_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "| A | B | C |\n| :--- | --- | ---: |\n| a1 | b1 | c1 |",
            };
            VisualDocumentText.PlaceCaretIn(editor, "b1");

            MarkdownEditingCommands.RemoveTableColumn.Execute(parameter: null, target: editor);

            // The removed column takes its own alignment with it and disturbs no other (INV-019).
            editor.Markdown.ShouldBe("| A | C |\n| :--- | ---: |\n| a1 | c1 |");
        });
    }

    [Fact]
    public void RemoveColumn_CannotExecute_WhenTableHasOneColumn_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "| A |\n| --- |\n| a1 |" };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            // A Table with no columns is not a Table (INV-019).
            MarkdownEditingCommands.RemoveTableColumn
                .CanExecute(parameter: null, target: editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void RemoveColumn_CanExecute_WhenTableHasMoreThanOneColumn_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.RemoveTableColumn
                .CanExecute(parameter: null, target: editor).ShouldBeTrue();
        });
    }

    [Fact]
    public void RemoveColumn_CannotExecute_WhenCaretIsOutsideTable()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "just prose" };
            VisualDocumentText.PlaceCaretIn(editor, "prose");

            MarkdownEditingCommands.RemoveTableColumn
                .CanExecute(parameter: null, target: editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void RemoveRow_FromTheHeaderRow_LeavesTheDocumentUnchanged_INV019()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "A");

            // Execute bypasses CanExecute, so the action must refuse on its own terms too.
            MarkdownEditingCommands.RemoveTableRow.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe(TwoColumnTable);
        });
    }

    [Fact]
    public void RemoveColumn_OnASingleColumnTable_LeavesTheDocumentUnchanged_INV019()
    {
        StaThread.Run(() =>
        {
            const string oneColumn = "| A |\n| --- |\n| a1 |";
            var editor = new MarkdownRichEditor { Markdown = oneColumn };
            VisualDocumentText.PlaceCaretIn(editor, "a1");

            MarkdownEditingCommands.RemoveTableColumn.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe(oneColumn);
        });
    }

    [Fact]
    public void RemoveRow_ResultRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "a1");
            MarkdownEditingCommands.RemoveTableRow.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }

    [Fact]
    public void RemoveColumn_ResultRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = TwoColumnTable };
            VisualDocumentText.PlaceCaretIn(editor, "a1");
            MarkdownEditingCommands.RemoveTableColumn.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }

    [Fact]
    public void InsertTable_AtEndOfDocument_LeavesALineBelow_INV055()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "before text" };
            VisualDocumentText.PlaceCaretIn(editor, "before text");

            MarkdownEditingCommands.InsertTable.Execute(parameter: null, target: editor);

            // A Table is a Block Island: it must never be the document's last block (INV-055).
            editor.Document.Blocks.LastBlock.ShouldBeOfType<Paragraph>();
        });
    }
}
