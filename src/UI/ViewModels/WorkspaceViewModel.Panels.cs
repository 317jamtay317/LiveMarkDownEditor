using System.Windows.Input;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Workspace's Panel Chrome surface: what the View binds to for every Dockable Panel — its
/// visibility, its open and pinned state, the Auto-Hide Bars' tabs, the open Panel Flyout — and the
/// commands its Panel Headers, Auto-Hide Bars, and View Menu invoke (INV-062, INV-063). The
/// placements those commands produce are resolved in <c>WorkspaceViewModel.PanelPlacement.cs</c>, and
/// persisted in <c>WorkspaceViewModel.PanelLayout.cs</c>.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private bool _isSourcePanelRequested;
    private bool _isPreviewPanelRequested;
    private bool _isEditorPaneOpen = true;
    private bool _isEditorPanePinned = true;
    private bool _isSourcePanelPinned = true;
    private bool _isPreviewPanelPinned = true;
    private bool _isFolderPanelPinned = true;
    private bool _isNavigationPanelPinned = true;
    private DockablePanel? _flyoutPanel;
    private double _workspaceWidth;
    private PanelVisibility _resolved;
    private PanelChromeState _lastChrome;
    private bool _isRecomputingPanels;
    private bool _isPanelChromeReady;
    private bool _isRestoringPanels;

    private RelayCommand _toggleSourcePanelCommand = null!;
    private RelayCommand _togglePreviewPanelCommand = null!;
    private RelayCommand _toggleEditorPaneCommand = null!;
    private RelayCommand<DockablePanel> _closePanelCommand = null!;
    private RelayCommand<DockablePanel> _togglePinCommand = null!;
    private RelayCommand<DockablePanel> _toggleFlyoutCommand = null!;
    private RelayCommand _dismissFlyoutCommand = null!;

    /// <summary>
    /// Whether the Source Panel — the raw, editable Markdown source of the Active Session shown
    /// alongside the Visual Document — is visible: Docked, and not auto-collapsed for width (Compact
    /// Layout). Presentation-only: neither toggling nor collapsing it changes any Markdown Document
    /// (INV-014, INV-059, INV-062).
    /// </summary>
    public bool IsSourcePanelVisible => _resolved.Source;

    /// <summary>
    /// Whether the Preview Panel — the live Diagram Preview of the Mermaid Diagram at the caret, shown
    /// beside the Visual Document — is visible: Docked, and not auto-collapsed for width (Compact
    /// Layout). Presentation-only: neither toggling nor collapsing it changes any Markdown Document
    /// (INV-048, INV-059, INV-062).
    /// </summary>
    public bool IsPreviewPanelVisible => _resolved.Preview;

    /// <summary>
    /// Whether the Editor Pane — the pane hosting the Active Session's Visual Document — is Docked.
    /// On by default; it can be Closed or Auto-Hidden only while the Source Panel is Docked in its
    /// stead (INV-063). Presentation-only (INV-062).
    /// </summary>
    public bool IsEditorPaneVisible => PanelChrome.IsDocked(ChromeState, DockablePanel.EditorPane);

    /// <summary>Whether the Editor Pane is open — Docked or Auto-Hidden, as opposed to Closed (INV-062).</summary>
    public bool IsEditorPaneOpen => _isEditorPaneOpen;

    /// <summary>Whether the Source Panel is open — Docked or Auto-Hidden, as opposed to Closed (INV-062).</summary>
    public bool IsSourcePanelOpen => _isSourcePanelRequested;

    /// <summary>Whether the Preview Panel is open — Docked or Auto-Hidden, as opposed to Closed (INV-062).</summary>
    public bool IsPreviewPanelOpen => _isPreviewPanelRequested;

    /// <summary>Whether the Editor Pane is pinned — its Panel Header's Pin Toggle state (INV-062).</summary>
    public bool IsEditorPanePinned => _isEditorPanePinned;

    /// <summary>Whether the Source Panel is pinned — its Panel Header's Pin Toggle state (INV-062).</summary>
    public bool IsSourcePanelPinned => _isSourcePanelPinned;

    /// <summary>Whether the Preview Panel is pinned — its Panel Header's Pin Toggle state (INV-062).</summary>
    public bool IsPreviewPanelPinned => _isPreviewPanelPinned;

    /// <summary>
    /// Whether the Source Panel is the primary Document Pane — filling the editing area in the Editor
    /// Pane's stead while the editor is not Docked (INV-063). Its Panel Column fills rather than
    /// keeping a dragged width.
    /// </summary>
    public bool IsSourcePanelPrimary => !IsEditorPaneVisible;

    /// <summary>
    /// Whether the Panel Splitter between the Visual Document and the Source Panel is shown — only
    /// while both Document Panes are visible; a primary Source Panel has no editor beside it to split
    /// against (INV-056, INV-063).
    /// </summary>
    public bool IsSourceSplitterVisible => IsEditorPaneVisible && IsSourcePanelVisible;

    /// <summary>
    /// The left Auto-Hide Bar's tabs — the Auto-Hidden Editor Pane, Folder Panel, and Navigation
    /// Panel, in that order; empty while none is Auto-Hidden (INV-062).
    /// </summary>
    public IReadOnlyList<AutoHideTab> LeftAutoHideTabs => PanelChrome.LeftAutoHideTabs(ChromeState);

    /// <summary>
    /// The right Auto-Hide Bar's tabs — the Auto-Hidden Source Panel and Preview Panel, in that
    /// order; empty while none is Auto-Hidden (INV-062).
    /// </summary>
    public IReadOnlyList<AutoHideTab> RightAutoHideTabs => PanelChrome.RightAutoHideTabs(ChromeState);

    /// <summary>Whether the left Auto-Hide Bar has tabs to show (INV-062).</summary>
    public bool HasLeftAutoHideTabs => LeftAutoHideTabs.Count > 0;

    /// <summary>Whether the right Auto-Hide Bar has tabs to show (INV-062).</summary>
    public bool HasRightAutoHideTabs => RightAutoHideTabs.Count > 0;

    /// <summary>Whether a Panel Flyout is open — at most one ever is (INV-062).</summary>
    public bool HasOpenFlyout => _flyoutPanel is not null;

    /// <summary>Whether the Editor Pane's Panel Flyout is open (INV-062).</summary>
    public bool IsEditorPaneFlyoutOpen => _flyoutPanel == DockablePanel.EditorPane;

    /// <summary>Whether the Source Panel's Panel Flyout is open (INV-062).</summary>
    public bool IsSourcePanelFlyoutOpen => _flyoutPanel == DockablePanel.SourcePanel;

    /// <summary>Whether the Preview Panel's Panel Flyout is open (INV-062).</summary>
    public bool IsPreviewPanelFlyoutOpen => _flyoutPanel == DockablePanel.PreviewPanel;

    /// <summary>Whether the Folder Panel's Panel Flyout is open (INV-062).</summary>
    public bool IsFolderPanelFlyoutOpen => _flyoutPanel == DockablePanel.FolderPanel;

    /// <summary>Whether the Navigation Panel's Panel Flyout is open (INV-062).</summary>
    public bool IsNavigationPanelFlyoutOpen => _flyoutPanel == DockablePanel.NavigationPanel;

    /// <summary>
    /// Whether a Side Dock panel's Panel Flyout is open — the dock presents the flyout panel over the
    /// editing area while one is (INV-062).
    /// </summary>
    public bool IsSideDockFlyoutOpen =>
        _flyoutPanel is DockablePanel.FolderPanel or DockablePanel.NavigationPanel;

    /// <summary>
    /// The tab the Side Dock currently presents: the flyout panel's while a Side Dock Panel Flyout is
    /// open, and the dock's own Selected tab otherwise (INV-046, INV-062).
    /// </summary>
    public SideDockTab SideDockDisplayedTab => _flyoutPanel switch
    {
        DockablePanel.FolderPanel => SideDockTab.Folder,
        DockablePanel.NavigationPanel => SideDockTab.Navigation,
        _ => SideDock.SelectedTab,
    };

    /// <summary>
    /// The Dockable Panel the Side Dock currently presents — the panel the dock strip's Pin Toggle
    /// and Close Button act on (INV-062).
    /// </summary>
    public DockablePanel DisplayedSideDockPanel =>
        SideDockDisplayedTab == SideDockTab.Folder ? DockablePanel.FolderPanel : DockablePanel.NavigationPanel;

    /// <summary>Whether the Side Dock's presented panel is pinned — the dock strip's Pin Toggle state (INV-062).</summary>
    public bool IsSideDockPanelPinned =>
        DisplayedSideDockPanel == DockablePanel.FolderPanel ? _isFolderPanelPinned : _isNavigationPanelPinned;

    /// <summary>
    /// The width available to the editing row, fed by the View through the <c>SizeObserver</c> behaviour.
    /// Compact Layout resolves which side panels fit from it, collapsing the Preview Panel, then the
    /// Source Panel, then the Side Dock until the primary Document Pane keeps its minimum width
    /// (INV-059, INV-063).
    /// </summary>
    public double WorkspaceWidth
    {
        get => _workspaceWidth;
        set
        {
            if (Set(ref _workspaceWidth, value))
            {
                RecomputePanels();
            }
        }
    }

    /// <summary>Shows the Source Panel if hidden, or closes it if open — unavailable on the last Docked Document Pane (INV-063).</summary>
    public ICommand ToggleSourcePanelCommand => _toggleSourcePanelCommand;

    /// <summary>Shows the Preview Panel if hidden, or closes it if open.</summary>
    public ICommand TogglePreviewPanelCommand => _togglePreviewPanelCommand;

    /// <summary>Shows the Editor Pane if closed, or closes it if open — unavailable on the last Docked Document Pane (INV-063).</summary>
    public ICommand ToggleEditorPaneCommand => _toggleEditorPaneCommand;

    /// <summary>
    /// Closes a Dockable Panel from its Panel Header's Close Button, wherever it stands — unavailable
    /// on the last Docked Document Pane (INV-062, INV-063). Parameter: the panel.
    /// </summary>
    public ICommand ClosePanelCommand => _closePanelCommand;

    /// <summary>
    /// The Pin Toggle: Unpins a Docked panel to Auto-Hidden, or Pins an Auto-Hidden one back to
    /// Docked — unavailable on the last Docked Document Pane (INV-062, INV-063). Parameter: the panel.
    /// </summary>
    public ICommand TogglePinCommand => _togglePinCommand;

    /// <summary>
    /// Opens an Auto-Hidden panel's Panel Flyout from its Auto-Hide Tab, or dismisses it when it is
    /// already open (INV-062). Parameter: the panel.
    /// </summary>
    public ICommand ToggleFlyoutCommand => _toggleFlyoutCommand;

    /// <summary>Dismisses the open Panel Flyout — Escape, or a click outside it (INV-062).</summary>
    public ICommand DismissFlyoutCommand => _dismissFlyoutCommand;

    /// <summary>The chrome state of every Dockable Panel, snapshotted from its owner (INV-062).</summary>
    private PanelChromeState ChromeState => new(
        EditorPane: new PanelState(_isEditorPaneOpen, _isEditorPanePinned),
        SourcePanel: new PanelState(_isSourcePanelRequested, _isSourcePanelPinned),
        PreviewPanel: new PanelState(_isPreviewPanelRequested, _isPreviewPanelPinned),
        FolderPanel: new PanelState(Folder.IsFolderPanelVisible, _isFolderPanelPinned),
        NavigationPanel: new PanelState(SideDock.IsNavigationPanelOpen, _isNavigationPanelPinned));

    /// <summary>Creates the Panel Chrome commands and subscriptions. Called once from the constructor.</summary>
    private void InitializePanelChrome()
    {
        _toggleSourcePanelCommand = new RelayCommand(
            () => TogglePanel(DockablePanel.SourcePanel),
            () => !_isSourcePanelRequested || PanelChrome.CanClose(ChromeState, DockablePanel.SourcePanel));
        _togglePreviewPanelCommand = new RelayCommand(() => TogglePanel(DockablePanel.PreviewPanel));
        _toggleEditorPaneCommand = new RelayCommand(
            () => TogglePanel(DockablePanel.EditorPane),
            () => !_isEditorPaneOpen || PanelChrome.CanClose(ChromeState, DockablePanel.EditorPane));
        _closePanelCommand = new RelayCommand<DockablePanel>(
            ClosePanel,
            panel => PanelChrome.CanClose(ChromeState, panel));
        _togglePinCommand = new RelayCommand<DockablePanel>(
            TogglePin,
            panel => PanelChrome.CanUnpin(ChromeState, panel) || PanelChrome.CanPin(ChromeState, panel));
        _toggleFlyoutCommand = new RelayCommand<DockablePanel>(ToggleFlyout);
        _dismissFlyoutCommand = new RelayCommand(() => SetFlyout(null), () => _flyoutPanel is not null);

        SideDock.PropertyChanged += OnSideDockPropertyChanged;
        Folder.PropertyChanged += OnFolderPropertyChanged;
        _lastChrome = ChromeState;
        RecomputePanels();

        // Only from here on does a chrome change mean the *user* moved a panel. Persisting during
        // construction would write the default layout over the very state Restore is about to read.
        _isPanelChromeReady = true;
    }

    private void RaisePanelChrome()
    {
        Raise(nameof(IsSourcePanelVisible));
        Raise(nameof(IsPreviewPanelVisible));
        Raise(nameof(IsEditorPaneVisible));
        Raise(nameof(IsEditorPaneOpen));
        Raise(nameof(IsSourcePanelOpen));
        Raise(nameof(IsPreviewPanelOpen));
        Raise(nameof(IsEditorPanePinned));
        Raise(nameof(IsSourcePanelPinned));
        Raise(nameof(IsPreviewPanelPinned));
        Raise(nameof(IsSourcePanelPrimary));
        Raise(nameof(IsSourceSplitterVisible));
        Raise(nameof(LeftAutoHideTabs));
        Raise(nameof(RightAutoHideTabs));
        Raise(nameof(HasLeftAutoHideTabs));
        Raise(nameof(HasRightAutoHideTabs));
        RaiseFlyoutProperties();
        _toggleSourcePanelCommand.RaiseCanExecuteChanged();
        _toggleEditorPaneCommand.RaiseCanExecuteChanged();
        _closePanelCommand.RaiseCanExecuteChanged();
        _togglePinCommand.RaiseCanExecuteChanged();
    }

    private void RaiseFlyoutProperties()
    {
        Raise(nameof(HasOpenFlyout));
        Raise(nameof(IsEditorPaneFlyoutOpen));
        Raise(nameof(IsSourcePanelFlyoutOpen));
        Raise(nameof(IsPreviewPanelFlyoutOpen));
        Raise(nameof(IsFolderPanelFlyoutOpen));
        Raise(nameof(IsNavigationPanelFlyoutOpen));
        Raise(nameof(IsSideDockFlyoutOpen));
        Raise(nameof(SideDockDisplayedTab));
        Raise(nameof(DisplayedSideDockPanel));
        Raise(nameof(IsSideDockPanelPinned));
        _dismissFlyoutCommand.RaiseCanExecuteChanged();
    }
}
