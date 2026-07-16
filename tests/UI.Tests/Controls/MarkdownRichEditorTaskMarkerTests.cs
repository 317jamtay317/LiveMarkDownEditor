using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;
using WpfList = System.Windows.Documents.List;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for Toggle Task Marker on the <see cref="MarkdownRichEditor"/>: clicking a Task Marker's
/// checkbox flips it between unchecked and checked and changes nothing else, keeping its glyph and
/// its role in agreement, and Capturing to canonical Markdown (INV-024).
/// </summary>
public sealed class MarkdownRichEditorTaskMarkerTests
{
    private const string Unchecked = "☐ ";
    private const string Checked = "☑ ";

    [Fact]
    public void ToggleTaskMarker_OnAnUncheckedMarker_ChecksIt_INV024()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] todo" };
            VisualDocumentText.PlaceCaretIn(editor, Unchecked);

            editor.ToggleTaskMarkerAt(editor.CaretPosition).ShouldBeTrue();

            editor.Markdown.ShouldBe("- [x] todo");
        });
    }

    [Fact]
    public void ToggleTaskMarker_OnACheckedMarker_UnchecksIt_INV024()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [x] done" };
            VisualDocumentText.PlaceCaretIn(editor, Checked);

            editor.ToggleTaskMarkerAt(editor.CaretPosition).ShouldBeTrue();

            editor.Markdown.ShouldBe("- [ ] done");
        });
    }

    [Fact]
    public void ToggleTaskMarker_LeavesEveryOtherListItemUntouched_INV024()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] alpha\n- [ ] bravo" };
            VisualDocumentText.PlaceCaretIn(editor, Unchecked);

            editor.ToggleTaskMarkerAt(editor.CaretPosition).ShouldBeTrue();

            editor.Markdown.ShouldBe("- [x] alpha\n- [ ] bravo");
        });
    }

    [Fact]
    public void ToggleTaskMarker_AwayFromAMarker_DoesNotToggle_INV024()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] todo" };
            VisualDocumentText.PlaceCaretIn(editor, "todo");

            // A click anywhere but the checkbox places the caret and never toggles a marker.
            editor.ToggleTaskMarkerAt(editor.CaretPosition).ShouldBeFalse();

            editor.Markdown.ShouldBe("- [ ] todo");
        });
    }

    [Fact]
    public void ToggleTaskMarker_KeepsTheGlyphAndTheRoleInAgreement_INV024()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] todo" };
            VisualDocumentText.PlaceCaretIn(editor, Unchecked);

            editor.ToggleTaskMarkerAt(editor.CaretPosition).ShouldBeTrue();

            // The glyph is what the user sees; the role is what Capture reads. They must never
            // disagree about whether the task is checked.
            MarkerRunOf(editor).Text.ShouldBe(Checked);
            editor.Markdown.ShouldBe("- [x] todo");
        });
    }

    [Fact]
    public void ToggleTaskMarker_ResultRoundTrips_INV024()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] todo" };
            VisualDocumentText.PlaceCaretIn(editor, Unchecked);
            editor.ToggleTaskMarkerAt(editor.CaretPosition);
            var captured = editor.Markdown;

            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }

    [Fact]
    public void Project_TaskMarker_SeparatesTheCheckboxFromTheItemTextWithOneSpace()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] todo" };

            // The marker owns the single separating space, so it is not doubled on screen by the one
            // the source's "- [ ] todo" already carries on the item's text. (The List's own bullet is
            // drawn by WPF outside the item's inlines, so the inlines are the whole shown text.)
            var shown = string.Concat(FirstItemParagraphOf(editor).Inlines.OfType<Run>().Select(run => run.Text));

            shown.ShouldBe("☐ todo");
        });
    }

    [Fact]
    public void TaskMarker_DoesNotSwallowTextTypedIntoItsOwnRun_INV024()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] todo" };

            // The marker's Run is ordinary editable text and the caret legitimately sits inside it,
            // so the user can type into it. Capture emits the marker from its role, and must not
            // drop the text along with the glyph — it would vanish from the file while still on
            // screen.
            var marker = MarkerRunOf(editor);
            marker.Text = Unchecked + "urgent: ";

            editor.Markdown.ShouldBe("- [ ] urgent: todo");
        });
    }

    [Fact]
    public void TaskMarker_PinsItsFont_SoTickingItDoesNotResizeTheCheckbox()
    {
        StaThread.Run(() =>
        {
            var unticked = new MarkdownRichEditor { Markdown = "- [ ] todo" };
            var ticked = new MarkdownRichEditor { Markdown = "- [x] done" };

            // Neither glyph is in the UI font. Left to inherit, each resolves through font fallback
            // on its own and lands on a different face, so the box changes size as it is ticked.
            // Both markers must name the same explicit family.
            var untickedFont = MarkerRunOf(unticked).FontFamily;
            untickedFont.Source.ShouldBe("Segoe UI Symbol");
            MarkerRunOf(ticked).FontFamily.Source.ShouldBe(untickedFont.Source);
        });
    }

    private static Paragraph FirstItemParagraphOf(MarkdownRichEditor editor) =>
        editor.Document.Blocks.OfType<WpfList>().First()
            .ListItems.First()
            .Blocks.OfType<Paragraph>().First();

    private static Run MarkerRunOf(MarkdownRichEditor editor) =>
        FirstItemParagraphOf(editor).Inlines.OfType<Run>().First();
}
