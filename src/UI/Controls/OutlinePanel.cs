using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using UI.Wysiwyg;

namespace UI.Controls;

/// <summary>
/// The Navigation Panel: the presentation-only panel that lists the Active Session's Outline — every
/// Section Heading as a clickable Outline Entry, indented by heading level. Selecting an Outline Entry
/// Navigates the editor to that Section Heading; as the caret moves, the Outline Entry of the Current
/// Section is highlighted. An Outline Entry that leads nested entries shows a Collapse toggle that
/// hides or shows those nested Outline Entries within the panel. It reads the editor's Outline and
/// drives Navigation, never mutating the document (INV-012).
/// </summary>
/// <remarks>
/// Authored as a custom Control (a <see cref="ListBox"/> subclass plus a ResourceDictionary for its
/// look), per the project's Control exception to the zero-code-behind rule — the same pattern as the
/// <see cref="EditorGutter"/>. It mirrors the editor through the <see cref="Editor"/> dependency
/// property, refreshing its Outline Entries on <see cref="MarkdownRichEditor.OutlineChanged"/> and its
/// highlight on <see cref="MarkdownRichEditor.CurrentSectionChanged"/>. Which entries are hidden under
/// a Collapsed ancestor is computed by the pure <see cref="OutlineView"/>.
/// </remarks>
public sealed class OutlinePanel : ListBox
{
    /// <summary>Identifies the <see cref="Editor"/> dependency property.</summary>
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor),
        typeof(MarkdownRichEditor),
        typeof(OutlinePanel),
        new PropertyMetadata(null, OnEditorChanged));

    // Every Outline Entry in document order, whatever its current visibility.
    private List<OutlineEntry> _entries = [];

    // The subset currently shown (entries not hidden under a Collapsed ancestor); the ItemsSource.
    private List<OutlineEntry> _visible = [];

    // True while the panel is mirroring editor/collapse state into its own selection, so the resulting
    // SelectionChanged is not mistaken for a user click and does not Navigate back.
    private bool _isSyncingSelection;

    /// <summary>The <see cref="MarkdownRichEditor"/> whose Outline this panel lists and Navigates.</summary>
    public MarkdownRichEditor? Editor
    {
        get => (MarkdownRichEditor?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>
    /// Navigates the editor to a clicked Outline Entry. Selections the panel makes itself to mirror
    /// the Current Section or a Collapse are ignored here, so highlighting never triggers Navigation.
    /// </summary>
    /// <param name="e">The selection change.</param>
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        if (_isSyncingSelection)
        {
            return;
        }

        if (SelectedItem is OutlineEntry entry)
        {
            Editor?.Navigate(entry.Heading);
        }
    }

    private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (OutlinePanel)d;
        if (e.OldValue is MarkdownRichEditor oldEditor)
        {
            oldEditor.OutlineChanged -= panel.OnOutlineChanged;
            oldEditor.CurrentSectionChanged -= panel.OnCurrentSectionChanged;
        }

        if (e.NewValue is MarkdownRichEditor newEditor)
        {
            newEditor.OutlineChanged += panel.OnOutlineChanged;
            newEditor.CurrentSectionChanged += panel.OnCurrentSectionChanged;
        }

        panel.RebuildEntries();
    }

    private void OnOutlineChanged(object? sender, EventArgs e) => RebuildEntries();

    private void OnCurrentSectionChanged(object? sender, EventArgs e) => HighlightCurrentSection();

    private void RebuildEntries()
    {
        var headings = Editor?.Outline ?? [];

        // Preserve which entries the user has Collapsed across edits, keyed by the heading's block
        // (stable while the user types; cleared only when the document is re-Projected).
        var collapsedBlocks = new HashSet<Block>(
            _entries.Where(entry => entry.IsCollapsed).Select(entry => entry.Heading.Block));

        var levels = headings.Select(heading => heading.Level).ToList();
        _entries = new List<OutlineEntry>(headings.Count);
        for (var index = 0; index < headings.Count; index++)
        {
            var entry = new OutlineEntry(headings[index], OutlineView.HasNestedEntries(levels, index), ToggleCollapse)
            {
                IsCollapsed = collapsedBlocks.Contains(headings[index].Block),
            };
            _entries.Add(entry);
        }

        ApplyVisibility();
        HighlightCurrentSection();
    }

    private void ToggleCollapse(OutlineEntry entry)
    {
        entry.IsCollapsed = !entry.IsCollapsed;
        ApplyVisibility();
        HighlightCurrentSection();
    }

    private void ApplyVisibility()
    {
        var levels = _entries.Select(entry => entry.Level).ToList();
        var collapsed = _entries.Select(entry => entry.IsCollapsed).ToList();
        var visibleFlags = OutlineView.VisibleEntries(levels, collapsed);

        _visible = _entries.Where((_, index) => visibleFlags[index]).ToList();

        _isSyncingSelection = true;
        try
        {
            ItemsSource = _visible;
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    private void HighlightCurrentSection()
    {
        var current = Editor?.CurrentSection;
        var target = current is null
            ? null
            : _entries.FirstOrDefault(entry => ReferenceEquals(entry.Heading, current));

        // If the Current Section is hidden under a Collapsed ancestor, highlight the nearest visible
        // entry above it (the Collapsed ancestor) so the user still sees where they are.
        if (target is not null && !_visible.Contains(target))
        {
            for (var index = _entries.IndexOf(target); index >= 0; index--)
            {
                if (_visible.Contains(_entries[index]))
                {
                    target = _entries[index];
                    break;
                }
            }
        }

        _isSyncingSelection = true;
        try
        {
            SelectedItem = _visible.Contains(target!) ? target : null;
            if (SelectedItem is not null)
            {
                ScrollIntoView(SelectedItem);
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }
}
