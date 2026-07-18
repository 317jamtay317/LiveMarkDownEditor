namespace Application;

/// <summary>
/// A persisted snapshot of the Workspace across runs: the Watched File paths that were open, in Tab
/// order, the Recent Files paths (newest first), and the open Folder Workspace's root path. Only saved
/// documents are recorded — a Tab with no Watched File, and any unsaved edits, are never persisted
/// (INV-037); the Folder Tree itself is never persisted, only its root, which is re-enumerated on
/// Restore (INV-045).
/// </summary>
/// <param name="OpenDocuments">The open Tabs' Watched File paths, in Tab order.</param>
/// <param name="RecentFiles">The recently-used Watched File paths, newest first.</param>
/// <param name="WorkspaceFolder">The open Folder Workspace's root path, or <see langword="null"/> when none is open.</param>
public sealed record WorkspaceState(
    IReadOnlyList<string> OpenDocuments,
    IReadOnlyList<string> RecentFiles,
    string? WorkspaceFolder = null)
{
    /// <summary>The empty Workspace State — nothing open, no recent files, no Folder Workspace.</summary>
    public static WorkspaceState Empty { get; } = new([], []);
}
