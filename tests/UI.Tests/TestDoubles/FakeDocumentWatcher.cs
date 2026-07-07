using Application;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Test double for <see cref="IDocumentWatcher"/>. Records the watched path and lets a test raise an
/// External Change on demand via <see cref="RaiseChanged"/>.
/// </summary>
public sealed class FakeDocumentWatcher : IDocumentWatcher
{
    /// <summary>The path most recently passed to <see cref="Watch"/>, or <see langword="null"/>.</summary>
    public string? WatchedPath { get; private set; }

    /// <inheritdoc />
    public event EventHandler<ExternalChange>? Changed;

    /// <inheritdoc />
    public void Watch(string path) => WatchedPath = path;

    /// <inheritdoc />
    public void StopWatching() => WatchedPath = null;

    /// <summary>Simulates an External Change to the file at <paramref name="path"/>.</summary>
    public void RaiseChanged(string path) => Changed?.Invoke(this, new ExternalChange(path));
}
