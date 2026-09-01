using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Toggle Block Quote Formatting Action on the <see cref="MarkdownRichEditor"/>: the
/// blocks the selection touches become a Block Quote, or a Block Quote's blocks become plain blocks
/// again. It quotes whole blocks and preserves them (INV-028), and every result must Capture to
/// canonical Markdown (INV-018).
/// </summary>
public sealed class MarkdownRichEditorQuoteTests
{
    [Fact]
    public void ToggleBlockQuote_OnAParagraph_QuotesIt_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "As they said." };
            VisualDocumentText.PlaceCaretIn(editor, "As they said.");

            MarkdownEditingCommands.ToggleBlockQuote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("> As they said.");
        });
    }

    [Fact]
    public void ToggleBlockQuote_WithTheWholeDocumentSelected_QuotesEveryBlock_INV028()
    {
        StaThread.Run(() =>
        {
            // Select All leaves the selection's end at the document's own edge, which is inside no
            // block. Read as "the selection ends nowhere", the action would decline to run at all.
            var editor = new MarkdownRichEditor { Markdown = "As they said.\n\nAnd then some." };
            editor.SelectAll();

            MarkdownEditingCommands.ToggleBlockQuote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("> As they said.\n>\n> And then some.");
        });
    }

    [Fact]
    public void ToggleBlockQuote_WithPartialSelection_QuotesTheWholeBlock_INV028()
    {
        StaThread.Run(() =>
        {
            // A "> " prefix applies to a line, so quoting half a paragraph is not expressible.
            var editor = new MarkdownRichEditor { Markdown = "As they said before." };
            VisualDocumentText.SelectText(editor, "they");

            MarkdownEditingCommands.ToggleBlockQuote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("> As they said before.");
        });
    }

    [Fact]
    public void ToggleBlockQuote_OnAQuote_RestoresItsBlocksAtTopLevel_INV028()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "> As they said." };
            VisualDocumentText.PlaceCaretIn(editor, "As they said.");

            MarkdownEditingCommands.ToggleBlockQuote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("As they said.");
        });
    }

    [Fact]
    public void ToggleBlockQuote_PreservesInlineFormatting_INV028()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "As **they** said." };
            VisualDocumentText.PlaceCaretIn(editor, "As ");

            MarkdownEditingCommands.ToggleBlockQuote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("> As **they** said.");
        });
    }

    [Fact]
    public void ToggleBlockQuote_PreservesTheBlocksKind_INV028()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title" };
            VisualDocumentText.PlaceCaretIn(editor, "Title");

            MarkdownEditingCommands.ToggleBlockQuote.Execute(parameter: null, target: editor);

            // A Heading stays a Heading inside the Block Quote.
            editor.Markdown.ShouldBe("> # Title");
        });
    }

    [Fact]
    public void ToggleBlockQuote_LeavesTheOtherBlocksAlone_INV028()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Before\n\nQuote me\n\nAfter" };
            VisualDocumentText.PlaceCaretIn(editor, "Quote me");

            MarkdownEditingCommands.ToggleBlockQuote.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("Before\n\n> Quote me\n\nAfter");
        });
    }

    [Fact]
    public void ToggleBlockQuote_CapturesCanonicalMarkdown_ThatRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "As **they** said." };
            VisualDocumentText.PlaceCaretIn(editor, "As ");

            MarkdownEditingCommands.ToggleBlockQuote.Execute(parameter: null, target: editor);

            var captured = editor.Markdown;
            var reloaded = new MarkdownRichEditor { Markdown = captured };
            reloaded.Markdown.ShouldBe(captured);
        });
    }

    // Enter's own paragraph break runs through WPF, which needs a focused editor and so does nothing
    // headless — these set up the empty line that break leaves inside the Block Quote and cover the
    // rule that takes it out again (INV-078). That the second Enter reaches this rule at all is
    // verified by driving the real app.

    [Fact]
    public void Enter_OnTheEmptyLineAtTheEndOfAQuote_LeavesTheQuote_INV078()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "> As they said." };
            var opened = OpenEmptyLineAfter(editor, "As they said.");

            editor.LeaveBlockQuoteAtCaret().ShouldBeTrue();

            // The line the first Enter opened is now a plain paragraph below the quote, and the
            // caret came with it.
            editor.Document.Blocks.LastBlock.ShouldBe(opened);
            VisualDocumentTraversal.AncestorOf<Section>(editor.CaretPosition).ShouldBeNull();
        });
    }

    [Fact]
    public void Enter_OnTheEmptyLine_ThenTyping_WritesBelowTheQuote_INV078()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "> As they said." };
            OpenEmptyLineAfter(editor, "As they said.");

            editor.LeaveBlockQuoteAtCaret();
            TypeAtCaret(editor, "And then some.");

            editor.Markdown.ShouldBe("> As they said.\n\nAnd then some.");
        });
    }

    [Fact]
    public void Enter_OnAnEmptyLineInTheMiddleOfAQuote_SplitsTheQuote_INV078()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "> # Title\n>\n> As they said." };
            OpenEmptyLineAfter(editor, "Title");

            editor.LeaveBlockQuoteAtCaret().ShouldBeTrue();
            TypeAtCaret(editor, "Between.");

            // The blocks below the line stay quoted, as a second Block Quote, and keep their kind
            // and content (INV-028).
            editor.Markdown.ShouldBe("> # Title\n\nBetween.\n\n> As they said.");
        });
    }

    [Fact]
    public void Enter_InAQuoteHoldingNothingElse_TakesTheQuoteAway_INV078()
    {
        StaThread.Run(() =>
        {
            // The state a user's last backspace leaves: a Block Quote holding one empty line.
            var editor = new MarkdownRichEditor { Markdown = "> As they said." };
            var quote = (Section)editor.Document.Blocks.FirstBlock;
            var only = (Paragraph)quote.Blocks.First();
            only.Inlines.Clear();
            editor.CaretPosition = only.ContentStart;

            editor.LeaveBlockQuoteAtCaret().ShouldBeTrue();

            // An empty Block Quote is a left rule around nothing, so it goes rather than being left behind.
            editor.Document.Blocks.OfType<Section>().ShouldBeEmpty();
            editor.Markdown.ShouldBe("");
        });
    }

    [Fact]
    public void Enter_OnALineWithTextInAQuote_LeavesTheBreakToWpf_INV078()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "> As they said." };
            VisualDocumentText.PlaceCaretAfter(editor, "As they");

            // A line with content is a line the user is still writing.
            editor.LeaveBlockQuoteAtCaret().ShouldBeFalse();

            editor.Markdown.ShouldBe("> As they said.");
        });
    }

    [Fact]
    public void Enter_OnAnEmptyLineOutsideAQuote_LeavesTheBreakToWpf_INV078()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "" };
            editor.CaretPosition = editor.Document.Blocks.FirstBlock.ContentStart;

            editor.LeaveBlockQuoteAtCaret().ShouldBeFalse();
        });
    }

    [Fact]
    public void Enter_OnAnEmptyListItemInsideAQuote_LeavesTheBreakToTheList_INV078()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "> - alpha" };
            var list = (System.Windows.Documents.List)((Section)editor.Document.Blocks.FirstBlock).Blocks.First();
            var opened = new Paragraph();
            list.ListItems.Add(new ListItem(opened));
            editor.CaretPosition = opened.ContentStart;

            // The line belongs to the List, which answers Enter its own way.
            editor.LeaveBlockQuoteAtCaret().ShouldBeFalse();
        });
    }

    [Fact]
    public void Enter_OnAnEmptyLineInANestedQuote_LeavesOneLevel_INV078()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "> > As they said." };
            OpenEmptyLineAfter(editor, "As they said.");

            editor.LeaveBlockQuoteAtCaret().ShouldBeTrue();
            TypeAtCaret(editor, "Still quoted.");

            // The line leaves the inner quote into the one around it — the level the user asked to leave.
            editor.Markdown.ShouldBe("> > As they said.\n>\n> Still quoted.");
        });
    }

    // The state WPF's own Enter leaves behind: an empty paragraph after the one the caret was in,
    // inside the same Block Quote, with the caret on it.
    private static Paragraph OpenEmptyLineAfter(MarkdownRichEditor editor, string text)
    {
        VisualDocumentText.PlaceCaretAfter(editor, text);
        var current = VisualDocumentTraversal.AncestorOf<Paragraph>(editor.CaretPosition).ShouldNotBeNull();
        var quote = current.Parent.ShouldBeOfType<Section>();

        var opened = new Paragraph();
        quote.Blocks.InsertAfter(current, opened);
        editor.CaretPosition = opened.ContentStart;
        return opened;
    }

    // Types the way WPF's editor does: into whichever Run the caret has been normalised into.
    private static void TypeAtCaret(MarkdownRichEditor editor, string text)
    {
        if (editor.CaretPosition.Parent is Run run)
        {
            run.Text += text;
            return;
        }

        _ = new Run(text, editor.CaretPosition);
    }
}
