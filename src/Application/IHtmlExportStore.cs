namespace Application;

/// <summary>
/// Port for writing an Export as HTML to the file the user chose. The Application layer owns this
/// contract; an adapter in the Infrastructure layer implements it against the file system.
/// </summary>
/// <remarks>
/// This is deliberately separate from <see cref="IDocumentStore"/>. An export writes a file that is
/// not the Watched File, and the Watched File is the only file an Editor Session ever writes to
/// (INV-032) — giving an export its own port means it has no route to the Watched File at all.
/// </remarks>
public interface IHtmlExportStore
{
    /// <summary>Writes the composed export HTML to the chosen path, replacing any existing file.</summary>
    /// <param name="path">The absolute path the user chose to export to.</param>
    /// <param name="html">The composed HTML, in the Export Shape the user chose.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    Task SaveAsync(string path, string html, CancellationToken cancellationToken = default);
}
