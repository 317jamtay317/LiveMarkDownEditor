using System.IO;
using Domain;
using Infrastructure.Markdown;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Infrastructure.Pdf;

/// <summary>
/// MigraDoc-backed adapter for the <see cref="IPdfExporter"/> port. Parses a
/// <see cref="MarkdownDocument"/> with the shared <see cref="GfmPipeline"/> and re-lays-out its
/// content into a PDF (INV-033), rendering each Mermaid Diagram to an image placed in the diagram's
/// place (INV-050).
/// </summary>
/// <remarks>
/// Parsing uses the same pipeline as the HTML render and the Visual Document projection, so the PDF
/// agrees with what the user edits and exports on the GFM feature set. Rendering needs the process's
/// fonts: on Windows the built-in resolver is enabled once via
/// <see cref="GlobalFontSettings.UseWindowsFontsUnderWindows"/>, and the composer stays within the
/// core font families that resolver maps. Each Mermaid Diagram is rendered through the injected
/// <see cref="IMermaidImageRenderer"/> and written to a temporary PNG the composer embeds; a diagram
/// the renderer cannot produce is passed as no image, so the composer falls back to its source text.
/// </remarks>
public sealed class MigraDocPdfExporter(IMermaidImageRenderer diagramRenderer) : IPdfExporter
{
    private static readonly Lock FontGate = new();
    private static bool _fontsConfigured;

    /// <inheritdoc />
    public async Task<byte[]> ExportAsync(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ConfigureFonts();

        var ast = Markdig.Markdown.Parse(document.Source.Text, GfmPipeline.Create());

        var (diagrams, tempFiles) = await RenderDiagramsAsync(ast).ConfigureAwait(true);
        try
        {
            var composed = new MarkdownPdfComposer(diagrams).Compose(ast);

            var renderer = new PdfDocumentRenderer { Document = composed };
            renderer.RenderDocument();

            using var stream = new MemoryStream();
            renderer.PdfDocument.Save(stream, false);
            return stream.ToArray();
        }
        finally
        {
            DeleteTempFiles(tempFiles);
        }
    }

    // Renders every Mermaid Diagram in the document to a temporary PNG (INV-050), returning the images
    // keyed by source for the composer plus the temp paths to clean up. A diagram the renderer cannot
    // produce is simply omitted, so the composer writes its source text instead.
    private async Task<(IReadOnlyDictionary<string, PreparedDiagram> Diagrams, IReadOnlyList<string> TempFiles)>
        RenderDiagramsAsync(Markdig.Syntax.MarkdownDocument ast)
    {
        var diagrams = new Dictionary<string, PreparedDiagram>();
        var tempFiles = new List<string>();

        foreach (var source in MermaidBlocks.Find(ast))
        {
            // Diagrams render light for print, whatever the app's on-screen theme (INV-050).
            var image = await diagramRenderer.RenderAsync(source, dark: false).ConfigureAwait(true);
            if (image is null)
            {
                continue;
            }

            var path = Path.Combine(Path.GetTempPath(), $"lmde-mermaid-{Guid.NewGuid():N}.png");
            await File.WriteAllBytesAsync(path, image.Png).ConfigureAwait(true);
            tempFiles.Add(path);
            diagrams[source] = new PreparedDiagram(path, image.PixelWidth, image.PixelHeight);
        }

        return (diagrams, tempFiles);
    }

    private static void DeleteTempFiles(IReadOnlyList<string> tempFiles)
    {
        foreach (var path in tempFiles)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // A temp file we cannot delete is left for the OS to reclaim — never fail an export over it.
            }
        }
    }

    /// <summary>
    /// Enables the built-in Windows font resolver, once per process. MigraDoc cannot resolve fonts on
    /// a non-Windows target framework without this, even when running on Windows.
    /// </summary>
    private static void ConfigureFonts()
    {
        if (_fontsConfigured)
        {
            return;
        }

        lock (FontGate)
        {
            if (_fontsConfigured)
            {
                return;
            }

            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            _fontsConfigured = true;
        }
    }
}
