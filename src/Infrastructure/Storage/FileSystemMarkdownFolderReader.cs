using Application;
using Domain;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IMarkdownFolderReader"/>. Reads the Folder Listing beneath a root
/// folder as root-relative, <c>/</c>-separated paths for <c>FolderWorkspace.From</c>, the way VS Code's
/// Explorer sees a folder: every folder, hidden ones included, and the Markdown Documents in them —
/// but nothing inside an Excluded Folder, which it never descends into (INV-042). It then asks the
/// <see cref="GitIgnoreChecker"/> which of those paths Git ignores (INV-084). Inaccessible locations are
/// skipped, and a symbolic link or junction is not followed, so a link loop cannot trap it.
/// </summary>
/// <param name="gitIgnore">Says which listed paths the Git repository holding the root ignores.</param>
public sealed class FileSystemMarkdownFolderReader(GitIgnoreChecker gitIgnore) : IMarkdownFolderReader
{
    // One level at a time, so an Excluded Folder is never entered at all. Only a link is skipped:
    // hidden and system entries are listed, as VS Code lists them.
    private static readonly EnumerationOptions Options = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.ReparsePoint,
    };

    /// <inheritdoc />
    public async Task<FolderListing> ReadListingAsync(string rootPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var root = Path.GetFullPath(rootPath);
        var listing = await Task.Run(() => Read(root, cancellationToken), cancellationToken).ConfigureAwait(false);
        var ignored = await gitIgnore.IgnoredAsync(root, listing.Paths, cancellationToken).ConfigureAwait(false);
        return listing.WithIgnored(ignored);
    }

    private static FolderListing Read(string root, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            // A root that is not there is raised (not silently empty), so Restore can tell a Folder
            // Workspace that has gone from one that is merely empty (INV-045).
            throw new DirectoryNotFoundException($"Folder not found: {root}");
        }

        var folders = new List<string>();
        var files = new List<string>();
        var pending = new Stack<string>([root]);
        while (pending.TryPop(out var folder))
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var child in Directory.EnumerateDirectories(folder, "*", Options))
            {
                if (!FolderListing.IsExcluded(Path.GetFileName(child)))
                {
                    folders.Add(RelativePath(root, child));
                    pending.Push(child);
                }
            }

            files.AddRange(Directory.EnumerateFiles(folder, "*", Options)
                .Where(MarkdownFile.IsMarkdown)
                .Select(file => RelativePath(root, file)));
        }

        return new FolderListing(folders, files);
    }

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
