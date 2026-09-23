using Application;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Test double for <see cref="IFileRenamer"/>. Records every rename, can run a callback as it renames
/// (to simulate the disk changing, or to record the order of events), and can be told to fail the way
/// the real adapter does (INV-082).
/// </summary>
public sealed class FakeFileRenamer : IFileRenamer
{
    private readonly List<(string Path, string NewPath)> _renamed = [];

    /// <summary>Every rename carried out, in order, as the path it had and the path it was given.</summary>
    public IReadOnlyList<(string Path, string NewPath)> Renamed => _renamed;

    /// <summary>When set, <see cref="RenameAsync"/> fails with this exception and renames nothing.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Run with the old and new paths as each rename happens.</summary>
    public Action<string, string>? OnRename { get; set; }

    /// <inheritdoc />
    public Task RenameAsync(string path, string newPath, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        _renamed.Add((path, newPath));
        OnRename?.Invoke(path, newPath);
        return Task.CompletedTask;
    }
}
