using Shouldly;
using UI.Controls;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Insert Link and Insert Image Formatting Actions on the
/// <see cref="MarkdownRichEditor"/>: each asks for its text and URL through the Link Prompt and
/// edits only on a usable answer (INV-030). Every result must Capture to canonical Markdown
/// (INV-018).
/// </summary>
public sealed class MarkdownRichEditorLinkTests
{
    [Fact]
    public void InsertLink_WithASelection_TurnsItIntoALink_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "see the docs here",
                LinkPrompt = new StubLinkPrompt(new LinkDetails("the docs", "https://example.com")),
            };
            VisualDocumentText.SelectText(editor, "the docs");

            MarkdownEditingCommands.InsertLink.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("see [the docs](https://example.com) here");
        });
    }

    [Fact]
    public void InsertLink_WithTrailingSpaceInSelection_KeepsTheSpaceOutsideTheLink_INV018()
    {
        StaThread.Run(() =>
        {
            // Double-clicking "docs" selects "docs " — the trailing space belongs to the sentence,
            // not to the Link's text, and left inside it swallows the separator before "here".
            var editor = new MarkdownRichEditor
            {
                Markdown = "see the docs here",
                LinkPrompt = new StubLinkPrompt(new LinkDetails("docs", "https://example.com")),
            };
            VisualDocumentText.SelectText(editor, "docs ");

            MarkdownEditingCommands.InsertLink.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("see the [docs](https://example.com) here");
        });
    }

    [Fact]
    public void InsertLink_WithTrailingSpaceInSelection_SeedsThePromptWithTheWordAlone_INV030()
    {
        StaThread.Run(() =>
        {
            var prompt = new StubLinkPrompt(new LinkDetails("docs", "https://example.com"));
            var editor = new MarkdownRichEditor { Markdown = "see the docs here", LinkPrompt = prompt };
            VisualDocumentText.SelectText(editor, "docs ");

            MarkdownEditingCommands.InsertLink.Execute(parameter: null, target: editor);

            // The proposed text is what the user sees in the Link Prompt; a stray trailing space
            // there reads as a typo they have to delete.
            prompt.ProposedText.ShouldBe("docs");
        });
    }

    [Fact]
    public void InsertLink_SeedsThePromptWithTheSelection_INV030()
    {
        StaThread.Run(() =>
        {
            var prompt = new StubLinkPrompt(new LinkDetails("the docs", "https://example.com"));
            var editor = new MarkdownRichEditor { Markdown = "see the docs here", LinkPrompt = prompt };
            VisualDocumentText.SelectText(editor, "the docs");

            MarkdownEditingCommands.InsertLink.Execute(parameter: null, target: editor);

            // Select a word, press Ctrl+K, paste a URL — the text must not need retyping.
            prompt.ProposedText.ShouldBe("the docs");
        });
    }

    [Fact]
    public void InsertLink_WhenThePromptIsDismissed_MakesNoEdit_INV030()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "see the docs here",
                LinkPrompt = new StubLinkPrompt(answer: null),
            };
            VisualDocumentText.SelectText(editor, "the docs");

            MarkdownEditingCommands.InsertLink.Execute(parameter: null, target: editor);

            // Asking a question is not an edit.
            editor.Markdown.ShouldBe("see the docs here");
        });
    }

    [Fact]
    public void InsertLink_WithAnEmptyUrl_MakesNoEdit_INV030()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "see the docs here",
                LinkPrompt = new StubLinkPrompt(new LinkDetails("the docs", "   ")),
            };
            VisualDocumentText.SelectText(editor, "the docs");

            MarkdownEditingCommands.InsertLink.Execute(parameter: null, target: editor);

            // A Link is shown by its text alone, so one with no destination could never be repaired
            // from the Visual Document.
            editor.Markdown.ShouldBe("see the docs here");
        });
    }

    [Fact]
    public void InsertLink_AtACaret_InsertsANewLink_INV030()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "see here",
                LinkPrompt = new StubLinkPrompt(new LinkDetails("the docs", "https://example.com")),
            };
            VisualDocumentText.PlaceCaretIn(editor, "here");

            MarkdownEditingCommands.InsertLink.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("see [the docs](https://example.com)here");
        });
    }

    [Fact]
    public void InsertLink_WithNoText_FallsBackToTheUrl_INV030()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "see",
                LinkPrompt = new StubLinkPrompt(new LinkDetails(string.Empty, "https://example.com")),
            };
            editor.CaretPosition = editor.Document.ContentEnd;

            MarkdownEditingCommands.InsertLink.Execute(parameter: null, target: editor);

            // A Link with no text at all would be invisible in the Visual Document.
            editor.Markdown.ShouldContain("https://example.com");
        });
    }

    [Fact]
    public void InsertLink_WithNoLinkPrompt_MakesNoEdit_INV030()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "see the docs here" };
            VisualDocumentText.SelectText(editor, "the docs");

            MarkdownEditingCommands.InsertLink.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("see the docs here");
        });
    }

    [Fact]
    public void InsertImage_WithASelection_TurnsItIntoAnImage_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "see logo here",
                LinkPrompt = new StubLinkPrompt(new LinkDetails("logo", "logo.png")),
            };
            VisualDocumentText.SelectText(editor, "logo");

            MarkdownEditingCommands.InsertImage.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("see ![logo](logo.png) here");
        });
    }

    [Fact]
    public void InsertImage_WhenThePromptIsDismissed_MakesNoEdit_INV030()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "see logo here",
                LinkPrompt = new StubLinkPrompt(answer: null),
            };
            VisualDocumentText.SelectText(editor, "logo");

            MarkdownEditingCommands.InsertImage.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("see logo here");
        });
    }

    [Fact]
    public void InsertLink_CapturesCanonicalMarkdown_ThatRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor
            {
                Markdown = "see the docs here",
                LinkPrompt = new StubLinkPrompt(new LinkDetails("the docs", "https://example.com")),
            };
            VisualDocumentText.SelectText(editor, "the docs");

            MarkdownEditingCommands.InsertLink.Execute(parameter: null, target: editor);

            var captured = editor.Markdown;
            var reloaded = new MarkdownRichEditor { Markdown = captured };
            reloaded.Markdown.ShouldBe(captured);
        });
    }
}
