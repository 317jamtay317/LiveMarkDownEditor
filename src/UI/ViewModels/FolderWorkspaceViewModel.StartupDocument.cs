using System.IO;

namespace UI.ViewModels;

/// <summary>
/// Open the Folder Workspace a Startup Document lives in (INV-089): the Folder Panel is brought to the
/// file the user launched the editor with, keeping an open root that already holds it and otherwise
/// opening the file's own folder.
/// </summary>
public sealed partial class FolderWorkspaceViewModel
{
    /// <summary>
    /// Brings the Folder Panel to where <paramref name="filePath"/> lives (INV-089). An open Folder Tree
    /// that already holds the file is kept; otherwise the file's folder is opened as the Folder
    /// Workspace and the new root persisted. Either way the Folder Panel is shown. A folder that cannot
    /// be read leaves the open Folder Workspace as it was.
    /// </summary>
    /// <param name="filePath">The absolute path of the Startup Document that was just opened.</param>
    public async Task OpenFolderHoldingAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (Folder?.FileFor(filePath) is not null)
        {
            IsFolderPanelVisible = true;
            return;
        }

        var root = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (root is null)
        {
            return;
        }

        try
        {
            await LoadRootAsync(root).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // A folder that cannot be read leaves the open Folder Workspace as it was.
            return;
        }

        await (PersistState?.Invoke() ?? Task.CompletedTask).ConfigureAwait(true);
    }
}
