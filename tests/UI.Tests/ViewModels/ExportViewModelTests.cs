using Domain;
using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="ExportViewModel"/> — Export as HTML. Covers INV-032: exporting is not an
/// edit, cancelling writes nothing, the export carries the Editor Session's own text (unsaved edits
/// and all), and the Export Shape chooses only the packaging.
/// </summary>
public sealed class ExportViewModelTests
{
    private const string DocumentPath = @"C:\docs\note.md";
    private const string ExportPath = @"C:\docs\note.html";

    private readonly FakeDocumentStore _store = new();
    private readonly FakeHtmlExportStore _exports = new();
    private readonly FakePdfExporter _pdfExporter = new();
    private readonly FakePdfExportStore _pdfExports = new();
    private readonly StubFilePicker _picker = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly FakeMarkdownRoundTrip _roundTrip = new();
    private readonly StubMarkdownRenderer _renderer = new();
    private readonly FakeMermaidScriptSource _mermaidScript = new();

    private ExportViewModel CreateExport() =>
        new(_picker, _renderer, _exports, _pdfExporter, _pdfExports, _mermaidScript);

    private EditorSessionViewModel CreateSession() =>
        new(_store, new FakeDocumentWatcher(), _dispatcher, _roundTrip);

    [Fact]
    public void Constructor_GivenNullFilePicker_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(
            () => new ExportViewModel(null!, _renderer, _exports, _pdfExporter, _pdfExports, _mermaidScript));
    }

    [Fact]
    public void Constructor_GivenNullRenderer_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(
            () => new ExportViewModel(_picker, null!, _exports, _pdfExporter, _pdfExports, _mermaidScript));
    }

    [Fact]
    public void Constructor_GivenNullExportStore_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(
            () => new ExportViewModel(_picker, _renderer, null!, _pdfExporter, _pdfExports, _mermaidScript));
    }

    [Fact]
    public void Constructor_GivenNullPdfExporter_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(
            () => new ExportViewModel(_picker, _renderer, _exports, null!, _pdfExports, _mermaidScript));
    }

    [Fact]
    public void Constructor_GivenNullPdfExportStore_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(
            () => new ExportViewModel(_picker, _renderer, _exports, _pdfExporter, null!, _mermaidScript));
    }

    [Fact]
    public void Constructor_GivenNullMermaidScriptSource_ThrowsAndPreservesInvariant()
    {
        Should.Throw<ArgumentNullException>(
            () => new ExportViewModel(_picker, _renderer, _exports, _pdfExporter, _pdfExports, null!));
    }

    [Fact]
    public async Task ExportHtml_WritesTheRenderedOutput_ToTheChosenPath_INV032()
    {
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.HtmlFragment);
        var session = CreateSession();
        session.Markdown = "# Title";

        await CreateExport().ExportHtmlAsync(session);

        _exports.SavedHtml(ExportPath).ShouldBe(_renderer.Render(new MarkdownDocument("# Title")).Html);
    }

    [Fact]
    public async Task ExportHtml_WhenTheSaveDialogIsCancelled_WritesNothing_INV032()
    {
        _picker.HtmlExportResult = null;
        var session = CreateSession();
        session.Markdown = "# Title";

        await CreateExport().ExportHtmlAsync(session);

        _exports.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExportHtml_WithNoActiveSession_WritesNothing_INV032()
    {
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.HtmlFragment);

        await CreateExport().ExportHtmlAsync(session: null);

        _exports.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExportHtml_WithUnsavedEdits_ExportsTheSessionsText_AndLeavesThemUnsaved_INV032()
    {
        // The export must show what the user is looking at, not what the Watched File still holds —
        // and exporting must not quietly count as saving.
        _store.Seed(DocumentPath, "# On disk");
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.HtmlFragment);
        var session = CreateSession();
        await session.LoadAsync(DocumentPath);
        session.Markdown = "# Edited but unsaved";

        await CreateExport().ExportHtmlAsync(session);

        _exports.SavedHtml(ExportPath).ShouldContain("Edited but unsaved");
        _exports.SavedHtml(ExportPath).ShouldNotContain("On disk");
        session.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public async Task ExportHtml_NeverWritesTheWatchedFile_INV032()
    {
        _store.Seed(DocumentPath, "# On disk");
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.HtmlFragment);
        var session = CreateSession();
        await session.LoadAsync(DocumentPath);
        session.Markdown = "# Edited but unsaved";

        await CreateExport().ExportHtmlAsync(session);

        _store.SavedText(DocumentPath).ShouldBe("# On disk");
    }

    [Fact]
    public async Task ExportHtml_LeavesTheMarkdownDocumentUnchanged_INV032()
    {
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.StandalonePage);
        var session = CreateSession();
        session.Markdown = "# Title";

        await CreateExport().ExportHtmlAsync(session);

        session.Markdown.ShouldBe("# Title");
    }

    [Fact]
    public async Task ExportHtml_AsAStandalonePage_WritesACompletePage_INV032()
    {
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.StandalonePage);
        var session = CreateSession();
        session.Markdown = "# Title";

        await CreateExport().ExportHtmlAsync(session);

        _exports.SavedHtml(ExportPath).ShouldStartWith("<!DOCTYPE html>");
    }

    [Fact]
    public async Task ExportHtml_SeedsTheDialogWithTheWatchedFilesName_INV032()
    {
        // Exporting note.md should propose note.html, not "Untitled".
        _store.Seed(DocumentPath, "# Title");
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.StandalonePage);
        var session = CreateSession();
        await session.LoadAsync(DocumentPath);

        await CreateExport().ExportHtmlAsync(session);

        _picker.SuggestedHtmlExportName.ShouldBe("note.html");
    }

    [Fact]
    public async Task ExportHtml_ForAnUnsavedSession_SeedsTheDialogWithUntitled_INV032()
    {
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.StandalonePage);

        await CreateExport().ExportHtmlAsync(CreateSession());

        _picker.SuggestedHtmlExportName.ShouldBe("Untitled.html");
    }

    [Fact]
    public async Task ExportHtml_AsAStandalonePage_TitlesThePageAfterTheDocument_INV032()
    {
        _store.Seed(DocumentPath, "# Title");
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.StandalonePage);
        var session = CreateSession();
        await session.LoadAsync(DocumentPath);

        await CreateExport().ExportHtmlAsync(session);

        _exports.SavedHtml(ExportPath).ShouldContain("<title>note</title>");
    }

    [Fact]
    public async Task ExportHtml_AsAStandalonePage_WithAMermaidDiagram_EmbedsTheScript_INV049()
    {
        // The stub renderer echoes the source, so this stands in for Rendered Output carrying a
        // language-mermaid code block; the real language-class output is covered by the Markdig adapter.
        _mermaidScript.Script = "MERMAID_LIB_CODE";
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.StandalonePage);
        var session = CreateSession();
        session.Markdown = "language-mermaid";

        await CreateExport().ExportHtmlAsync(session);

        _exports.SavedHtml(ExportPath).ShouldContain("MERMAID_LIB_CODE");
    }

    [Fact]
    public async Task ExportHtml_AsAStandalonePage_WithoutAMermaidDiagram_EmbedsNoScript_INV049()
    {
        _mermaidScript.Script = "MERMAID_LIB_CODE";
        _picker.HtmlExportResult = new HtmlExportTarget(ExportPath, ExportShape.StandalonePage);
        var session = CreateSession();
        session.Markdown = "# Just a heading";

        await CreateExport().ExportHtmlAsync(session);

        _exports.SavedHtml(ExportPath).ShouldNotContain("MERMAID_LIB_CODE");
    }

    private const string PdfPath = @"C:\docs\note.pdf";

    [Fact]
    public async Task ExportPdf_WritesTheExportedBytes_ToTheChosenPath_INV033()
    {
        _picker.PdfExportResult = PdfPath;
        var session = CreateSession();
        session.Markdown = "# Title";

        await CreateExport().ExportPdfAsync(session);

        _pdfExports.SavedBytes(PdfPath).ShouldBe(FakePdfExporter.BytesFor("# Title"));
    }

    [Fact]
    public async Task ExportPdf_WhenTheSaveDialogIsCancelled_WritesNothing_INV033()
    {
        _picker.PdfExportResult = null;
        var session = CreateSession();
        session.Markdown = "# Title";

        await CreateExport().ExportPdfAsync(session);

        _pdfExports.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExportPdf_WithNoActiveSession_WritesNothing_INV033()
    {
        _picker.PdfExportResult = PdfPath;

        await CreateExport().ExportPdfAsync(session: null);

        _pdfExports.WriteCount.ShouldBe(0);
    }

    [Fact]
    public async Task ExportPdf_WithUnsavedEdits_ExportsTheSessionsText_INV033()
    {
        // The export must show what the user is looking at, not what the Watched File still holds —
        // and exporting must not quietly count as saving.
        _store.Seed(DocumentPath, "# On disk");
        _picker.PdfExportResult = PdfPath;
        var session = CreateSession();
        await session.LoadAsync(DocumentPath);
        session.Markdown = "# Edited but unsaved";

        await CreateExport().ExportPdfAsync(session);

        _pdfExporter.Exported.ShouldContain("# Edited but unsaved");
        _pdfExporter.Exported.ShouldNotContain("# On disk");
        session.HasUnsavedEdits.ShouldBeTrue();
    }

    [Fact]
    public async Task ExportPdf_NeverWritesTheWatchedFile_INV033()
    {
        _store.Seed(DocumentPath, "# On disk");
        _picker.PdfExportResult = PdfPath;
        var session = CreateSession();
        await session.LoadAsync(DocumentPath);
        session.Markdown = "# Edited but unsaved";

        await CreateExport().ExportPdfAsync(session);

        _store.SavedText(DocumentPath).ShouldBe("# On disk");
    }

    [Fact]
    public async Task ExportPdf_LeavesTheMarkdownDocumentUnchanged_INV033()
    {
        _picker.PdfExportResult = PdfPath;
        var session = CreateSession();
        session.Markdown = "# Title";

        await CreateExport().ExportPdfAsync(session);

        session.Markdown.ShouldBe("# Title");
    }

    [Fact]
    public async Task ExportPdf_SeedsTheDialogWithTheWatchedFilesName_INV033()
    {
        // Exporting note.md should propose note.pdf, not "Untitled".
        _store.Seed(DocumentPath, "# Title");
        _picker.PdfExportResult = PdfPath;
        var session = CreateSession();
        await session.LoadAsync(DocumentPath);

        await CreateExport().ExportPdfAsync(session);

        _picker.SuggestedPdfExportName.ShouldBe("note.pdf");
    }

    [Fact]
    public async Task ExportPdf_ForAnUnsavedSession_SeedsTheDialogWithUntitled_INV033()
    {
        _picker.PdfExportResult = PdfPath;

        await CreateExport().ExportPdfAsync(CreateSession());

        _picker.SuggestedPdfExportName.ShouldBe("Untitled.pdf");
    }
}
