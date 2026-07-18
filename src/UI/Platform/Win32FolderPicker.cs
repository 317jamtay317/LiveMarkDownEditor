using Microsoft.Win32;
using UI.Core;

namespace UI.Platform;

/// <summary>
/// <see cref="IFolderPicker"/> implementation backed by the Windows common folder dialog
/// (<see cref="OpenFolderDialog"/>).
/// </summary>
public sealed class Win32FolderPicker : IFolderPicker
{
    /// <inheritdoc />
    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Open folder",
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
