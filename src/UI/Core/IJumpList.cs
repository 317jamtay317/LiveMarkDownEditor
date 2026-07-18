namespace UI.Core;

/// <summary>
/// Abstraction over the Windows taskbar Jump List, so the Workspace can offer Recent Files there
/// without depending on the WPF shell types directly (keeping it testable).
/// </summary>
public interface IJumpList
{
    /// <summary>
    /// Rebuilds the Jump List's Recent Files from the given Watched File paths, newest first. Each
    /// becomes an entry that reopens that file in the editor.
    /// </summary>
    /// <param name="paths">The recent Watched File paths, newest first.</param>
    void ShowRecentFiles(IReadOnlyList<string> paths);
}
