using System.IO;
using Application;
using Domain;

namespace UI.Tests.TestDoubles;

/// <summary>In-memory <see cref="IDocumentStore"/> for tests, backed by a path→text dictionary.</summary>
public sealed class FakeDocumentStore : IDocumentStore
{
    private readonly Dictionary<string, string> _files = new();

    /// <summary>Seeds the store with a file at <paramref name="path"/> containing <paramref name="text"/>.</summary>
    public void Seed(string path, string text) => _files[path] = text;

    /// <summary>The text last saved to <paramref name="path"/>, or <see langword="null"/> if never saved.</summary>
    public string? SavedText(string path) => _files.TryGetValue(path, out var text) ? text : null;

    /// <inheritdoc />
    public Task<MarkdownDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        _files.TryGetValue(path, out var text)
            ? Task.FromResult(new MarkdownDocument(text))
            : throw new FileNotFoundException("No such file in fake store.", path);

    /// <inheritdoc />
    public Task SaveAsync(string path, MarkdownDocument document, CancellationToken cancellationToken = default)
    {
        _files[path] = document.Source.Text;
        return Task.CompletedTask;
    }
}
