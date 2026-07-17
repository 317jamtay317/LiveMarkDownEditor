namespace Application;

/// <summary>
/// Port for writing an Export as PDF to the file the user chose. The Application layer owns this
/// contract; an adapter in the Infrastructure layer implements it against the file system.
/// </summary>
/// <remarks>
/// Like <see cref="IHtmlExportStore"/>, this is deliberately separate from <see cref="IDocumentStore"/>.
/// An export writes a file that is not the Watched File, and the Watched File is the only file an
/// Editor Session ever writes to (INV-006) — giving an export its own port means it has no route to
/// the Watched File at all (INV-033).
/// </remarks>
public interface IPdfExportStore
{
    /// <summary>Writes the exported PDF bytes to the chosen path, replacing any existing file.</summary>
    /// <param name="path">The absolute path the user chose to export to.</param>
    /// <param name="content">The bytes of the PDF document.</param>
    /// <param name="cancellationToken">Token to cancel the write.</param>
    Task SaveAsync(string path, byte[] content, CancellationToken cancellationToken = default);
}
