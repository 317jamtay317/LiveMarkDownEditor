namespace Application;

/// <summary>
/// Port for Rename File: giving a File's Markdown Document its New Name on disk, in the folder it
/// already sits in (INV-082). The Application layer owns this contract; an adapter in the
/// Infrastructure layer implements it against the file system.
/// </summary>
public interface IFileRenamer
{
    /// <summary>
    /// Renames the file at <paramref name="path"/> to <paramref name="newPath"/>. It never overwrites:
    /// a <paramref name="newPath"/> that another file or folder already has is refused. Changing only
    /// the capitals of the name is a rename like any other.
    /// </summary>
    /// <param name="path">The absolute path of the file to rename.</param>
    /// <param name="newPath">The absolute path the file should have, in the same folder.</param>
    /// <param name="cancellationToken">Cancels the rename before it starts.</param>
    /// <exception cref="FileNotFoundException">Thrown when the file has already gone from disk.</exception>
    /// <exception cref="IOException">
    /// Thrown when the file exists but could not be renamed, including when <paramref name="newPath"/>
    /// is already taken.
    /// </exception>
    Task RenameAsync(string path, string newPath, CancellationToken cancellationToken = default);
}
