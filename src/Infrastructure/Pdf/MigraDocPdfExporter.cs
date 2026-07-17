using Domain;
using Infrastructure.Markdown;
using MigraDoc.Rendering;
using PdfSharp.Fonts;

namespace Infrastructure.Pdf;

/// <summary>
/// MigraDoc-backed adapter for the <see cref="IPdfExporter"/> port. Parses a
/// <see cref="MarkdownDocument"/> with the shared <see cref="GfmPipeline"/> and re-lays-out its
/// content into a PDF (INV-033).
/// </summary>
/// <remarks>
/// Parsing uses the same pipeline as the HTML render and the Visual Document projection, so the PDF
/// agrees with what the user edits and exports on the GFM feature set. Rendering needs the process's
/// fonts: on Windows the built-in resolver is enabled once via
/// <see cref="GlobalFontSettings.UseWindowsFontsUnderWindows"/>, and the composer stays within the
/// core font families that resolver maps.
/// </remarks>
public sealed class MigraDocPdfExporter : IPdfExporter
{
    private static readonly Lock FontGate = new();
    private static bool _fontsConfigured;

    /// <inheritdoc />
    public byte[] Export(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ConfigureFonts();

        var ast = Markdig.Markdown.Parse(document.Source.Text, GfmPipeline.Create());
        var composed = new MarkdownPdfComposer().Compose(ast);

        var renderer = new PdfDocumentRenderer { Document = composed };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
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
