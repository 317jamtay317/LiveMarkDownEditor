using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
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
}
