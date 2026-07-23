using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using UI.Core;
using UI.Wysiwyg;

namespace UI.Controls;

// The Formatting Actions the command bar and keyboard invoke — code, links, images, strikethrough,
// block quotes, headings, tables, and lists — each delegating to the matching pure helper in
// UI.Wysiwyg so this control stays wiring. Every action edits the Visual Document and Captures back
// into the Markdown source like any other edit (INV-018). The editing input gestures (Enter in a Task
// List, a click on a Task Marker or a Mermaid Diagram) live here too, routing to those actions.
public sealed partial class MarkdownRichEditor
{
    /// <summary>
    /// Identifies the <see cref="LinkPrompt"/> dependency property. Insert Link and Insert Image ask
    /// through it for their text and URL; the composition root supplies the real Link Prompt, and a
    /// test supplies a stub. Left unset, neither action edits (INV-030).
    /// </summary>
    public static readonly DependencyProperty LinkPromptProperty = DependencyProperty.Register(
        nameof(LinkPrompt),
        typeof(ILinkPrompt),
        typeof(MarkdownRichEditor),
        new PropertyMetadata(defaultValue: null));

    /// <summary>
    /// The Link Prompt that Insert Link and Insert Image ask for a text and URL. Left
    /// <see langword="null"/>, neither action makes an edit (INV-030).
    /// </summary>
    public ILinkPrompt? LinkPrompt
    {
        get => (ILinkPrompt?)GetValue(LinkPromptProperty);
        set => SetValue(LinkPromptProperty, value);
    }

    /// <summary>
    /// Whether the caret currently sits inside a Table — the availability switch for the Table
    /// Formatting Actions: Insert Table runs only outside a Table; Add Row and Add Column run only
    /// inside one (INV-018).
    /// </summary>
    public bool IsCaretInTable => TableEditing.IsInTable(CaretPosition);

    /// <summary>
    /// Applies the Toggle Code Formatting Action at the current selection: a selection within a
    /// single line becomes a Code Span, a selection spanning multiple lines (or a whole line)
    /// becomes a Code Block, and inside existing code the code formatting is removed. The edit
    /// Captures back into <see cref="Markdown"/> like any other edit (INV-018).
    /// </summary>
    public void ToggleCodeAtSelection() => CodeFormatting.Toggle(this);

    /// <summary>
    /// Applies the Insert Link Formatting Action: asks the <see cref="LinkPrompt"/> for the Link's
    /// text (seeded with the selection) and destination URL, and turns the selection into that Link.
    /// No edit is made when the Link Prompt is dismissed or gives no URL (INV-030). The edit Captures
    /// back into <see cref="Markdown"/> like any other edit (INV-018).
    /// </summary>
    public void InsertLinkAtSelection() => LinkFormatting.InsertLink(this, LinkPrompt);

    /// <summary>
    /// Applies the Insert Image Formatting Action: asks the <see cref="LinkPrompt"/> for the Image's
    /// alt text (seeded with the selection) and source URL, and inserts that Image. No edit is made
    /// when the Link Prompt is dismissed or gives no URL (INV-030). The edit Captures back into
    /// <see cref="Markdown"/> like any other edit (INV-018).
    /// </summary>
    public void InsertImageAtSelection() => LinkFormatting.InsertImage(this, LinkPrompt, BaseDirectory);

    /// <summary>
    /// Applies the Toggle Strikethrough Formatting Action at the current selection: the selection is
    /// struck through, or struck-through prose is restored to plain text — whether that
    /// Strikethrough was loaded or applied by a previous toggle (INV-029). The edit Captures back
    /// into <see cref="Markdown"/> like any other edit (INV-018).
    /// </summary>
    public void ToggleStrikethroughAtSelection() => StrikethroughFormatting.Toggle(this);

    /// <summary>
    /// Applies the Toggle Block Quote Formatting Action at the current selection: the whole blocks
    /// the selection touches become a Block Quote, or the selected Block Quote's blocks become plain
    /// blocks again (INV-028). The edit Captures back into <see cref="Markdown"/> like any other
    /// edit (INV-018).
    /// </summary>
    public void ToggleBlockQuoteAtSelection() => QuoteFormatting.Toggle(this);

    /// <summary>
    /// Applies the Set Heading Level Formatting Action at the caret: the block at the caret becomes a
    /// Heading at <paramref name="level"/> (1–6), or a plain paragraph again given
    /// <see cref="MarkdownEditingCommands.ParagraphHeadingLevel"/>. It sets a level rather than
    /// toggling one, and its content survives the change (INV-027). The edit Captures back into
    /// <see cref="Markdown"/> like any other edit (INV-018).
    /// </summary>
    /// <param name="level">The Heading Level to set, or the Paragraph level to clear the Heading.</param>
    public void SetHeadingLevelAtCaret(int level) => HeadingFormatting.SetLevel(this, level);

    // Ctrl+1..Ctrl+6 set a Heading Level and Ctrl+0 clears it back to a paragraph. Set Heading Level
    // takes the level as a command parameter, and a RoutedUICommand's own KeyGesture carries none —
    // so the gestures ride on KeyBindings, which do. Paragraph is a Heading's only exit (the action
    // never toggles), so it is bound as directly as the levels it undoes (INV-027).
    private void RegisterHeadingLevelGestures()
    {
        var keys = new[] { Key.D0, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6 };
        for (var level = 0; level < keys.Length; level++)
        {
            InputBindings.Add(new KeyBinding(MarkdownEditingCommands.SetHeadingLevel, keys[level], ModifierKeys.Control)
            {
                CommandParameter = level,
            });
        }
    }

    // The Heading Level Picker's XAML passes its CommandParameter as a string ("2"), while a test or
    // caller passes an int — so the parameter is resolved to a level before the action runs. An
    // unreadable parameter names no level, and so relevels nothing.
    private void SetHeadingLevelAtCaret(object? parameter)
    {
        if (parameter is int level)
        {
            SetHeadingLevelAtCaret(level);
        }
        else if (int.TryParse(parameter?.ToString(), out var parsed))
        {
            SetHeadingLevelAtCaret(parsed);
        }
    }

    /// <summary>
    /// Applies the Insert Table Formatting Action: inserts a new three-column Table (header row plus
    /// two empty body rows) at the caret and selects the first header cell. No-op while the caret is
    /// inside a Table (INV-018).
    /// </summary>
    public void InsertTableAtCaret() => TableEditing.InsertTable(this);

    /// <summary>
    /// Applies the Add Row Formatting Action: inserts a new empty row below the caret's row, at the
    /// Table's column count (INV-019). No-op while the caret is not inside a Table.
    /// </summary>
    public void AddTableRowAtCaret() => TableEditing.AddRow(this);

    /// <summary>
    /// Applies the Add Column Formatting Action: inserts a new empty column to the right of the
    /// caret's column, extending every row (INV-019). No-op while the caret is not inside a Table.
    /// </summary>
    public void AddTableColumnAtCaret() => TableEditing.AddColumn(this);

    /// <summary>
    /// Applies the Remove Row Formatting Action: deletes the caret's row from its Table (INV-019).
    /// No-op while the caret is not inside a Table, or is in its header row.
    /// </summary>
    public void RemoveTableRowAtCaret() => TableEditing.RemoveRow(this);

    /// <summary>
    /// Applies the Remove Column Formatting Action: deletes the caret's column from its Table,
    /// shrinking every row and dropping that column's alignment (INV-019). No-op while the caret is
    /// not inside a Table, or the Table has only one column.
    /// </summary>
    public void RemoveTableColumnAtCaret() => TableEditing.RemoveColumn(this);

    /// <summary>
    /// Applies the Toggle Unordered List Formatting Action at the current selection: the selected
    /// paragraphs become an Unordered List, an Unordered List becomes plain paragraphs again, and an
    /// Ordered List is converted rather than removed. The items' content is preserved (INV-023) and
    /// the edit Captures back into <see cref="Markdown"/> like any other (INV-018).
    /// </summary>
    public void ToggleUnorderedListAtSelection() => ListFormatting.ToggleUnordered(this);

    /// <summary>
    /// Applies the Toggle Ordered List Formatting Action at the current selection — the counterpart
    /// of <see cref="ToggleUnorderedListAtSelection"/> (INV-018, INV-023).
    /// </summary>
    public void ToggleOrderedListAtSelection() => ListFormatting.ToggleOrdered(this);

    /// <summary>
    /// Applies the Toggle Task List Formatting Action at the current selection: gives every selected
    /// List Item lacking one an unchecked Task Marker, or clears them all when every selected List
    /// Item already carries one. No-op while the selection is not inside a List (INV-023).
    /// </summary>
    public void ToggleTaskListAtSelection() => ListFormatting.ToggleTaskList(this);

    /// <summary>
    /// Continues a Task List across a paragraph break: when the caret sits in a task item, breaks the
    /// line and gives the new List Item its own unchecked Task Marker, the way a bullet or a number
    /// carries to the next item (INV-023). Called by the control's Enter handling.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the break was handled; <see langword="false"/> when the caret is
    /// not in a task item, so Enter should behave as it normally does.
    /// </returns>
    public bool ContinueTaskListAtCaret() => ListFormatting.TryContinueTaskList(this);

    /// <summary>
    /// Gives the List Item at the caret an unchecked Task Marker when the item before it has one and
    /// it does not — the rule that makes Enter continue a Task List (INV-023). A no-op anywhere else.
    /// </summary>
    /// <returns><see langword="true"/> when a Task Marker was added.</returns>
    public bool MarkContinuedTaskItemAtCaret() => ListFormatting.MarkContinuedTaskItem(this);

    /// <summary>
    /// Applies the Toggle Task Marker edit: flips the Task Marker at <paramref name="position"/>
    /// between unchecked and checked, changing nothing else (INV-024).
    /// </summary>
    /// <param name="position">The position to toggle at — where the user clicked.</param>
    /// <returns>
    /// <see langword="true"/> when a Task Marker was toggled; <see langword="false"/> when the
    /// position is not on one, so the click should place the caret as usual.
    /// </returns>
    public bool ToggleTaskMarkerAt(TextPointer? position) => TaskMarkerEditing.Toggle(this, position);

    /// <inheritdoc />
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        // Enter in a Task List carries the checkbox to the new item, the way WPF carries a bullet or
        // a number (INV-023). Shift+Enter is a soft break within the same item, so it is left alone.
        if (e.Key == Key.Return && Keyboard.Modifiers == ModifierKeys.None && ContinueTaskListAtCaret())
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // Double-clicking a Mermaid Diagram's picture opens the Flowchart Builder on that diagram: the
        // caret is placed on its block so Open Flowchart Builder seeds from — and Insert replaces — it
        // (INV-047/INV-053). The block is found by the text hit-test (GetPositionFromPoint), since a
        // RichTextBox overlays a text layer over embedded elements — a visual hit-test misses them.
        if (e.ClickCount == 2 &&
            VisualDocumentTraversal.TopLevelBlockOf(GetPositionFromPoint(e.GetPosition(this), snapToText: true))
                is BlockUIContainer { Tag: MermaidDiagramRole } container)
        {
            CaretPosition = container.ContentStart;
            OpenFlowchartBuilderAtCaret();
            e.Handled = true;
            return;
        }

        // A click on a Task Marker's checkbox toggles it (INV-024). snapToText is off so only a click
        // on the checkbox itself resolves to it; every other click falls through to the base class and
        // places the caret exactly as it always has.
        if (ToggleTaskMarkerAt(GetPositionFromPoint(e.GetPosition(this), snapToText: false)))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }
}
