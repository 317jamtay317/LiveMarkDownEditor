using System.IO;

namespace UI.ViewModels;

/// <summary>
/// The Workspace's Recent Files: the recently opened or saved Watched File paths, newest first, shown
/// in the Open Recent menu and mirrored to the Windows Jump List (INV-036, INV-037). A path joins the
/// list when a document is opened or saved, and leaves it when it is known to be gone: it failed to
/// open, or the user deleted it with Delete File (INV-081). A path Rename File moves keeps its place
/// under the new path (INV-082).
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private Domain.RecentFiles _recent = Domain.RecentFiles.Empty;

    /// <summary>
    /// The Recent Files — recently opened or saved Watched File paths, newest first — shown in the
    /// Open Recent menu and mirrored to the Windows Jump List. Persisted across runs (INV-037).
    /// </summary>
    public IReadOnlyList<string> RecentFiles => _recent.Paths;

    /// <summary>
    /// Opens a Recent File. A path that no longer exists is dropped from the Recent Files rather than
    /// opened, so the list keeps only files that are still there.
    /// </summary>
    /// <param name="path">The Recent File's path.</param>
    public async Task OpenRecentAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            await OpenPathAsync(path).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // A Recent File that has gone drops off the list rather than opening.
            await ForgetRecentAsync(path).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Drops <paramref name="path"/> from the Recent Files and persists the change. Used for a Recent
    /// File that has gone: one that failed to open, or one the user deleted with Delete File (INV-081).
    /// </summary>
    /// <param name="path">The Watched File path that is gone.</param>
    public async Task ForgetRecentAsync(string path)
    {
        _recent = _recent.Remove(path);
        Raise(nameof(RecentFiles));
        await PersistStateAsync().ConfigureAwait(true);
    }

    private void RememberRecent(string path)
    {
        _recent = _recent.Add(path);
        Raise(nameof(RecentFiles));
    }

    // Rename File moved a Recent File: it keeps its place in the list under its new path (INV-082).
    private void RenameRecent(string path, string newPath)
    {
        _recent = _recent.Rename(path, newPath);
        Raise(nameof(RecentFiles));
    }
}
