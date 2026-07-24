using System.Windows.Documents;

namespace UI.Core;

/// <summary>
/// Port for the Print Preview: the window that shows a document laid out into the very pages Print
/// would produce, truly paginated under the one editor-wide Page Setup, with a Print action beside it
/// (INV-061). Keeping the window behind a port makes Print Preview testable headlessly against a
/// fake, exactly as <see cref="IDocumentPrinter"/> keeps the print dialog out of the editor (INV-034).
/// </summary>
public interface IPrintPreview
{
    /// <summary>
    /// Shows the Print Preview for the given document. The document handed over is self-contained —
    /// freshly projected from the Markdown source, never the live editing surface — so previewing it
    /// neither disturbs what the user is editing nor changes any document (INV-034, INV-061).
    /// </summary>
    /// <param name="document">The self-contained document to preview.</param>
    /// <param name="pageSetup">The Page Setup the preview paginates under — the one the printout obeys.</param>
    /// <param name="description">The print-queue job description, should the user print from the preview.</param>
    void Show(FlowDocument document, PageSetup pageSetup, string description);
}
