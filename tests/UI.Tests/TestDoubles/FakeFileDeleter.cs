using Application;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Test double for <see cref="IFileDeleter"/>. Records every path it deletes, can run a callback as it
/// deletes (to simulate the disk changing, or to record the order of events), and can be told to fail
/// the way the real adapter does (INV-081).
/// </summary>
public sealed class FakeFileDeleter : IFileDeleter
{
    private readonly List<string> _deleted = [];

    /// <summary>Every path deleted, in the order it was deleted.</summary>
    public IReadOnlyList<string> Deleted => _deleted;

    /// <summary>When set, <see cref="DeleteAsync"/> fails with this exception and deletes nothing.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Run with the path as each delete happens.</summary>
    public Action<string>? OnDelete { get; set; }

    /// <inheritdoc />
    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        _deleted.Add(path);
        OnDelete?.Invoke(path);
        return Task.CompletedTask;
    }
}
