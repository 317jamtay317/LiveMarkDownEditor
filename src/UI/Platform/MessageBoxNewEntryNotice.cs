using System.Windows;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// WPF adapter for <see cref="INewEntryNotice"/> that tells the user, with a modal message box, why New
/// File or New Folder created nothing (INV-085, INV-086).
/// </summary>
public sealed class MessageBoxNewEntryNotice : INewEntryNotice
{
    /// <inheritdoc />
    public void Explain(string heading, string reason) =>
        MessageBox.Show(reason, heading, MessageBoxButton.OK, MessageBoxImage.Warning);
}
