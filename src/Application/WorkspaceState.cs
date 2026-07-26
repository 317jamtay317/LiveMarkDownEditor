namespace Application;

/// <summary>
/// A persisted snapshot of the Workspace across runs: the Watched File paths that were open, in Tab
/// order, which of them are Pinned Tabs, the Recent Files paths (newest first), the open Folder
/// Workspace's root path, and the Panel Layout. Only saved documents are recorded — a Tab with no
/// Watched File, and any unsaved edits, are never persisted (INV-037); the Folder Tree itself is never
/// persisted, only its root, which is re-enumerated on Restore (INV-045).
/// </summary>
/// <param name="OpenDocuments">The open Tabs' Watched File paths, in Tab order — the Pinned Row first (INV-071).</param>
/// <param name="RecentFiles">The recently-used Watched File paths, newest first.</param>
/// <param name="WorkspaceFolder">The open Folder Workspace's root path, or <see langword="null"/> when none is open.</param>
/// <param name="Panels">
/// The Panel Layout — every Dockable Panel's open and pinned state (INV-067) — or
/// <see langword="null"/> when none was persisted, which restores the default layout.
/// </param>
public sealed record WorkspaceState(
    IReadOnlyList<string> OpenDocuments,
    IReadOnlyList<string> RecentFiles,
    string? WorkspaceFolder = null,
    PanelLayout? Panels = null)
{
    /// <summary>
    /// The Watched File paths of the Tabs that were Pinned Tabs, so Restore reopens each of them
    /// pinned (INV-071). A subset of <see cref="OpenDocuments"/>. Empty when nothing was pinned — and
    /// empty, rather than absent, for a state file written before pinning existed, so an older file
    /// still loads.
    /// </summary>
    public IReadOnlyList<string> PinnedDocuments { get; init; } = [];

    /// <summary>The empty Workspace State — nothing open, no recent files, no Folder Workspace, no Panel Layout.</summary>
    public static WorkspaceState Empty { get; } = new([], []);
}
