using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Domain;
using UI.Core;

namespace UI.Controls;

/// <summary>
/// Rename File in the Folder Panel (INV-082): F2, or <b>Rename file</b> on a File's context menu, edits
/// the selected File's name in place on its row. Enter, or focus leaving the name editor, commits the
/// New Name by running the <see cref="RenameCommand"/>; Escape changes nothing. Once the rebuilt Folder
/// Tree holds the renamed File, its row is highlighted, so the user keeps their place. New Folder names
/// its Folder in the same name editor, so the editing here serves both (INV-085).
/// </summary>
public sealed partial class FolderPanel
{
    /// <summary>Identifies the <see cref="RenameCommand"/> dependency property.</summary>
    public static readonly DependencyProperty RenameCommandProperty = DependencyProperty.Register(
        nameof(RenameCommand),
        typeof(ICommand),
        typeof(FolderPanel),
        new PropertyMetadata(null));

    // The row whose name is being edited — a File's for Rename File, or New Folder's own row — or null
    // when none is.
    private FolderPanelRow? _renaming;

    // The relative path the renamed File or the new Folder will have, highlighted once a rebuild holds
    // it; and whether the row should take keyboard focus too, as it does when the user committed with
    // Enter.
    private string? _highlightAfterRebuild;
    private bool _focusAfterRebuild;

    /// <summary>
    /// Edits the selected File's name in place (F2): the start of Rename File (INV-082). Available only
    /// on a File, never a Folder, and only when there is a <see cref="RenameCommand"/> to commit to.
    /// </summary>
    public static RoutedUICommand EditFileName { get; } = new(
        "Rename file",
        nameof(EditFileName),
        typeof(FolderPanel),
        [new KeyGesture(Key.F2)]);

    /// <summary>
    /// The command run when a New Name is committed for Rename File, with a
    /// <see cref="RenameFileRequest"/> naming the File and the New Name exactly as typed. Never run for a
    /// Folder, for Escape, or for a name left unchanged (INV-082).
    /// </summary>
    public ICommand? RenameCommand
    {
        get => (ICommand?)GetValue(RenameCommandProperty);
        set => SetValue(RenameCommandProperty, value);
    }

    /// <summary>
    /// Commits the New Name when keyboard focus leaves the name editor, as moving away from a row does
    /// in every Windows file tree. Focus moving into the editor's own context menu (to paste, say) keeps
    /// the editing going.
    /// </summary>
    /// <param name="e">The focus change.</param>
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);

        if (_renaming is not null && e.OriginalSource is TextBox && !IsInContextMenu(e.NewFocus as DependencyObject))
        {
            CommitRename(refocus: false);
        }
    }

    /// <summary>
    /// Handles a key pressed while a name is being edited: Enter commits the New Name and Escape
    /// abandons it. Every other key belongs to the name editor, so it neither opens nor deletes the File.
    /// </summary>
    /// <param name="key">The key pressed.</param>
    /// <returns><see langword="true"/> when the key was consumed by the edit.</returns>
    private bool HandleRenameKey(Key key)
    {
        switch (key)
        {
            case Key.Enter:
                CommitRename(refocus: true);
                return true;
            case Key.Escape:
                CancelRename();
                return true;
            default:
                return false;
        }
    }

    private void OnCanEditFileName(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = _renaming is null
                       && RenameCommand is not null
                       && SelectedItem is FolderPanelRow { Kind: FolderEntryKind.File };
        e.Handled = true;
    }

    private void OnEditFileName(object sender, ExecutedRoutedEventArgs e)
    {
        if (_renaming is not null || SelectedItem is not FolderPanelRow { Kind: FolderEntryKind.File } row)
        {
            return;
        }

        row.NewName = row.Name;
        row.IsRenaming = true;
        _renaming = row;
        e.Handled = true;

        // The editor appears with the next layout, so it can take focus only once that has run.
        Dispatcher.BeginInvoke(() => FocusNameEditor(row), DispatcherPriority.Input);
    }

    // Ends the edit and runs the RenameCommand for a changed name, remembering where the renamed File
    // will be so the rebuild can highlight it. A New Entry row commits an Entry Name instead.
    private void CommitRename(bool refocus)
    {
        if (EndRename(refocus) is not { } row)
        {
            return;
        }

        if (row.IsNewEntry)
        {
            CommitNewEntry(row, refocus);
            return;
        }

        var rename = Workspace?.CanRename(row.Entry) == true ? Workspace.Rename(row.Entry, row.NewName) : null;
        if (rename?.IsUnchanged == true)
        {
            return;
        }

        if (rename?.Renamed is { } renamed)
        {
            _highlightAfterRebuild = renamed.RelativePath;
            _focusAfterRebuild = refocus;
        }

        Run(RenameCommand, new RenameFileRequest(row.Entry, row.NewName));
    }

    private void CancelRename() => EndRename(refocus: true);

    private FolderPanelRow? EndRename(bool refocus)
    {
        // Cleared first: hiding the editor moves focus, and that must not commit a second time.
        var row = _renaming;
        _renaming = null;
        if (row is null)
        {
            return null;
        }

        row.IsRenaming = false;
        CommandManager.InvalidateRequerySuggested();
        if (row.IsNewEntry)
        {
            // New Folder's row only ever stood for the name being typed, so it goes with the edit.
            RemoveNewEntryRow(row);
            if (refocus)
            {
                FocusSelection();
            }
        }
        else if (refocus)
        {
            RealizedRow(row)?.Focus();
        }

        return row;
    }

    /// <summary>Forgets any edit in progress, as a different root or no root at all does.</summary>
    private void ForgetRename()
    {
        _renaming = null;
        _newEntrySelection = null;
        _highlightAfterRebuild = null;
    }

    /// <summary>
    /// Highlights the renamed File, or the new Folder, once a rebuild of the same root holds it. Only
    /// the first rebuild after a commit looks: if the rename or New Folder was refused, a later rebuild
    /// must not highlight an entry of that name that appears for some other reason.
    /// </summary>
    private void HighlightCommittedEntry()
    {
        if (_highlightAfterRebuild is not { } path)
        {
            return;
        }

        var focus = _focusAfterRebuild;
        _highlightAfterRebuild = null;

        // The rebuilt row gets its WPF row with the next layout, so it can be highlighted only once that has run.
        Dispatcher.BeginInvoke(() => Highlight(path, focus), DispatcherPriority.Loaded);
    }

    private void FocusNameEditor(FolderPanelRow row)
    {
        // New Folder's row may be below the fold of a long folder, so it is brought into view first.
        if (_renaming != row
            || PathTo(_rows, row.Entry.RelativePath) is not { } chain
            || RealizedRow(chain, bringIntoView: true) is not { } container
            || NameEditorIn(container) is not { } editor)
        {
            return;
        }

        container.BringIntoView();

        // Select the name without its extension, as Windows does, so typing replaces just the name.
        editor.Focus();
        editor.Select(0, Path.GetFileNameWithoutExtension(row.NewName).Length);
    }

    // Whether the source sits inside a row's name editor, where a double-click selects a word.
    private static bool IsInNameEditor(DependencyObject? source)
    {
        for (; source is not null and not TreeViewItem; source = ParentOf(source))
        {
            if (source is TextBoxBase)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInContextMenu(DependencyObject? element)
    {
        for (; element is not null; element = ParentOf(element))
        {
            if (element is ContextMenu)
            {
                return true;
            }
        }

        return false;
    }

    private static TextBox? NameEditorIn(DependencyObject element) => FindInHeader<TextBox>(element);
}
