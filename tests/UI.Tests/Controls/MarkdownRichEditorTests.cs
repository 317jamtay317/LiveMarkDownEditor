using System.Windows;
using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// End-to-end tests for the <see cref="MarkdownRichEditor"/> custom control: assigning
/// <see cref="MarkdownRichEditor.Markdown"/> Projects it into the Visual Document, and editing the
/// Visual Document Captures it back into <see cref="MarkdownRichEditor.Markdown"/>.
/// </summary>
public sealed class MarkdownRichEditorTests
{
    [Fact]
    public void SettingMarkdown_ProjectsFormattedVisualDocument_WithoutRawSyntax()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title" };

            var visibleText = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd).Text;

            visibleText.ShouldContain("Title");
            visibleText.ShouldNotContain("#");
        });
    }

    [Fact]
    public void EditingTheVisualDocument_CapturesBackToMarkdown()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor();

            // Simulate the user applying bold in the editor (property-based, as the toolbar does).
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run("hello ") { });
            paragraph.Inlines.Add(new Run("world") { FontWeight = FontWeights.Bold });
            editor.Document = new FlowDocument(paragraph);

            editor.Markdown.ShouldBe("hello **world**");
        });
    }
}
