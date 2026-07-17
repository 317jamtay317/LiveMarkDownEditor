using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// <see cref="IDocumentPrinter"/> implementation backed by the WPF <see cref="PrintDialog"/>. It
/// prints the Visual Document itself (a <see cref="FlowDocument"/>), so the printout — and a PDF made
/// through the dialog's "Microsoft Print to PDF" printer — matches what the user sees on screen.
/// </summary>
public sealed class PrintDialogDocumentPrinter : IDocumentPrinter
{
    /// <inheritdoc />
    public void Print(FlowDocument document, string description)
    {
        ArgumentNullException.ThrowIfNull(document);

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            // Choosing not to print is not a failure — it simply prints nothing.
            return;
        }

        // Lay the document out to the chosen printer's page: a single column the width of the
        // printable area, with a comfortable margin.
        document.ColumnWidth = double.PositiveInfinity;
        document.PageWidth = dialog.PrintableAreaWidth;
        document.PageHeight = dialog.PrintableAreaHeight;
        document.PagePadding = new Thickness(48);

        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, description);
    }
}
