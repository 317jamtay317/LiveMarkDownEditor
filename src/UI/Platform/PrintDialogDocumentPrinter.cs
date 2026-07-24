using System.Printing;
using System.Windows.Controls;
using System.Windows.Documents;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// <see cref="IDocumentPrinter"/> implementation backed by the WPF <see cref="PrintDialog"/>. It
/// prints the Visual Document itself (a <see cref="FlowDocument"/>), so the printout — and a PDF made
/// through the dialog's "Microsoft Print to PDF" printer — matches what the user sees on screen, laid
/// out under the one editor-wide Page Setup: the print ticket is asked for the setup's orientation and
/// the page is laid out with its Print Margins (INV-061).
/// </summary>
public sealed class PrintDialogDocumentPrinter : IDocumentPrinter
{
    /// <inheritdoc />
    public void Print(FlowDocument document, string description, PageSetup pageSetup)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(pageSetup);

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            // Choosing not to print is not a failure — it simply prints nothing.
            return;
        }

        // The Page Setup's orientation goes onto the print ticket before the printable area is read,
        // so the area already reflects the turned page (INV-061).
        dialog.PrintTicket.PageOrientation = pageSetup.Orientation == UI.Core.PageOrientation.Landscape
            ? System.Printing.PageOrientation.Landscape
            : System.Printing.PageOrientation.Portrait;

        // Lay the document out to the chosen printer's page: a single column the width of the
        // printable area, inset by the Page Setup's Print Margins — the margins the Sheet shows.
        document.ColumnWidth = double.PositiveInfinity;
        document.PageWidth = dialog.PrintableAreaWidth;
        document.PageHeight = dialog.PrintableAreaHeight;
        document.PagePadding = pageSetup.Margins.ToThickness();

        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, description);
    }
}
