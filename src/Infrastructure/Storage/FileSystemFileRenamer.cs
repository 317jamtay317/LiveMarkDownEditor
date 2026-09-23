using Application;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IFileRenamer"/>. It moves the file to its New Name without
/// overwriting, so a New Name that another file or folder already has fails rather than destroying
/// what is there (INV-082). A change of capitals alone is a rename Windows carries out in place.
/// </summary>
public sealed class FileSystemFileRenamer : IFileRenamer
{
    /// <inheritdoc />
    public Task RenameAsync(string path, string newPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);
        cancellationToken.ThrowIfCancellationRequested();

        var source = Path.GetFullPath(path);
        var target = Path.GetFullPath(newPath);
        if (!File.Exists(source))
        {
            return Task.FromException(new FileNotFoundException("The file to rename does not exist.", source));
        }

        try
        {
            File.Move(source, target, overwrite: false);
            return Task.CompletedTask;
        }
        catch (IOException exception)
        {
            return Task.FromException(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            // A folder the user may not write to refuses the rename the same way a taken name does.
            return Task.FromException(new IOException(exception.Message, exception));
        }
    }
}
