using System.Linq;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Diagram Preview seam on the <see cref="MarkdownRichEditor"/>: its read-only
/// <c>CurrentDiagramSource</c> tracks the Mermaid Diagram the caret is within, and computing it never
/// changes the Markdown Document (INV-047).
/// </summary>
public sealed class MarkdownRichEditorMermaidTests
{
    [Fact]
    public void CurrentDiagramSource_WhenCaretIsInAMermaidBlock_IsItsSource_INV047()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "```mermaid\ngraph TD\n  A-->B\n```" };
            var block = editor.Document.Blocks.FirstBlock!;

            editor.Selection.Select(block.ContentStart, block.ContentStart);

            editor.CurrentDiagramSource.ShouldBe("graph TD\n  A-->B");
        });
    }

    [Fact]
    public void CurrentDiagramSource_WhenCaretIsInProse_IsNull_INV047()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Just ordinary prose." };
            var block = editor.Document.Blocks.FirstBlock!;

            editor.Selection.Select(block.ContentStart, block.ContentStart);

            editor.CurrentDiagramSource.ShouldBeNull();
        });
    }

    [Fact]
    public void Preview_DoesNotChangeCapturedMarkdown_INV047()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Title\n\n```mermaid\ngraph TD\n  A-->B\n```\n\nAfter." };
            var before = editor.Markdown;

            // Move the caret into the diagram so its Diagram Preview is computed.
            var mermaidBlock = editor.Document.Blocks.ToList()[1];
            editor.Selection.Select(mermaidBlock.ContentStart, mermaidBlock.ContentStart);

            editor.CurrentDiagramSource.ShouldBe("graph TD\n  A-->B");
            editor.Markdown.ShouldBe(before);
        });
    }
}
