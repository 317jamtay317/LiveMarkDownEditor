using System.Windows;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for Smart Paste on the <see cref="MarkdownRichEditor"/> (INV-041): a URL pasted over a
/// selection becomes a Link, and HTML converts to Markdown and pastes as formatted content. (The image
/// case writes a file beside the Watched File — a platform boundary exercised in the app.)
/// </summary>
public sealed class MarkdownRichEditorSmartPasteTests
{
    [Fact]
    public void SmartPaste_AUrlOverASelection_TurnsItIntoALink_INV041()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "see docs here" };
            VisualDocumentText.SelectText(editor, "docs");
            var data = new DataObject();
            data.SetText("https://example.com");

            var handled = editor.SmartPaste(data);

            handled.ShouldBeTrue();
            editor.Markdown.ShouldBe("see [docs](https://example.com) here");
        });
    }

    [Fact]
    public void SmartPaste_AUrlWithNoSelection_IsNotHandled_INV041()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "see here" };
            var data = new DataObject();
            data.SetText("https://example.com");

            // No selection to turn into a Link, and a plain URL is not HTML or an image: default paste.
            editor.SmartPaste(data).ShouldBeFalse();
            editor.Markdown.ShouldBe("see here");
        });
    }

    [Fact]
    public void SmartPaste_Html_ConvertsToMarkdownAndPastesFormatted_INV041()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = string.Empty };
            var data = new DataObject();
            data.SetData(DataFormats.Html, CfHtml.Wrap("<h2>Title</h2><p>Body text</p>"));

            var handled = editor.SmartPaste(data);

            handled.ShouldBeTrue();
            editor.Markdown.ShouldContain("## Title");
            editor.Markdown.ShouldContain("Body text");
        });
    }

    [Fact]
    public void SmartPaste_CodeCopiedFromACodeEditor_PastesAsACodeBlockKeepingItsIndentation_INV041()
    {
        StaThread.Run(() =>
        {
            // The clipboard shape a code editor writes: a white-space:pre wrapper, one element per
            // line. Pasted as prose the indentation would be collapsed away by HTML's own whitespace
            // rules — and a Markdown paragraph could not carry it back out again either.
            var editor = new MarkdownRichEditor { Markdown = string.Empty };
            var data = new DataObject();
            data.SetData(DataFormats.Html, CfHtml.Wrap(
                "<div style=\"white-space: pre;\">" +
                "<div><span>if (x) {</span></div>" +
                "<div><span>    doThing();</span></div>" +
                "<div><span>}</span></div>" +
                "</div>"));

            var handled = editor.SmartPaste(data);

            handled.ShouldBeTrue();
            editor.Markdown.ShouldBe("```\nif (x) {\n    doThing();\n}\n```");
        });
    }

    [Fact]
    public void SmartPaste_PlainText_IsNotHandled_INV041()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "hello" };
            var data = new DataObject();
            data.SetText("just some plain text");

            editor.SmartPaste(data).ShouldBeFalse();
            editor.Markdown.ShouldBe("hello");
        });
    }
}
