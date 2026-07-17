using Application;

namespace Infrastructure.Storage;

/// <summary>
/// File-system adapter for <see cref="IPdfExportStore"/>. Writes an Export as PDF as raw bytes,
/// replacing any existing file.
/// </summary>
public sealed class FilePdfExportStore : IPdfExportStore
{
    /// <inheritdoc />
    public async Task SaveAsync(string path, byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(content);

        await File.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);
    }
}
