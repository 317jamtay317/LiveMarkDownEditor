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
    public void InsertFootnote_WithTheCaretInsideBoldText_CitesTheFootnote_INV065()
    {
        StaThread.Run(() =>
        {
            // The caret's Run belongs to the Bold span, not to the paragraph, so the Reference has to be
            // placed relative to the span rather than blindly into the paragraph's own inlines.
            var editor = new MarkdownRichEditor { Markdown = "A **bold claim** here." };
            VisualDocumentText.PlaceCaretAfter(editor, "bold claim");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("A **bold claim**[^1] here.\n\n[^1]:");
        });
    }

    [Fact]
    public void InsertFootnote_WithTheCaretInsideALink_CitesTheFootnote_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "See [the docs](https://x/y) now." };
            VisualDocumentText.PlaceCaretAfter(editor, "the docs");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("See [the docs](https://x/y)[^1] now.\n\n[^1]:");
        });
    }

    [Fact]
    public void InsertFootnote_WithTheCaretInAListItem_CitesTheFootnote_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- an item" };
            VisualDocumentText.PlaceCaretAfter(editor, "an item");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- an item[^1]\n\n[^1]:");
        });
    }

    [Fact]
    public void InsertFootnote_WithTheCaretInATableCell_CitesTheFootnote_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "| A |\n| --- |\n| cell |" };
            VisualDocumentText.PlaceCaretAfter(editor, "cell");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("| A |\n| --- |\n| cell[^1] |\n\n[^1]:");
        });
    }

    [Fact]
    public void InsertFootnote_OverASelection_CitesItsEnd_AndKeepsTheProse_INV065()
    {
        StaThread.Run(() =>
        {
            // A citation marks the phrase it follows. Replacing the selection with a Reference would
            // delete the very words being cited.
            var editor = new MarkdownRichEditor { Markdown = "A claim worth citing here." };
            VisualDocumentText.SelectText(editor, "claim worth citing");

            MarkdownEditingCommands.InsertFootnote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("A claim worth citing[^1] here.\n\n[^1]:");
        });
    }

    [Fact]
    public void InsertFootnote_WithTheCaretInTheFootnoteSection_IsUnavailable_INV065()
    {
        StaThread.Run(() =>
        {
            // The Footnote Section holds notes, not the prose that cites them.
            var editor = new MarkdownRichEditor { Markdown = "A claim.[^a]\n\n[^a]: the note" };
            VisualDocumentText.PlaceCaretIn(editor, "the note");

            MarkdownEditingCommands.InsertFootnote.CanExecute(parameter: null, target: editor)
                .ShouldBeFalse();
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

    [Fact]
    public void Capture_WithTheFootnoteSectionInsideAFoldedSection_KeepsEveryNote_INV011()
    {
        StaThread.Run(() =>
        {
            // The Footnote Section sits at the end of the Visual Document, so Folding the last Section
            // Heading hides it. A Fold is view-only: the notes must still be Captured (INV-011).
            const string Markdown = "# Top\n\nA claim.[^a]\n\n[^a]: the note\n\n## Last\n\nUnder the last heading.";
            var editor = new MarkdownRichEditor { Markdown = Markdown };
            var heading = editor.Document.Blocks.OfType<Paragraph>()
                .First(paragraph => paragraph.Tag is HeadingRole { Level: 2 });

            editor.ToggleFold(heading);

            editor.Markdown.ShouldBe(Markdown);
        });
    }

    [Fact]
    public void Capture_WithEveryFoldCollapsed_KeepsEveryNote_INV011()
    {
        StaThread.Run(() =>
        {
            const string Markdown = "# Top\n\nA claim.[^a]\n\n[^a]: the note\n\n## Last\n\nUnder the last heading.";
            var editor = new MarkdownRichEditor { Markdown = Markdown };

            editor.CollapseAllFolds();

            editor.Markdown.ShouldBe(Markdown);
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
