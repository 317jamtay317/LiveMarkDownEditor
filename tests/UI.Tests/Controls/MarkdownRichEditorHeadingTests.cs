using System.Windows.Input;
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
    public void SetHeadingLevel_InsideAListItem_RelevelsTheItemsParagraph_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 2, target: editor);

            editor.Markdown.ShouldBe("- ## Introduction");
        });
    }

    [Fact]
    public void SetHeadingLevel_InsideAListItem_IsAvailable_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            // The Heading Level Picker is greyed out unless the command reports it can run, so a
            // Formatting Action that works is not enough on its own.
            MarkdownEditingCommands.SetHeadingLevel.CanExecute(parameter: 2, target: editor)
                .ShouldBeTrue();
        });
    }

    [Fact]
    public void SetHeadingLevel_InsideANestedListItem_RelevelsTheItemsParagraph_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- Outer\n  - Inner" };
            VisualDocumentText.PlaceCaretIn(editor, "Inner");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 3, target: editor);

            editor.Markdown.ShouldBe("- Outer\n  - ### Inner");
        });
    }

    [Fact]
    public void SetHeadingLevel_InsideAListItem_LeavesTheOtherItemsAlone_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- First\n- Second" };
            VisualDocumentText.PlaceCaretIn(editor, "Second");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 2, target: editor);

            editor.Markdown.ShouldBe("- First\n- ## Second");
        });
    }

    [Fact]
    public void SetHeadingLevel_ToParagraph_InsideAListItem_ClearsTheHeading_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- ## Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            MarkdownEditingCommands.SetHeadingLevel.Execute(
                parameter: MarkdownEditingCommands.ParagraphHeadingLevel,
                target: editor);

            editor.Markdown.ShouldBe("- Introduction");
        });
    }

    [Fact]
    public void SetHeadingLevel_InsideAListItem_PreservesInlineFormatting_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- Meet **Bob** now" };
            VisualDocumentText.PlaceCaretIn(editor, "Meet ");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 2, target: editor);

            editor.Markdown.ShouldBe("- ## Meet **Bob** now");
        });
    }

    [Fact]
    public void SetHeadingLevel_InsideAListItem_CapturesMarkdownThatRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            MarkdownEditingCommands.SetHeadingLevel.Execute(parameter: 2, target: editor);

            var captured = editor.Markdown;
            var reloaded = new MarkdownRichEditor { Markdown = captured };
            reloaded.Markdown.ShouldBe(captured);
        });
    }

    [Fact]
    public void SetHeadingLevel_InsideATableCell_IsRefused_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "| Head |\n| --- |\n| Cell |" };
            VisualDocumentText.PlaceCaretIn(editor, "Cell");

            // A GFM table cell holds inline content only, so there is no Heading for it to become.
            MarkdownEditingCommands.SetHeadingLevel.CanExecute(parameter: 2, target: editor)
                .ShouldBeFalse();
        });
    }

    [Fact]
    public void SetHeadingLevel_InsideACodeBlock_IsRefused_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "```\ncode line\n```" };
            VisualDocumentText.PlaceCaretIn(editor, "code line");

            // A Code Block's text is code; relevelling one would turn its first line into prose.
            MarkdownEditingCommands.SetHeadingLevel.CanExecute(parameter: 2, target: editor)
                .ShouldBeFalse();
        });
    }

    [Theory]
    [InlineData(Key.D0, 0)]
    [InlineData(Key.D1, 1)]
    [InlineData(Key.D2, 2)]
    [InlineData(Key.D3, 3)]
    [InlineData(Key.D4, 4)]
    [InlineData(Key.D5, 5)]
    [InlineData(Key.D6, 6)]
    public void SetHeadingLevel_IsBoundToACtrlDigitGesture_INV027(Key key, int level)
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor();

            var binding = editor.InputBindings
                .OfType<KeyBinding>()
                .SingleOrDefault(b => b.Key == key && b.Modifiers == ModifierKeys.Control);

            binding.ShouldNotBeNull();
            binding.Command.ShouldBeSameAs(MarkdownEditingCommands.SetHeadingLevel);

            // A RoutedUICommand's own KeyGesture carries no parameter, so the level has to ride on
            // the KeyBinding — without it the gesture would name no level and releveled nothing.
            binding.CommandParameter.ShouldBe(level);
        });
    }

    [Fact]
    public void CtrlZero_TurnsAHeadingBackIntoAParagraph_INV027()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "## Introduction" };
            VisualDocumentText.PlaceCaretIn(editor, "Introduction");

            // Paragraph is a Heading's only exit, so its gesture must reach it as directly as the
            // levels' gestures reach them.
            var binding = editor.InputBindings
                .OfType<KeyBinding>()
                .Single(b => b.Key == Key.D0 && b.Modifiers == ModifierKeys.Control);
            ((RoutedCommand)binding.Command).Execute(binding.CommandParameter, editor);

            editor.Markdown.ShouldBe("Introduction");
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
