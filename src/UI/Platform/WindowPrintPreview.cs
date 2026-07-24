using System.Windows;
using System.Windows.Documents;
using UI.Core;
using UI.ViewModels;
using UI.Views;

namespace UI.Platform;

/// <summary>
/// Realises the Print Preview as a modal <see cref="PrintPreviewWindow"/> over the active window. It
/// is the WPF adapter behind <see cref="IPrintPreview"/>: keeping the window behind the port is what
/// lets Print Preview (INV-061) be tested headlessly, exactly as <see cref="WindowFlowchartBuilder"/>
/// does for the Flowchart Builder (INV-053).
/// </summary>
/// <param name="printer">The printer the preview's Print action sends the document to (INV-034).</param>
public sealed class WindowPrintPreview(IDocumentPrinter printer) : IPrintPreview
{
    private readonly IDocumentPrinter _printer = printer ?? throw new ArgumentNullException(nameof(printer));

    /// <inheritdoc />
    public void Show(FlowDocument document, PageSetup pageSetup, string description)
    {
        var viewModel = new PrintPreviewViewModel(document, pageSetup, description, _printer);
        var window = new PrintPreviewWindow
        {
            DataContext = viewModel,
            // Fully qualified: "Application" alone binds to the Application layer's namespace.
            Owner = System.Windows.Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(candidate => candidate.IsActive),
        };

        window.ShowDialog();
    }
}
