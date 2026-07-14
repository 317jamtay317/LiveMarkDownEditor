using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Table Formatting Actions on the <see cref="MarkdownRichEditor"/>: Insert Table
/// places a new three-column Table at the caret (only outside a Table), Add Row and Add Column grow
/// the Table containing the caret (only inside one), and every operation keeps the Table rectangular
/// (INV-019) and Captures to canonical Markdown (INV-018).
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
}
