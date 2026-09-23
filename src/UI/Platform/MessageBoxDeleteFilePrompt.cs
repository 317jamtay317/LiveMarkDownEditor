using System.Windows;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// WPF adapter for <see cref="IDeleteFilePrompt"/> that asks, with a modal message box, whether the
/// user is sure they want to delete a File (INV-081). No is the default button, so a stray Enter keeps
/// the File.
/// </summary>
public sealed class MessageBoxDeleteFilePrompt : IDeleteFilePrompt
{
    /// <inheritdoc />
    public bool Confirm(string fileName)
    {
        var result = MessageBox.Show(
            $"Are you sure you want to delete “{fileName}”?\n\nIt will be moved to the Recycle Bin.",
            "Delete file",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }
}
