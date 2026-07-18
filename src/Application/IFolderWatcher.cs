namespace Application;

/// <summary>
/// Port for monitoring a Folder Workspace's root for structural change — a Markdown Document added,
/// removed, or renamed anywhere beneath it — so the Folder Tree can track the disk live (INV-044). The
/// Application layer owns this contract; an adapter in the Infrastructure layer implements it against
/// the file system, mirroring <see cref="IDocumentWatcher"/>.
/// </summary>
public interface IFolderWatcher
{
    /// <summary>
    /// Raised when the watched folder's contents change on disk. Carries no payload: the listener
    /// re-enumerates the root to rebuild the Folder Tree (INV-042). Bursts are debounced into one raise.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>Begins watching the folder at <paramref name="rootPath"/> (and its subfolders), replacing any prior watch.</summary>
    /// <param name="rootPath">The absolute path of the root folder to watch.</param>
    void Watch(string rootPath);

    /// <summary>Stops watching the current folder, if any.</summary>
    void StopWatching();
}
