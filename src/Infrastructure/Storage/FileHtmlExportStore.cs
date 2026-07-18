using System.Text;
using Application;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IHtmlExportStore"/>. Writes an Export as HTML as UTF-8 text,
/// matching the <c>&lt;meta charset="utf-8"&gt;</c> a Standalone Page declares.
/// </summary>
public sealed class FileHtmlExportStore : IHtmlExportStore
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <inheritdoc />
    public async Task SaveAsync(string path, string html, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(html);

        await File.WriteAllTextAsync(path, html, Utf8NoBom, cancellationToken).ConfigureAwait(false);
    }
}
