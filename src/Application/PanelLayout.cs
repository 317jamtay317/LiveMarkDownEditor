namespace Application;

/// <summary>
/// One Dockable Panel's persisted chrome state: whether it was open, and whether it was pinned.
/// Together the two decide the panel's Panel Placement when the layout is restored — Closed when not
/// open, Docked while pinned, Auto-Hidden otherwise (INV-062, INV-067).
/// </summary>
/// <param name="IsOpen">Whether the panel was open — Docked or Auto-Hidden, as opposed to Closed.</param>
/// <param name="IsPinned">Whether the panel was pinned. Meaningful only while open; a Closed panel is Closed either way.</param>
public readonly record struct PersistedPanelState(bool IsOpen, bool IsPinned);

/// <summary>
/// The Panel Layout: every Dockable Panel's open and pinned state, persisted as part of the
/// <see cref="WorkspaceState"/> so each panel's Panel Placement is restored exactly as the user left
/// it (INV-067). The Application layer owns the persisted shape; the UI maps it to and from its own
/// Panel Chrome state, so no layer below the UI needs to know what a panel looks like.
/// </summary>
/// <param name="EditorPane">The Editor Pane's persisted state.</param>
/// <param name="SourcePanel">The Source Panel's persisted state.</param>
/// <param name="PreviewPanel">The Preview Panel's persisted state.</param>
/// <param name="FolderPanel">The Folder Panel's persisted state.</param>
/// <param name="NavigationPanel">The Navigation Panel's persisted state.</param>
public sealed record PanelLayout(
    PersistedPanelState EditorPane,
    PersistedPanelState SourcePanel,
    PersistedPanelState PreviewPanel,
    PersistedPanelState FolderPanel,
    PersistedPanelState NavigationPanel)
{
    /// <summary>
    /// The layout the Workspace opens with when nothing was persisted: the Editor Pane Docked, every
    /// other panel Closed (INV-062).
    /// </summary>
    public static PanelLayout Default { get; } = new(
        EditorPane: new PersistedPanelState(IsOpen: true, IsPinned: true),
        SourcePanel: new PersistedPanelState(IsOpen: false, IsPinned: true),
        PreviewPanel: new PersistedPanelState(IsOpen: false, IsPinned: true),
        FolderPanel: new PersistedPanelState(IsOpen: false, IsPinned: true),
        NavigationPanel: new PersistedPanelState(IsOpen: false, IsPinned: true));
}
