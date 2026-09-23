using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace UI.Controls;

/// <summary>
/// The Folder Panel's row plumbing: finding the WPF row that shows a Folder Panel Row, and
/// highlighting one by its relative path. Shared by the two things that highlight a row for the user —
/// Rename File, which highlights the renamed File once the rebuilt Folder Tree holds it (INV-082), and
/// Follow the Active Session, which highlights the File being edited (INV-083).
/// </summary>
public sealed partial class FolderPanel
{
    /// <summary>
    /// Highlights the row of the entry at <paramref name="relativePath"/>, revealing it first: every
    /// Folder above it is Expanded and the row is brought into view, because a row inside a Collapsed
    /// or scrolled-away Folder has no WPF row of its own to highlight.
    /// </summary>
    /// <param name="relativePath">The relative path of the entry to highlight.</param>
    /// <param name="focus">Whether the row should take keyboard focus as well.</param>
    /// <returns><see langword="true"/> when the row was found and highlighted.</returns>
    private bool Highlight(string relativePath, bool focus)
    {
        if (PathTo(_rows, relativePath) is not { } chain || RealizedRow(chain, bringIntoView: true) is not { } row)
        {
            return false;
        }

        row.IsSelected = true;
        row.BringIntoView();
        if (focus)
        {
            row.Focus();
        }

        return true;
    }

    /// <summary>Leaves no row highlighted, whichever one currently is.</summary>
    /// <remarks>
    /// A <see cref="TreeView"/> has no "select nothing" of its own — its
    /// <see cref="TreeView.SelectedItem"/> is read-only — so the highlighted row is what clears it.
    /// </remarks>
    private void Deselect()
    {
        if (SelectedItem is FolderPanelRow row && RealizedRow(row) is { } container)
        {
            container.IsSelected = false;
        }
    }

    // The WPF row showing the given row, when it exists.
    private TreeViewItem? RealizedRow(FolderPanelRow row) =>
        PathTo(_rows, row.Entry.RelativePath) is { } chain ? RealizedRow(chain, bringIntoView: false) : null;

    // Walks the rows from the top level down, finding each one's WPF row. A Folder's rows exist only
    // while it is Expanded, and a virtualized level only has WPF rows for what is on screen — so
    // Expanding each Folder on the way, and bringing the row into view, is what makes them exist.
    private TreeViewItem? RealizedRow(IReadOnlyList<FolderPanelRow> chain, bool bringIntoView)
    {
        ItemsControl level = this;
        TreeViewItem? container = null;
        foreach (var row in chain)
        {
            if (bringIntoView && level is TreeViewItem { IsExpanded: false } collapsed)
            {
                // Expanding generates the rows beneath the Folder, but only once layout has run.
                collapsed.IsExpanded = true;
                collapsed.UpdateLayout();
            }

            container = level.ItemContainerGenerator.ContainerFromItem(row) as TreeViewItem;
            if (container is null && bringIntoView && ItemsHostOf(level) is VirtualizingStackPanel host)
            {
                host.BringIndexIntoViewPublic(level.Items.IndexOf(row));
                container = level.ItemContainerGenerator.ContainerFromItem(row) as TreeViewItem;
            }

            if (container is null)
            {
                return null;
            }

            level = container;
        }

        return container;
    }

    // The rows from the top level down to the one whose entry has the given relative path, or null.
    private static List<FolderPanelRow>? PathTo(IEnumerable<FolderPanelRow> rows, string relativePath)
    {
        foreach (var row in rows)
        {
            if (string.Equals(row.Entry.RelativePath, relativePath, StringComparison.Ordinal))
            {
                return [row];
            }

            if (PathTo(row.Children, relativePath) is { } below)
            {
                below.Insert(0, row);
                return below;
            }
        }

        return null;
    }

    private static Panel? ItemsHostOf(ItemsControl level) =>
        FindInHeader<ItemsPresenter>(level) is { } presenter && VisualTreeHelper.GetChildrenCount(presenter) > 0
            ? VisualTreeHelper.GetChild(presenter, 0) as Panel
            : null;

    // The first T in the element's own visuals, not looking inside the rows nested under it.
    private static T? FindInHeader<T>(DependencyObject element)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++)
        {
            var child = VisualTreeHelper.GetChild(element, index);
            if (child is T found)
            {
                return found;
            }

            if (child is not TreeViewItem && FindInHeader<T>(child) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }
}
