using System.Windows.Input;
using UI.Wysiwyg;

namespace UI.Controls;

/// <summary>
/// Routed commands the <see cref="MarkdownRichEditor"/> handles for Folding Sections, mirroring the
/// way WPF's <see cref="EditingCommands"/> expose formatting. A command bar button or key gesture
/// raises one of these against the editor (via <c>CommandTarget</c>); the editor's command bindings
/// carry it out on the Section at the caret.
/// </summary>
public static class MarkdownEditingCommands
{
    /// <summary>
    /// Prints the whole document (Ctrl+P). The Visual Document is re-projected from the current
    /// Markdown source before printing, so a Folded Section's hidden body prints too and the live
    /// editing surface is left undisturbed. Printing is not an edit (INV-034).
    /// </summary>
    public static RoutedUICommand Print { get; } = new(
        "Print",
        nameof(Print),
        typeof(MarkdownEditingCommands),
        [new KeyGesture(Key.P, ModifierKeys.Control)]);

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
    /// The Insert Link Formatting Action: turns the selection into a Link, or inserts a new one at
    /// the caret, asking for its text and destination URL through the Link Prompt (Ctrl+K). No edit
    /// is made when the Link Prompt is dismissed or gives no URL (INV-030).
    /// </summary>
    public static RoutedUICommand InsertLink { get; } = new(
        "Insert link",
        nameof(InsertLink),
        typeof(MarkdownEditingCommands),
        [new KeyGesture(Key.K, ModifierKeys.Control)]);

    /// <summary>
    /// The Insert Image Formatting Action: inserts an Image at the caret, asking for its alt text
    /// and source URL through the Link Prompt. No edit is made when the Link Prompt is dismissed or
    /// gives no URL (INV-030).
    /// </summary>
    public static RoutedUICommand InsertImage { get; } = new(
        "Insert image",
        nameof(InsertImage),
        typeof(MarkdownEditingCommands));

    /// <summary>
    /// The Toggle Strikethrough Formatting Action: the selection is struck through, or
    /// struck-through prose is restored to plain text. It removes a Strikethrough the Projector
    /// loaded exactly as readily as one a previous toggle applied (INV-029).
    /// </summary>
    public static RoutedUICommand ToggleStrikethrough { get; } = new(
        "Strikethrough",
        nameof(ToggleStrikethrough),
        typeof(MarkdownEditingCommands));

    /// <summary>
    /// The Toggle Block Quote Formatting Action: the blocks the selection touches become a Block
    /// Quote, or the selected Block Quote's blocks become plain blocks again. Whole blocks are
    /// quoted (INV-028).
    /// </summary>
    public static RoutedUICommand ToggleBlockQuote { get; } = new(
        "Block quote",
        nameof(ToggleBlockQuote),
        typeof(MarkdownEditingCommands));

    /// <summary>
    /// The <see cref="SetHeadingLevel"/> parameter that means "not a Heading" — the Heading Level
    /// Picker's Paragraph choice, which turns the Heading at the caret back into a plain paragraph.
    /// </summary>
    public const int ParagraphHeadingLevel = HeadingFormatting.ParagraphLevel;

    /// <summary>
    /// The Set Heading Level Formatting Action: makes the block at the caret a Heading at the
    /// Heading Level given as the command parameter (1–6), or a plain paragraph again given
    /// <see cref="ParagraphHeadingLevel"/>. It sets a level rather than toggling one, so choosing a
    /// Heading's current level leaves it unchanged (INV-027).
    /// </summary>
    public static RoutedUICommand SetHeadingLevel { get; } = new(
        "Heading level",
        nameof(SetHeadingLevel),
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

    /// <summary>
    /// The Toggle Unordered List Formatting Action: the selected paragraphs become an Unordered List,
    /// an Unordered List becomes plain paragraphs again, and an Ordered List is converted rather than
    /// removed (INV-023).
    /// </summary>
    public static RoutedUICommand ToggleUnorderedList { get; } = new(
        "Bulleted list",
        nameof(ToggleUnorderedList),
        typeof(MarkdownEditingCommands));

    /// <summary>
    /// The Toggle Ordered List Formatting Action: the selected paragraphs become an Ordered List, an
    /// Ordered List becomes plain paragraphs again, and an Unordered List is converted rather than
    /// removed (INV-023).
    /// </summary>
    public static RoutedUICommand ToggleOrderedList { get; } = new(
        "Numbered list",
        nameof(ToggleOrderedList),
        typeof(MarkdownEditingCommands));

    /// <summary>
    /// The Toggle Task List Formatting Action: gives the selected List Items their Task Markers, or
    /// clears them when every one already carries one. Available only while the selection is inside a
    /// List (INV-023).
    /// </summary>
    public static RoutedUICommand ToggleTaskList { get; } = new(
        "Checklist",
        nameof(ToggleTaskList),
        typeof(MarkdownEditingCommands));

    /// <summary>Opens the Find Bar and focuses its query box (Ctrl+F).</summary>
    public static RoutedUICommand ShowFind { get; } = new(
        "Find",
        nameof(ShowFind),
        typeof(MarkdownEditingCommands),
        [new KeyGesture(Key.F, ModifierKeys.Control)]);

    /// <summary>Opens the Find Bar with its Replace Row, and focuses the query box (Ctrl+H).</summary>
    public static RoutedUICommand ShowReplace { get; } = new(
        "Replace",
        nameof(ShowReplace),
        typeof(MarkdownEditingCommands),
        [new KeyGesture(Key.H, ModifierKeys.Control)]);

    /// <summary>Closes the Find Bar (its Replace Row included) and clears the Find highlights.</summary>
    public static RoutedUICommand HideFind { get; } = new(
        "Close find",
        nameof(HideFind),
        typeof(MarkdownEditingCommands));

    /// <summary>
    /// The Replace edit: swaps the Current Match for the Replacement, then moves to the next Match.
    /// Available only while there are Matches (INV-022).
    /// </summary>
    public static RoutedUICommand Replace { get; } = new(
        "Replace",
        nameof(Replace),
        typeof(MarkdownEditingCommands));

    /// <summary>
    /// The Replace All edit: swaps every Match in the Markdown Document for the Replacement in a
    /// single undoable step, Unfolding every Folded Section first. Available only while there are
    /// Matches (INV-022).
    /// </summary>
    public static RoutedUICommand ReplaceAll { get; } = new(
        "Replace all",
        nameof(ReplaceAll),
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
