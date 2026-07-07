using System.Text;
using Application;
using Domain;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IDocumentStore"/>. Reads and writes the Watched File as
/// UTF-8 text, treating its contents as the Markdown Document's canonical source text.
/// </summary>
public sealed class FileDocumentStore : IDocumentStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public async Task<MarkdownDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The Watched File does not exist.", path);
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return new MarkdownDocument(text);
    }

    /// <inheritdoc />
    public async Task SaveAsync(string path, MarkdownDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(document);

        await File.WriteAllTextAsync(path, document.Source.Text, Utf8NoBom, cancellationToken).ConfigureAwait(false);
    }
}
