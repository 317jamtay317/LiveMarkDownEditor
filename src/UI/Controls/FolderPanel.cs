using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Domain;

namespace UI.Controls;

/// <summary>
/// The Folder Panel: the presentation-only Control that shows a Folder Workspace's Folder Tree as a
/// <see cref="TreeView"/> and raises activation when a File is opened. Folders Expand and Collapse
/// natively; double-clicking (or pressing Enter on) a File runs the <see cref="ActivateCommand"/> with
/// that <see cref="FolderEntry"/>, which the Workspace routes to its open-in-a-Tab path (INV-043). A
/// File's context menu, or the Delete key on a selected File, raises the <see cref="DeleteCommand"/> for
/// Delete File (INV-081). F2, or the context menu, edits a File's name in place for Rename File, and
/// committing it raises the <see cref="RenameCommand"/> (INV-082). The panel itself only reads the tree:
/// it never mutates any document or the disk, and leaves the confirming, the checking, the deleting and
/// the renaming to the commands.
/// </summary>
/// <remarks>
/// Authored as a custom Control (a <see cref="TreeView"/> subclass plus a ResourceDictionary for its
/// look), per the project's Control exception to the zero-code-behind rule — the same pattern as the
/// <see cref="OutlinePanel"/>. Because a <see cref="TreeView"/>'s <see cref="TreeView.SelectedItem"/> is
/// read-only (unlike the <see cref="OutlinePanel"/>'s <see cref="ListBox"/>), activation is driven from
/// <see cref="OnMouseDoubleClick"/> and <see cref="OnKeyDown"/> rather than a selection change.
/// </remarks>
public sealed partial class FolderPanel : TreeView
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

    /// <summary>Identifies the <see cref="DeleteCommand"/> dependency property.</summary>
    public static readonly DependencyProperty DeleteCommandProperty = DependencyProperty.Register(
        nameof(DeleteCommand),
        typeof(ICommand),
        typeof(FolderPanel),
        new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="SelectedEntry"/> dependency property.</summary>
    /// <remarks>
    /// A <see cref="TreeView"/>'s own <see cref="TreeView.SelectedItem"/> is read-only, and a read-only
    /// dependency property cannot carry a binding — so the panel republishes its selection through this
    /// writable one, which the Workspace binds <c>TwoWay</c>: out of the panel when the user highlights
    /// a row (INV-080), and into it to Follow the Active Session (INV-083).
    /// </remarks>
    public static readonly DependencyProperty SelectedEntryProperty = DependencyProperty.Register(
        nameof(SelectedEntry),
        typeof(FolderEntry),
        typeof(FolderPanel),
        new PropertyMetadata(null, OnSelectedEntryChanged));

    // The panel's own rows for the open root. The Folder Tree is rebuilt whenever the disk changes;
    // each rebuild is applied to these rows as a change rather than replacing them, which is what keeps
    // the user's place (INV-044).
    private ObservableCollection<FolderPanelRow> _rows = [];

    /// <summary>Creates the Folder Panel, ready to edit a File's name in place with F2 (INV-082).</summary>
    public FolderPanel() =>
        CommandBindings.Add(new CommandBinding(EditFileName, OnEditFileName, OnCanEditFileName));

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

    /// <summary>
    /// The command run for Delete File (the Delete key, or Delete on a File's context menu), with the
    /// File's <see cref="FolderEntry"/> as its parameter. Never run for a Folder (INV-081).
    /// </summary>
    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    /// <summary>
    /// The Selected Folder Entry: the highlighted row, or <see langword="null"/> when none is. Selecting
    /// is browsing — it opens nothing (INV-043) — but it names the folder a new Markdown Document is
    /// saved into (INV-080). Set from outside, it is the File to Follow: the panel reveals and
    /// highlights that File's row (INV-083).
    /// </summary>
    public FolderEntry? SelectedEntry
    {
        get => (FolderEntry?)GetValue(SelectedEntryProperty);
        set => SetValue(SelectedEntryProperty, value);
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
        // A double-click inside a name being edited selects a word of it; it does not open the File.
        if (IsInNameEditor(clicked) || ResolveEntry(clicked) is not { Kind: FolderEntryKind.File } file)
        {
            return false;
        }

        Run(ActivateCommand, file);
        return true;
    }

    /// <summary>
    /// Acts on the selected entry when it is a File: Enter activates it (INV-043), and Delete asks to
    /// delete it (INV-081). Neither key does anything on a Folder. While a name is being edited, Enter
    /// commits it and Escape abandons it instead, and every other key belongs to the name (INV-082).
    /// </summary>
    /// <param name="e">The key press.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_renaming is not null)
        {
            e.Handled |= HandleRenameKey(e.Key);
            return;
        }

        base.OnKeyDown(e);

        if (SelectedItem is not FolderPanelRow { Kind: FolderEntryKind.File } file)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                Run(ActivateCommand, file.Entry);
                e.Handled = true;
                break;
            case Key.Delete:
                Run(DeleteCommand, file.Entry);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Shows the File context menu only over a File, whose row it selects first.</summary>
    /// <param name="e">The context-menu request; handled (so no menu shows) when it is not over a File.</param>
    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        base.OnContextMenuOpening(e);

        if (!PrepareContextMenuAt(e.OriginalSource as DependencyObject))
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// Readies the File context menu for the row containing <paramref name="source"/>. Over a File, it
    /// selects that row, so the menu's Delete acts on exactly the File the user pointed at and the
    /// highlight shows which one that is (INV-081). Over a Folder or empty space, there is no menu.
    /// </summary>
    /// <param name="source">The visual the right-click landed on (the request's <c>OriginalSource</c>).</param>
    /// <returns><see langword="true"/> when the menu should show; otherwise <see langword="false"/>.</returns>
    internal bool PrepareContextMenuAt(DependencyObject? source)
    {
        // While a name is being edited, the right-click belongs to the name editor's own menu.
        if (_renaming is not null
            || ResolveRow(source) is not { DataContext: FolderPanelRow { Kind: FolderEntryKind.File } } row)
        {
            return false;
        }

        row.IsSelected = true;
        return true;
    }

    /// <summary>Republishes the highlighted row as the <see cref="SelectedEntry"/>.</summary>
    /// <param name="e">The selection change.</param>
    protected override void OnSelectedItemChanged(RoutedPropertyChangedEventArgs<object> e)
    {
        base.OnSelectedItemChanged(e);
        SelectedEntry = (SelectedItem as FolderPanelRow)?.Entry;
    }

    /// <summary>Creates the row for a top-level Folder Panel Row: one bound to its Expanded state (INV-044).</summary>
    /// <returns>A new row.</returns>
    protected override DependencyObject GetContainerForItemOverride() => new EntryRow();

    /// <summary>Prepares a top-level row, binding it to its Folder Panel Row's Expanded state (INV-044).</summary>
    /// <param name="element">The row.</param>
    /// <param name="item">The Folder Panel Row it shows.</param>
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        BindExpansion(element, item);
    }

    private static void OnWorkspaceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var panel = (FolderPanel)d;
        var previous = e.OldValue as FolderWorkspace;
        var next = e.NewValue as FolderWorkspace;

        if (next is null)
        {
            panel.ForgetRename();
            panel._rows = [];
            panel.ItemsSource = null;
            return;
        }

        // A rebuild of the same root changes only the rows that changed, keeping the user's place. A
        // different root is a different tree, so it starts afresh with every Folder Collapsed.
        if (previous is not null
            && string.Equals(previous.RootPath, next.RootPath, StringComparison.OrdinalIgnoreCase))
        {
            FolderPanelRow.Sync(panel._rows, next.Entries);

            // The highlighted row survives, but its entry is the rebuilt one now. A File just renamed
            // has a new row, which takes the highlight (INV-082).
            panel.SelectedEntry = (panel.SelectedItem as FolderPanelRow)?.Entry;
            panel.HighlightRenamedFile();
            return;
        }

        panel.ForgetRename();
        panel._rows = new ObservableCollection<FolderPanelRow>(next.Entries.Select(entry => new FolderPanelRow(entry)));
        panel.ItemsSource = panel._rows;
    }

    private static FolderEntry? ResolveEntry(DependencyObject? source) =>
        (ResolveRow(source)?.DataContext as FolderPanelRow)?.Entry;

    private static TreeViewItem? ResolveRow(DependencyObject? source)
    {
        // A double-click's OriginalSource is a visual inside the row, so walk up to the row itself.
        // ItemsControl.ContainerFromElement cannot do this: a nested row belongs to its parent row, not
        // to the TreeView, so asking the TreeView for it skips past every File below the top level.
        while (source is not null and not TreeViewItem)
        {
            source = ParentOf(source);
        }

        return source as TreeViewItem;
    }

    // A click can land on a ContentElement (a Run inside the row's name), which has no visual parent of
    // its own — its logical parent leads back into the visual tree.
    private static DependencyObject? ParentOf(DependencyObject source) =>
        source is Visual or Visual3D
            ? VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source)
            : LogicalTreeHelper.GetParent(source);

    private static void Run(ICommand? command, object parameter)
    {
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    // Binds a newly prepared row's IsExpanded, two ways, to its Folder Panel Row, so the row opens as
    // the Folder was and Expanding or Collapsing it is remembered. It is bound rather than set because
    // WPF's virtualization clears a plain IsExpanded on every row it prepares (to stop a recycled row
    // carrying another entry's state) but leaves a binding alone.
    private static void BindExpansion(DependencyObject element, object item)
    {
        if (element is TreeViewItem container && item is FolderPanelRow row)
        {
            container.SetBinding(TreeViewItem.IsExpandedProperty, new Binding(nameof(FolderPanelRow.IsExpanded))
            {
                Source = row,
                Mode = BindingMode.TwoWay,
            });
        }
    }

    /// <summary>
    /// The container for a Folder Panel Row, at any depth. It prepares the containers nested under it
    /// the way the panel prepares its top-level ones, so every row in the tree is bound to its own
    /// Expanded state (INV-044).
    /// </summary>
    private sealed class EntryRow : TreeViewItem
    {
        protected override DependencyObject GetContainerForItemOverride() => new EntryRow();

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            BindExpansion(element, item);
        }
    }
}
