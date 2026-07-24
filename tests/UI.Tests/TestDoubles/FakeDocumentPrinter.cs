using System.Windows.Documents;
using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IDocumentPrinter"/> for tests. It records how many times Print was called and
/// the text of the last document it was handed, so a test can assert what would have been printed
/// without a real printer (INV-034).
/// </summary>
public sealed class FakeDocumentPrinter : IDocumentPrinter
{
    /// <summary>How many times Print was called. Zero means nothing was printed.</summary>
    public int PrintCount { get; private set; }

    /// <summary>The plain text of the last document handed to Print, or <see langword="null"/> if never.</summary>
    public string? PrintedText { get; private set; }

    /// <summary>The job description of the last Print call.</summary>
    public string? Description { get; private set; }

    /// <summary>The Page Setup of the last Print call, or <see langword="null"/> if never (INV-061).</summary>
    public PageSetup? PrintedSetup { get; private set; }

    /// <inheritdoc />
    public void Print(FlowDocument document, string description, PageSetup pageSetup)
    {
        ArgumentNullException.ThrowIfNull(document);

        PrintCount++;
        Description = description;
        PrintedSetup = pageSetup;
        PrintedText = new TextRange(document.ContentStart, document.ContentEnd).Text;
    }
}
