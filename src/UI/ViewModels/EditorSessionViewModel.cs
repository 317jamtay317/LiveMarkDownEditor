using System.IO;
using System.Windows.Input;
using Application;
using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// One Editor Session realised for the UI — a single Tab in the Workspace. It holds the current
/// Markdown Document's source text (bound two-way to the WYSIWYG editor as the canonical model), the
/// associated Watched File, and whether unsaved edits exist. It loads and saves its own Watched File
/// and reacts to External Change on it — reloading live when clean, or raising a Conflict when there
/// are unsaved edits (INV-006/007). Choosing files and managing Tabs belong to the
/// <see cref="WorkspaceViewModel"/>, not here.
/// </summary>
public sealed class EditorSessionViewModel : ObservableObject, IDisposable
{
    private readonly IDocumentStore _store;
    private readonly IDocumentWatcher _watcher;
    private readonly IUiDispatcher _dispatcher;

    private string _markdown = string.Empty;
    private string? _filePath;
    private bool _hasUnsavedEdits;
    private bool _hasConflict;
    private string _conflictingDiskText = string.Empty;

    /// <summary>Creates a new, empty Editor Session over the given store, watcher, and dispatcher.</summary>
    /// <param name="store">The port used to load and save the Watched File.</param>
    /// <param name="watcher">The port that raises External Change for this session's Watched File.</param>
    /// <param name="dispatcher">Marshals External Change handling onto the UI thread.</param>
    public EditorSessionViewModel(IDocumentStore store, IDocumentWatcher watcher, IUiDispatcher dispatcher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _watcher.Changed += OnWatcherChanged;

        KeepMyEditsCommand = new RelayCommand(KeepMyEdits, () => HasConflict);
        ReloadFromDiskCommand = new RelayCommand(ReloadFromDisk, () => HasConflict);
    }

    /// <summary>
    /// The canonical Markdown source text of the current Markdown Document. Two-way bound to the
    /// WYSIWYG editor; assigning it from user editing marks the session as having unsaved edits.
    /// </summary>
    public string Markdown
    {
        get => _markdown;
        set
        {
            if (Set(ref _markdown, value))
            {
                HasUnsavedEdits = true;
            }
        }
    }

    /// <summary>The path of the Watched File backing this session, or <see langword="null"/> if unsaved.</summary>
    public string? FilePath
    {
        get => _filePath;
        private set
        {
            if (Set(ref _filePath, value))
            {
                Raise(nameof(Name));
                Raise(nameof(Title));
            }
        }
    }

    /// <summary>Whether the session holds edits not yet persisted to the Watched File.</summary>
    public bool HasUnsavedEdits
    {
        get => _hasUnsavedEdits;
        private set
        {
            if (Set(ref _hasUnsavedEdits, value))
            {
                Raise(nameof(Title));
            }
        }
    }

    /// <summary>
    /// Whether an External Change was detected while the session had unsaved edits and is awaiting
    /// the user's resolution (keep edits, or reload from disk).
    /// </summary>
    public bool HasConflict
    {
        get => _hasConflict;
        private set => Set(ref _hasConflict, value);
    }

    /// <summary>The Watched File's name, or "Untitled" when unsaved. Shown on the session's Tab.</summary>
    public string Name => FilePath is null ? "Untitled" : Path.GetFileName(FilePath);

    /// <summary>The <see cref="Name"/> with a "*" suffix when edits are unsaved; used as the window title.</summary>
    public string Title => HasUnsavedEdits ? $"{Name} *" : Name;

    /// <summary>Resolves a Conflict by keeping the unsaved edits and discarding the disk change.</summary>
    public ICommand KeepMyEditsCommand { get; }

    /// <summary>Resolves a Conflict by discarding unsaved edits and loading the on-disk contents.</summary>
    public ICommand ReloadFromDiskCommand { get; }

    /// <summary>Loads the Markdown file at <paramref name="path"/> into this session and watches it.</summary>
    /// <param name="path">The absolute path of the Watched File to load.</param>
    public async Task LoadAsync(string path)
    {
        var document = await _store.LoadAsync(path).ConfigureAwait(true);
        SetSourceText(document.Source.Text);
        FilePath = path;
        HasUnsavedEdits = false;
        ClearConflict();
        _watcher.Watch(path);
    }

    /// <summary>Saves this session's Markdown Document to <paramref name="path"/> and watches it.</summary>
    /// <param name="path">The absolute path of the Watched File to save to.</param>
    public async Task SaveAsync(string path)
    {
        await _store.SaveAsync(path, new MarkdownDocument(Markdown)).ConfigureAwait(true);
        FilePath = path;
        HasUnsavedEdits = false;
        ClearConflict();
        _watcher.Watch(path);
    }

    /// <summary>
    /// Handles an External Change to the Watched File: reloads live when the session is clean
    /// (INV-007), or raises a Conflict when there are unsaved edits (INV-006). Self-writes (disk
    /// contents already equal to the session) are ignored.
    /// </summary>
    /// <param name="path">The path reported as changed.</param>
    public async Task HandleExternalChangeAsync(string path)
    {
        if (FilePath is null || !string.Equals(path, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        MarkdownDocument disk;
        try
        {
            disk = await _store.LoadAsync(path).ConfigureAwait(true);
        }
        catch (IOException)
        {
            return;
        }

        if (disk.Source.Text == Markdown)
        {
            return;
        }

        if (ExternalChangeReconciler.Reconcile(HasUnsavedEdits) == ExternalChangeResolution.ReloadFromDisk)
        {
            SetSourceText(disk.Source.Text);
            HasUnsavedEdits = false;
        }
        else
        {
            _conflictingDiskText = disk.Source.Text;
            HasConflict = true;
        }
    }

    /// <summary>Stops watching the Watched File and releases this session's watcher subscription.</summary>
    public void Dispose()
    {
        _watcher.Changed -= OnWatcherChanged;
        _watcher.StopWatching();
    }

    private void KeepMyEdits() => ClearConflict();

    private void ReloadFromDisk()
    {
        SetSourceText(_conflictingDiskText);
        HasUnsavedEdits = false;
        ClearConflict();
    }

    private void ClearConflict()
    {
        HasConflict = false;
        _conflictingDiskText = string.Empty;
    }

    private void OnWatcherChanged(object? sender, ExternalChange change) =>
        _dispatcher.Post(() => _ = HandleExternalChangeAsync(change.Path));

    /// <summary>Replaces the source text without marking the session dirty (used by load/reload).</summary>
    private void SetSourceText(string text)
    {
        _markdown = text;
        Raise(nameof(Markdown));
    }
}
