using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Shouldly;
using UI.Controls;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for Copy as Rich Text and Copy as Markdown on the <see cref="MarkdownRichEditor"/> (INV-035):
/// a selection copies as rich text (RTF, and HTML rendered from the selection) and, on request, as its
/// Markdown source — all captured from the blocks the selection spans, and copying is not an edit.
/// </summary>
public sealed class MarkdownRichEditorCopyTests
{
    [Fact]
    public void CaptureSelection_ReturnsTheSelectedBlocksMarkdown_INV035()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title" };
            VisualDocumentText.SelectText(editor, "Title");

            editor.CaptureSelection().Trim().ShouldBe("# Title");
        });
    }

    [Fact]
    public void CaptureSelection_WithAPartialSelection_CapturesTheWholeBlock_INV035()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Hello world" };
            VisualDocumentText.SelectText(editor, "Hello");

            // Whole-block granularity: selecting part of a paragraph copies the whole paragraph.
            editor.CaptureSelection().Trim().ShouldBe("Hello world");
        });
    }

    [Fact]
    public void CaptureSelection_WithNoSelection_ReturnsEmpty_INV035()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title" };

            editor.CaptureSelection().ShouldBe(string.Empty);
        });
    }

    [Fact]
    public void CaptureSelection_IsNotAnEdit_INV035()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title" };
            VisualDocumentText.SelectText(editor, "Title");

            editor.CaptureSelection();

            editor.Markdown.ShouldBe("# Title");
        });
    }

    [Fact]
    public void SelectionAsCfHtml_RendersTheSelectionToTheHtmlFlavor_INV035()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title", Renderer = new StubMarkdownRenderer() };
            VisualDocumentText.SelectText(editor, "Title");

            var cfHtml = editor.SelectionAsCfHtml();

            cfHtml.ShouldNotBeNull();
            cfHtml.ShouldStartWith("Version:0.9");
            cfHtml.ShouldContain("<article># Title</article>");
        });
    }

    [Fact]
    public void SelectionAsCfHtml_WithNoSelection_ReturnsNull_INV035()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title", Renderer = new StubMarkdownRenderer() };

            editor.SelectionAsCfHtml().ShouldBeNull();
        });
    }

    [Fact]
    public void SelectionAsCfHtml_WithNoRenderer_ReturnsNull_INV035()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title" };
            VisualDocumentText.SelectText(editor, "Title");

            // Without a Renderer no HTML flavor is added; the built-in rich text is unaffected.
            editor.SelectionAsCfHtml().ShouldBeNull();
        });
    }

    [Fact]
    public void Copy_SerialisesTheSelectionAsRichText_INV035()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "**bold**" };
            VisualDocumentText.SelectText(editor, "bold");

            using var stream = new MemoryStream();
            editor.Selection.Save(stream, DataFormats.Rtf);
            var rtf = Encoding.UTF8.GetString(stream.ToArray());

            // The RichTextBox serialises the selection as RTF, which Word and Outlook paste formatted:
            // the bold run carries the RTF bold control word (\b or \b0 — not a prefix like \bullet).
            Regex.IsMatch(rtf, @"\\b(?![a-z])").ShouldBeTrue("The RTF should carry the bold control word.");
        });
    }
}
