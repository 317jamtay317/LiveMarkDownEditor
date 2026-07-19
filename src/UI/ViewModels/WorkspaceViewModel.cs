using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Application;
using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Workspace: the editor shell that holds the open Editor Sessions as Tabs, tracks the Active
/// Session, and exposes the New / Open / Save / Close behaviours. It owns file selection (via the
/// <see cref="IFilePicker"/>) and Tab lifetime, delegating per-document state to each
/// <see cref="EditorSessionViewModel"/>. It enforces INV-008 (never empty, always one Active
/// Session), INV-009 (a file is open in at most one Tab), and INV-010 (closing with unsaved edits is
/// never silent).
/// </summary>
public sealed class WorkspaceViewModel : ObservableObject
{
    private readonly EditorSessionFactory _createSession;
    private readonly IFilePicker _filePicker;
    private readonly IUnsavedEditsPrompt _unsavedEditsPrompt;
    private readonly IWorkspaceStateStore _stateStore;
    private readonly ObservableCollection<EditorSessionViewModel> _sessions = [];

    private EditorSessionViewModel? _activeSession;
    private Domain.RecentFiles _recent = Domain.RecentFiles.Empty;
    private bool _isRestoring;
    private bool _isSourcePanelVisible;
    private bool _isPreviewPanelVisible;

    /// <summary>Creates a Workspace with a single empty Editor Session (INV-008).</summary>
    /// <param name="createSession">Factory that mints a fresh Editor Session (with its own watcher) per Tab.</param>
    /// <param name="filePicker">The abstraction used to prompt for Watched File paths.</param>
    /// <param name="unsavedEditsPrompt">Asks the user how to close a Tab with unsaved edits (INV-010).</param>
    /// <param name="linkPrompt">Asks the user for a Link's or Image's text and URL (INV-030).</param>
    /// <param name="documentPrinter">Sends the Visual Document to a printer for Print (INV-034).</param>
    /// <param name="renderer">Renders a copied selection to HTML for the clipboard's HTML flavor (INV-035).</param>
    /// <param name="flowchartBuilder">Opens the Flowchart Builder for Open Flowchart Builder (INV-053).</param>
    /// <param name="appearance">The visual-theme ViewModel exposed to the shell's chrome.</param>
    /// <param name="export">The Export as HTML and PDF actions exposed to the shell's chrome (INV-032, INV-033).</param>
    /// <param name="folder">The Folder Workspace shell — the file-tree panel for browsing a folder (INV-042/043/044/045).</param>
    /// <param name="sideDock">The Side Dock — the tabbed left panel hosting the Folder and Navigation panels (INV-046).</param>
    /// <param name="stateStore">Persists and restores the Workspace across runs — open Tabs, Recent Files, and the Folder Workspace (INV-037, INV-045).</param>
    public WorkspaceViewModel(
        EditorSessionFactory createSession,
        IFilePicker filePicker,
        IUnsavedEditsPrompt unsavedEditsPrompt,
        ILinkPrompt linkPrompt,
        IDocumentPrinter documentPrinter,
        IMarkdownRenderer renderer,
        IFlowchartBuilder flowchartBuilder,
        AppearanceViewModel appearance,
        ExportViewModel export,
        FolderWorkspaceViewModel folder,
        SideDockViewModel sideDock,
        IWorkspaceStateStore stateStore)
    {
        _createSession = createSession ?? throw new ArgumentNullException(nameof(createSession));
        _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        _unsavedEditsPrompt = unsavedEditsPrompt ?? throw new ArgumentNullException(nameof(unsavedEditsPrompt));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        LinkPrompt = linkPrompt ?? throw new ArgumentNullException(nameof(linkPrompt));
        DocumentPrinter = documentPrinter ?? throw new ArgumentNullException(nameof(documentPrinter));
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        FlowchartBuilder = flowchartBuilder ?? throw new ArgumentNullException(nameof(flowchartBuilder));
        Appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));
        Export = export ?? throw new ArgumentNullException(nameof(export));
        Folder = folder ?? throw new ArgumentNullException(nameof(folder));
        SideDock = sideDock ?? throw new ArgumentNullException(nameof(sideDock));

        // The Folder Workspace opens a File in a Tab through the same dedupe-and-load path the picker
        // uses (INV-009/043), and persists alongside the open Tabs and Recent Files (INV-045).
        Folder.OpenFile = OpenPathAsync;
        Folder.PersistState = PersistStateAsync;

        Sessions = new ReadOnlyObservableCollection<EditorSessionViewModel>(_sessions);

        NewCommand = new RelayCommand(New);
        OpenCommand = new AsyncRelayCommand(OpenAsync);
        SaveCommand = new AsyncRelayCommand(SaveActiveAsync, CanSaveActive);
        CloseSessionCommand = new AsyncRelayCommand<EditorSessionViewModel>(CloseSessionAsync);
        OpenRecentCommand = new AsyncRelayCommand<string>(OpenRecentAsync);
        FollowLinkCommand = new AsyncRelayCommand<string>(FollowMarkdownLinkAsync);
        ToggleSourcePanelCommand = new RelayCommand(ToggleSourcePanel);
        TogglePreviewPanelCommand = new RelayCommand(TogglePreviewPanel);

        New();
    }

    /// <summary>The open Editor Sessions, one per Tab, in Tab order.</summary>
    public ReadOnlyObservableCollection<EditorSessionViewModel> Sessions { get; }

    /// <summary>
    /// The Active Session: the Tab currently shown in the editing pane and targeted by Save, or
    /// <see langword="null"/> when the Workspace is empty (every Tab has been closed).
    /// </summary>
    public EditorSessionViewModel? ActiveSession
    {
        get => _activeSession;
        set
        {
            if (Set(ref _activeSession, value))
            {
                Raise(nameof(HasOpenSessions));
                Raise(nameof(IsWorkspaceEmpty));
            }
        }
    }

    /// <summary>Whether the Workspace has at least one open Editor Session (a Tab to edit in).</summary>
    public bool HasOpenSessions => ActiveSession is not null;

    /// <summary>
    /// Whether the Workspace is empty — no open Editor Session. When true the editing area shows the
    /// Empty-Workspace Placeholder instead of an editor.
    /// </summary>
    public bool IsWorkspaceEmpty => ActiveSession is null;

    /// <summary>The visual-theme ViewModel for the shell's chrome (light/dark toggle).</summary>
    public AppearanceViewModel Appearance { get; }

    /// <summary>
    /// The Export as HTML actions (INV-032). Its commands take the Editor Session to export as their
    /// parameter, so the Command Bar passes the <see cref="ActiveSession"/>.
    /// </summary>
    public ExportViewModel Export { get; }

    /// <summary>
    /// The Folder Workspace shell — the toggleable Folder Panel for opening a folder and browsing its
    /// Markdown Documents as a tree (INV-042/043/044/045). Workspace-wide: unlike the Navigation Panel
    /// it is independent of the Active Session.
    /// </summary>
    public FolderWorkspaceViewModel Folder { get; }

    /// <summary>
    /// The Side Dock — the tabbed left panel that hosts the Folder Panel and the Navigation Panel as
    /// tabs, so the two do not each take a column of their own (INV-046). It owns the Navigation Panel's
    /// visibility toggle.
    /// </summary>
    public SideDockViewModel SideDock { get; }

    /// <summary>
    /// The Recent Files — recently opened or saved Watched File paths, newest first — shown in the
    /// Open Recent menu and mirrored to the Windows Jump List. Persisted across runs (INV-037).
    /// </summary>
    public IReadOnlyList<string> RecentFiles => _recent.Paths;

    /// <summary>
    /// The Link Prompt the editing surface asks for a Link's or Image's text and URL (INV-030).
    /// Exposed so the View can hand it to the <c>MarkdownRichEditor</c>, which owns the Formatting
    /// Actions but is composed in XAML rather than by the container.
    /// </summary>
    public ILinkPrompt LinkPrompt { get; }

    /// <summary>
    /// The printer the editing surface sends the Visual Document to for Print (INV-034). Exposed so
    /// the View can hand it to the <c>MarkdownRichEditor</c>, which owns Print but is composed in XAML
    /// rather than by the container — the same reason <see cref="LinkPrompt"/> is exposed.
    /// </summary>
    public IDocumentPrinter DocumentPrinter { get; }

    /// <summary>
    /// The renderer the editing surface uses to render a copied selection to HTML for the clipboard's
    /// HTML flavor (INV-035). Exposed so the View can hand it to the <c>MarkdownRichEditor</c>, which
    /// owns Copy but is composed in XAML rather than by the container — as with <see cref="LinkPrompt"/>.
    /// </summary>
    public IMarkdownRenderer Renderer { get; }

    /// <summary>
    /// The Flowchart Builder that Open Flowchart Builder opens (INV-053). Exposed so the View can hand
    /// it to the <c>MarkdownRichEditor</c>, which owns the action but is composed in XAML rather than by
    /// the container — the same reason <see cref="LinkPrompt"/> is exposed.
    /// </summary>
    public IFlowchartBuilder FlowchartBuilder { get; }

    /// <summary>
    /// Whether the Source Panel — the raw, editable Markdown source of the Active Session shown
    /// alongside the Visual Document — is visible. Hidden until the user toggles it on.
    /// Presentation-only: toggling it never changes any Markdown Document (INV-014).
    /// </summary>
    public bool IsSourcePanelVisible
    {
        get => _isSourcePanelVisible;
        private set => Set(ref _isSourcePanelVisible, value);
    }

    /// <summary>
    /// Whether the Preview Panel — the live Diagram Preview of the Mermaid Diagram at the caret, shown
    /// beside the Visual Document — is visible. Hidden until the user toggles it on. Presentation-only:
    /// toggling it never changes any Markdown Document (INV-048).
    /// </summary>
    public bool IsPreviewPanelVisible
    {
        get => _isPreviewPanelVisible;
        private set => Set(ref _isPreviewPanelVisible, value);
    }

    /// <summary>Opens a new, empty Editor Session in a new Tab and activates it.</summary>
    public ICommand NewCommand { get; }

    /// <summary>Prompts for a Markdown file and opens it in a Tab (activating an existing one if already open).</summary>
    public ICommand OpenCommand { get; }

    /// <summary>Saves the Active Session, prompting for a path when it has no Watched File yet.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Closes a Tab, prompting to save when it has unsaved edits (INV-010). Parameter: the session.</summary>
    public ICommand CloseSessionCommand { get; }

    /// <summary>Opens a Recent File by its path, dropping it from the list if it has since gone. Parameter: the path.</summary>
    public ICommand OpenRecentCommand { get; }

    /// <summary>Opens a followed Markdown Link's file in a new Tab. Parameter: the absolute path (INV-038).</summary>
    public ICommand FollowLinkCommand { get; }

    /// <summary>Shows the Source Panel if hidden, or hides it if shown.</summary>
    public ICommand ToggleSourcePanelCommand { get; }

    /// <summary>Shows the Preview Panel if hidden, or hides it if shown.</summary>
    public ICommand TogglePreviewPanelCommand { get; }

    /// <summary>Opens a new, empty Editor Session in a new Tab and makes it the Active Session.</summary>
    public void New()
    {
        var session = _createSession();
        _sessions.Add(session);
        ActiveSession = session;
    }

    /// <summary>
    /// Prompts for a Markdown file and opens it. If the file is already open in the Workspace, its
    /// existing Tab is activated instead of opening a duplicate (INV-009).
    /// </summary>
    public async Task OpenAsync()
    {
        var path = _filePicker.PickOpen();
        if (path is null)
        {
            return;
        }

        await OpenPathAsync(path).ConfigureAwait(true);
    }

    /// <summary>
    /// Opens the Markdown file at <paramref name="path"/> — the path-first counterpart of
    /// <see cref="OpenAsync"/>, used for a Startup Document handed over at launch or forwarded by a
    /// later launch (INV-020). If the file is already open in the Workspace, its existing Tab is
    /// activated instead of opening a duplicate (INV-009).
    /// </summary>
    /// <param name="path">The absolute path of the Markdown file to open.</param>
    public async Task OpenPathAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var alreadyOpen = _sessions.FirstOrDefault(session =>
            session.FilePath is not null &&
            string.Equals(session.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (alreadyOpen is not null)
        {
            ActiveSession = alreadyOpen;
        }
        else
        {
            var opened = _createSession();
            await opened.LoadAsync(path).ConfigureAwait(true);
            _sessions.Add(opened);
            ActiveSession = opened;
        }

        // Restoring seeds the Recent Files straight from persisted state, so it must not reorder them
        // by re-remembering each restored path (INV-037).
        if (!_isRestoring)
        {
            RememberRecent(path);
            await PersistStateAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Restores the Workspace from the last run: reopens the Watched Files that were open — skipping
    /// any that have since gone — and loads the Recent Files. Only saved documents are restored; an
    /// unsaved Tab was never persisted (INV-037). Call once at startup.
    /// </summary>
    public async Task RestoreAsync()
    {
        var state = _stateStore.Load();
        _recent = Domain.RecentFiles.From(state.RecentFiles);
        Raise(nameof(RecentFiles));

        // Reopen the Folder Workspace that was open last run, skipping a root that has gone (INV-045).
        await Folder.RestoreAsync(state.WorkspaceFolder).ConfigureAwait(true);

        // The empty Tab the constructor seeds is a placeholder; replace it if we restore real Tabs.
        var placeholder = _sessions.Count == 1 && _sessions[0].FilePath is null && !_sessions[0].HasUnsavedEdits
            ? _sessions[0]
            : null;

        _isRestoring = true;
        try
        {
            foreach (var path in state.OpenDocuments)
            {
                try
                {
                    await OpenPathAsync(path).ConfigureAwait(true);
                }
                catch (IOException)
                {
                    // A Watched File that has gone is simply not restored (INV-037).
                }
            }
        }
        finally
        {
            _isRestoring = false;
        }

        if (placeholder is not null && _sessions.Count > 1)
        {
            RemoveSession(placeholder);
        }
    }

    /// <summary>
    /// Persists the Workspace: the open Tabs' Watched File paths (unsaved Tabs are skipped) and the
    /// Recent Files, so the next run can restore them (INV-037).
    /// </summary>
    public Task PersistStateAsync()
    {
        var openDocuments = _sessions
            .Where(session => session.FilePath is not null)
            .Select(session => session.FilePath!)
            .ToList();

        return _stateStore.SaveAsync(new WorkspaceState(openDocuments, _recent.Paths, Folder.Folder?.RootPath));
    }

    /// <summary>
    /// Opens a Recent File. A path that no longer exists is dropped from the Recent Files rather than
    /// opened, so the list keeps only files that are still there.
    /// </summary>
    /// <param name="path">The Recent File's path.</param>
    public async Task OpenRecentAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await OpenPathAsync(path).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // A Recent File that has gone drops off the list rather than opening.
            _recent = Domain.RecentFiles.From(_recent.Paths.Where(
                existing => !string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)));
            Raise(nameof(RecentFiles));
            await PersistStateAsync().ConfigureAwait(true);
        }
    }

    private async Task FollowMarkdownLinkAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await OpenPathAsync(path).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // A Link to a Markdown file that isn't there opens nothing.
        }
    }

    private void RememberRecent(string path)
    {
        _recent = _recent.Add(path);
        Raise(nameof(RecentFiles));
    }

    /// <summary>Saves the Active Session, prompting for a path when it has no Watched File yet.</summary>
    public Task SaveActiveAsync() =>
        ActiveSession is null ? Task.CompletedTask : TrySaveAsync(ActiveSession);

    /// <summary>
    /// Closes the given Tab. When it has unsaved edits the user is asked to Save, Discard, or Cancel;
    /// Cancel (or cancelling the save-as prompt) aborts the close and keeps the Tab (INV-010). Closing
    /// the last Tab opens a fresh empty one so the Workspace is never empty (INV-008).
    /// </summary>
    /// <param name="session">The Editor Session whose Tab is being closed.</param>
    public async Task CloseSessionAsync(EditorSessionViewModel? session)
    {
        if (session is null)
        {
            return;
        }

        if (session.HasUnsavedEdits)
        {
            var decision = _unsavedEditsPrompt.Confirm(session.Name);
            if (decision == UnsavedEditsDecision.Cancel)
            {
                return;
            }

            // Save chosen but the save-as prompt was cancelled → abort the close, keep the Tab.
            if (decision == UnsavedEditsDecision.Save && !await TrySaveAsync(session).ConfigureAwait(true))
            {
                return;
            }
        }

        RemoveSession(session);
        await PersistStateAsync().ConfigureAwait(true);
    }

    private void ToggleSourcePanel() => IsSourcePanelVisible = !IsSourcePanelVisible;

    private void TogglePreviewPanel() => IsPreviewPanelVisible = !IsPreviewPanelVisible;

    private bool CanSaveActive() =>
        ActiveSession is not null && (ActiveSession.HasUnsavedEdits || ActiveSession.FilePath is null);

    private async Task<bool> TrySaveAsync(EditorSessionViewModel session)
    {
        var path = session.FilePath ?? _filePicker.PickSave(suggestedFileName: "Untitled.md");
        if (path is null)
        {
            return false;
        }

        await session.SaveAsync(path).ConfigureAwait(true);
        RememberRecent(path);
        await PersistStateAsync().ConfigureAwait(true);
        return true;
    }

    private void RemoveSession(EditorSessionViewModel session)
    {
        var index = _sessions.IndexOf(session);
        if (index < 0)
        {
            return;
        }

        // Move activation onto a neighbour before removing, so the bound tab-strip selection never
        // transiently goes null while a middle Tab is closed.
        if (ActiveSession == session && _sessions.Count > 1)
        {
            var neighbour = index == _sessions.Count - 1 ? index - 1 : index + 1;
            ActiveSession = _sessions[neighbour];
        }

        _sessions.RemoveAt(index);
        session.Dispose();

        if (_sessions.Count == 0)
        {
            // INV-008: the Workspace may be empty — leave it with no Active Session (do not re-seed).
            // The shell shows the Empty-Workspace Placeholder until the user opens or creates a document.
            ActiveSession = null;
        }
    }
}
