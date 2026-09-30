using Application;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IEntryCreator"/>. It makes New Folder's empty folder and New File's
/// empty Markdown Document, creating any folders on the way, and refuses a path that a folder or a file
/// already has rather than quietly reusing or replacing it — Windows would treat creating a folder that
/// exists as success (INV-085, INV-086).
/// </summary>
public sealed class FileSystemEntryCreator : IEntryCreator
{
    /// <inheritdoc />
    public Task CreateFolderAsync(string path, CancellationToken cancellationToken = default) =>
        Create(path, cancellationToken, target => Directory.CreateDirectory(target));

    /// <inheritdoc />
    public Task CreateFileAsync(string path, CancellationToken cancellationToken = default) =>
        Create(path, cancellationToken, target =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            // CreateNew fails rather than truncating, should anything appear there in the meantime.
            using var _ = new FileStream(target, FileMode.CreateNew, FileAccess.Write);
        });

    private static Task Create(string path, CancellationToken cancellationToken, Action<string> create)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var target = Path.GetFullPath(path);
        if (Path.Exists(target))
        {
            return Task.FromException(new IOException("A file or folder with that name already exists."));
        }

        try
        {
            create(target);
            return Task.CompletedTask;
        }
        catch (IOException exception)
        {
            return Task.FromException(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            // A folder the user may not write to refuses the creation the same way a taken name does.
            return Task.FromException(new IOException(exception.Message, exception));
        }
    }
}
