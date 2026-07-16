using System.Windows;
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
    public void ToggleTaskList_CanExecute_OutsideAList_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            // A Task Marker exists only on a List Item — so Toggle Task List makes the List rather
            // than standing there disabled until the user makes one themselves.
            MarkdownEditingCommands.ToggleTaskList
                .CanExecute(parameter: null, target: editor)
                .ShouldBeTrue();
        });
    }

    [Fact]
    public void ToggleTaskList_OnParagraph_MakesAOneItemTaskList_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            MarkdownEditingCommands.ToggleTaskList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- [ ] alpha");
        });
    }

    [Fact]
    public void ToggleTaskList_OnSelectedParagraphs_MakesATaskListOfThemAll_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha\n\nbravo" };
            SelectWholeDocument(editor);

            MarkdownEditingCommands.ToggleTaskList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- [ ] alpha\n- [ ] bravo");
        });
    }

    [Fact]
    public void ToggleTaskList_OnParagraphs_ResultRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha\n\nbravo" };
            SelectWholeDocument(editor);
            MarkdownEditingCommands.ToggleTaskList.Execute(parameter: null, target: editor);
            var captured = editor.Markdown;

            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }

    [Fact]
    public void TaskList_ShowsNoBullet_BecauseTheCheckboxIsTheMarker_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] alpha\n- [x] bravo" };

            // A checkbox beside a bullet is one marker too many: the checkbox is the item's marker.
            ListOf(editor).MarkerStyle.ShouldBe(TextMarkerStyle.None);
        });
    }

    [Fact]
    public void OrderedTaskList_KeepsItsNumbers_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "1. [ ] alpha\n2. [x] bravo" };

            // Only the bullet is redundant beside a checkbox — the numbers still carry order.
            ListOf(editor).MarkerStyle.ShouldBe(TextMarkerStyle.Decimal);
            editor.Markdown.ShouldBe("1. [ ] alpha\n2. [x] bravo");
        });
    }

    [Fact]
    public void PartlyMarkedList_KeepsItsBullets_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- alpha\n- [ ] bravo" };

            // WPF gives a List one marker for all its items, so the bullet only goes away once
            // every item is a task item; otherwise the unmarked items would lose their marker.
            ListOf(editor).MarkerStyle.ShouldBe(TextMarkerStyle.Disc);
        });
    }

    [Fact]
    public void ToggleTaskList_RemovingTheMarkers_RestoresTheBullets_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] alpha\n- [x] bravo" };
            SelectWholeList(editor);

            MarkdownEditingCommands.ToggleTaskList.Execute(parameter: null, target: editor);

            editor.Markdown.ShouldBe("- alpha\n- bravo");
            ListOf(editor).MarkerStyle.ShouldBe(TextMarkerStyle.Disc);
        });
    }

    [Fact]
    public void ToggleTaskList_MarkingEveryItem_TakesTheBulletsAway_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- alpha\n- bravo" };
            SelectWholeList(editor);

            MarkdownEditingCommands.ToggleTaskList.Execute(parameter: null, target: editor);

            ListOf(editor).MarkerStyle.ShouldBe(TextMarkerStyle.None);
        });
    }

    // Enter itself runs through WPF's EnterParagraphBreak, which needs a focused editor and so does
    // nothing headless — these cover the rule that turns the item it creates into a task item. That
    // the break reaches this rule at all is verified by driving the real app.

    [Fact]
    public void ContinueTaskList_WhenThePreviousItemIsATask_MarksTheNewItem_INV023()
    {
        StaThread.Run(() =>
        {
            // The state Enter leaves behind: a fresh, unmarked item after a task item.
            var editor = new MarkdownRichEditor { Markdown = "- [ ] alpha\n- bravo" };
            VisualDocumentText.PlaceCaretIn(editor, "bravo");

            editor.MarkContinuedTaskItemAtCaret().ShouldBeTrue();

            editor.Markdown.ShouldBe("- [ ] alpha\n- [ ] bravo");
        });
    }

    [Fact]
    public void ContinueTaskList_KeepsTheBulletsAway_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] alpha\n- bravo" };
            VisualDocumentText.PlaceCaretIn(editor, "bravo");

            editor.MarkContinuedTaskItemAtCaret();

            // Every item is a task item again, so the List goes back to showing no bullets rather
            // than leaving them on the moment the user pressed Enter.
            ListOf(editor).MarkerStyle.ShouldBe(TextMarkerStyle.None);
        });
    }

    [Fact]
    public void ContinueTaskList_ThenTypingTheLabel_ReachesTheMarkdown_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] alpha\n- " };
            var emptyItem = ListOf(editor).ListItems.Last();
            editor.CaretPosition = emptyItem.ContentStart;
            editor.MarkContinuedTaskItemAtCaret().ShouldBeTrue();

            // A new task item's marker is its only inline, so WPF normalises the caret into the
            // marker's own Run and the label the user types lands there. It must still reach the
            // Markdown rather than being dropped as part of the marker.
            TypeAtCaret(editor, "bravo");

            editor.Markdown.ShouldBe("- [ ] alpha\n- [ ] bravo");
        });
    }

    // Types the way WPF's editor does: into whichever Run the caret has been normalised into.
    private static void TypeAtCaret(MarkdownRichEditor editor, string text)
    {
        if (editor.CaretPosition.Parent is Run run)
        {
            var caretOffset = new TextRange(run.ContentStart, editor.CaretPosition).Text.Length;
            run.Text = run.Text.Insert(Math.Min(caretOffset, run.Text.Length), text);
            return;
        }

        _ = new Run(text, editor.CaretPosition);
    }

    [Fact]
    public void ContinueTaskList_InAPlainList_LeavesTheNewItemAlone_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- alpha\n- bravo" };
            VisualDocumentText.PlaceCaretIn(editor, "bravo");

            // Not a Task List: WPF already carries the bullet, so Enter is none of our business.
            editor.MarkContinuedTaskItemAtCaret().ShouldBeFalse();

            editor.Markdown.ShouldBe("- alpha\n- bravo");
        });
    }

    [Fact]
    public void ContinueTaskList_OnTheFirstItem_LeavesItAlone_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- alpha" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            // No item before it to continue from.
            editor.MarkContinuedTaskItemAtCaret().ShouldBeFalse();

            editor.Markdown.ShouldBe("- alpha");
        });
    }

    [Fact]
    public void ContinueTaskList_OutsideAList_LeavesItAlone_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "alpha" };
            VisualDocumentText.PlaceCaretIn(editor, "alpha");

            editor.MarkContinuedTaskItemAtCaret().ShouldBeFalse();

            editor.Markdown.ShouldBe("alpha");
        });
    }

    [Fact]
    public void ContinueTaskList_DoesNotRemarkAnItemThatAlreadyHasACheckbox_INV023()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] alpha\n- [x] bravo" };
            VisualDocumentText.PlaceCaretIn(editor, "bravo");

            // Already a task item — and a checked one, which must not be reset to unchecked.
            editor.MarkContinuedTaskItemAtCaret().ShouldBeFalse();

            editor.Markdown.ShouldBe("- [ ] alpha\n- [x] bravo");
        });
    }

    [Fact]
    public void ContinueTaskList_ResultRoundTrips_INV018()
    {
        StaThread.Run(() =>
        {
            var editor = new MarkdownRichEditor { Markdown = "- [ ] alpha\n- bravo" };
            VisualDocumentText.PlaceCaretIn(editor, "bravo");
            editor.MarkContinuedTaskItemAtCaret();
            var captured = editor.Markdown;

            var reopened = new MarkdownRichEditor { Markdown = captured };

            reopened.Capture().ShouldBe(captured);
        });
    }

    private static WpfList ListOf(MarkdownRichEditor editor) =>
        editor.Document.Blocks.OfType<WpfList>().First();

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
