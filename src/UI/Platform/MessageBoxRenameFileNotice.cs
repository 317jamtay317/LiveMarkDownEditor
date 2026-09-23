using System.Windows;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// WPF adapter for <see cref="IRenameFileNotice"/> that tells the user, with a modal message box, why
/// Rename File renamed nothing (INV-082).
/// </summary>
public sealed class MessageBoxRenameFileNotice : IRenameFileNotice
{
    /// <inheritdoc />
    public void Explain(string reason) =>
        MessageBox.Show(reason, "Rename file", MessageBoxButton.OK, MessageBoxImage.Warning);
}
