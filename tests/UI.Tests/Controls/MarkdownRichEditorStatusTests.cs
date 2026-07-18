using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the <see cref="MarkdownRichEditor.Status"/> shown in the Status Bar (INV-039): the word
/// count reflects the visible document and follows its changes. (Caret line/column depend on layout
/// and are exercised by driving the real app; the counts are what these headless tests pin.)
/// </summary>
public sealed class MarkdownRichEditorStatusTests
{
    [Fact]
    public void Status_ReflectsTheDocumentWordCount_INV039()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "the quick brown fox" };

            editor.Status.WordCount.ShouldBe(4);
        });
    }

    [Fact]
    public void Status_UpdatesTheWordCount_WhenTheDocumentChanges_INV039()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "one two" };
            editor.Status.WordCount.ShouldBe(2);

            editor.Markdown = "one two three four five";

            editor.Status.WordCount.ShouldBe(5);
        });
    }
}
