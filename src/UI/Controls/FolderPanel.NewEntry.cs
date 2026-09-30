using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Domain;
using UI.Core;

namespace UI.Controls;

/// <summary>
/// New File and New Folder in the Folder Panel (INV-085, INV-086), as VS Code's Explorer does them: the
/// <see cref="CreateFile"/> and <see cref="CreateFolder"/> commands — the header's buttons, the panel's
/// context menu, and, for New File, a <see cref="NewFileRequest"/> from New Document (INV-087) — open an
/// empty name editor in the Save Folder, Expanding it: at the top of its Folders for a new Folder, and at
/// the top of its files for a new File. Enter, or focus leaving the editor, commits the Entry Name by
/// running the <see cref="NewFileCommand"/> or <see cref="NewFolderCommand"/>; Escape and a blank name
/// create nothing. Once the rebuilt Folder Tree holds a new Folder, its row is highlighted; a new File
/// is highlighted by Following the Tab it opens in (INV-083).
/// </summary>
public sealed partial class FolderPanel
{
    /// <summary>Identifies the <see cref="NewFolderCommand"/> dependency property.</summary>
    public static readonly DependencyProperty NewFolderCommandProperty = DependencyProperty.Register(
        nameof(NewFolderCommand),
        typeof(ICommand),
        typeof(FolderPanel),
        new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="NewFileCommand"/> dependency property.</summary>
    public static readonly DependencyProperty NewFileCommandProperty = DependencyProperty.Register(
        nameof(NewFileCommand),
        typeof(ICommand),
        typeof(FolderPanel),
        new PropertyMetadata(null));

    /// <summary>Identifies the <see cref="NewFileRequest"/> dependency property.</summary>
    public static readonly DependencyProperty NewFileRequestProperty = DependencyProperty.Register(
        nameof(NewFileRequest),
        typeof(object),
        typeof(FolderPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnNewFileRequestChanged));

    // The Selected Folder Entry when New File or New Folder began, which names the Save Folder it goes in.
    private FolderEntry? _newEntrySelection;

    /// <summary>
    /// Opens New Folder's name editor at the top of the Save Folder: the start of New Folder (INV-085).
    /// Available while a Folder Workspace is shown, there is a <see cref="NewFolderCommand"/> to commit
    /// to, and no name is already being edited.
    /// </summary>
    public static RoutedUICommand CreateFolder { get; } = new("New folder", nameof(CreateFolder), typeof(FolderPanel));

    /// <summary>
    /// Opens New File's name editor at the top of the Save Folder's files: the start of New File
    /// (INV-086). Available while a Folder Workspace is shown, there is a <see cref="NewFileCommand"/> to
    /// commit to, and no name is already being edited.
    /// </summary>
    public static RoutedUICommand CreateFile { get; } = new("New file", nameof(CreateFile), typeof(FolderPanel));

    /// <summary>
    /// The command run when an Entry Name is committed for New Folder, with a
    /// <see cref="NewEntryRequest"/> naming the Selected Folder Entry New Folder began with and the Entry
    /// Name exactly as typed. Never run for Escape or a blank name (INV-085).
    /// </summary>
    public ICommand? NewFolderCommand
    {
        get => (ICommand?)GetValue(NewFolderCommandProperty);
        set => SetValue(NewFolderCommandProperty, value);
    }

    /// <summary>
    /// The command run when an Entry Name is committed for New File, with a <see cref="NewEntryRequest"/>
    /// naming the Selected Folder Entry New File began with and the Entry Name exactly as typed. Never run
    /// for Escape or a blank name (INV-086).
    /// </summary>
    public ICommand? NewFileCommand
    {
        get => (ICommand?)GetValue(NewFileCommandProperty);
        set => SetValue(NewFileCommandProperty, value);
    }

    /// <summary>
    /// A request from New Document to start New File (INV-087). When a request arrives the panel takes it —
    /// handing the property back as <see langword="null"/> — and, once layout has run (the panel may just
    /// have been shown), opens New File's name editor as <see cref="CreateFile"/> does. Bound two ways by
    /// default.
    /// </summary>
    public object? NewFileRequest
    {
        get => GetValue(NewFileRequestProperty);
        set => SetValue(NewFileRequestProperty, value);
    }

    private static void OnNewFileRequestChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is null)
        {
            return;
        }

        var panel = (FolderPanel)d;
        panel.NewFileRequest = null;
        panel.Dispatcher.BeginInvoke(() => panel.StartNewEntry(FolderEntryKind.File), DispatcherPriority.Loaded);
    }

    private void OnCanCreateFolder(object sender, CanExecuteRoutedEventArgs e) =>
        CanStart(e, NewFolderCommand);

    private void OnCanCreateFile(object sender, CanExecuteRoutedEventArgs e) =>
        CanStart(e, NewFileCommand);

    private void CanStart(CanExecuteRoutedEventArgs e, ICommand? commit)
    {
        e.CanExecute = _renaming is null && commit is not null && Workspace is not null;
        e.Handled = true;
    }

    private void OnCreateFolder(object sender, ExecutedRoutedEventArgs e) =>
        e.Handled = StartNewEntry(FolderEntryKind.Folder);

    private void OnCreateFile(object sender, ExecutedRoutedEventArgs e) =>
        e.Handled = StartNewEntry(FolderEntryKind.File);

    // Opens the name editor for a new entry of the given kind in the Save Folder, where VS Code puts it:
    // a Folder at the top, and a File at the top of the files, below the Folders.
    private bool StartNewEntry(FolderEntryKind kind)
    {
        var commit = kind == FolderEntryKind.File ? NewFileCommand : NewFolderCommand;
        if (_renaming is not null || commit is null || Workspace is not { } workspace)
        {
            return false;
        }

        var selection = SelectedEntry;
        var saveFolder = workspace.SaveFolderEntryFor(selection);
        var rows = RowsOfSaveFolder(saveFolder);
        var row = FolderPanelRow.ForNewEntry(saveFolder?.RelativePath ?? string.Empty, kind);
        rows.Insert(kind == FolderEntryKind.Folder ? 0 : rows.Count(each => each.Kind == FolderEntryKind.Folder), row);
        _newEntrySelection = selection;
        _renaming = row;

        // No second new entry while this one is being named: the header buttons grey out until it ends.
        CommandManager.InvalidateRequerySuggested();

        // The editor appears with the next layout, so it can take focus only once that has run.
        Dispatcher.BeginInvoke(() => FocusNameEditor(row), DispatcherPriority.Input);
        return true;
    }

    // The rows of the Save Folder, where the editor goes: the root's own, or a Folder's, Expanded so
    // the editor shows.
    private ObservableCollection<FolderPanelRow> RowsOfSaveFolder(FolderEntry? folder)
    {
        if (folder is null || PathTo(_rows, folder.RelativePath) is not { } chain)
        {
            return _rows;
        }

        foreach (var above in chain)
        {
            above.IsExpanded = true;
        }

        return chain[^1].Children;
    }

    // Commits a new entry: the editor row goes, and an Entry Name that is not blank is handed to the
    // NewFileCommand or NewFolderCommand — which explains any other refusal. A new Folder is remembered so
    // the rebuild can highlight it; a new File opens in a Tab, whose Following highlights it (INV-083), and
    // the editor keeps the keyboard.
    private void CommitNewEntry(FolderPanelRow row, bool refocus)
    {
        var selection = _newEntrySelection;
        _newEntrySelection = null;
        if (Workspace is not { } workspace)
        {
            return;
        }

        var forFile = row.Kind == FolderEntryKind.File;
        var creation = forFile ? workspace.NewFile(selection, row.NewName) : workspace.NewFolder(selection, row.NewName);
        if (creation.Refusal == NameRefusal.Blank)
        {
            return;
        }

        if (creation.Created is { Kind: FolderEntryKind.Folder } created)
        {
            _highlightAfterRebuild = created.RelativePath;
            _focusAfterRebuild = refocus;
        }

        Run(forFile ? NewFileCommand : NewFolderCommand, new NewEntryRequest(selection, row.NewName));
    }

    // Takes a New Entry row out of whichever rows hold it.
    private void RemoveNewEntryRow(FolderPanelRow row)
    {
        if (!_rows.Remove(row) && PathTo(_rows, row.Entry.RelativePath) is { Count: > 1 } chain)
        {
            chain[^2].Children.Remove(row);
        }
    }

    // Gives keyboard focus back to the highlighted row, or to the panel when none is highlighted.
    private void FocusSelection()
    {
        if (SelectedItem is FolderPanelRow selected && RealizedRow(selected) is { } container)
        {
            container.Focus();
        }
        else
        {
            Focus();
        }
    }
}
