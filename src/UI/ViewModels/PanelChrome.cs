namespace UI.ViewModels;

/// <summary>The Dockable Panels of the Workspace — the panes that carry a Panel Header and stand in a Panel Placement (INV-062).</summary>
public enum DockablePanel
{
    /// <summary>The Editor Pane — the pane hosting the Active Session's Visual Document.</summary>
    EditorPane,

    /// <summary>The Source Panel — the raw, editable Markdown source beside the Visual Document.</summary>
    SourcePanel,

    /// <summary>The Preview Panel — the Diagram Preview pane along the right edge.</summary>
    PreviewPanel,

    /// <summary>The Folder Panel — the Folder Tree tab of the Side Dock.</summary>
    FolderPanel,

    /// <summary>The Navigation Panel — the Outline tab of the Side Dock.</summary>
    NavigationPanel,
}

/// <summary>
/// Where a Dockable Panel currently stands — exactly one of the three Panel Placements (INV-062).
/// </summary>
public enum PanelPlacement
{
    /// <summary>Not shown; reopened Docked from its Command Bar View Menu toggle.</summary>
    Closed,

    /// <summary>Pinned open, taking its Panel Column or Side Dock tab.</summary>
    Docked,

    /// <summary>Unpinned — off the layout, reachable from its Auto-Hide Bar's tab.</summary>
    AutoHidden,
}

/// <summary>One Dockable Panel's chrome state: whether it is open (toggled on) and whether it is pinned.</summary>
/// <param name="IsOpen">Whether the panel is toggled on — Docked or Auto-Hidden, as opposed to Closed.</param>
/// <param name="IsPinned">Whether the panel is pinned. Meaningful only while open; a Closed panel is Closed either way.</param>
public readonly record struct PanelState(bool IsOpen, bool IsPinned);

/// <summary>
/// The chrome state of every Dockable Panel at once — the single input the pure
/// <see cref="PanelChrome"/> rules read (INV-062, INV-063).
/// </summary>
/// <param name="EditorPane">The Editor Pane's state.</param>
/// <param name="SourcePanel">The Source Panel's state.</param>
/// <param name="PreviewPanel">The Preview Panel's state.</param>
/// <param name="FolderPanel">The Folder Panel's state.</param>
/// <param name="NavigationPanel">The Navigation Panel's state.</param>
public readonly record struct PanelChromeState(
    PanelState EditorPane,
    PanelState SourcePanel,
    PanelState PreviewPanel,
    PanelState FolderPanel,
    PanelState NavigationPanel)
{
    /// <summary>The state the Workspace opens with: the Editor Pane Docked, every other panel Closed (INV-062).</summary>
    public static PanelChromeState Default => new(
        EditorPane: new PanelState(IsOpen: true, IsPinned: true),
        SourcePanel: new PanelState(IsOpen: false, IsPinned: true),
        PreviewPanel: new PanelState(IsOpen: false, IsPinned: true),
        FolderPanel: new PanelState(IsOpen: false, IsPinned: true),
        NavigationPanel: new PanelState(IsOpen: false, IsPinned: true));

    /// <summary>Reads one panel's state.</summary>
    /// <param name="panel">The Dockable Panel to read.</param>
    /// <returns>That panel's open and pinned state.</returns>
    public PanelState Of(DockablePanel panel) => panel switch
    {
        DockablePanel.EditorPane => EditorPane,
        DockablePanel.SourcePanel => SourcePanel,
        DockablePanel.PreviewPanel => PreviewPanel,
        DockablePanel.FolderPanel => FolderPanel,
        _ => NavigationPanel,
    };

    /// <summary>Returns a copy of this state with one panel's state replaced.</summary>
    /// <param name="panel">The Dockable Panel to replace.</param>
    /// <param name="state">Its new open and pinned state.</param>
    /// <returns>The new chrome state; this one is unchanged.</returns>
    public PanelChromeState With(DockablePanel panel, PanelState state) => panel switch
    {
        DockablePanel.EditorPane => this with { EditorPane = state },
        DockablePanel.SourcePanel => this with { SourcePanel = state },
        DockablePanel.PreviewPanel => this with { PreviewPanel = state },
        DockablePanel.FolderPanel => this with { FolderPanel = state },
        _ => this with { NavigationPanel = state },
    };
}

/// <summary>One Auto-Hide Bar tab: an Auto-Hidden Dockable Panel together with the title its tab shows.</summary>
/// <param name="Panel">The Auto-Hidden panel the tab names.</param>
/// <param name="Title">The tab's title — the same name the panel's Panel Header shows.</param>
public sealed record AutoHideTab(DockablePanel Panel, string Title)
{
    /// <summary>Mints the Auto-Hide Tab for a panel, titled as its Panel Header titles it.</summary>
    /// <param name="panel">The panel the tab names.</param>
    /// <returns>The tab.</returns>
    public static AutoHideTab For(DockablePanel panel) => new(panel, panel switch
    {
        DockablePanel.EditorPane => "Editor",
        DockablePanel.SourcePanel => "Source",
        DockablePanel.PreviewPanel => "Diagram Preview",
        DockablePanel.FolderPanel => "Folder",
        _ => "Outline",
    });
}

/// <summary>
/// The pure rules of the Panel Chrome (INV-062, INV-063): how a Dockable Panel's Placement derives
/// from its open and pinned state, the guards that keep at least one Document Pane — the Editor
/// Pane or the Source Panel — Docked at every moment, and the Auto-Hide Bar projections that list
/// exactly the Auto-Hidden panels. It has no state and does no I/O, so the same chrome state always
/// yields the same placements — the <see cref="CompactLayout"/> discipline, applied to placement.
/// </summary>
public static class PanelChrome
{
    private static readonly DockablePanel[] LeftBarOrder =
        [DockablePanel.EditorPane, DockablePanel.FolderPanel, DockablePanel.NavigationPanel];

    private static readonly DockablePanel[] RightBarOrder =
        [DockablePanel.SourcePanel, DockablePanel.PreviewPanel];

    /// <summary>Derives a panel's Panel Placement: Closed when not open, Docked while pinned, Auto-Hidden otherwise.</summary>
    /// <param name="state">The chrome state of every panel.</param>
    /// <param name="panel">The panel to place.</param>
    /// <returns>The panel's Placement.</returns>
    public static PanelPlacement PlacementOf(PanelChromeState state, DockablePanel panel)
    {
        var (isOpen, isPinned) = state.Of(panel);
        return !isOpen ? PanelPlacement.Closed
            : isPinned ? PanelPlacement.Docked
            : PanelPlacement.AutoHidden;
    }

    /// <summary>Whether a panel is Docked — the only Placement that takes layout width (INV-056, INV-059).</summary>
    /// <param name="state">The chrome state of every panel.</param>
    /// <param name="panel">The panel to test.</param>
    /// <returns><see langword="true"/> while the panel is Docked.</returns>
    public static bool IsDocked(PanelChromeState state, DockablePanel panel) =>
        PlacementOf(state, panel) == PanelPlacement.Docked;

    /// <summary>
    /// Whether a panel may be Closed: it must be open, and closing it must leave a Document Pane
    /// Docked (INV-063) — closing the last Docked Document Pane is unavailable, not refused after
    /// the fact.
    /// </summary>
    /// <param name="state">The chrome state of every panel.</param>
    /// <param name="panel">The panel to close.</param>
    /// <returns><see langword="true"/> when closing the panel is available.</returns>
    public static bool CanClose(PanelChromeState state, DockablePanel panel) =>
        state.Of(panel).IsOpen &&
        KeepsADocumentPaneDocked(state.With(panel, state.Of(panel) with { IsOpen = false }));

    /// <summary>
    /// Whether a panel may be Unpinned to Auto-Hidden: it must be Docked, and unpinning it must
    /// leave a Document Pane Docked (INV-063).
    /// </summary>
    /// <param name="state">The chrome state of every panel.</param>
    /// <param name="panel">The panel to unpin.</param>
    /// <returns><see langword="true"/> when unpinning the panel is available.</returns>
    public static bool CanUnpin(PanelChromeState state, DockablePanel panel) =>
        PlacementOf(state, panel) == PanelPlacement.Docked &&
        KeepsADocumentPaneDocked(state.With(panel, state.Of(panel) with { IsPinned = false }));

    /// <summary>Whether a panel may be Pinned back to Docked: exactly while it is Auto-Hidden. Pinning can never endanger INV-063.</summary>
    /// <param name="state">The chrome state of every panel.</param>
    /// <param name="panel">The panel to pin.</param>
    /// <returns><see langword="true"/> when pinning the panel is available.</returns>
    public static bool CanPin(PanelChromeState state, DockablePanel panel) =>
        PlacementOf(state, panel) == PanelPlacement.AutoHidden;

    /// <summary>The left Auto-Hide Bar: the Auto-Hidden panels of the Workspace's left edge — Editor Pane, Folder Panel, Navigation Panel — in that order (INV-062).</summary>
    /// <param name="state">The chrome state of every panel.</param>
    /// <returns>One tab per Auto-Hidden left panel; empty when there are none.</returns>
    public static IReadOnlyList<AutoHideTab> LeftAutoHideTabs(PanelChromeState state) =>
        TabsOf(state, LeftBarOrder);

    /// <summary>The right Auto-Hide Bar: the Auto-Hidden panels of the Workspace's right edge — Source Panel, Preview Panel — in that order (INV-062).</summary>
    /// <param name="state">The chrome state of every panel.</param>
    /// <returns>One tab per Auto-Hidden right panel; empty when there are none.</returns>
    public static IReadOnlyList<AutoHideTab> RightAutoHideTabs(PanelChromeState state) =>
        TabsOf(state, RightBarOrder);

    private static IReadOnlyList<AutoHideTab> TabsOf(PanelChromeState state, DockablePanel[] order) =>
        [.. order
            .Where(panel => PlacementOf(state, panel) == PanelPlacement.AutoHidden)
            .Select(AutoHideTab.For)];

    // The Document Pane rule (INV-063): whatever the operation, at least one of the Editor Pane and
    // the Source Panel stays Docked.
    private static bool KeepsADocumentPaneDocked(PanelChromeState state) =>
        IsDocked(state, DockablePanel.EditorPane) || IsDocked(state, DockablePanel.SourcePanel);
}
