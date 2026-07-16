using System.Windows.Documents;
using Shouldly;
using UI.Controls;
using UI.Tests.Wysiwyg;
using Xunit;
using WpfList = System.Windows.Documents.List;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for the List Formatting Actions on the <see cref="MarkdownRichEditor"/>: Toggle Unordered
/// List and Toggle Ordered List turn paragraphs into a List, a List back into paragraphs, or one
/// List kind into the other; Toggle Task List marks the selected List Items or clears their Task
/// Markers. Every toggle preserves its List Items' content (INV-023) and Captures to canonical
/// Markdown (INV-018).
/// </summary>
public sealed class MarkdownRichEditorListTests
{
    [Fact]
    public void ToggleUnorderedList_OnParagraph_MakesUnorderedList_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            MarkdownEditingCommands.ToggleUnorderedList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- alpha");
        });
    }

    [Fact]
    public void ToggleOrderedList_OnParagraph_MakesOrderedList_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            MarkdownEditingCommands.ToggleOrderedList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("1. alpha");
        });
    }

    [Fact]
    public void ToggleUnorderedList_OnSelectedParagraphs_MakesOneListItemPerParagraph_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha\n\nbravo" };
            SelectWholeDocument(editor);

            MarkdownEditingCommands.ToggleUnorderedList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- alpha\n- bravo");
        });
    }

    [Fact]
    public void ToggleUnorderedList_OnUnorderedList_RestoresOneParagraphPerListItem_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- alpha\n- bravo" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            MarkdownEditingCommands.ToggleUnorderedList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("alpha\n\nbravo");
        });
    }

    [Fact]
    public void ToggleOrderedList_OnUnorderedList_ConvertsItRatherThanRemovingIt_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- alpha\n- bravo" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            MarkdownEditingCommands.ToggleOrderedList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("1. alpha\n2. bravo");
        });
    }

    [Fact]
    public void ToggleUnorderedList_OnOrderedList_ConvertsItRatherThanRemovingIt_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "1. alpha\n2. bravo" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            MarkdownEditingCommands.ToggleUnorderedList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- alpha\n- bravo");
        });
    }

    [Fact]
    public void ToggleUnorderedList_PreservesTheItemsInlineFormatting_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "**bold** and `code`" };
            VisualDocumentText.PlaceCaretIn(editor, "bold");

            MarkdownEditingCommands.ToggleUnorderedList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- **bold** and `code`");
        });
    }

    [Fact]
    public void ToggleUnorderedList_ResultRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha\n\nbravo" };
            SelectWholeDocument(editor);
            MarkdownEditingCommands.ToggleUnorderedList.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            // INV-018: a fresh Project of the captured source Captures back to the identical text.
            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }

    [Fact]
    public void ToggleTaskList_OnListItem_GivesItAnUncheckedTaskMarker_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- alpha\n- bravo" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            MarkdownEditingCommands.ToggleTaskList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- [ ] alpha\n- bravo");
        });
    }

    [Fact]
    public void ToggleTaskList_WhenEverySelectedItemIsMarked_RemovesTheMarkers_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] alpha\n- [x] bravo" };
            SelectWholeList(editor);

            MarkdownEditingCommands.ToggleTaskList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- alpha\n- bravo");
        });
    }

    [Fact]
    public void ToggleTaskList_WhenOnlySomeSelectedItemsAreMarked_MarksTheRest_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [x] alpha\n- bravo" };
            SelectWholeList(editor);

            // Converges on marked rather than flip-flopping — and an already-checked marker keeps
            // its state rather than being reset to unchecked.
            MarkdownEditingCommands.ToggleTaskList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- [x] alpha\n- [ ] bravo");
        });
    }

    [Fact]
    public void ToggleTaskList_CannotExecute_OutsideAList_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            // A Task Marker exists only on a List Item.
            MarkdownEditingCommands.ToggleTaskList
                .CanExecute(parameter: null, target: editor)
                .ShouldBeFalse();
        });
    }

    [Fact]
    public void ToggleTaskList_CanExecute_InsideAList_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- alpha" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            MarkdownEditingCommands.ToggleTaskList
                .CanExecute(parameter: null, target: editor)
                .ShouldBeTrue();
        });
    }

    [Fact]
    public void ToggleUnorderedList_OnTaskList_RemovesTheTaskMarkersWithTheList_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [x] alpha" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            // No Task Marker outlives the List Item that carried it.
            MarkdownEditingCommands.ToggleUnorderedList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("alpha");
        });
    }

    [Fact]
    public void ToggleTaskList_ResultRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- alpha\n- bravo" };
            SelectWholeList(editor);
            MarkdownEditingCommands.ToggleTaskList.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            captured.ShouldBe("- [ ] alpha\n- [ ] bravo");
            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }

    private static void SelectWholeDocument(MarkdownRichEditor editor)
    {
        var blocks = editor.Document.Blocks.ToList();
        editor.Selection.Select(blocks[0].ContentStart, blocks[^1].ContentEnd);
    }

    private static void SelectWholeList(MarkdownRichEditor editor)
    {
        var list = editor.Document.Blocks.OfType<WpfList>().First();
        editor.Selection.Select(list.ContentStart, list.ContentEnd);
    }
}
