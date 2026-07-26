using System.IO;
using Application;

namespace UI.ViewModels;

/// <summary>
/// The Workspace's Panel Layout: every Dockable Panel's open and pinned state persisted as part of
/// the Workspace State and restored at startup, so the editor reopens with the panels the user left
/// it with (INV-067). The mapping between a layout and the Panel Chrome state is the pure
/// <see cref="PanelChrome"/>'s; this side owns only the when — when to save, and when to apply.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>
    /// The Panel Layout to persist, as the panels stand (INV-067). It records their Placement, never
    /// the width-driven Compact Layout resolution — a narrow window collapses panels for the run it is
    /// narrow in, and must not be saved as the user's choice (INV-059).
    /// </summary>
    private PanelLayout PanelLayoutOf() => PanelChrome.ToLayout(ChromeState);

    /// <summary>
    /// Restores a persisted Panel Layout, putting every Dockable Panel back in the Placement the last
    /// run left it in (INV-067). A <see langword="null"/> layout — a first run, or a state file written
    /// before the Panel Layout existed — leaves the default layout untouched.
    /// </summary>
    /// <param name="layout">The persisted Panel Layout, or <see langword="null"/> when none was saved.</param>
    /// <remarks>
    /// Called from <c>RestoreAsync</c> after the Folder Workspace has been restored, because the
    /// Folder Panel comes back only alongside the folder it browses (INV-045): a persisted root that
    /// has gone takes its panel with it. Restoring is not a reopen, so the pin a panel was left with
    /// stands — an Auto-Hidden panel comes back Auto-Hidden rather than Docked (INV-062) — and it is
    /// not itself a change, so it does not re-persist the state it just read.
    /// </remarks>
    private void RestorePanelLayout(PanelLayout? layout)
    {
        if (layout is null)
        {
            return;
        }

        var restored = PanelChrome.FromLayout(layout, Folder.HasFolder);

        _isRestoringPanels = true;
        try
        {
            foreach (var panel in PanelChrome.All)
            {
                var state = restored.Of(panel);
                SetOpen(panel, state.IsOpen);
                SetPinned(panel, state.IsPinned);
            }

            // Take the restored state as the baseline, so no panel reads as newly reopened and has
            // its pin reset out from under the layout just applied.
            _lastChrome = ChromeState;
        }
        finally
        {
            _isRestoringPanels = false;
        }

        RecomputePanels();
    }

    // Persists the Panel Layout alongside the rest of the Workspace State, best-effort: a state file
    // that cannot be written must never take a panel toggle down with it (INV-037).
    private async void PersistPanelLayout()
    {
        try
        {
            await PersistStateAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Serilog.Log.Error(exception, "Failed to persist the Panel Layout");
        }
    }
}
