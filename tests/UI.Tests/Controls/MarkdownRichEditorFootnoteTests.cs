using System.Linq;
using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Insert Footnote Formatting Action on the <see cref="MarkdownRichEditor"/>: it cites a
/// new Footnote at the caret — a Footnote Reference there and an empty Footnote Definition in the
/// Footnote Section — and leaves the caret in the Definition, ready for the note (INV-065). Every
/// result must Capture to canonical Markdown (INV-018).
/// </summary>
public sealed class MarkdownRichEditorFootnoteTests
{
    [Fact]
    public void InsertFootnote_AtTheCaret_CitesANewFootnote_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "A claim." };
            VisualDocumentText.PlaceCaretAfter(editor, "A claim.");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("A claim.[^1]\n\n[^1]:");
        });
    }

    [Fact]
    public void InsertFootnote_LeavesTheCaretInTheDefinition_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "A claim." };
            VisualDocumentText.PlaceCaretAfter(editor, "A claim.");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            // The caret is in the new Definition, so the first thing typed becomes the note.
            var definition = Definitions(editor).Single();
            definition.ContentStart.CompareTo(editor.Selection.Start).ShouldBeLessThanOrEqualTo(0);
            definition.ContentEnd.CompareTo(editor.Selection.Start).ShouldBeGreaterThanOrEqualTo(0);
        });
    }

    [Fact]
    public void InsertFootnote_ThenTypingTheNote_CapturesIt_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "A claim." };
            VisualDocumentText.PlaceCaretAfter(editor, "A claim.");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);
            editor.Selection.Text = "the note";

            editor.Markdown.ShouldBe("A claim.[^1]\n\n[^1]: the note");
        });
    }

    [Fact]
    public void InsertFootnote_ChoosesTheLowestUnusedLabel_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "First.[^1] Second.[^2]\n\n[^1]: one\n\n[^2]: two" };
            VisualDocumentText.PlaceCaretAfter(editor, "Second.");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldContain("[^3]:");
        });
    }

    [Fact]
    public void InsertFootnote_GivenANamedLabelInUse_DoesNotReuseIt_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "A claim.[^1]\n\n[^1]: the note" };
            VisualDocumentText.PlaceCaretAfter(editor, "A claim.");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            // Both Footnotes survive with labels of their own — a Reference sharing a Label would
            // share the Definition too.
            Definitions(editor).Count.ShouldBe(2);
            editor.Markdown.ShouldContain("[^2]");
        });
    }

    [Fact]
    public void InsertFootnote_CitedInsideAWord_KeepsTheProseIntact_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "A claim here." };
            VisualDocumentText.PlaceCaretAfter(editor, "A claim");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("A claim[^1] here.\n\n[^1]:");
        });
    }

    [Fact]
    public void InsertFootnote_OnASecondCitation_AddsToTheSameFootnoteSection_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "First. Second." };
            VisualDocumentText.PlaceCaretAfter(editor, "First.");
            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            VisualDocumentText.PlaceCaretAfter(editor, "Second.");
            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            editor.Document.Blocks.OfType<Section>()
                .Count(block => block.Tag is BlockSemantic.FootnoteSection)
                .ShouldBe(1);
            Definitions(editor).Count.ShouldBe(2);
        });
    }

    [Fact]
    public void InsertFootnote_RoundTripsWhatItWrote_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "A claim." };
            VisualDocumentText.PlaceCaretAfter(editor, "A claim.");
            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);
            editor.Selection.Text = "the note";
            var captured = editor.Markdown;

            // Re-projecting what was captured captures the same source again (INV-005).
            var reprojected = new FlowDocumentToMarkdownCapturer()
                .Capture(new MarkdownToFlowDocumentProjector().Project(captured));

            reprojected.ShouldBe(captured);
        });
    }

    [Fact]
    public void InsertFootnote_WithTheCaretInACodeBlock_IsUnavailable_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "```\nvar x = 1;\n```" };
            VisualDocumentText.PlaceCaretIn(editor, "var x = 1;");

            // A Footnote Reference in a Code Block would be code text, not a citation.
            MarkdownEditingCommands.InsertFootnote.CanExecute(parameter: null, target: editor)
                .ShouldBeFalse();
        });
    }

    private static List<Section> Definitions(MarkdownRichEditor editor) =>
    [
        .. editor.Document.Blocks.OfType<Section>()
            .Where(block => block.Tag is BlockSemantic.FootnoteSection)
            .SelectMany(section => section.Blocks.OfType<Section>())
            .Where(definition => definition.Tag is FootnoteDefinitionRole),
    ];
}
