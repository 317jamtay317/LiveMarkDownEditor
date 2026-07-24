using System.Windows.Documents;
using System.Windows.Input;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The Print Preview's state: the self-contained document being previewed — re-projected from the
/// Markdown source the way Print re-projects it (INV-034) — and the one editor-wide Page Setup its
/// pages are paginated under. Print sends that very document under that very setup to the printer, so
/// what the preview shows is what prints; previewing changes nothing (INV-061).
/// </summary>
public sealed class PrintPreviewViewModel
{
    private readonly string _description;
    private readonly IDocumentPrinter _printer;

    /// <summary>Creates the Print Preview's state.</summary>
    /// <param name="document">The self-contained document to preview.</param>
    /// <param name="setup">The Page Setup the preview paginates under.</param>
    /// <param name="description">The print-queue job description used when printing from the preview.</param>
    /// <param name="printer">The printer Print sends the document to (INV-034).</param>
    public PrintPreviewViewModel(FlowDocument document, PageSetup setup, string description, IDocumentPrinter printer)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _description = description ?? throw new ArgumentNullException(nameof(description));
        _printer = printer ?? throw new ArgumentNullException(nameof(printer));
        PrintCommand = new RelayCommand(Print);
    }

    /// <summary>The self-contained document being previewed.</summary>
    public FlowDocument Document { get; }

    /// <summary>The Page Setup the preview's pages are paginated under (INV-061).</summary>
    public PageSetup Setup { get; }

    /// <summary>Prints the previewed document under the previewed Page Setup (INV-034, INV-061).</summary>
    public ICommand PrintCommand { get; }

    private void Print() => _printer.Print(Document, _description, Setup);
}
