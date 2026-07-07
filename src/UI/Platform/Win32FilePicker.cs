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
}
