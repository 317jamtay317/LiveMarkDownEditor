using Application;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IDocumentWatcher"/>, backed by a <see cref="FileSystemWatcher"/>.
/// Raises <see cref="Changed"/> when the Watched File is modified on disk. Editors and tools often
/// emit several file-system events for one save, so events are debounced into a single notification.
/// </summary>
public sealed class FileSystemDocumentWatcher : IDocumentWatcher, IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(150);

    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private Timer? _debounce;
    private string? _path;

    /// <inheritdoc />
    public event EventHandler<ExternalChange>? Changed;

    /// <inheritdoc />
    public void Watch(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        var fileName = Path.GetFileName(fullPath);
        if (directory is null)
        {
            return;
        }

        StopWatching();

        lock (_gate)
        {
            _path = path;
            _watcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnFileSystemEvent;
            _watcher.Created += OnFileSystemEvent;
            _watcher.Renamed += OnFileSystemEvent;
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
            _path = null;
        }
    }

    /// <summary>Disposes the underlying watcher and debounce timer.</summary>
    public void Dispose() => StopWatching();

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        lock (_gate)
        {
            _debounce?.Dispose();
            _debounce = new Timer(_ => RaiseChanged(), null, DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void RaiseChanged()
    {
        string? path;
        lock (_gate)
        {
            path = _path;
        }

        if (path is not null)
        {
            Changed?.Invoke(this, new ExternalChange(path));
        }
    }
}
