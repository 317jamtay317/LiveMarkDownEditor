using System.IO;
using System.Windows.Input;
using Application;
using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// The document export actions. Export as HTML renders the Active Session's Markdown Document to its
/// Rendered Output in the chosen Export Shape (INV-032); Export as PDF re-lays-out the same document
/// as a PDF (INV-033). Each writes a file the user chooses — never the Watched File.
/// </summary>
/// <remarks>
/// The export actions live here rather than on <see cref="WorkspaceViewModel"/> so the Workspace
/// keeps to Tab lifetime and file selection — the same reason <see cref="AppearanceViewModel"/> owns
/// the theme. The Editor Session to export arrives as a command parameter (the Active Session), so
/// this ViewModel never needs a reference back to the Workspace.
/// <para>
/// It writes through <see cref="IHtmlExportStore"/> and <see cref="IPdfExportStore"/> — never
/// <see cref="IDocumentStore"/>: an export has no route to the Watched File at all, which is what
/// makes "exporting is not an edit" structural rather than merely intended.
/// </para>
/// </remarks>
public sealed class ExportViewModel : ObservableObject
{
    private readonly IFilePicker _filePicker;
    private readonly IMarkdownRenderer _renderer;
    private readonly IHtmlExportStore _exportStore;
    private readonly IPdfExporter _pdfExporter;
    private readonly IPdfExportStore _pdfExportStore;

    /// <summary>Creates the export actions.</summary>
    /// <param name="filePicker">Asks the user where to export and in which Export Shape.</param>
    /// <param name="renderer">Renders the Markdown Document to its Rendered Output (INV-002).</param>
    /// <param name="exportStore">Writes the composed HTML to the chosen path.</param>
    /// <param name="pdfExporter">Exports the Markdown Document as PDF bytes (INV-033).</param>
    /// <param name="pdfExportStore">Writes the exported PDF bytes to the chosen path.</param>
    public ExportViewModel(
        IFilePicker filePicker,
        IMarkdownRenderer renderer,
        IHtmlExportStore exportStore,
        IPdfExporter pdfExporter,
        IPdfExportStore pdfExportStore)
    {
        _filePicker = filePicker ?? throw new ArgumentNullException(nameof(filePicker));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _exportStore = exportStore ?? throw new ArgumentNullException(nameof(exportStore));
        _pdfExporter = pdfExporter ?? throw new ArgumentNullException(nameof(pdfExporter));
        _pdfExportStore = pdfExportStore ?? throw new ArgumentNullException(nameof(pdfExportStore));

        ExportHtmlCommand = new AsyncRelayCommand<EditorSessionViewModel>(ExportHtmlAsync);
        ExportPdfCommand = new AsyncRelayCommand<EditorSessionViewModel>(ExportPdfAsync);
    }

    /// <summary>
    /// Exports the given Editor Session's Rendered Output as HTML. Parameter: the Active Session.
    /// </summary>
    public ICommand ExportHtmlCommand { get; }

    /// <summary>
    /// Exports the given Editor Session's Markdown Document as PDF. Parameter: the Active Session.
    /// </summary>
    public ICommand ExportPdfCommand { get; }

    /// <summary>
    /// Renders <paramref name="session"/>'s current source text and writes it to the file the user
    /// chooses. Cancelling the save dialog writes nothing, and the export never touches the Markdown
    /// Document or the Watched File (INV-032).
    /// </summary>
    /// <param name="session">The Editor Session to export, or <see langword="null"/> for none.</param>
    public async Task ExportHtmlAsync(EditorSessionViewModel? session)
    {
        if (session is null)
        {
            return;
        }

        var target = _filePicker.PickHtmlExport(SuggestedFileName(session));
        if (target is null)
        {
            // Asking the user where to put a file is not an export.
            return;
        }

        // Rendered from the session's own text, not re-read from the Watched File: an export shows
        // what the user is looking at, unsaved edits and all.
        var output = _renderer.Render(new MarkdownDocument(session.Markdown));
        var html = HtmlExport.Compose(output, target.Shape, TitleFor(target.Path));

        await _exportStore.SaveAsync(target.Path, html).ConfigureAwait(true);
    }

    /// <summary>
    /// Exports <paramref name="session"/>'s current source text as a PDF and writes it to the file the
    /// user chooses. Because a PDF cannot embed the Visual Document, its content is re-laid-out from the
    /// Markdown. Cancelling the save dialog writes nothing, and the export never touches the Markdown
    /// Document or the Watched File (INV-033).
    /// </summary>
    /// <param name="session">The Editor Session to export, or <see langword="null"/> for none.</param>
    public async Task ExportPdfAsync(EditorSessionViewModel? session)
    {
        if (session is null)
        {
            return;
        }

        var path = _filePicker.PickPdfExport(SuggestedPdfName(session));
        if (path is null)
        {
            // Asking the user where to put a file is not an export.
            return;
        }

        // Exported from the session's own text, not re-read from the Watched File: an export shows
        // what the user is looking at, unsaved edits and all.
        var bytes = _pdfExporter.Export(new MarkdownDocument(session.Markdown));

        await _pdfExportStore.SaveAsync(path, bytes).ConfigureAwait(true);
    }

    /// <summary>The Watched File's name with an .html extension, so exporting note.md proposes note.html.</summary>
    private static string SuggestedFileName(EditorSessionViewModel session) =>
        session.FilePath is null
            ? "Untitled.html"
            : Path.ChangeExtension(Path.GetFileName(session.FilePath), ".html");

    /// <summary>The Watched File's name with a .pdf extension, so exporting note.md proposes note.pdf.</summary>
    private static string SuggestedPdfName(EditorSessionViewModel session) =>
        session.FilePath is null
            ? "Untitled.pdf"
            : Path.ChangeExtension(Path.GetFileName(session.FilePath), ".pdf");

    /// <summary>
    /// The Standalone Page's title: the export file's own name. The Markdown Document has no title of
    /// its own — a first Heading is content, not metadata, and need not exist at all — so the name the
    /// user chose in the save dialog is the closest thing to one they actually picked.
    /// </summary>
    private static string TitleFor(string exportPath) => Path.GetFileNameWithoutExtension(exportPath);
}
