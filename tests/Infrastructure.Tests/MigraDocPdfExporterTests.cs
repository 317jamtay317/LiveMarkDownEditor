using System.Text;
using Domain;
using Infrastructure.Pdf;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="MigraDocPdfExporter"/>, the adapter that re-lays-out a Markdown Document as a
/// PDF (INV-033). PDF content is compressed and stamped with a creation time, so these assert
/// structural facts — a valid header, non-empty output, relative size, and no throw across the whole
/// GFM construct set — rather than exact bytes.
/// </summary>
public sealed class MigraDocPdfExporterTests
{
    private readonly IPdfExporter _exporter = new MigraDocPdfExporter();

    private static string Header(byte[] pdf) => Encoding.ASCII.GetString(pdf, 0, 5);

    [Fact]
    public void Export_GivenNullDocument_Throws()
    {
        Should.Throw<ArgumentNullException>(() => _exporter.Export(null!));
    }

    [Fact]
    public void Export_GivenEmptyDocument_ProducesAValidPdf()
    {
        var pdf = _exporter.Export(MarkdownDocument.Empty);

        pdf.Length.ShouldBeGreaterThan(0);
        Header(pdf).ShouldBe("%PDF-");
    }

    [Fact]
    public void Export_ProducesBytesBeginningWithThePdfHeader()
    {
        var pdf = _exporter.Export(new MarkdownDocument("# Title\n\nA paragraph."));

        Header(pdf).ShouldBe("%PDF-");
    }

    [Fact]
    public void Export_GivenARichDocument_ExercisingEveryConstruct_DoesNotThrow_AndProducesAPdf()
    {
        var markdown = string.Join("\n\n",
            "# Heading 1",
            "## Heading 2",
            "### Heading 3",
            "A paragraph with **bold**, *italic*, `code`, ~~strike~~ and a [link](https://example.com).",
            "- bullet one\n- bullet two\n  - nested bullet",
            "1. first\n2. second",
            "- [ ] a task\n- [x] a done task",
            "```csharp\nvar x = 42;\nConsole.WriteLine(x);\n```",
            "> a block quote\n> spanning two lines",
            "---",
            "| Left | Centre | Right |\n|:-----|:------:|------:|\n| a | b | c |",
            "![a picture](cat.png)");

        var pdf = _exporter.Export(new MarkdownDocument(markdown));

        Header(pdf).ShouldBe("%PDF-");
        pdf.Length.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public void Export_GivenMoreContent_ProducesALargerPdfThanLessContent()
    {
        var shortPdf = _exporter.Export(new MarkdownDocument("A short line."));

        var longMarkdown = string.Join("\n\n", Enumerable.Repeat("A full paragraph of prose that fills a real line and then some.", 60));
        var longPdf = _exporter.Export(new MarkdownDocument(longMarkdown));

        longPdf.Length.ShouldBeGreaterThan(shortPdf.Length);
    }

    [Fact]
    public void Export_CalledTwice_DoesNotThrow()
    {
        // Guards the one-time static font configuration against a second call.
        _exporter.Export(new MarkdownDocument("# One"));
        var second = _exporter.Export(new MarkdownDocument("# Two"));

        Header(second).ShouldBe("%PDF-");
    }
}
