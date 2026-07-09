using System.Windows.Input;
using UI.Core;

namespace UI.Controls;

/// <summary>
/// One row of the Navigation Panel: an Outline Entry wrapping a <see cref="SectionHeading"/> and
/// adding the panel-only Collapse state. An entry that <see cref="LeadsNestedEntries"/> can be
/// Collapsed to hide the nested Outline Entries beneath it, or Expanded to show them again — a
/// view-only disclosure that never changes any document (INV-012).
/// </summary>
public sealed class OutlineEntry : ObservableObject
{
    private bool _isCollapsed;

    /// <summary>Creates an Outline Entry for a Section Heading.</summary>
    /// <param name="heading">The Section Heading this entry represents and Navigates to.</param>
    /// <param name="leadsNestedEntries">Whether the entry leads nested (deeper-level) Outline Entries.</param>
    /// <param name="toggleCollapse">Callback that toggles this entry's Collapse state in the panel.</param>
    internal OutlineEntry(SectionHeading heading, bool leadsNestedEntries, Action<OutlineEntry> toggleCollapse)
    {
        Heading = heading;
        LeadsNestedEntries = leadsNestedEntries;
        ToggleCollapseCommand = new RelayCommand(() => toggleCollapse(this));
    }

    /// <summary>The Section Heading this entry represents; Navigating targets it.</summary>
    public SectionHeading Heading { get; }

    /// <summary>The heading level, 1–6 — used to indent the entry by document depth.</summary>
    public int Level => Heading.Level;

    /// <summary>The heading's plain text, shown as the entry's label.</summary>
    public string Text => Heading.Text;

    /// <summary>Whether this entry leads nested Outline Entries, so it can be Collapsed/Expanded.</summary>
    public bool LeadsNestedEntries { get; }

    /// <summary>
    /// Whether this entry is Collapsed, hiding its nested Outline Entries in the Navigation Panel.
    /// Panel-only view state — it never changes the Markdown Document or any Fold (INV-012).
    /// </summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => Set(ref _isCollapsed, value);
    }

    /// <summary>Collapses this entry if Expanded, or Expands it if Collapsed.</summary>
    public ICommand ToggleCollapseCommand { get; }
}
