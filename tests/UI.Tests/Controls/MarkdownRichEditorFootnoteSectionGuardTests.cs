using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests that the block-spanning Formatting Actions leave the Footnote Section alone. Project composes
/// it at the end of the Visual Document rather than the author writing it, so an action that moved it
/// into a Block Quote or turned its notes into List Items would capture the notes somewhere they were
/// never written (INV-065).
/// </summary>
public sealed class MarkdownRichEditorFootnoteSectionGuardTests
{
    private const string Markdown = "A claim.[^a]\n\n[^a]: the note\n\nMore prose.";

    [Fact]
    public void ToggleBlockQuote_OverTheWholeDocument_LeavesTheNoteOutsideTheQuote_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = Markdown };
            editor.SelectAll();

            MarkdownEditingCommands.ToggleBlockQuote.Execute(parameter: null, target: editor);

            // The prose is quoted; the note is still a Footnote Definition, not a quoted line.
            editor.Markdown.ShouldContain("[^a]: the note");
            editor.Markdown.ShouldNotContain("> [^a]");
        });
    }

    [Fact]
    public void ToggleUnorderedList_OverTheWholeDocument_LeavesTheNoteOutsideTheList_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = Markdown };
            editor.SelectAll();

            MarkdownEditingCommands.ToggleUnorderedList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldContain("[^a]: the note");
            editor.Markdown.ShouldNotContain("- [^a]");
        });
    }

    [Fact]
    public void ToggleCode_OverTheWholeDocument_LeavesTheNoteReadable_INV065()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = Markdown };
            editor.SelectAll();

            MarkdownEditingCommands.ToggleCode.Execute(parameter: null, target: editor);

            // Whatever it does to the prose, the note is still a Definition rather than fenced code.
            editor.Markdown.ShouldContain("[^a]:");
        });
    }
}
