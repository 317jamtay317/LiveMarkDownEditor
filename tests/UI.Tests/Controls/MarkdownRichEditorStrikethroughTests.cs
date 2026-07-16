using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the Toggle Strikethrough Formatting Action on the <see cref="MarkdownRichEditor"/>: the
/// selection is struck through, or struck-through prose is restored to plain text. It is symmetric
/// over where the Strikethrough came from (INV-029), and every result must Capture to canonical
/// Markdown (INV-018).
/// </summary>
public sealed class MarkdownRichEditorStrikethroughTests
{
    [Fact]
    public void ToggleStrikethrough_OnSelection_StrikesItThrough_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "keep this drop this" };
            VisualDocumentText.SelectText(editor, "drop this");

            MarkdownEditingCommands.ToggleStrikethrough.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("keep this ~~drop this~~");
        });
    }

    [Fact]
    public void ToggleStrikethrough_OnALoadedStrikethrough_RemovesIt_INV029()
    {
        StaThread.Run(() =>
        {
            // The Strikethrough here came from the Projector, not from a previous toggle: the two
            // are the same thing to the user, so the action must undo both alike.
            var editor = new MarkdownRichEditor { Markdown = "keep this ~~drop this~~" };
            VisualDocumentText.SelectText(editor, "drop this");

            MarkdownEditingCommands.ToggleStrikethrough.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("keep this drop this");
        });
    }

    [Fact]
    public void ToggleStrikethrough_AppliedTwice_RestoresPlainText_INV029()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "keep this drop this" };
            VisualDocumentText.SelectText(editor, "drop this");

            MarkdownEditingCommands.ToggleStrikethrough.Execute(parameter: null, target: editor);
            VisualDocumentText.SelectText(editor, "drop this");
            MarkdownEditingCommands.ToggleStrikethrough.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("keep this drop this");
        });
    }

    [Fact]
    public void ToggleStrikethrough_PreservesOtherInlineFormatting_INV029()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "keep **bold text** here" };
            VisualDocumentText.SelectText(editor, "bold text");

            MarkdownEditingCommands.ToggleStrikethrough.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("keep ~~**bold text**~~ here");
        });
    }

    [Fact]
    public void ToggleStrikethrough_WithEmptySelection_LeavesTheDocumentUnchanged_INV029()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "nothing selected" };
            VisualDocumentText.PlaceCaretIn(editor, "nothing");

            MarkdownEditingCommands.ToggleStrikethrough.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("nothing selected");
        });
    }

    [Fact]
    public void ToggleStrikethrough_WithTrailingSpaceInSelection_KeepsTheSpaceOutsideTheDelimiters_INV018()
    {
        StaThread.Run(() =>
        {
            // A user selecting a word by double-click or Ctrl+Shift+Right takes its trailing space
            // with it. `~~two ~~` does not close in Markdown — the closing delimiter is preceded by
            // a space — so the strike must sit inside the spaces, not around them.
            var editor = new MarkdownRichEditor { Markdown = "one two three" };
            VisualDocumentText.SelectText(editor, "two ");

            MarkdownEditingCommands.ToggleStrikethrough.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("one ~~two~~ three");
        });
    }

    [Fact]
    public void ToggleStrikethrough_WithTrailingSpaceInSelection_StillRoundTrips_INV005()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "one two three" };
            VisualDocumentText.SelectText(editor, "two ");

            MarkdownEditingCommands.ToggleStrikethrough.Execute(parameter: null, target: editor);

            var captured = editor.Markdown;
            var reloaded = new MarkdownRichEditor { Markdown = captured };
            reloaded.Markdown.ShouldBe(captured);
        });
    }

    /// <summary>
    /// The whitespace rule is Capture's, not Toggle Strikethrough's, so it holds for the emphasis
    /// Formatting Actions that predate it: <c>**bold **</c> does not close in Markdown either.
    /// </summary>
    [Fact]
    public void ToggleBold_WithTrailingSpaceInSelection_KeepsTheSpaceOutsideTheDelimiters_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "one two three" };
            VisualDocumentText.SelectText(editor, "two ");

            EditingCommands.ToggleBold.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("one **two** three");
        });
    }

    [Fact]
    public void ToggleStrikethrough_CapturesCanonicalMarkdown_ThatRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "keep this drop this" };
            VisualDocumentText.SelectText(editor, "drop this");

            MarkdownEditingCommands.ToggleStrikethrough.Execute(parameter: null, target: editor);

            var captured = editor.Markdown;
            var reloaded = new MarkdownRichEditor { Markdown = captured };
            reloaded.Markdown.ShouldBe(captured);
        });
    }
}
