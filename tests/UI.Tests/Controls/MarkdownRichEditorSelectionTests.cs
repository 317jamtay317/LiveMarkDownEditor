using System.Windows;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests that a Pointer Selection on the <see cref="MarkdownRichEditor"/> is character-precise: a
/// drag selects exactly the text the pointer passed over, never rounding out to whole words or
/// running on to the end of the block (INV-079).
/// </summary>
public sealed class MarkdownRichEditorSelectionTests
{
    [Fact]
    public void Editor_DoesNotRoundAPointerSelectionOutToWholeWords_INV079()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Meet Bob now" };

            // WPF's RichTextBox turns Auto Word Selection on by default (its TextBox sibling does
            // not), which is what makes a short drag swallow the rest of the block.
            editor.AutoWordSelection.ShouldBeFalse();
        });
    }

    [Fact]
    public void Editor_KeepsAPartialWordSelection_INV079()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Meet Bob now" };

            VisualDocumentText.SelectText(editor, "Bo");

            editor.Selection.Text.ShouldBe("Bo");
        });
    }

    [Fact]
    public void ContentInset_InsetsTheVisualDocument_RatherThanTheControl_INV079()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Meet Bob now" };

            editor.ContentInset = new Thickness(96d);

            // Only the document's own padding is part of the text layout, so only it leaves a drag
            // landing where the pointer is.
            editor.Document.PagePadding.ShouldBe(new Thickness(96d));
        });
    }

    [Fact]
    public void ContentInset_SurvivesAReProjection_INV079()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { ContentInset = new Thickness(96d) };

            // A projection mints a fresh Visual Document, which would otherwise arrive uninset.
            editor.Markdown = "# Reloaded";

            editor.Document.PagePadding.ShouldBe(new Thickness(96d));
        });
    }
}
