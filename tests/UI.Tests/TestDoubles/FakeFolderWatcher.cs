using Application;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Test double for <see cref="IFolderWatcher"/>. Records the watched root and lets a test raise a
/// structural change on demand via <see cref="RaiseChanged"/> (INV-044).
/// </summary>
public sealed class FakeFolderWatcher : IFolderWatcher
{
    /// <summary>The root most recently passed to <see cref="Watch"/>, or <see langword="null"/>.</summary>
    public string? WatchedRoot { get; private set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Watch(string rootPath) => WatchedRoot = rootPath;

    /// <inheritdoc />
    public void StopWatching() => WatchedRoot = null;

    /// <summary>Simulates a Markdown Document being added, removed, or renamed under the watched root.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
