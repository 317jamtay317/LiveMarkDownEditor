using System.Windows;
using System.Windows.Threading;
using Domain;

namespace UI.Controls;

/// <summary>
/// Follow the Active Session in the Folder Panel (INV-083): the Workspace pushes the File it is
/// editing into <see cref="SelectedEntry"/>, and the panel reveals that File's row — Expanding every
/// Folder above it and bringing it into view — and highlights it. Nothing pushed leaves no row
/// highlighted. Following only browses: it never runs the <see cref="ActivateCommand"/>, so no Tab
/// opens (INV-043).
/// </summary>
public sealed partial class FolderPanel
{
    private static void OnSelectedEntryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (FolderPanel)d;

        // The push that only echoes the row the user just highlighted asks for no reveal at all.
        if (panel.ShowsAsSelected(e.NewValue as FolderEntry))
        {
            return;
        }

        // A File pushed while its rows are still being built — a Folder Workspace opened and its Tabs
        // Restored in one pass — has no row to reveal yet, so the reveal waits for that layout.
        panel.Dispatcher.BeginInvoke(panel.RevealSelectedEntry, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Reveals and highlights the currently pushed <see cref="SelectedEntry"/>, or leaves no row
    /// highlighted when none is pushed. It reads the property rather than the pushed value, so several
    /// pushes in one pass settle on the last of them rather than fighting each other.
    /// </summary>
    private void RevealSelectedEntry()
    {
        var entry = SelectedEntry;
        if (ShowsAsSelected(entry))
        {
            return;
        }

        if (entry is null || !Highlight(entry.RelativePath, focus: false))
        {
            // Nothing to follow, or a Folder Tree that no longer holds it: the highlight never names a
            // row that is not the document on screen.
            Deselect();
        }
    }

    // Whether the highlighted row already shows the given entry. A rebuild replaces every Folder Entry
    // (INV-044), so the entry is matched by what identifies it rather than by reference.
    private bool ShowsAsSelected(FolderEntry? entry) =>
        (SelectedItem as FolderPanelRow)?.Entry is { } selected
            ? entry is not null
              && selected.Kind == entry.Kind
              && string.Equals(selected.RelativePath, entry.RelativePath, StringComparison.Ordinal)
            : entry is null;
}
