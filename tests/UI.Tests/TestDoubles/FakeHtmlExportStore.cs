using Application;

namespace UI.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IHtmlExportStore"/> for tests, backed by a path→HTML dictionary. Counts its
/// writes so a test can assert that an export wrote <em>nothing at all</em> (INV-032).
/// </summary>
public sealed class FakeHtmlExportStore : IHtmlExportStore
{
    private readonly Dictionary<string, string> _files = new();

    /// <summary>How many times an export was written. Zero means nothing was exported.</summary>
    public int WriteCount { get; private set; }

    /// <summary>
    /// The HTML last exported to <paramref name="path"/>. Throws when nothing was exported there:
    /// a test asserting on an export that never happened is asserting on a bug in the test.
    /// </summary>
    public string SavedHtml(string path) =>
        _files.TryGetValue(path, out var html)
            ? html
            : throw new KeyNotFoundException($"No HTML was exported to '{path}'.");

    /// <inheritdoc />
    public Task SaveAsync(string path, string html, CancellationToken cancellationToken = default)
    {
        _files[path] = html;
        WriteCount++;
        return Task.CompletedTask;
    }
}
