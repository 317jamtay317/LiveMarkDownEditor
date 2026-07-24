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
/// are unsaved edits (INV-006/007). While a Conflict awaits resolution it can also show the Conflict
/// Difference between the two sides (View Difference, INV-021), comparing the Canonical Markdown of
/// each side so only differences of content are shown (INV-025). Choosing files and managing Tabs
/// belong to the <see cref="WorkspaceViewModel"/>, not here.
/// </summary>
public sealed class EditorSessionViewModel : ObservableObject, IDisposable
{
    private readonly IDocumentStore _store;
    private readonly IDocumentWatcher _watcher;
    private readonly IUiDispatcher _dispatcher;
    private readonly IMarkdownRoundTrip _roundTrip;
    private readonly RelayCommand _keepMyEditsCommand;
    private readonly RelayCommand _reloadFromDiskCommand;
    private readonly RelayCommand _viewDifferenceCommand;

    private string _markdown = string.Empty;
    private string? _filePath;
    private bool _hasUnsavedEdits;
    private bool _hasConflict;
    private string _conflictingDiskText = string.Empty;
    private bool _isDifferenceVisible;
    private IReadOnlyList<DifferenceLine> _differenceLines = [];
    private IReadOnlyList<ChangedRegion> _changeHighlight = [];

    /// <summary>Creates a new, empty Editor Session over the given store, watcher, dispatcher, and Round-Trip.</summary>
    /// <param name="store">The port used to load and save the Watched File.</param>
    /// <param name="watcher">The port that raises External Change for this session's Watched File.</param>
    /// <param name="dispatcher">Marshals External Change handling onto the UI thread.</param>
    /// <param name="roundTrip">Yields the Canonical Markdown of each side of a Conflict Difference (INV-025).</param>
    public EditorSessionViewModel(
        IDocumentStore store,
        IDocumentWatcher watcher,
        IUiDispatcher dispatcher,
        IMarkdownRoundTrip roundTrip)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _watcher = watcher ?? throw new ArgumentNullException(nameof(watcher));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _roundTrip = roundTrip ?? throw new ArgumentNullException(nameof(roundTrip));

        _watcher.Changed += OnWatcherChanged;

        _keepMyEditsCommand = new RelayCommand(KeepMyEdits, () => HasConflict);
        _reloadFromDiskCommand = new RelayCommand(ReloadFromDisk, () => HasConflict);
        _viewDifferenceCommand = new RelayCommand(ToggleViewDifference, () => HasConflict);
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

                // The user has taken over the text, so a Change Highlight would no longer describe
                // what is on screen (INV-060).
                ChangeHighlight = [];
                RefreshDifferenceIfVisible();
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
                Raise(nameof(BaseDirectory));
            }
        }
    }

    /// <summary>
    /// The Base Directory: the folder the Watched File lives in, which a relative Image Source
    /// resolves against. <see langword="null"/> while the session is unsaved — an Image written
    /// "beside this document" names no folder until the document has one (INV-031).
    /// </summary>
    public string? BaseDirectory => FilePath is null ? null : Path.GetDirectoryName(FilePath);

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
    /// the user's resolution (keep edits, reload from disk, or view the difference).
    /// </summary>
    public bool HasConflict
    {
        get => _hasConflict;
        private set
        {
            if (Set(ref _hasConflict, value))
            {
                RequeryConflictCommands();
            }
        }
    }

    /// <summary>
    /// Whether the Conflict Difference is shown over the editing area (View Difference, INV-021).
    /// Presentation-only: it never affects the Markdown Document or the Conflict.
    /// </summary>
    public bool IsDifferenceVisible
    {
        get => _isDifferenceVisible;
        private set => Set(ref _isDifferenceVisible, value);
    }

    /// <summary>
    /// The Conflict Difference currently shown — the Difference Lines between the unsaved source
    /// text and the conflicting on-disk contents. Empty while the difference is hidden.
    /// </summary>
    public IReadOnlyList<DifferenceLine> DifferenceLines
    {
        get => _differenceLines;
        private set => Set(ref _differenceLines, value);
    }

    /// <summary>
    /// The Change Highlight: the Changed Regions of the live reload that has just happened, for the
    /// editor to shade briefly so the reader can see what another user or an AI changed (INV-060).
    /// Empty whenever there is nothing to show — no reload has happened, or an edit, a load, or a
    /// save has since made the last one stale.
    /// </summary>
    public IReadOnlyList<ChangedRegion> ChangeHighlight
    {
        get => _changeHighlight;
        private set => Set(ref _changeHighlight, value);
    }

    /// <summary>The Watched File's name, or "Untitled" when unsaved. Shown on the session's Tab.</summary>
    public string Name => FilePath is null ? "Untitled" : Path.GetFileName(FilePath);

    /// <summary>The <see cref="Name"/> with a "*" suffix when edits are unsaved; used as the window title.</summary>
    public string Title => HasUnsavedEdits ? $"{Name} *" : Name;

    /// <summary>Resolves a Conflict by keeping the unsaved edits and discarding the disk change.</summary>
    public ICommand KeepMyEditsCommand => _keepMyEditsCommand;

    /// <summary>Resolves a Conflict by discarding unsaved edits and loading the on-disk contents.</summary>
    public ICommand ReloadFromDiskCommand => _reloadFromDiskCommand;

    /// <summary>
    /// Shows the Conflict Difference over the editing area, or hides it again — the action toggles.
    /// Available only while a Conflict awaits resolution; it resolves nothing itself (INV-021).
    /// </summary>
    public ICommand ViewDifferenceCommand => _viewDifferenceCommand;

    /// <summary>Loads the Markdown file at <paramref name="path"/> into this session and watches it.</summary>
    /// <param name="path">The absolute path of the Watched File to load.</param>
    public async Task LoadAsync(string path)
    {
        var document = await _store.LoadAsync(path).ConfigureAwait(true);
        SetSourceText(document.Source.Text);
        FilePath = path;
        HasUnsavedEdits = false;
        ChangeHighlight = [];
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
        ChangeHighlight = [];
        ClearConflict();
        _watcher.Watch(path);
    }

    /// <summary>
    /// Handles an External Change to the Watched File: reloads live when the session is clean
    /// (INV-007), or raises a Conflict when there are unsaved edits (INV-006). An External Change
    /// that changes no content — the session's own save, or another writer restyling the file — is
    /// ignored (INV-026).
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

        if (ChangesNoContent(disk.Source.Text))
        {
            return;
        }

        if (ExternalChangeReconciler.Reconcile(HasUnsavedEdits) == ExternalChangeResolution.ReloadFromDisk)
        {
            ReloadInto(disk.Source.Text);
        }
        else
        {
            _conflictingDiskText = disk.Source.Text;
            HasConflict = true;
            RefreshDifferenceIfVisible();
        }
    }

    /// <summary>Stops watching the Watched File and releases this session's watcher subscription.</summary>
    public void Dispose()
    {
        _watcher.Changed -= OnWatcherChanged;
        _watcher.StopWatching();
    }

    /// <summary>
    /// Whether the Watched File's new contents say what this session already says, differing from it
    /// in bytes alone — the session's own save, or another writer restyling the file (INV-026).
    /// </summary>
    /// <param name="diskText">The Watched File's new on-disk contents.</param>
    private bool ChangesNoContent(string diskText) =>
        diskText == Markdown || _roundTrip.RoundTrip(diskText) == _roundTrip.RoundTrip(Markdown);

    private void KeepMyEdits() => ClearConflict();

    private void ReloadFromDisk()
    {
        var disk = _conflictingDiskText;
        ClearConflict();
        ReloadInto(disk);
    }

    /// <summary>
    /// Replaces the session's source text with what arrived from disk and publishes the Change
    /// Highlight describing what that did (INV-060). The new text is set <em>first</em>: the Changed
    /// Regions are numbered within it, so the editor must already be showing it when they arrive.
    /// </summary>
    /// <param name="diskText">The Watched File's contents, which become the session's text.</param>
    private void ReloadInto(string diskText)
    {
        var replaced = Markdown;
        SetSourceText(diskText);
        HasUnsavedEdits = false;
        ChangeHighlight = ReloadDifference.Compute(new MarkdownSource(replaced), new MarkdownSource(diskText));
    }

    /// <summary>Shows the Conflict Difference, or hides it when already shown (INV-021).</summary>
    private void ToggleViewDifference()
    {
        if (IsDifferenceVisible)
        {
            HideDifference();
            return;
        }

        RefreshDifference();
        IsDifferenceVisible = true;
    }

    /// <summary>Recomputes the shown Conflict Difference after either side changed.</summary>
    private void RefreshDifferenceIfVisible()
    {
        if (IsDifferenceVisible)
        {
            RefreshDifference();
        }
    }

    /// <summary>
    /// Recomputes the Conflict Difference over the Canonical Markdown of each side (INV-025), so a
    /// Watched File authored in another Markdown style differs only where its content differs.
    /// </summary>
    private void RefreshDifference() =>
        DifferenceLines = ConflictDifference.Compute(
            new MarkdownSource(_roundTrip.RoundTrip(Markdown)),
            new MarkdownSource(_roundTrip.RoundTrip(_conflictingDiskText)));

    private void HideDifference()
    {
        IsDifferenceVisible = false;
        DifferenceLines = [];
    }

    /// <summary>
    /// Requeries the commands whose availability follows <see cref="HasConflict"/>. A Conflict is
    /// raised by the file watcher rather than by user input, so the conflict bar's buttons would
    /// otherwise stay disabled until the next input requeried them.
    /// </summary>
    private void RequeryConflictCommands()
    {
        _keepMyEditsCommand.RaiseCanExecuteChanged();
        _reloadFromDiskCommand.RaiseCanExecuteChanged();
        _viewDifferenceCommand.RaiseCanExecuteChanged();
    }

    private void ClearConflict()
    {
        HasConflict = false;
        _conflictingDiskText = string.Empty;
        HideDifference();
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
