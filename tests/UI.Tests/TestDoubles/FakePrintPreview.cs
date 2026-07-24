using System.Windows.Documents;
using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IPrintPreview"/> for tests. It records how many times the Print Preview was
/// shown, the text of the document it was handed, and the Page Setup it was asked to paginate under,
/// so a test can assert what would have been previewed without a window (INV-061).
/// </summary>
public sealed class FakePrintPreview : IPrintPreview
{
    /// <summary>How many times Show was called. Zero means no preview was shown.</summary>
    public int ShowCount { get; private set; }

    /// <summary>The plain text of the last document handed over, or <see langword="null"/> if never.</summary>
    public string? PreviewedText { get; private set; }

    /// <summary>The Page Setup of the last Show call, or <see langword="null"/> if never.</summary>
    public PageSetup? PreviewedSetup { get; private set; }

    /// <inheritdoc />
    public void Show(FlowDocument document, PageSetup pageSetup, string description)
    {
        ArgumentNullException.ThrowIfNull(document);

        ShowCount++;
        PreviewedSetup = pageSetup;
        PreviewedText = new TextRange(document.ContentStart, document.ContentEnd).Text;
    }
}
