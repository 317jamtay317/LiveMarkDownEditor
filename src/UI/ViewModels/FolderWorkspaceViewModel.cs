using System.IO;
using System.Windows.Input;
using Application;
using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Folder Workspace: the shell that opens a folder and presents its Markdown Documents as a Folder
/// Tree in the Folder Panel. It is navigation chrome — opening a folder, toggling the panel, and
/// browsing never change any Markdown Document (INV-043); activating a File routes it to the
/// <see cref="OpenFile"/> callback that opens it in a Tab. The tree tracks the disk live (INV-044), and
/// the open root is persisted and Restored across runs (INV-045). It is composed as a child of the
/// <see cref="WorkspaceViewModel"/>, alongside the Appearance and Export ViewModels.
/// </summary>
public sealed class FolderWorkspaceViewModel : ObservableObject, IDisposable
{
    private readonly IFolderPicker _picker;
    private readonly IMarkdownFolderReader _reader;
    private readonly IFolderWatcher _watcher;
    private readonly IUiDispatcher _dispatcher;

    private FolderWorkspace? _folder;
    private bool _isFolderPanelVisible;

    /// <summary>Creates the Folder Workspace shell.</summary>
    /// <param name="picker">Prompts for a folder to open (INV-043).</param>
    /// <param name="reader">Enumerates the Markdown Documents beneath a folder (INV-042).</param>
    /// <param name="watcher">Watches the open folder for structural change, driving live refresh (INV-044).</param>
    /// <param name="dispatcher">Marshals the watcher's background notification onto the UI thread.</param>
    public FolderWorkspaceViewModel(
        IFolderPicker picker,
        IMarkdownFolderReader reader,
        IFolderWatcher watcher,
        IUiDispatcher dispatcher)
    {
        _picker = picker ?? throw new ArgumentNullException(nameof(picker));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _watcher.Changed += OnWatcherChanged;

        OpenFolderCommand = new AsyncRelayCommand(OpenFolderAsync);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => Folder is not null);
        ActivateEntryCommand = new AsyncRelayCommand<FolderEntry>(ActivateAsync);
        ToggleFolderPanelCommand = new RelayCommand(ToggleFolderPanel);
    }

    /// <summary>
    /// Opens a File in a Tab — the Workspace wires this to its <c>OpenPathAsync</c>, so activating a
    /// File dedupes against the open Tabs (INV-009). Left null in isolation, activation opens nothing.
    /// </summary>
    public Func<string, Task>? OpenFile { get; set; }

    /// <summary>Persists the Workspace State — the Workspace wires this to its <c>PersistStateAsync</c> (INV-045).</summary>
    public Func<Task>? PersistState { get; set; }

    /// <summary>The open Folder Workspace and its Folder Tree, or <see langword="null"/> when none is open.</summary>
    public FolderWorkspace? Folder
    {
        get => _folder;
        private set
        {
            if (Set(ref _folder, value))
            {
                Raise(nameof(HasFolder));
            }
        }
    }

    /// <summary>Whether a Folder Workspace is open (a Folder Tree to browse).</summary>
    public bool HasFolder => Folder is not null;

    /// <summary>Whether the Folder Panel is shown. Presentation-only — toggling it edits nothing (INV-043).</summary>
    public bool IsFolderPanelVisible
    {
        get => _isFolderPanelVisible;
        private set => Set(ref _isFolderPanelVisible, value);
    }

    /// <summary>Prompts for a folder and opens it as a Folder Workspace, showing the Folder Panel.</summary>
    public ICommand OpenFolderCommand { get; }

    /// <summary>Re-enumerates the open folder and rebuilds the Folder Tree.</summary>
    public ICommand RefreshCommand { get; }

    /// <summary>Activates a Folder Entry — a File opens in a Tab; a Folder does nothing here. Parameter: the entry.</summary>
    public ICommand ActivateEntryCommand { get; }

    /// <summary>Shows the Folder Panel if hidden, or hides it if shown.</summary>
    public ICommand ToggleFolderPanelCommand { get; }

    /// <summary>
    /// Prompts for a folder and opens it as a Folder Workspace, showing the Folder Panel, watching the
    /// root for live refresh, and persisting the new state. A folder that has gone since it was picked
    /// opens nothing.
    /// </summary>
    public async Task OpenFolderAsync()
    {
        var root = _picker.PickFolder();
        if (root is null)
        {
            return;
        }

        try
        {
            await LoadRootAsync(root).ConfigureAwait(true);
        }
        catch (IOException)
        {
            return;
        }

        await (PersistState?.Invoke() ?? Task.CompletedTask).ConfigureAwait(true);
    }

    /// <summary>
    /// Re-enumerates the open folder and rebuilds the Folder Tree (INV-044). Does nothing when no folder
    /// is open; keeps the last-known tree if the folder has since gone.
    /// </summary>
    public async Task RefreshAsync()
    {
        var root = Folder?.RootPath;
        if (root is null)
        {
            return;
        }

        try
        {
            var files = await _reader.EnumerateMarkdownFilesAsync(root).ConfigureAwait(true);
            Folder = FolderWorkspace.From(root, files);
        }
        catch (IOException)
        {
            // The open folder has gone; keep the last-known tree rather than crashing.
        }
    }

    /// <summary>
    /// Activates a Folder Entry: a File opens its Markdown Document in a Tab through the open callback
    /// (INV-043); a Folder does nothing (its Expand/Collapse is the tree's own). A File that has gone
    /// since it was enumerated opens nothing.
    /// </summary>
    /// <param name="entry">The Folder Entry that was activated.</param>
    public async Task ActivateAsync(FolderEntry? entry)
    {
        if (entry is null || entry.Kind != FolderEntryKind.File || Folder is null || OpenFile is null)
        {
            return;
        }

        var path = Folder.AbsolutePathOf(entry);
        try
        {
            await OpenFile(path).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // A File that has gone since it was enumerated opens nothing.
        }
    }

    /// <summary>
    /// Reopens a persisted folder at startup, skipping one that has gone (INV-045). Restoring is not
    /// itself an open, so it does not re-persist the state.
    /// </summary>
    /// <param name="rootPath">The persisted Folder Workspace root, or <see langword="null"/> when none was open.</param>
    public async Task RestoreAsync(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return;
        }

        try
        {
            await LoadRootAsync(rootPath).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // A saved folder that has gone is simply not restored (INV-045).
        }
    }

    /// <summary>Unsubscribes from and stops the folder watcher.</summary>
    public void Dispose()
    {
        _watcher.Changed -= OnWatcherChanged;
        _watcher.StopWatching();
    }

    private async Task LoadRootAsync(string rootPath)
    {
        var files = await _reader.EnumerateMarkdownFilesAsync(rootPath).ConfigureAwait(true);
        Folder = FolderWorkspace.From(rootPath, files);
        _watcher.StopWatching();
        _watcher.Watch(rootPath);
        IsFolderPanelVisible = true;
    }

    private void ToggleFolderPanel() => IsFolderPanelVisible = !IsFolderPanelVisible;

    private void OnWatcherChanged(object? sender, EventArgs e) =>
        _dispatcher.Post(() => _ = RefreshAsync());
}
