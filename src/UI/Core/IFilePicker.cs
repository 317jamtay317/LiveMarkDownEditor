namespace UI.Core;

/// <summary>
/// Abstraction over the platform's open/save file dialogs, so ViewModels can request a Watched File
/// path without depending on WPF dialog types (keeping them unit-testable).
/// </summary>
public interface IFilePicker
{
    /// <summary>Prompts the user to choose an existing Markdown file to open.</summary>
    /// <returns>The chosen file path, or <see langword="null"/> if the user cancelled.</returns>
    string? PickOpen();

    /// <summary>Prompts the user to choose a destination path to save a Markdown file.</summary>
    /// <param name="suggestedFileName">A suggested file name, or <see langword="null"/> for none.</param>
    /// <returns>The chosen file path, or <see langword="null"/> if the user cancelled.</returns>
    string? PickSave(string? suggestedFileName);

    /// <summary>
    /// Prompts the user to choose a destination path and an Export Shape for an Export as HTML. The
    /// Export Shape is chosen in the same dialog, as the file type, because it is a choice of
    /// packaging rather than of document (INV-032).
    /// </summary>
    /// <param name="suggestedFileName">A suggested file name, or <see langword="null"/> for none.</param>
    /// <returns>The chosen target, or <see langword="null"/> if the user cancelled.</returns>
    HtmlExportTarget? PickHtmlExport(string? suggestedFileName);
}
