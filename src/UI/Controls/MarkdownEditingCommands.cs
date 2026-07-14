using System.Windows.Input;

namespace UI.Controls;

/// <summary>
/// Routed commands the <see cref="MarkdownRichEditor"/> handles for Folding Sections, mirroring the
/// way WPF's <see cref="EditingCommands"/> expose formatting. A command bar button or key gesture
/// raises one of these against the editor (via <c>CommandTarget</c>); the editor's command bindings
/// carry it out on the Section at the caret.
/// </summary>
public static class MarkdownEditingCommands
{
    /// <summary>Folds or Unfolds the Section that contains the caret (Ctrl+M).</summary>
    public static RoutedUICommand ToggleFold { get; } = new(
        "Collapse / expand section",
        nameof(ToggleFold),
        typeof(MarkdownEditingCommands),
        [new KeyGesture(Key.M, ModifierKeys.Control)]);

    /// <summary>Folds every Section, collapsing the document to its Section Headings.</summary>
    public static RoutedUICommand CollapseAllFolds { get; } = new(
        "Collapse all sections",
        nameof(CollapseAllFolds),
        typeof(MarkdownEditingCommands));

    /// <summary>Unfolds every Folded Section, restoring the full document (Ctrl+Shift+M).</summary>
    public static RoutedUICommand ExpandAllFolds { get; } = new(
        "Expand all sections",
        nameof(ExpandAllFolds),
        typeof(MarkdownEditingCommands),
        [new KeyGesture(Key.M, ModifierKeys.Control | ModifierKeys.Shift)]);

    /// <summary>
    /// The Toggle Code Formatting Action: the selection becomes a Code Span (within a single line) or
    /// a Code Block (spanning multiple lines, or a whole line); inside existing code it removes the
    /// code formatting instead (INV-018).
    /// </summary>
    public static RoutedUICommand ToggleCode { get; } = new(
        "Toggle code",
        nameof(ToggleCode),
        typeof(MarkdownEditingCommands));

    /// <summary>
    /// The Insert Table Formatting Action: inserts a new three-column Table (header row plus two
    /// empty body rows) at the caret. Available only while the caret is not inside a Table (INV-018).
    /// </summary>
    public static RoutedUICommand InsertTable { get; } = new(
        "Add table",
        nameof(InsertTable),
        typeof(MarkdownEditingCommands));

    /// <summary>
    /// The Add Row Formatting Action: inserts a new empty row below the caret's row, at the Table's
    /// column count (INV-019). Available only while the caret is inside a Table.
    /// </summary>
    public static RoutedUICommand AddTableRow { get; } = new(
        "Add row",
        nameof(AddTableRow),
        typeof(MarkdownEditingCommands));

    /// <summary>
    /// The Add Column Formatting Action: inserts a new empty column to the right of the caret's
    /// column, extending every row (INV-019). Available only while the caret is inside a Table.
    /// </summary>
    public static RoutedUICommand AddTableColumn { get; } = new(
        "Add column",
        nameof(AddTableColumn),
        typeof(MarkdownEditingCommands));

    /// <summary>Opens the Find Bar and focuses its query box (Ctrl+F).</summary>
    public static RoutedUICommand ShowFind { get; } = new(
        "Find",
        nameof(ShowFind),
        typeof(MarkdownEditingCommands),
        [new KeyGesture(Key.F, ModifierKeys.Control)]);

    /// <summary>Closes the Find Bar and clears the Find highlights.</summary>
    public static RoutedUICommand HideFind { get; } = new(
        "Close find",
        nameof(HideFind),
        typeof(MarkdownEditingCommands));

    /// <summary>Moves the Current Match to the next Match, wrapping past the last (F3).</summary>
    public static RoutedUICommand FindNext { get; } = new(
        "Find next",
        nameof(FindNext),
        typeof(MarkdownEditingCommands),
        [new KeyGesture(Key.F3)]);

    /// <summary>Moves the Current Match to the previous Match, wrapping past the first (Shift+F3).</summary>
    public static RoutedUICommand FindPrevious { get; } = new(
        "Find previous",
        nameof(FindPrevious),
        typeof(MarkdownEditingCommands),
        [new KeyGesture(Key.F3, ModifierKeys.Shift)]);
}
