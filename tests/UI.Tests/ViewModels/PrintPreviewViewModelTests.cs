using System.Windows.Documents;
using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.Tests.Wysiwyg;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="PrintPreviewViewModel"/>: the Print Preview holds the re-projected document
/// and the one editor-wide Page Setup, prints that very document under that very setup through the
/// printer, and previewing changes nothing (INV-061).
/// </summary>
public sealed class PrintPreviewViewModelTests
{
    private static FlowDocument Document(string text) => new(new Paragraph(new Run(text)));

    [Fact]
    public void Construct_HoldsTheDocumentAndTheSetup_INV061()
    {
        StaThread.Run(() =>
        {
            var document = Document("Body text");
            var setup = new PageSetup(PageOrientation.Landscape, PrintMargins.For(MarginPreset.Narrow));

            var viewModel = new PrintPreviewViewModel(document, setup, "job", new FakeDocumentPrinter());

            viewModel.Document.ShouldBeSameAs(document);
            viewModel.Setup.ShouldBe(setup);
        });
    }

    [Fact]
    public void Print_SendsTheSameDocumentAndSetupToThePrinter_INV061()
    {
        StaThread.Run(() =>
        {
            var printer = new FakeDocumentPrinter();
            var setup = new PageSetup(PageOrientation.Landscape, PrintMargins.For(MarginPreset.Wide));
            var viewModel = new PrintPreviewViewModel(Document("Body text"), setup, "job", printer);

            viewModel.PrintCommand.Execute(null);

            // What the preview shows is what prints: the same document, the same Page Setup.
            printer.PrintCount.ShouldBe(1);
            printer.PrintedText!.ShouldContain("Body text");
            printer.PrintedSetup.ShouldBe(setup);
            printer.Description.ShouldBe("job");
        });
    }

    [Fact]
    public void Print_DoesNotChangeTheDocument_INV061()
    {
        StaThread.Run(() =>
        {
            var document = Document("Body text");
            var viewModel = new PrintPreviewViewModel(
                document, PageSetup.Default, "job", new FakeDocumentPrinter());

            viewModel.PrintCommand.Execute(null);

            new TextRange(document.ContentStart, document.ContentEnd).Text.ShouldContain("Body text");
        });
    }
}
