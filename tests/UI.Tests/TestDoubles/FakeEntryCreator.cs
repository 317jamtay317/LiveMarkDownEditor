using Application;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Test double for <see cref="IEntryCreator"/>. Records every folder and file created, can run a callback
/// as it creates one (to simulate the disk changing), and can be told to fail the way the real adapter
/// does (INV-085, INV-086).
/// </summary>
public sealed class FakeEntryCreator : IEntryCreator
{
    private readonly List<string> _created = [];
    private readonly List<string> _createdFiles = [];

    /// <summary>Every folder created, in order, by its absolute path.</summary>
    public IReadOnlyList<string> Created => _created;

    /// <summary>Every file created, in order, by its absolute path.</summary>
    public IReadOnlyList<string> CreatedFiles => _createdFiles;

    /// <summary>When set, creating fails with this exception and creates nothing.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Run with the new entry's path as it is created.</summary>
    public Action<string>? OnCreate { get; set; }

    /// <inheritdoc />
    public Task CreateFolderAsync(string path, CancellationToken cancellationToken = default) => Create(_created, path);

    /// <inheritdoc />
    public Task CreateFileAsync(string path, CancellationToken cancellationToken = default) => Create(_createdFiles, path);

    private Task Create(List<string> record, string path)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        record.Add(path);
        OnCreate?.Invoke(path);
        return Task.CompletedTask;
    }
}
