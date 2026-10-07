namespace UI.ViewModels;

/// <summary>
/// Open a Startup Document (INV-020, INV-089): the file the editor was launched with — or that a later
/// launch forwarded — opens in a Tab, and the Folder Panel is brought to the folder it lives in.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>
    /// Opens the Startup Document at <paramref name="path"/> through <see cref="OpenPathAsync"/>
    /// (activating its existing Tab if it is already open, INV-009), then brings the Folder Panel to the
    /// folder it lives in, keeping an open Folder Workspace that already holds it, and selects the Side
    /// Dock's Folder tab over the Outline (INV-089). A document that fails to open changes no Folder
    /// Workspace.
    /// </summary>
    /// <param name="path">The absolute path of the Startup Document.</param>
    public async Task OpenStartupDocumentAsync(string path)
    {
        await OpenPathAsync(path).ConfigureAwait(true);

        // Bring the Folder tab in front first (INV-046): the Folder Panel can only reveal and highlight
        // a row it has laid out, and a tree built behind the Outline has none. A Folder Panel not yet
        // shown selects its own tab as opening the folder shows it.
        SideDock.SelectFolderTabCommand.Execute(null);
        await Folder.OpenFolderHoldingAsync(path).ConfigureAwait(true);
    }
}
