using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Set Heading Level Formatting Action on the <see cref="MarkdownRichEditor"/>: the
/// block at the caret becomes a Heading of the chosen Heading Level, or a plain paragraph again.
/// It changes a block's level, never its content (INV-027), and every result must Capture to
/// canonical Markdown (INV-018).
/// </summary>
public sealed class MarkdownRichEditorHeadingTests
{
    [Fact]
    public void SetHeadingLevel_OnParagraph_MakesHeading_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 2, target: editor);

            editor.Markdown.ShouldBe("## Introduction");
        });
    }

    [Theory]
    [InlineData(1, "# Introduction")]
    [InlineData(2, "## Introduction")]
    [InlineData(3, "### Introduction")]
    [InlineData(4, "#### Introduction")]
    [InlineData(5, "##### Introduction")]
    [InlineData(6, "###### Introduction")]
    public void SetHeadingLevel_MakesHeadingAtEveryLevel_INV027(int level, string expected)
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: level, target: editor);

            editor.Markdown.ShouldBe(expected);
        });
    }

    [Fact]
    public void SetHeadingLevel_ToParagraph_ClearsTheHeading_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "## Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            MarkdownEditingCommands.SetHeadingLevel.Execute(
                parameter: MarkdownEditingCommands.ParagraphHeadingLevel,
                target: editor);

            editor.Markdown.ShouldBe("Introduction");
        });
    }

    [Fact]
    public void SetHeadingLevel_OnAHeading_ChangesItsLevel_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "# Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 3, target: editor);

            editor.Markdown.ShouldBe("### Introduction");
        });
    }

    [Fact]
    public void SetHeadingLevel_ToTheSameLevel_LeavesItAHeading_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "## Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            // Set Heading Level sets a level rather than toggling one: reaching for the level a
            // Heading already has must never destroy the Heading.
            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 2, target: editor);

            editor.Markdown.ShouldBe("## Introduction");
        });
    }

    [Fact]
    public void SetHeadingLevel_PreservesInlineFormatting_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Meet **Bob** now" };
            VisualDocumentText.PlaceCaretIn(editor, "Meet ");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 2, target: editor);

            editor.Markdown.ShouldBe("## Meet **Bob** now");
        });
    }

    [Fact]
    public void SetHeadingLevel_SizesTheHeading_WithoutWeighting_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 1, target: editor);

            // A bold weight on the Heading would make Capture read its text as inline-bold.
            editor.Markdown.ShouldBe("# Introduction");
            editor.Markdown.ShouldNotContain("**");
        });
    }

    [Fact]
    public void SetHeadingLevel_LeavesTheOtherBlocksAlone_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Introduction\n\nBody text" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 2, target: editor);

            editor.Markdown.ShouldBe("## Introduction\n\nBody text");
        });
    }

    [Theory]
    [InlineData(7)]
    [InlineData(-1)]
    public void SetHeadingLevel_GivenLevelOutsideOneToSix_LeavesTheDocumentUnchanged_INV027(int level)
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "## Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: level, target: editor);

            editor.Markdown.ShouldBe("## Introduction");
        });
    }

    [Fact]
    public void SetHeadingLevel_CapturesCanonicalMarkdown_ThatRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "Meet **Bob** now" };
            VisualDocumentText.PlaceCaretIn(editor, "Meet ");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 2, target: editor);

            // Re-projecting what was Captured and Capturing it again must converge (INV-005).
            var captured = editor.Markdown;
            var reloaded = new MarkdownRichEditor { Markdown = captured };
            reloaded.Markdown.ShouldBe(captured);
        });
    }
}
