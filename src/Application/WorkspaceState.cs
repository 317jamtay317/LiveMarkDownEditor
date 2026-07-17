namespace Application;

/// <summary>
/// A persisted snapshot of the Workspace across runs: the Watched File paths that were open, in Tab
/// order, and the Recent Files paths (newest first). Only saved documents are recorded — a Tab with
/// no Watched File, and any unsaved edits, are never persisted (INV-037).
/// </summary>
/// <param name="OpenDocuments">The open Tabs' Watched File paths, in Tab order.</param>
/// <param name="RecentFiles">The recently-used Watched File paths, newest first.</param>
public sealed record WorkspaceState(IReadOnlyList<string> OpenDocuments, IReadOnlyList<string> RecentFiles)
{
    /// <summary>The empty Workspace State — nothing open, no recent files.</summary>
    public static WorkspaceState Empty { get; } = new([], []);
}
