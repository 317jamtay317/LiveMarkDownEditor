namespace Application;

/// <summary>
/// Port for Delete File: removing a File's Markdown Document from disk once the user has confirmed it
/// (INV-081). The Application layer owns this contract; an adapter in the Infrastructure layer
/// implements it against the file system, sending the file to the Recycle Bin rather than erasing it.
/// </summary>
public interface IFileDeleter
{
    /// <summary>
    /// Deletes the file at <paramref name="path"/> by sending it to the Recycle Bin, so a mistaken
    /// delete can be undone from Windows.
    /// </summary>
    /// <param name="path">The absolute path of the file to delete.</param>
    /// <param name="cancellationToken">Cancels the delete before it starts.</param>
    /// <exception cref="FileNotFoundException">Thrown when the file has already gone from disk.</exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the file was not deleted because the user declined, for example when Windows could
    /// not recycle it and asked before erasing it for good.
    /// </exception>
    /// <exception cref="IOException">Thrown when the file exists but could not be deleted.</exception>
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}
