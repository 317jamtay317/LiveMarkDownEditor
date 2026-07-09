using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
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
    private readonly ObservableCollection<EditorSessionViewModel> _sessions = [];

    private EditorSessionViewModel? _activeSession;
    private bool _isNavigationPanelVisible;
    private bool _isSourcePanelVisible;

    /// <summary>Creates a Workspace with a single empty Editor Session (INV-008).</summary>
    /// <param name="createSession">Factory that mints a fresh Editor Session (with its own watcher) per Tab.</param>
    /// <param name="filePicker">The abstraction used to prompt for Watched File paths.</param>
    /// <param name="unsavedEditsPrompt">Asks the user how to close a Tab with unsaved edits (INV-010).</param>
    /// <param name="appearance">The visual-theme ViewModel exposed to the shell's chrome.</param>
    public WorkspaceViewModel(
        EditorSessionFactory createSession,
        IFilePicker filePicker,
        IUnsavedEditsPrompt unsavedEditsPrompt,
        AppearanceViewModel appearance)
    {
        _createSession = createSession ?? throw new ArgumentNullException(nameof(createSession));
        _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        _unsavedEditsPrompt = unsavedEditsPrompt ?? throw new ArgumentNullException(nameof(unsavedEditsPrompt));
        Appearance = appearance ?? throw new ArgumentNullException(nameof(appearance));

        Sessions = new ReadOnlyObservableCollection<EditorSessionViewModel>(_sessions);

        NewCommand = new RelayCommand(New);
        OpenCommand = new AsyncRelayCommand(OpenAsync);
        SaveCommand = new AsyncRelayCommand(SaveActiveAsync, CanSaveActive);
        CloseSessionCommand = new AsyncRelayCommand<EditorSessionViewModel>(CloseSessionAsync);
        ToggleNavigationPanelCommand = new RelayCommand(ToggleNavigationPanel);
        ToggleSourcePanelCommand = new RelayCommand(ToggleSourcePanel);

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
    /// Whether the Navigation Panel — the left-edge Outline of the Active Session — is shown. Hidden
    /// until the user toggles it on. Presentation-only: toggling it never changes any document (INV-012).
    /// </summary>
    public bool IsNavigationPanelVisible
    {
        get => _isNavigationPanelVisible;
        private set => Set(ref _isNavigationPanelVisible, value);
    }

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

    /// <summary>Opens a new, empty Editor Session in a new Tab and activates it.</summary>
    public ICommand NewCommand { get; }

    /// <summary>Prompts for a Markdown file and opens it in a Tab (activating an existing one if already open).</summary>
    public ICommand OpenCommand { get; }

    /// <summary>Saves the Active Session, prompting for a path when it has no Watched File yet.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Closes a Tab, prompting to save when it has unsaved edits (INV-010). Parameter: the session.</summary>
    public ICommand CloseSessionCommand { get; }

    /// <summary>Shows the Navigation Panel if hidden, or hides it if shown.</summary>
    public ICommand ToggleNavigationPanelCommand { get; }

    /// <summary>Shows the Source Panel if hidden, or hides it if shown.</summary>
    public ICommand ToggleSourcePanelCommand { get; }

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

        var alreadyOpen = _sessions.FirstOrDefault(session =>
            session.FilePath is not null &&
            string.Equals(session.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (alreadyOpen is not null)
        {
            ActiveSession = alreadyOpen;
            return;
        }

        var opened = _createSession();
        await opened.LoadAsync(path).ConfigureAwait(true);
        _sessions.Add(opened);
        ActiveSession = opened;
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
    }

    private void ToggleNavigationPanel() => IsNavigationPanelVisible = !IsNavigationPanelVisible;

    private void ToggleSourcePanel() => IsSourcePanelVisible = !IsSourcePanelVisible;

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
