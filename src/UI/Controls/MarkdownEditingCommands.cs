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

    /// <summary>Unfolds every Folded Section, restoring the full document (Ctrl+Shift+M).</summary>
    public static RoutedUICommand ExpandAllFolds { get; } = new(
        "Expand all sections",
        nameof(ExpandAllFolds),
        typeof(MarkdownEditingCommands),
        [new KeyGesture(Key.M, ModifierKeys.Control | ModifierKeys.Shift)]);
}
