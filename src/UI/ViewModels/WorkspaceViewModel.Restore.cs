using System.IO;
using Application;

namespace UI.ViewModels;

/// <summary>
/// Restore: the Workspace reopens the previous run's Workspace State at startup (INV-037, INV-045,
/// INV-067) — in full, or, for a launch with a Startup Document, everything but the Tabs, as VS Code
/// does when it is handed a file (INV-090).
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>
    /// Restores the Workspace from the last run: reopens the Watched Files that were open — skipping
    /// any that have since gone — loads the Recent Files, and puts every Dockable Panel back in the
    /// Placement it was left in (INV-067). Only saved documents are restored; an unsaved Tab was never
    /// persisted (INV-037). Call once at startup, when the editor was launched without a Startup
    /// Document.
    /// </summary>
    public async Task RestoreAsync()
    {
        var state = await RestoreAllButTabsAsync().ConfigureAwait(true);
        await ReopenTabsAsync(state).ConfigureAwait(true);
    }

    /// <summary>
    /// Restores the Workspace for a launch with the Startup Document at <paramref name="path"/>
    /// (INV-090): the Recent Files, the Folder Workspace, and the Panel Layout come back, but the
    /// previous run's Tabs do not — the Startup Document opens on its own, in place of the placeholder
    /// Tab, with its folder in the Folder Panel (INV-089). A Startup Document that fails to open
    /// reopens the previous run's Tabs instead, then rethrows. Call once at startup, instead of
    /// <see cref="RestoreAsync"/>.
    /// </summary>
    /// <param name="path">The absolute path of the Startup Document the editor was launched with.</param>
    /// <exception cref="IOException">The Startup Document could not be opened.</exception>
    public async Task RestoreForStartupDocumentAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var state = await RestoreAllButTabsAsync().ConfigureAwait(true);
        var placeholder = PlaceholderTab();

        try
        {
            await OpenStartupDocumentAsync(path).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // A bad path never costs the user their Tabs: restore as a launch without one would.
            await ReopenTabsAsync(state).ConfigureAwait(true);
            throw;
        }

        if (placeholder is not null)
        {
            RemoveSession(placeholder);
        }
    }

    // Restores the Recent Files, the Folder Workspace, and the Panel Layout, returning the loaded state
    // so its Tabs can be reopened — or not (INV-090).
    private async Task<WorkspaceState> RestoreAllButTabsAsync()
    {
        var state = _stateStore.Load();
        _recent = Domain.RecentFiles.From(state.RecentFiles);
        Raise(nameof(RecentFiles));

        // Reopen the Folder Workspace that was open last run, skipping a root that has gone (INV-045).
        await Folder.RestoreAsync(state.WorkspaceFolder).ConfigureAwait(true);

        // Put every Dockable Panel back where the last run left it. After the folder, because opening
        // one shows its Folder Panel — the persisted layout has the last word on that (INV-067).
        RestorePanelLayout(state.Panels);

        return state;
    }

    // Reopens the persisted Tabs by path, skipping any that have gone, and puts the Pinned Row back.
    private async Task ReopenTabsAsync(WorkspaceState state)
    {
        // The empty Tab the constructor seeds is a placeholder; replace it if we restore real Tabs.
        var placeholder = PlaceholderTab();

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

        if (placeholder is not null && Sessions.Count > 1)
        {
            RemoveSession(placeholder);
        }

        // Put the Pinned Row back as the last run left it, after every Tab is open (INV-071).
        RestorePinnedTabs(state.PinnedDocuments);
    }

    // The lone empty, untouched Tab the constructor seeds (INV-008), or null once the user has used it.
    private EditorSessionViewModel? PlaceholderTab()
    {
        var seeded = Sessions;
        return seeded.Count == 1 && seeded[0].FilePath is null && !seeded[0].HasUnsavedEdits
            ? seeded[0]
            : null;
    }
}
