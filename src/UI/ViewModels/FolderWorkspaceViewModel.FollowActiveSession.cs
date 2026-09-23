namespace UI.ViewModels;

/// <summary>
/// Follow the Active Session in the Folder Panel (INV-083): the File holding the Active Session's
/// Watched File becomes the Selected Folder Entry, so the Folder Tree shows where the document on
/// screen lives. A Watched File the Folder Tree does not hold — and an unsaved Tab, which has none —
/// leaves nothing selected rather than a stale highlight. Following is browsing: it highlights a row
/// and opens nothing (INV-043).
/// </summary>
public sealed partial class FolderWorkspaceViewModel
{
    // The Active Session's Watched File, as the Workspace last reported it. Remembered so that a root
    // opened or Restored afterwards can follow the same document into its new Folder Tree.
    private string? _activeFilePath;

    /// <summary>
    /// Follows the Active Session: highlights the File this Folder Tree holds for
    /// <paramref name="watchedFilePath"/>, or highlights nothing when it holds none (INV-083). The path
    /// is remembered, so opening or Restoring a folder later follows the same document.
    /// </summary>
    /// <param name="watchedFilePath">
    /// The Active Session's Watched File path, or <see langword="null"/> when the Active Session has no
    /// Watched File (an unsaved Tab) or the Workspace is empty.
    /// </param>
    public void FollowActiveSession(string? watchedFilePath)
    {
        _activeFilePath = watchedFilePath;
        FollowActiveFile();
    }

    // Re-applies the remembered Watched File to the current Folder Tree — the one step both a change of
    // Active Session and a newly opened root take.
    private void FollowActiveFile() => SelectedEntry = Folder?.FileFor(_activeFilePath);
}
