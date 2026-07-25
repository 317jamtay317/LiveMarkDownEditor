using System.Linq;
using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Toggle Definition List Formatting Action on the <see cref="MarkdownRichEditor"/>: the
/// paragraphs the selection touches become a Definition List — the first a Definition Term, the rest
/// its Definition Descriptions — or a selected Definition List becomes plain paragraphs again. It works
/// on whole blocks (INV-066), and every result must Capture to canonical Markdown (INV-018).
/// </summary>
public sealed class MarkdownRichEditorDefinitionListTests
{
    [Fact]
    public void ToggleDefinitionList_OnAParagraph_MakesItATermWithAnEmptyDescription_INV066()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Markdown" };
            VisualDocumentText.PlaceCaretIn(editor, "Markdown");

            MarkdownEditingCommands.ToggleDefinitionList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("Markdown\n:   ");
        });
    }

    [Fact]
    public void ToggleDefinitionList_LeavesTheCaretInTheDescription_INV066()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Markdown" };
            VisualDocumentText.PlaceCaretIn(editor, "Markdown");

            MarkdownEditingCommands.ToggleDefinitionList.Execute(parameter: null, target: editor);
            editor.Selection.Text = "A markup language.";

            editor.Markdown.ShouldBe("Markdown\n:   A markup language.");
        });
    }

    [Fact]
    public void ToggleDefinitionList_OnTwoParagraphs_MakesATermAndItsDescription_INV066()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Markdown\n\nA markup language." };
            editor.SelectAll();

            MarkdownEditingCommands.ToggleDefinitionList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("Markdown\n:   A markup language.");
        });
    }

    [Fact]
    public void ToggleDefinitionList_OnThreeParagraphs_MakesTheRestDescriptions_INV066()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Round-Trip\n\nOne.\n\nTwo." };
            editor.SelectAll();

            MarkdownEditingCommands.ToggleDefinitionList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("Round-Trip\n:   One.\n:   Two.");
        });
    }

    [Fact]
    public void ToggleDefinitionList_WithPartialSelection_TakesTheWholeBlock_INV066()
    {
        StaThread.Run(() =>
        {
            // A ":" prefix applies to a line, so defining half a paragraph is not expressible.
            var editor = new MarkdownRichEditor { Markdown = "A markup language." };
            VisualDocumentText.SelectText(editor, "markup");

            MarkdownEditingCommands.ToggleDefinitionList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("A markup language.\n:   ");
        });
    }

    [Fact]
    public void ToggleDefinitionList_OnADefinitionList_RestoresPlainParagraphs_INV066()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Markdown\n:   A markup language." };
            VisualDocumentText.PlaceCaretIn(editor, "A markup language.");

            MarkdownEditingCommands.ToggleDefinitionList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("Markdown\n\nA markup language.");
            editor.Document.Blocks.OfType<Section>()
                .Any(block => block.Tag is BlockSemantic.DefinitionList)
                .ShouldBeFalse();
        });
    }

    [Fact]
    public void ToggleDefinitionList_KeepsInlineFormatting_INV066()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "**Bold term**\n\nhas *italic* text." };
            editor.SelectAll();

            MarkdownEditingCommands.ToggleDefinitionList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("**Bold term**\n:   has *italic* text.");
        });
    }

    [Fact]
    public void ToggleDefinitionList_RoundTripsWhatItWrote_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Markdown\n\nA markup language." };
            editor.SelectAll();
            MarkdownEditingCommands.ToggleDefinitionList.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            var reprojected = new FlowDocumentToMarkdownCapturer()
                .Capture(new MarkdownToFlowDocumentProjector().Project(captured));

            reprojected.ShouldBe(captured);
        });
    }

    [Fact]
    public void ToggleDefinitionList_LeavesTheBlocksAroundItAlone_INV066()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Intro para.\n\nMarkdown" };
            VisualDocumentText.PlaceCaretIn(editor, "Markdown");

            MarkdownEditingCommands.ToggleDefinitionList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("Intro para.\n\nMarkdown\n:   ");
        });
    }

    [Fact]
    public void ToggleDefinitionList_NeverSwallowsTheFootnoteSection_INV066()
    {
        StaThread.Run(() =>
        {
            // The Footnote Section is composed by Project, not authored prose — a Definition List must
            // not take it in as a Description, which would capture the note inside the glossary.
            var editor = new MarkdownRichEditor
            {
                Markdown = "A claim.[^a]\n\n[^a]: the note\n\nMarkdown\n\nA markup language.",
            };
            editor.SelectAll();

            MarkdownEditingCommands.ToggleDefinitionList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldNotContain(":   [^a]");
            editor.Markdown.ShouldContain("[^a]: the note");
        });
    }

    [Fact]
    public void ToggleDefinitionList_WithTheCaretInTheFootnoteSection_IsUnavailable_INV066()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "A claim.[^a]\n\n[^a]: the note" };
            VisualDocumentText.PlaceCaretIn(editor, "the note");

            MarkdownEditingCommands.ToggleDefinitionList.CanExecute(parameter: null, target: editor)
                .ShouldBeFalse();
        });
    }

    [Fact]
    public void ToggleDefinitionList_WithTheCaretInACodeBlock_IsUnavailable_INV066()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "```\nvar x = 1;\n```" };
            VisualDocumentText.PlaceCaretIn(editor, "var x = 1;");

            MarkdownEditingCommands.ToggleDefinitionList.CanExecute(parameter: null, target: editor)
                .ShouldBeFalse();
        });
    }
}
