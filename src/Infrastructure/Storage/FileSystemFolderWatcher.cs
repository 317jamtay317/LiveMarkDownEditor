using Application;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IFolderWatcher"/>, backed by a recursive
/// <see cref="FileSystemWatcher"/>. Raises <see cref="Changed"/> when a file or directory is created,
/// deleted, or renamed anywhere beneath the watched root, so the Folder Tree can track the disk live
/// (INV-044). As with a save, one change can surface several file-system events, so they are debounced
/// into a single notification — mirroring <see cref="FileSystemDocumentWatcher"/>.
/// </summary>
public sealed class FileSystemFolderWatcher : IFolderWatcher, IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(150);

    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;

    /// <inheritdoc />
    public event EventHandler? Changed;

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
            _watcher = new FileSystemWatcher(fullPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true,
            };
            _watcher.Created += OnFolderChanged;
            _watcher.Deleted += OnFolderChanged;
            _watcher.Renamed += OnFolderChanged;
            _watcher.Error += OnWatcherError;
        }
    }

    /// <inheritdoc />
    public void StopWatching()
    {
        lock (_gate)
        {
            _watcher?.Dispose();
            _watcher = null;
            _debounce?.Dispose();
            _debounce = null;
        }
    }

    /// <summary>Disposes the underlying watcher and debounce timer.</summary>
    public void Dispose() => StopWatching();

    private void OnFolderChanged(object sender, FileSystemEventArgs e) => ScheduleRaise();

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
