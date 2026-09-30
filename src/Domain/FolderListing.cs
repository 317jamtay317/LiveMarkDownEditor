namespace Domain;

/// <summary>
/// A Folder Listing: what the disk holds beneath a Folder Workspace's root, as the Folder Tree is built
/// from it — the root-relative, <c>/</c>-separated paths of its folders and of its files, and which of
/// those paths Git ignores (INV-042, INV-084). Reading it from disk is the Infrastructure's job, so
/// <see cref="FolderWorkspace.From(string, FolderListing)"/> stays a pure projection.
/// </summary>
public sealed class FolderListing
{
    // The version-control stores VS Code's files.exclude hides by default.
    private static readonly HashSet<string> ExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".svn",
        ".hg",
        ".jj",
    };

    /// <summary>Creates a Folder Listing.</summary>
    /// <param name="folders">The folders beneath the root, each a root-relative, <c>/</c>-separated path.</param>
    /// <param name="files">The files beneath the root, each a root-relative, <c>/</c>-separated path.</param>
    /// <param name="ignored">
    /// The listed paths Git ignores, or <see langword="null"/> when nothing is; everything beneath an
    /// ignored folder is Ignored too, whether or not it is named here.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="folders"/> or <paramref name="files"/> is null.</exception>
    public FolderListing(IEnumerable<string> folders, IEnumerable<string> files, IEnumerable<string>? ignored = null)
    {
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(files);

        Folders = [.. folders];
        Files = [.. files];
        Ignored = new HashSet<string>(ignored ?? [], StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The folders beneath the root, as root-relative, <c>/</c>-separated paths.</summary>
    public IReadOnlyList<string> Folders { get; }

    /// <summary>The files beneath the root, as root-relative, <c>/</c>-separated paths.</summary>
    public IReadOnlyList<string> Files { get; }

    /// <summary>The listed paths Git ignores, compared without regard to capitals (INV-084).</summary>
    public IReadOnlySet<string> Ignored { get; }

    /// <summary>Every listed path — the folders, then the files — as asked of Git to learn what it ignores.</summary>
    public IEnumerable<string> Paths => Folders.Concat(Files);

    /// <summary>
    /// Whether a folder of this name is an Excluded Folder — <c>.git</c>, <c>.svn</c>, <c>.hg</c>, or
    /// <c>.jj</c>, compared without regard to capitals — which the Folder Tree never shows, together
    /// with everything beneath it (INV-042).
    /// </summary>
    /// <param name="folderName">A folder's own name (one path segment).</param>
    /// <returns><see langword="true"/> when the folder is excluded.</returns>
    public static bool IsExcluded(string folderName) => ExcludedFolders.Contains(folderName);

    /// <summary>The same folders and files, with <paramref name="ignored"/> as what Git ignores (INV-084).</summary>
    /// <param name="ignored">The listed paths Git ignores.</param>
    /// <returns>A new Folder Listing.</returns>
    public FolderListing WithIgnored(IEnumerable<string> ignored) => new(Folders, Files, ignored);
}
