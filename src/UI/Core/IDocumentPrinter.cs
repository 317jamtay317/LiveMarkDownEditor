using System.Windows.Documents;

namespace UI.Core;

/// <summary>
/// Abstraction over the platform's printing, so the editor can send a Visual Document to a printer
/// without depending on the WPF print dialog directly (keeping the Print action unit-testable).
/// </summary>
/// <remarks>
/// The document handed over is a self-contained <see cref="FlowDocument"/> — freshly projected from
/// the Markdown source, not the live editing surface — so printing it neither disturbs what the user
/// is editing nor changes any document (INV-034).
/// </remarks>
public interface IDocumentPrinter
{
    /// <summary>Prints the given Visual Document, prompting the user for a printer.</summary>
    /// <param name="document">The self-contained document to print.</param>
    /// <param name="description">A short job description shown in the print queue.</param>
    void Print(FlowDocument document, string description);
}
