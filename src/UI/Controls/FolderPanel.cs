using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Domain;

namespace UI.Controls;

/// <summary>
/// The Folder Panel: the presentation-only Control that shows a Folder Workspace's Folder Tree as a
/// <see cref="TreeView"/> and raises activation when a File is opened. Folders Expand and Collapse
/// natively; double-clicking (or pressing Enter on) a File runs the <see cref="ActivateCommand"/> with
/// that <see cref="FolderEntry"/>, which the Workspace routes to its open-in-a-Tab path (INV-043). It
/// reads the tree and never mutates any document.
/// </summary>
/// <remarks>
/// Authored as a custom Control (a <see cref="TreeView"/> subclass plus a ResourceDictionary for its
/// look), per the project's Control exception to the zero-code-behind rule — the same pattern as the
/// <see cref="OutlinePanel"/>. Because a <see cref="TreeView"/>'s <see cref="TreeView.SelectedItem"/> is
/// read-only (unlike the <see cref="OutlinePanel"/>'s <see cref="ListBox"/>), activation is driven from
/// <see cref="OnMouseDoubleClick"/> and <see cref="OnKeyDown"/> rather than a selection change.
/// </remarks>
public sealed class FolderPanel : TreeView
{
    /// <summary>Identifies the <see cref="Workspace"/> dependency property.</summary>
    public static readonly DependencyProperty WorkspaceProperty = DependencyProperty.Register(
        nameof(Workspace),
        typeof(FolderWorkspace),
        typeof(FolderPanel),
        new PropertyMetadata(null, OnWorkspaceChanged));

    /// <summary>Identifies the <see cref="ActivateCommand"/> dependency property.</summary>
    public static readonly DependencyProperty ActivateCommandProperty = DependencyProperty.Register(
        nameof(ActivateCommand),
        typeof(ICommand),
        typeof(FolderPanel),
        new PropertyMetadata(null));

    /// <summary>The Folder Workspace whose Folder Tree this panel lists. Its entries are the tree's roots.</summary>
    public FolderWorkspace? Workspace
    {
        get => (FolderWorkspace?)GetValue(WorkspaceProperty);
        set => SetValue(WorkspaceProperty, value);
    }

    /// <summary>The command run when a File is activated (double-click or Enter), with the File's <see cref="FolderEntry"/> as its parameter.</summary>
    public ICommand? ActivateCommand
    {
        get => (ICommand?)GetValue(ActivateCommandProperty);
        set => SetValue(ActivateCommandProperty, value);
    }

    /// <summary>Activates the double-clicked entry when it is a File; a Folder is left to its native Expand/Collapse.</summary>
    /// <param name="e">The double-click.</param>
    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        if (ActivateAt(e.OriginalSource as DependencyObject))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Activates the Folder Entry whose row contains <paramref name="clicked"/>, when that entry is a
    /// File. A double-click lands on a visual inside the row — its glyph or its name — so the row that
    /// visual sits in is what names the entry.
    /// </summary>
    /// <param name="clicked">The visual the click landed on (a double-click's <c>OriginalSource</c>).</param>
    /// <returns><see langword="true"/> when a File was activated; otherwise <see langword="false"/>.</returns>
    internal bool ActivateAt(DependencyObject? clicked)
    {
        if (ResolveEntry(clicked) is not { Kind: FolderEntryKind.File } file)
        {
            return false;
        }

        Activate(file);
        return true;
    }

    /// <summary>Activates the selected entry on Enter when it is a File.</summary>
    /// <param name="e">The key press.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Enter && SelectedItem is FolderEntry { Kind: FolderEntryKind.File } file)
        {
            Activate(file);
            e.Handled = true;
        }
    }

    private static void OnWorkspaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (FolderPanel)d;
        panel.ItemsSource = (e.NewValue as FolderWorkspace)?.Entries;
    }

    private static FolderEntry? ResolveEntry(DependencyObject? source)
    {
        // A double-click's OriginalSource is a visual inside the row, so walk up to the row itself.
        // ItemsControl.ContainerFromElement cannot do this: a nested row belongs to its parent row, not
        // to the TreeView, so asking the TreeView for it skips past every File below the top level.
        while (source is not null and not TreeViewItem)
        {
            // A click can land on a ContentElement (a Run inside the row's name), which has no visual
            // parent of its own — its logical parent leads back into the visual tree.
            source = source is Visual or Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return (source as TreeViewItem)?.DataContext as FolderEntry;
    }

    private void Activate(FolderEntry entry)
    {
        if (ActivateCommand?.CanExecute(entry) == true)
        {
            ActivateCommand.Execute(entry);
        }
    }
}
