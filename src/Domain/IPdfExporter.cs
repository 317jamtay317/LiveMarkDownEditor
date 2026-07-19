namespace Domain;

/// <summary>
/// Port for exporting a <see cref="MarkdownDocument"/> as a PDF document. The Domain owns this
/// contract; an adapter in the Infrastructure layer implements it.
/// </summary>
/// <remarks>
/// Unlike <see cref="IMarkdownRenderer"/>, which produces the Rendered Output (HTML), a PDF cannot
/// embed the Visual Document, so an implementation re-lays-out the document from its Markdown source.
/// The output is a function of the source text in its content, though the PDF's own bytes need not be
/// identical between runs (a PDF records when it was produced). Exporting is not an edit (INV-033).
/// <para>
/// Exporting is asynchronous because a Mermaid Diagram is rendered to an image before it is placed
/// (INV-050) — the render is an out-of-process operation the exporter awaits.
/// </para>
/// </remarks>
public interface IPdfExporter
{
    /// <summary>Exports the given Markdown Document as the bytes of a PDF file.</summary>
    /// <param name="document">The Markdown Document to export.</param>
    /// <returns>The bytes of the produced PDF document.</returns>
    Task<byte[]> ExportAsync(MarkdownDocument document);
}
