using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Workspace's Tabs: the two rows they stand in, the Pin Tab and Unpin Tab actions that move a Tab
/// between them (INV-071), and the two bulk closes — Close All Tabs and Close All But Pinned — which
/// are applications of Close Tab and so obey INV-010 for every Tab they touch (INV-072).
/// </summary>
/// <remarks>
/// The rows are the storage: there is no third collection of "all Tabs" to keep in step with them.
/// <see cref="Sessions"/> is the two rows read in order, which is what makes the partition of INV-071
/// true by construction rather than by maintenance.
/// </remarks>
public sealed partial class WorkspaceViewModel
{
    private readonly ObservableCollection<EditorSessionViewModel> _pinnedTabs = [];
    private readonly ObservableCollection<EditorSessionViewModel> _unpinnedTabs = [];

    private ReadOnlyObservableCollection<EditorSessionViewModel>? _pinnedRow;
    private ReadOnlyObservableCollection<EditorSessionViewModel>? _unpinnedRow;
    private ICommand? _pinTabCommand;
    private ICommand? _unpinTabCommand;
    private ICommand? _closeAllTabsCommand;
    private ICommand? _closeAllButPinnedCommand;

    /// <summary>
    /// The open Editor Sessions, one per Tab, in Tab order — the Pinned Row first, then the ordinary
    /// row (INV-071).
    /// </summary>
    public IReadOnlyList<EditorSessionViewModel> Sessions => [.. _pinnedTabs, .. _unpinnedTabs];

    /// <summary>
    /// The Pinned Row: every Pinned Tab, in the order it was pinned. Shown above the ordinary Tabs,
    /// and only while <see cref="HasPinnedTabs"/> (INV-071).
    /// </summary>
    public ReadOnlyObservableCollection<EditorSessionViewModel> PinnedTabs =>
        _pinnedRow ??= new ReadOnlyObservableCollection<EditorSessionViewModel>(_pinnedTabs);

    /// <summary>
    /// The Ordinary Row: every Tab that is not a Pinned Tab, in the order it joined the row (INV-071).
    /// </summary>
    public ReadOnlyObservableCollection<EditorSessionViewModel> UnpinnedTabs =>
        _unpinnedRow ??= new ReadOnlyObservableCollection<EditorSessionViewModel>(_unpinnedTabs);

    /// <summary>
    /// Whether any Tab is a Pinned Tab. The Pinned Row is shown only while this holds, and takes no
    /// space at all otherwise (INV-071).
    /// </summary>
    public bool HasPinnedTabs => _pinnedTabs.Count > 0;

    /// <summary>
    /// Whether the Ordinary Row is shown. Like the Pinned Row it collapses once it has no Tabs, so
    /// pinning the last one away — or Close All But Pinned — leaves the Pinned Row directly above the
    /// editing area rather than above an empty band. The exception keeps the strip from vanishing
    /// altogether: while nothing is pinned it is shown even when empty, so an empty Workspace still
    /// presents a tab strip (INV-071).
    /// </summary>
    public bool IsOrdinaryRowVisible => _unpinnedTabs.Count > 0 || !HasPinnedTabs;

    /// <summary>
    /// The Pinned Row's selected Tab — the Active Session when it is a Pinned Tab, and
    /// <see langword="null"/> when the Active Session is in the other row. Setting it to
    /// <see langword="null"/> is ignored: the two rows are two selectors over one Active Session, so
    /// the row that does not hold it must be able to show nothing selected without deselecting the
    /// Workspace itself (INV-008).
    /// </summary>
    public EditorSessionViewModel? SelectedPinnedTab
    {
        get => ActiveSession is { IsPinned: true } ? ActiveSession : null;
        set
        {
            if (value is not null)
            {
                ActiveSession = value;
            }
        }
    }

    /// <summary>
    /// The ordinary row's selected Tab — the counterpart of <see cref="SelectedPinnedTab"/>, bound by
    /// the same rule: a null set is ignored.
    /// </summary>
    public EditorSessionViewModel? SelectedUnpinnedTab
    {
        get => ActiveSession is { IsPinned: false } ? ActiveSession : null;
        set
        {
            if (value is not null)
            {
                ActiveSession = value;
            }
        }
    }

    /// <summary>Makes a Tab a Pinned Tab, moving it to the end of the Pinned Row. Parameter: the session (INV-071).</summary>
    public ICommand PinTabCommand =>
        _pinTabCommand ??= new AsyncRelayCommand<EditorSessionViewModel>(PinTabAsync);

    /// <summary>Returns a Pinned Tab to the end of the ordinary row. Parameter: the session (INV-071).</summary>
    public ICommand UnpinTabCommand =>
        _unpinTabCommand ??= new AsyncRelayCommand<EditorSessionViewModel>(UnpinTabAsync);

    /// <summary>Closes every Tab, Pinned Tabs included, leaving the Workspace empty (INV-072).</summary>
    public ICommand CloseAllTabsCommand =>
        _closeAllTabsCommand ??= new AsyncRelayCommand(CloseAllTabsAsync);

    /// <summary>Closes every Tab that is not a Pinned Tab, leaving the Pinned Row intact (INV-072).</summary>
    public ICommand CloseAllButPinnedCommand =>
        _closeAllButPinnedCommand ??= new AsyncRelayCommand(CloseAllButPinnedAsync);

    /// <summary>
    /// Pin Tab: makes <paramref name="session"/>'s Tab a Pinned Tab and moves it to the end of the
    /// Pinned Row. Pinning changes nothing else — not the Markdown Document, not the Editor Session,
    /// and not which Tab is active (INV-071). A Tab already pinned is left alone.
    /// </summary>
    /// <param name="session">The Editor Session whose Tab is being pinned.</param>
    public async Task PinTabAsync(EditorSessionViewModel? session)
    {
        if (MoveTab(session, pinned: true))
        {
            await PersistStateAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Unpin Tab: returns <paramref name="session"/>'s Tab to the end of the ordinary row. The inverse
    /// of <see cref="PinTabAsync"/> and bounded by the same rules (INV-071).
    /// </summary>
    /// <param name="session">The Editor Session whose Tab is being unpinned.</param>
    public async Task UnpinTabAsync(EditorSessionViewModel? session)
    {
        if (MoveTab(session, pinned: false))
        {
            await PersistStateAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Close All Tabs: closes every open Tab, Pinned Tabs included — a pin is not "pinned open"
    /// (INV-071). Each Tab with unsaved edits is asked about in turn and Cancel stops the whole
    /// action (INV-072).
    /// </summary>
    public Task CloseAllTabsAsync() => CloseTabsAsync(Sessions);

    /// <summary>
    /// Close All But Pinned: closes every Tab that is not a Pinned Tab, leaving the Pinned Row exactly
    /// as it was — the bulk close a pin protects a Tab from (INV-072).
    /// </summary>
    public Task CloseAllButPinnedAsync() => CloseTabsAsync([.. _unpinnedTabs]);

    /// <summary>Adds a newly opened Editor Session as a Tab at the end of the ordinary row.</summary>
    /// <param name="session">The Editor Session to give a Tab.</param>
    private void AddTab(EditorSessionViewModel session)
    {
        _unpinnedTabs.Add(session);
        RaiseRowsChanged();
    }

    /// <summary>Takes a closing Editor Session's Tab out of whichever row holds it.</summary>
    /// <param name="session">The Editor Session whose Tab is being removed.</param>
    private void RemoveTab(EditorSessionViewModel session)
    {
        if (!_pinnedTabs.Remove(session))
        {
            _unpinnedTabs.Remove(session);
        }

        RaiseRowsChanged();
    }

    /// <summary>
    /// Announces everything derived from the two rows' contents. Every path that adds, removes, or
    /// moves a Tab ends here, so a new row-derived property cannot be left un-raised on one of them.
    /// </summary>
    private void RaiseRowsChanged()
    {
        Raise(nameof(Sessions));
        Raise(nameof(HasPinnedTabs));
        Raise(nameof(IsOrdinaryRowVisible));
    }

    /// <summary>
    /// Moves a Tab into the Pinned Row or back out of it, returning whether it actually moved. The
    /// rows are the storage, so the move is a remove and an add — which is what keeps
    /// <see cref="EditorSessionViewModel.IsPinned"/> and row membership from disagreeing (INV-071).
    /// A Tab already in the requested row, or one not open at all, does not move.
    /// </summary>
    /// <param name="session">The Editor Session whose Tab is moving.</param>
    /// <param name="pinned">Whether the Tab is becoming a Pinned Tab.</param>
    private bool MoveTab(EditorSessionViewModel? session, bool pinned)
    {
        var from = pinned ? _unpinnedTabs : _pinnedTabs;
        var to = pinned ? _pinnedTabs : _unpinnedTabs;

        if (session is null || !from.Remove(session))
        {
            return false;
        }

        session.IsPinned = pinned;
        to.Add(session);

        RaiseRowsChanged();

        // The Active Session has not changed, but which row shows it selected has.
        Raise(nameof(SelectedPinnedTab));
        Raise(nameof(SelectedUnpinnedTab));
        return true;
    }

    /// <summary>
    /// Closes each of the given Tabs in turn through the same Close Tab that INV-010 governs, stopping
    /// at the first Tab the user chooses to keep. A Tab still open after its close was attempted is
    /// exactly that case — the user answered Cancel, or cancelled the save-as prompt — so the rest are
    /// left alone (INV-072).
    /// </summary>
    /// <param name="tabs">A snapshot of the Tabs to close, in the order to ask about them.</param>
    private async Task CloseTabsAsync(IReadOnlyList<EditorSessionViewModel> tabs)
    {
        foreach (var tab in tabs)
        {
            await CloseSessionAsync(tab).ConfigureAwait(true);

            if (_pinnedTabs.Contains(tab) || _unpinnedTabs.Contains(tab))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Puts the persisted Pinned Tabs back after a Restore, in the order they were persisted, so the
    /// Pinned Row is as the last run left it (INV-071). Restoring is not a change by the user, so it
    /// moves the Tabs without re-persisting them.
    /// </summary>
    /// <param name="pinnedDocuments">The Watched File paths that were Pinned Tabs.</param>
    private void RestorePinnedTabs(IReadOnlyList<string> pinnedDocuments)
    {
        foreach (var path in pinnedDocuments)
        {
            var tab = _unpinnedTabs.FirstOrDefault(session =>
                session.FilePath is not null &&
                string.Equals(session.FilePath, path, StringComparison.OrdinalIgnoreCase));

            MoveTab(tab, pinned: true);
        }
    }
}
