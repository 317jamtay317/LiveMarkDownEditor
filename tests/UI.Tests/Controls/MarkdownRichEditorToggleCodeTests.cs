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

