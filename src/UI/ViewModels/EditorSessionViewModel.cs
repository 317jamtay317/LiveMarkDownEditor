using System.IO;
using System.Windows.Input;
using Application;
using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Editor Session realised for the UI: it holds the current Markdown Document's source text
/// (bound two-way to the WYSIWYG editor as the canonical model), the associated Watched File, and
/// whether unsaved edits exist, and it exposes the open, save, and new behaviours as commands.
/// It also reacts to External Change on the Watched File — reloading live when clean, or raising a
/// Conflict when there are unsaved edits (INV-006/007).
/// </summary>
public sealed class EditorSessionViewModel : ObservableObject
{
    private readonly IDocumentStore _store;
    private readonly IFilePicker _filePicker;
    private readonly IDocumentWatcher _watcher;
    private readonly IUiDispatcher _dispatcher;

    private string _markdown = string.Empty;
    private string? _filePath;
    private bool _hasUnsavedEdits;
    private bool _hasConflict;
    private string _conflictingDiskText = string.Empty;

    /// <summary>Creates an Editor Session over the given store, picker, watcher, and dispatcher.</summary>
    /// <param name="store">The port used to load and save the Watched File.</param>
    /// <param name="filePicker">The abstraction used to prompt for Watched File paths.</param>
    /// <param name="watcher">The port that raises External Change for the Watched File.</param>
    /// <param name="dispatcher">Marshals External Change handling onto the UI thread.</param>
    public EditorSessionViewModel(
        IDocumentStore store,
        IFilePicker filePicker,
        IDocumentWatcher watcher,
        IUiDispatcher dispatcher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        _watcher.Changed += OnWatcherChanged;

        NewCommand = new RelayCommand(New);
        OpenCommand = new AsyncRelayCommand(OpenAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasUnsavedEdits || FilePath is null);
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

    /// <summary>A display title: the Watched File name (or "Untitled") with a "*" when edits are unsaved.</summary>
    public string Title
    {
        get
        {
            var name = FilePath is null ? "Untitled" : Path.GetFileName(FilePath);
            return HasUnsavedEdits ? $"{name} *" : name;
        }
    }

    /// <summary>Starts a new, empty Markdown Document with no Watched File.</summary>
    public ICommand NewCommand { get; }

    /// <summary>Opens an existing Markdown file chosen by the user into the session.</summary>
    public ICommand OpenCommand { get; }

    /// <summary>Saves the current Markdown Document to its Watched File, prompting for a path if needed.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Resolves a Conflict by keeping the unsaved edits and discarding the disk change.</summary>
    public ICommand KeepMyEditsCommand { get; }

    /// <summary>Resolves a Conflict by discarding unsaved edits and loading the on-disk contents.</summary>
    public ICommand ReloadFromDiskCommand { get; }

    /// <summary>Resets the session to a new, empty Markdown Document.</summary>
    public void New()
    {
        _watcher.StopWatching();
        SetSourceText(string.Empty);
        FilePath = null;
        HasUnsavedEdits = false;
        ClearConflict();
    }

    /// <summary>Prompts for a Markdown file and loads it into the session.</summary>
    public async Task OpenAsync()
    {
        var path = _filePicker.PickOpen();
        if (path is null)
        {
            return;
        }

        var document = await _store.LoadAsync(path).ConfigureAwait(true);
        SetSourceText(document.Source.Text);
        FilePath = path;
        HasUnsavedEdits = false;
        ClearConflict();
        _watcher.Watch(path);
    }

    /// <summary>Saves the session, prompting for a Watched File path when the session has none.</summary>
    public async Task SaveAsync()
    {
        var path = FilePath ?? _filePicker.PickSave(suggestedFileName: "Untitled.md");
        if (path is null)
        {
            return;
        }

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

    /// <summary>Replaces the source text without marking the session dirty (used by load/new/reload).</summary>
    private void SetSourceText(string text)
    {
        _markdown = text;
        Raise(nameof(Markdown));
    }
}
