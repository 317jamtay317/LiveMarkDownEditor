using Application;
using Domain;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IFolderWatcher"/>, backed by a recursive
/// <see cref="FileSystemWatcher"/>. Raises <see cref="Changed"/> when a file or directory is created,
/// deleted, or renamed anywhere beneath the watched root, or a <c>.gitignore</c> or
/// <c>.git/info/exclude</c> is changed, so the Folder Tree can track the disk live (INV-044, INV-084).
/// A root nested inside a Git repository has ignore rules above it too — every <c>.gitignore</c> on the
/// way up to the repository, and the repository's <c>.git/info/exclude</c> — so those files are watched
/// as well (see <see cref="IgnoreSourcesAbove"/>).
/// Git's own bookkeeping inside an Excluded Folder is not a change to the tree (see
/// <see cref="IsTreeChange"/>). As with a save, one change can surface several file-system events, so
/// they are debounced into a single notification — mirroring <see cref="FileSystemDocumentWatcher"/>.
/// </summary>
public sealed class FileSystemFolderWatcher : IFolderWatcher, IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(150);

    private static readonly char[] Separators = ['/', '\\'];

    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private readonly List<FileSystemWatcher> _ignoreWatchers = [];
    private Timer? _debounce;
    private string _root = string.Empty;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>
    /// Whether a file-system event rebuilds the Folder Tree (INV-044). An entry created, deleted, or
    /// renamed does; so does a <c>.gitignore</c> changed in place, which changes what is Ignored
    /// (INV-084), but a document saved in place does not. Anything inside an Excluded Folder is Git's
    /// (or another version-control tool's) own bookkeeping and rebuilds nothing — except
    /// <c>.git/info/exclude</c>, which is ignore rules too.
    /// </summary>
    /// <param name="change">What happened to the path.</param>
    /// <param name="relativePath">The path, relative to the watched root, with either separator.</param>
    /// <returns><see langword="true"/> when the Folder Tree should be rebuilt.</returns>
    public static bool IsTreeChange(WatcherChangeTypes change, string relativePath)
    {
        var segments = relativePath.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(FolderListing.IsExcluded))
        {
            return IsGitExclude(segments);
        }

        return change != WatcherChangeTypes.Changed
               || (segments.Length > 0 && string.Equals(segments[^1], ".gitignore", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The ignore rules above a root nested inside a Git repository, which the root's own recursive
    /// watcher cannot see (INV-084): every <c>.gitignore</c> in the folders from the root's parent up to
    /// the repository's root, nearest first, and the repository's <c>.git/info/exclude</c>. A root that
    /// is itself a repository's root, and one inside no repository, have none. The repository is the
    /// nearest folder above the root holding a <c>.git</c>; <c>.git/info/exclude</c> is named only when
    /// that <c>.git</c> is a folder.
    /// </summary>
    /// <param name="rootPath">The watched root.</param>
    /// <returns>Each ignore file as its folder and its file name, nearest first.</returns>
    public static IReadOnlyList<(string Folder, string FileName)> IgnoreSourcesAbove(string rootPath)
    {
        var root = new DirectoryInfo(Path.GetFullPath(rootPath));
        if (HasGit(root.FullName))
        {
            return [];
        }

        var sources = new List<(string Folder, string FileName)>();
        for (var folder = root.Parent; folder is not null; folder = folder.Parent)
        {
            sources.Add((folder.FullName, ".gitignore"));
            if (!HasGit(folder.FullName))
            {
                continue;
            }

            var gitFolder = Path.Combine(folder.FullName, ".git");
            if (Directory.Exists(gitFolder))
            {
                sources.Add((Path.Combine(gitFolder, "info"), "exclude"));
            }

            return sources;
        }

        // No repository above the root: nothing Git reads lies outside it.
        return [];
    }

    /// <inheritdoc />
    public void Watch(string rootPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootPath);

        var fullPath = Path.GetFullPath(rootPath);
        StopWatching();

        if (!Directory.Exists(fullPath))
        {
            return;
        }

        lock (_gate)
        {
            _root = fullPath;

            // LastWrite is what reports a .gitignore edited in place; IsTreeChange drops every other
            // file saved in place.
            _watcher = new FileSystemWatcher(fullPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };
            _watcher.Created += OnFolderChanged;
            _watcher.Deleted += OnFolderChanged;
            _watcher.Changed += OnFolderChanged;
            _watcher.Renamed += OnFolderChanged;
            _watcher.Error += OnWatcherError;

            foreach (var (folder, fileName) in IgnoreSourcesAbove(fullPath))
            {
                WatchIgnoreFile(folder, fileName);
            }
        }
    }

    /// <inheritdoc />
    public void StopWatching()
    {
        lock (_gate)
        {
            _watcher?.Dispose();
            _watcher = null;
            foreach (var watcher in _ignoreWatchers)
            {
                watcher.Dispose();
            }

            _ignoreWatchers.Clear();
            _debounce?.Dispose();
            _debounce = null;
        }
    }

    /// <summary>Disposes the underlying watcher and debounce timer.</summary>
    public void Dispose() => StopWatching();

    private void OnFolderChanged(object sender, FileSystemEventArgs e)
    {
        // A rename counts when either end of it is part of the tree: a folder renamed into .git leaves it.
        var changed = IsTreeChange(e.ChangeType, Path.GetRelativePath(_root, e.FullPath))
                      || (e is RenamedEventArgs renamed
                          && IsTreeChange(e.ChangeType, Path.GetRelativePath(_root, renamed.OldFullPath)));
        if (changed)
        {
            ScheduleRaise();
        }
    }

    // Watches one ignore file above the root: any change to it, its creation, or its removal changes
    // what is Ignored (INV-084). A folder that is not there cannot be watched, and holds no rules.
    private void WatchIgnoreFile(string folder, string fileName)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        var watcher = new FileSystemWatcher(folder, fileName)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        watcher.Created += OnIgnoreFileChanged;
        watcher.Deleted += OnIgnoreFileChanged;
        watcher.Changed += OnIgnoreFileChanged;
        watcher.Renamed += OnIgnoreFileChanged;
        _ignoreWatchers.Add(watcher);
    }

    private void OnIgnoreFileChanged(object sender, FileSystemEventArgs e) => ScheduleRaise();

    // A folder is a repository's root when it holds a .git — a folder, or the file a worktree or
    // submodule keeps in its place.
    private static bool HasGit(string folder) => Path.Exists(Path.Combine(folder, ".git"));

    // .git/info/exclude holds ignore rules, so a change to it changes what is Ignored (INV-084).
    private static bool IsGitExclude(string[] segments) =>
        segments.Length == 3
        && string.Equals(segments[0], ".git", StringComparison.OrdinalIgnoreCase)
        && string.Equals(segments[1], "info", StringComparison.OrdinalIgnoreCase)
        && string.Equals(segments[2], "exclude", StringComparison.OrdinalIgnoreCase);

    // A buffer overflow means events were missed; a full re-enumeration recovers the true tree.
    private void OnWatcherError(object sender, ErrorEventArgs e) => ScheduleRaise();

    private void ScheduleRaise()
    {
        lock (_gate)
        {
            _debounce?.Dispose();
            _debounce = new Timer(_ => Changed?.Invoke(this, EventArgs.Empty), null, DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }
}
