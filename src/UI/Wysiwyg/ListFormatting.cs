using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfList = System.Windows.Documents.List;

namespace UI.Wysiwyg;

/// <summary>
/// The List Formatting Actions: Toggle Unordered List and Toggle Ordered List turn the selected
/// paragraphs into a List, the selected List back into paragraphs, or one List kind into the other;
/// Toggle Task List gives the selected List Items their Task Markers, or clears them. Every toggle
/// preserves its items' content (INV-023). The Projector composes a List through the same
/// <see cref="ApplyList"/> method, so Capture treats a user-built List and a loaded one uniformly
/// (INV-018).
/// </summary>
internal static class ListFormatting
{
    /// <summary>
    /// The Toggle Unordered List Formatting Action: the selected paragraphs become an Unordered
    /// List, an Unordered List becomes plain paragraphs again, and an Ordered List is converted
    /// rather than removed.
    /// </summary>
    /// <param name="editor">The editor whose selection is being formatted.</param>
    internal static void ToggleUnordered(RichTextBox editor) => Toggle(editor, ordered: false);

    /// <summary>
    /// The Toggle Ordered List Formatting Action: the selected paragraphs become an Ordered List, an
    /// Ordered List becomes plain paragraphs again, and an Unordered List is converted rather than
    /// removed.
    /// </summary>
    /// <param name="editor">The editor whose selection is being formatted.</param>
    internal static void ToggleOrdered(RichTextBox editor) => Toggle(editor, ordered: true);

    /// <summary>
    /// Whether Toggle Task List can run: the selection touches at least one List Item, because a
    /// Task Marker exists only on a List Item (INV-023).
    /// </summary>
    /// <param name="editor">The editor whose selection is queried.</param>
    internal static bool CanToggleTaskList(RichTextBox editor) => SelectedItems(editor).Count > 0;

    /// <summary>
    /// The Toggle Task List Formatting Action: gives every selected List Item lacking one an
    /// unchecked Task Marker, or — when every selected List Item already carries one — removes them
    /// all. A partly-marked selection therefore converges on marked (INV-023).
    /// </summary>
    /// <param name="editor">The editor whose List Items are being marked.</param>
    internal static void ToggleTaskList(RichTextBox editor)
    {
        var items = SelectedItems(editor);
        if (items.Count == 0)
        {
            return;
        }

        editor.BeginChange();
        try
        {
            var clearing = items.TrueForAll(item => TaskMarkerEditing.MarkerOf(FirstParagraphOf(item)) is not null);
            foreach (var paragraph in items.Select(FirstParagraphOf).OfType<Paragraph>())
            {
                if (clearing)
                {
                    RemoveTaskMarker(paragraph);
                }
                else
                {
                    AddTaskMarker(paragraph);
                }
            }
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// Dresses a List with the marker its kind calls for, exactly as the Projector does — a bullet
    /// for an Unordered List, incrementing numbers for an Ordered List. A List carries no role tag:
    /// its kind rides on <see cref="WpfList.MarkerStyle"/>, which is what Capture reads (INV-018).
    /// </summary>
    /// <param name="list">The List to dress.</param>
    /// <param name="ordered">Whether it is an Ordered List.</param>
    internal static void ApplyList(WpfList list, bool ordered)
    {
        list.MarkerStyle = ordered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc;
        list.Margin = BodySpacing;
    }

    // Toggling to the kind a List already is removes the List; toggling to the other kind converts
    // it, so reaching for the other kind never silently destroys a List (INV-023).
    private static void Toggle(RichTextBox editor, bool ordered)
    {
        editor.BeginChange();
        try
        {
            var items = SelectedItems(editor);
            if (items.Count > 0 && items[0].Parent is WpfList list)
            {
                if (IsOrdered(list) == ordered)
                {
                    Unwrap(list);
                }
                else
                {
                    ApplyList(list, ordered);
                }

                return;
            }

            Wrap(editor, ordered);
        }
        finally
        {
            editor.EndChange();
        }
    }

    // Moves each selected top-level paragraph into a new List — moved, not rebuilt, so the items'
    // inline formatting survives untouched (INV-023).
    private static void Wrap(RichTextBox editor, bool ordered)
    {
        var document = editor.Document;
        var blocks = document.Blocks.ToList();
        var first = VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.Start);
        var last = VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.End);
        var startIndex = first is null ? -1 : blocks.IndexOf(first);
        var endIndex = last is null ? -1 : blocks.IndexOf(last);
        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        var list = new WpfList();
        ApplyList(list, ordered);
        document.Blocks.InsertBefore(blocks[startIndex], list);

        for (var i = startIndex; i <= endIndex; i++)
        {
            if (blocks[i] is not Paragraph paragraph)
            {
                continue;
            }

            document.Blocks.Remove(paragraph);
            paragraph.Margin = new Thickness(0);
            list.ListItems.Add(new ListItem(paragraph));
        }

        if (list.ListItems.Count == 0)
        {
            document.Blocks.Remove(list);
            return;
        }

        var lastParagraph = (Paragraph)list.ListItems.Last().Blocks.LastBlock!;
        editor.Selection.Select(lastParagraph.ContentEnd, lastParagraph.ContentEnd);
    }

    // Lifts every List Item's blocks back out to where the List sat, dropping the Task Markers with
    // it: no Task Marker outlives the List Item that carried it (INV-023).
    private static void Unwrap(WpfList list)
    {
        if (ContainerOf(list) is not { } container)
        {
            return;
        }

        foreach (var item in list.ListItems.ToList())
        {
            list.ListItems.Remove(item);
            foreach (var block in item.Blocks.ToList())
            {
                item.Blocks.Remove(block);
                if (block is Paragraph paragraph)
                {
                    RemoveTaskMarker(paragraph);
                    paragraph.Margin = BodySpacing;
                }

                container.InsertBefore(list, block);
            }
        }

        container.Remove(list);
    }

    // The block collection the List sits in — the document, or an enclosing List Item or Section for
    // a nested List.
    private static BlockCollection? ContainerOf(WpfList list) => list.Parent switch
    {
        FlowDocument document => document.Blocks,
        ListItem item => item.Blocks,
        Section section => section.Blocks,
        _ => null,
    };

    private static void AddTaskMarker(Paragraph paragraph)
    {
        if (TaskMarkerEditing.MarkerOf(paragraph) is not null)
        {
            return;
        }

        var marker = TaskMarkerEditing.CreateMarker(isChecked: false);
        if (paragraph.Inlines.FirstInline is { } firstInline)
        {
            paragraph.Inlines.InsertBefore(firstInline, marker);
        }
        else
        {
            paragraph.Inlines.Add(marker);
        }
    }

    private static void RemoveTaskMarker(Paragraph paragraph)
    {
        if (TaskMarkerEditing.MarkerOf(paragraph) is { } marker)
        {
            paragraph.Inlines.Remove(marker);
        }
    }

    // Every List Item the selection touches, in document order. An empty selection (a caret) yields
    // the single List Item holding it.
    private static List<ListItem> SelectedItems(RichTextBox editor)
    {
        var items = new List<ListItem>();
        var selection = editor.Selection;

        for (var pointer = selection.Start;
             pointer is not null && pointer.CompareTo(selection.End) <= 0;
             pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (VisualDocumentTraversal.AncestorOf<ListItem>(pointer) is { } item && !items.Contains(item))
            {
                items.Add(item);
            }
        }

        return items;
    }

    private static Paragraph? FirstParagraphOf(ListItem item) => item.Blocks.FirstBlock as Paragraph;

    private static bool IsOrdered(WpfList list) => list.MarkerStyle == TextMarkerStyle.Decimal;

    // The uniform block spacing the Projector gives body blocks.
    private static readonly Thickness BodySpacing = new(0, 0, 0, 6);
}
