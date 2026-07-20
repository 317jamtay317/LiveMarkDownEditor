using System.Text;
using Domain;
using Infrastructure.Pdf;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="MigraDocPdfExporter"/>, the adapter that re-lays-out a Markdown Document as a
/// PDF (INV-033) and embeds each Mermaid Diagram as an image (INV-050). PDF content is compressed and
/// stamped with a creation time, so these assert structural facts — a valid header, non-empty output,
/// relative size, and no throw across the whole GFM construct set — rather than exact bytes.
/// </summary>
public sealed class MigraDocPdfExporterTests
{
    private const string Diagram = "```mermaid\ngraph TD\n  A-->B\n```";

    private readonly IPdfExporter _exporter = new MigraDocPdfExporter(new FakeMermaidImageRenderer());

    private static string Header(byte[] pdf) => Encoding.ASCII.GetString(pdf, 0, 5);

    [Fact]
    public async Task Export_GivenNullDocument_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () => await _exporter.ExportAsync(null!));
    }

    [Fact]
    public async Task Export_GivenEmptyDocument_ProducesAValidPdf()
    {
        var pdf = await _exporter.ExportAsync(MarkdownDocument.Empty);

        pdf.Length.ShouldBeGreaterThan(0);
        Header(pdf).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task Export_ProducesBytesBeginningWithThePdfHeader()
    {
        var pdf = await _exporter.ExportAsync(new MarkdownDocument("# Title\n\nA paragraph."));

        Header(pdf).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task Export_GivenARichDocument_ExercisingEveryConstruct_DoesNotThrow_AndProducesAPdf()
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

        var pdf = await _exporter.ExportAsync(new MarkdownDocument(markdown));

        Header(pdf).ShouldBe("%PDF-");
        pdf.Length.ShouldBeGreaterThan(1000);
    }

    [Fact]
    public async Task Export_GivenMoreContent_ProducesALargerPdfThanLessContent()
    {
        var shortPdf = await _exporter.ExportAsync(new MarkdownDocument("A short line."));

        var longMarkdown = string.Join("\n\n", Enumerable.Repeat("A full paragraph of prose that fills a real line and then some.", 60));
        var longPdf = await _exporter.ExportAsync(new MarkdownDocument(longMarkdown));

        longPdf.Length.ShouldBeGreaterThan(shortPdf.Length);
    }

    [Fact]
    public async Task Export_CalledTwice_DoesNotThrow()
    {
        // Guards the one-time static font configuration against a second call.
        await _exporter.ExportAsync(new MarkdownDocument("# One"));
        var second = await _exporter.ExportAsync(new MarkdownDocument("# Two"));

        Header(second).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task Export_WithAMermaidDiagram_RendersItThroughTheRenderer_AndProducesAValidPdf_INV050()
    {
        var renderer = new FakeMermaidImageRenderer();
        var exporter = new MigraDocPdfExporter(renderer);

        var pdf = await exporter.ExportAsync(new MarkdownDocument($"# Title\n\n{Diagram}"));

        renderer.Rendered.ShouldContain("graph TD\n  A-->B");
        Header(pdf).ShouldBe("%PDF-");
    }

    [Fact]
    public async Task Export_WithAMermaidDiagram_WhenTheRendererProducesNothing_FallsBackToAValidPdf_INV050()
    {
        // A diagram the renderer cannot produce falls back to its source text (INV-050), never a crash.
        var exporter = new MigraDocPdfExporter(new FakeMermaidImageRenderer(rendersNothing: true));

        var pdf = await exporter.ExportAsync(new MarkdownDocument($"# Title\n\n{Diagram}"));

        Header(pdf).ShouldBe("%PDF-");
    }
}
