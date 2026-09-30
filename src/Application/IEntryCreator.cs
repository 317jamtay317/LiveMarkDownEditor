namespace Application;

/// <summary>
/// Port for New Folder and New File: making an empty folder, or an empty Markdown Document, on disk once
/// the user has committed its Entry Name (INV-085, INV-086). The Application layer owns this contract; an
/// adapter in the Infrastructure layer implements it against the file system.
/// </summary>
public interface IEntryCreator
{
    /// <summary>
    /// Creates an empty folder at <paramref name="path"/>, with any folders on the way that are not
    /// there yet. It never overwrites: a path that a folder or a file already has is refused.
    /// </summary>
    /// <param name="path">The absolute path the new folder should have.</param>
    /// <param name="cancellationToken">Cancels the creation before it starts.</param>
    /// <exception cref="IOException">
    /// Thrown when the folder could not be created, including when <paramref name="path"/> is already taken.
    /// </exception>
    Task CreateFolderAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an empty file at <paramref name="path"/>, with any folders on the way that are not there
    /// yet. It never overwrites: a path that a file or a folder already has is refused.
    /// </summary>
    /// <param name="path">The absolute path the new file should have.</param>
    /// <param name="cancellationToken">Cancels the creation before it starts.</param>
    /// <exception cref="IOException">
    /// Thrown when the file could not be created, including when <paramref name="path"/> is already taken.
    /// </exception>
    Task CreateFileAsync(string path, CancellationToken cancellationToken = default);
}
