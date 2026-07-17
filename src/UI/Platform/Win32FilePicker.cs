using Domain;
using Microsoft.Win32;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// <see cref="IFilePicker"/> implementation backed by the Windows common file dialogs.
/// </summary>
public sealed class Win32FilePicker : IFilePicker
{
    private const string MarkdownFilter =
        "Markdown files (*.md;*.markdown)|*.md;*.markdown|All files (*.*)|*.*";

    /// <summary>
    /// The Export Shapes, as the save dialog's file types. The order is the dialog's order, and
    /// <see cref="SaveFileDialog.FilterIndex"/> is 1-based — hence the index arithmetic below.
    /// </summary>
    private static readonly ExportShape[] ExportShapes =
        [ExportShape.StandalonePage, ExportShape.HtmlFragment];

    private const string HtmlExportFilter =
        "HTML document (*.html)|*.html|HTML fragment (*.html)|*.html";

    /// <inheritdoc />
    public string? PickOpen()
    {
        var dialog = new OpenFileDialog
        {
            Filter = MarkdownFilter,
            CheckFileExists = true,
            Title = "Open Markdown file",
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public string? PickSave(string? suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = MarkdownFilter,
            FileName = suggestedFileName ?? "Untitled.md",
            DefaultExt = ".md",
            AddExtension = true,
            Title = "Save Markdown file",
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <inheritdoc />
    public HtmlExportTarget? PickHtmlExport(string? suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Filter = HtmlExportFilter,
            FileName = suggestedFileName ?? "Untitled.html",
            DefaultExt = ".html",
            AddExtension = true,
            Title = "Export as HTML",
        };

        if (dialog.ShowDialog() != true)
        {
            return null;
        }

        var index = Math.Clamp(dialog.FilterIndex - 1, 0, ExportShapes.Length - 1);
        return new HtmlExportTarget(dialog.FileName, ExportShapes[index]);
    }
}
