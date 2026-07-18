using Application;

namespace UI.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IPdfExportStore"/> for tests, backed by a path→bytes dictionary. Counts its
/// writes so a test can assert that an export wrote <em>nothing at all</em> (INV-033).
/// </summary>
public sealed class FakePdfExportStore : IPdfExportStore
{
    private readonly Dictionary<string, byte[]> _files = new();

    /// <summary>How many times an export was written. Zero means nothing was exported.</summary>
    public int WriteCount { get; private set; }

    /// <summary>
    /// The bytes last exported to <paramref name="path"/>. Throws when nothing was exported there:
    /// a test asserting on an export that never happened is asserting on a bug in the test.
    /// </summary>
    public byte[] SavedBytes(string path) =>
        _files.TryGetValue(path, out var bytes)
            ? bytes
            : throw new KeyNotFoundException($"No PDF was exported to '{path}'.");

    /// <inheritdoc />
    public Task SaveAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        _files[path] = content;
        WriteCount++;
        return Task.CompletedTask;
    }
}
