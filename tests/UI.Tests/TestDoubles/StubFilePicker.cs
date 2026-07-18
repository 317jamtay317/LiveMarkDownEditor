using UI.Core;

namespace UI.Tests.TestDoubles;

/// <summary>
/// Scriptable <see cref="IFilePicker"/> for tests: returns pre-set paths for open/save prompts
/// (<see langword="null"/> simulates the user cancelling).
/// </summary>
public sealed class StubFilePicker : IFilePicker
{
    /// <summary>The path returned by <see cref="PickOpen"/>.</summary>
    public string? OpenResult { get; set; }

    /// <summary>The path returned by <see cref="PickSave"/>.</summary>
    public string? SaveResult { get; set; }

    /// <summary>The target returned by <see cref="PickHtmlExport"/>.</summary>
    public HtmlExportTarget? HtmlExportResult { get; set; }

    /// <summary>The file name <see cref="PickHtmlExport"/> was last seeded with.</summary>
    public string? SuggestedHtmlExportName { get; private set; }

    /// <summary>The path returned by <see cref="PickPdfExport"/>.</summary>
    public string? PdfExportResult { get; set; }

    /// <summary>The file name <see cref="PickPdfExport"/> was last seeded with.</summary>
    public string? SuggestedPdfExportName { get; private set; }

    /// <inheritdoc />
    public string? PickOpen() => OpenResult;

    /// <inheritdoc />
    public string? PickSave(string? suggestedFileName) => SaveResult;

    /// <inheritdoc />
    public HtmlExportTarget? PickHtmlExport(string? suggestedFileName)
    {
        SuggestedHtmlExportName = suggestedFileName;
        return HtmlExportResult;
    }

    /// <inheritdoc />
    public string? PickPdfExport(string? suggestedFileName)
    {
        SuggestedPdfExportName = suggestedFileName;
        return PdfExportResult;
    }
}
