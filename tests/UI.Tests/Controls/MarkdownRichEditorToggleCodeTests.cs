using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Toggle Code Formatting Action on the <see cref="MarkdownRichEditor"/>: a selection
/// within a single line becomes a Code Span, a selection spanning multiple lines (or a whole line)
/// becomes a Code Block, and applying it inside existing code removes the code formatting. Every
/// result must Capture to canonical Markdown (INV-018).
/// </summary>
public sealed class MarkdownRichEditorToggleCodeTests
{
    [Fact]
    public void ToggleCode_WithSingleLinePartialSelection_MakesCodeSpan_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "make this fast" };
            VisualDocumentText.SelectText(editor, "this");

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("make `this` fast");
        });
    }

    [Fact]
    public void ToggleCode_WithMultiLineSelection_MakesCodeBlock_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha line\n\nbeta line" };
            var blocks = editor.Document.Blocks.ToList();
            editor.Selection.Select(blocks[0].ContentStart, blocks[1].ContentEnd);

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("```\nalpha line\nbeta line\n```");
        });
    }

    [Fact]
    public void ToggleCode_WithWholeLineSelection_MakesCodeBlock_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "var x = 1;" };
            var paragraph = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("```\nvar x = 1;\n```");
        });
    }

    [Fact]
    public void ToggleCode_WithTheWholeDocumentSelected_MakesCodeBlock_INV018()
    {
        StaThread.Run(() =>
        {
            // Select All puts the selection's ends at the document's own edges, whose parent is the
            // FlowDocument rather than a block. Read as "not in a top-level block" they would send the
            // action down the Code Span path, leaving backticked lines where a fence belongs.
            var editor = new MarkdownRichEditor { Markdown = "alpha line\n\nbeta line" };
            editor.SelectAll();

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("```\nalpha line\nbeta line\n```");
        });
    }

    [Fact]
    public void ToggleCode_WithIndentedLines_KeepsTheIndentation_INV018()
    {
        StaThread.Run(() =>
        {
            // Indentation is the code's own content: a fence preserves it verbatim, so the block the
            // action makes must carry the lines exactly as they were written. The paragraphs are built
            // here as typing them produces them — Markdown has no way to express an indented paragraph,
            // which is precisely why the fence is the only place that indentation survives.
            var editor = new MarkdownRichEditor { Markdown = string.Empty };
            editor.Document.Blocks.Clear();
            editor.Document.Blocks.Add(new Paragraph(new Run("if (x) {")));
            editor.Document.Blocks.Add(new Paragraph(new Run("    doThing();")));
            editor.Document.Blocks.Add(new Paragraph(new Run("}")));
            editor.SelectAll();

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("```\nif (x) {\n    doThing();\n}\n```");
        });
    }

    [Fact]
    public void ToggleCode_WithTrailingSpaceInSelection_KeepsTheSpaceOutsideTheDelimiters_INV018()
    {
        StaThread.Run(() =>
        {
            // A user selecting a word by double-click takes its trailing space with it. Left inside
            // the backticks the space is shaded as code and the next word butts against it — the
            // delimiter must hug its text, exactly as an emphasis delimiter does.
            var editor = new MarkdownRichEditor { Markdown = "make this fast now" };
            VisualDocumentText.SelectText(editor, "fast ");

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("make this `fast` now");
        });
    }

    [Fact]
    public void ToggleCode_WithLeadingSpaceInSelection_KeepsTheSpaceOutsideTheDelimiters_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "make this fast now" };
            VisualDocumentText.SelectText(editor, " fast");

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("make this `fast` now");
        });
    }

    [Fact]
    public void ToggleCode_WithSelectionInsideAListItem_MakesACodeSpan_AndLeavesTheListIntact_INV018()
    {
        StaThread.Run(() =>
        {
            // A fence cannot replace the List: the Code Block path rewrites whole top-level blocks,
            // and the List is the top-level block here. Inside a List the action is a Code Span.
            var editor = new MarkdownRichEditor { Markdown = "- alpha item\n- beta item\n- gamma item" };
            VisualDocumentText.SelectText(editor, "beta item");

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- alpha item\n- `beta item`\n- gamma item");
        });
    }

    [Fact]
    public void ToggleCode_WithSelectionInsideABlockQuote_MakesACodeSpan_AndLeavesTheQuoteIntact_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "> quoted alpha\n>\n> quoted beta" };
            VisualDocumentText.SelectText(editor, "quoted beta");

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("> quoted alpha\n>\n> `quoted beta`");
        });
    }

    [Fact]
    public void ToggleCode_WithSelectionInsideATableCell_MakesACodeSpan_AndLeavesTheTableIntact_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "| a | b |\n| --- | --- |\n| one | two |" };
            VisualDocumentText.SelectText(editor, "one");

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("| a | b |\n| --- | --- |\n| `one` | two |");
        });
    }

    [Fact]
    public void ToggleCode_WithSelectionSpanningListItems_MakesACodeSpanOfEach_INV018()
    {
        StaThread.Run(() =>
        {
            // A Code Span may not straddle a line break, so a selection crossing List Items becomes
            // one Code Span per Item rather than a single span holding a newline.
            var editor = new MarkdownRichEditor { Markdown = "- alpha item\n- beta item\n- gamma item" };
            var list = (System.Windows.Documents.List)editor.Document.Blocks.FirstBlock!;
            var items = list.ListItems.ToList();
            editor.Selection.Select(items[0].ContentStart, items[1].ContentEnd);

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- `alpha item`\n- `beta item`\n- gamma item");
        });
    }

    [Fact]
    public void ToggleCode_AcrossALineBreak_MakesACodeSpanPerLine_INV018()
    {
        StaThread.Run(() =>
        {
            // A Code Span may not straddle a line break, and the break is content: joining the two
            // lines into one span would lose it silently.
            var editor = new MarkdownRichEditor { Markdown = "alpha one  \nbeta two" };
            var paragraph = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(paragraph.ContentStart, paragraph.ContentEnd);

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("```\nalpha one\nbeta two\n```");
        });
    }

    [Fact]
    public void ToggleCode_WithSelectionSpanningAMermaidDiagram_LeavesTheDiagramIntact_INV018()
    {
        StaThread.Run(() =>
        {
            // The paragraphs either side are both top-level, so the fence path is reached — but the
            // Diagram between them is a Block Island a fence cannot absorb, and it holds no text to
            // absorb it as. Swallowing it would delete the diagram outright.
            var editor = new MarkdownRichEditor
            {
                Markdown = "before text\n\n```mermaid\nflowchart TD\n  A --> B\n```\n\nafter text",
            };
            var blocks = editor.Document.Blocks.ToList();
            editor.Selection.Select(blocks[0].ContentStart, blocks[^1].ContentEnd);

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldContain("```mermaid");
            editor.Markdown.ShouldContain("A --> B");
        });
    }

    [Fact]
    public void ToggleCode_WithSelectionSpanningATable_LeavesTheTableIntact_INV018()
    {
        StaThread.Run(() =>
        {
            // A fence cannot replace a Table, so a selection running from the paragraph above it to
            // the one below becomes Code Spans — the Table is still a Table afterwards.
            var editor = new MarkdownRichEditor
            {
                Markdown = "before text\n\n| a | b |\n| --- | --- |\n| one | two |\n\nafter text",
            };
            var blocks = editor.Document.Blocks.ToList();
            editor.Selection.Select(blocks[0].ContentStart, blocks[^1].ContentEnd);

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldContain("| --- | --- |");
            editor.Markdown.ShouldNotContain("```");
        });
    }

    [Fact]
    public void ToggleCode_InsideAListItem_RoundTrips_INV005()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- alpha item\n- beta item" };
            VisualDocumentText.SelectText(editor, "beta item");
            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            var reloaded = new MarkdownRichEditor { Markdown = captured };

            reloaded.Markdown.ShouldBe(captured);
        });
    }

    [Fact]
    public void ToggleCode_WithSelectionOnCodeSpan_RemovesTheCodeFormatting()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "make `this` fast" };
            VisualDocumentText.SelectText(editor, "this");

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("make this fast");
        });
    }

    [Fact]
    public void ToggleCode_WithCaretInCodeBlock_RemovesTheCodeFormatting()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "```\nvar x = 1;\n```" };
            var codeParagraph = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(codeParagraph.ContentStart, codeParagraph.ContentStart);

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("var x = 1;");
        });
    }

    [Fact]
    public void ToggleCode_ResultRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha line\n\nbeta line" };
            var blocks = editor.Document.Blocks.ToList();
            editor.Selection.Select(blocks[0].ContentStart, blocks[1].ContentEnd);
            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            // INV-018: the Formatting Action produced canonical Markdown — a fresh Project of the
            // captured source Captures back to the identical text (INV-005 holds immediately).
            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }

    [Fact]
    public void ToggleCode_CannotExecute_WithEmptySelectionOutsideCode()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "just prose" };
            var paragraph = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(paragraph.ContentStart, paragraph.ContentStart);

            MarkdownEditingCommands.ToggleCode.CanExecute(parameter: null, target: editor).ShouldBeFalse();
        });
    }

    [Fact]
    public void ToggleCode_CanExecute_WithNonEmptySelection()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "just prose" };
            VisualDocumentText.SelectText(editor, "prose");

            MarkdownEditingCommands.ToggleCode.CanExecute(parameter: null, target: editor).ShouldBeTrue();
        });
    }

    [Fact]
    public void ToggleCode_CanExecute_WithCaretInsideCodeBlock()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "```\nvar x = 1;\n```" };
            var codeParagraph = editor.Document.Blocks.FirstBlock!;
            editor.Selection.Select(codeParagraph.ContentStart, codeParagraph.ContentStart);

            MarkdownEditingCommands.ToggleCode.CanExecute(parameter: null, target: editor).ShouldBeTrue();
        });
    }
}

