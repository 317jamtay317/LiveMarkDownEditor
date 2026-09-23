using System.IO;
using System.Windows.Input;
using Domain;

namespace UI.ViewModels;

/// <summary>
/// Delete File: the one Folder Panel action that is not browsing. It removes a File from disk, but
/// only once the user has confirmed it, only after the File's Tab has closed through Close Tab, and
/// into the Recycle Bin rather than for good (INV-081).
/// </summary>
public sealed partial class FolderWorkspaceViewModel
{
    /// <summary>
    /// Closes the Tab holding a File that is about to be deleted, if one does. The Workspace wires this
    /// to its <c>CloseFileAsync</c>, which goes through Close Tab, so unsaved edits are asked about
    /// (INV-010). It returns <see langword="false"/> when the user keeps the Tab, and that cancels the
    /// delete. Left null in isolation, there is no Tab to close.
    /// </summary>
    public Func<string, Task<bool>>? CloseFile { get; set; }

    /// <summary>
    /// Reports a File that Delete File has removed, by its absolute path. The Workspace wires this to
    /// drop the path from the Recent Files (INV-081).
    /// </summary>
    public Func<string, Task>? FileDeleted { get; set; }

    /// <summary>
    /// Delete File: asks the user to confirm, then deletes the File. Available only for a File the
    /// Folder Tree holds, never a Folder (INV-081). Parameter: the entry.
    /// </summary>
    public ICommand DeleteEntryCommand { get; }

    /// <summary>
    /// Delete File (INV-081). The user is asked whether they are sure, naming the File, and No changes
    /// nothing. On Yes, the File's Tab is closed first through <see cref="CloseFile"/>, and choosing to
    /// keep that Tab cancels the delete. The file then goes to the Recycle Bin, the Folder Tree is
    /// refreshed so it no longer shows the File, and <see cref="FileDeleted"/> reports it. A File that
    /// had already gone counts as deleted. When Windows cannot recycle a file, it warns before erasing
    /// it for good, and declining that warning keeps the file. A Folder, or an entry the tree no longer
    /// holds, is ignored.
    /// </summary>
    /// <param name="entry">The Folder Entry the user wants to delete.</param>
    public async Task DeleteAsync(FolderEntry? entry)
    {
        if (Folder is not { } folder || !folder.CanDelete(entry) || !_deletePrompt.Confirm(entry.Name))
        {
            return;
        }

        var path = folder.AbsolutePathOf(entry);
        if (CloseFile is not null && !await CloseFile(path).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            await _deleter.DeleteAsync(path).ConfigureAwait(true);
        }
        catch (FileNotFoundException)
        {
            // Already gone from disk: that is what the user asked for, so it counts as deleted.
        }
        catch (OperationCanceledException)
        {
            // Windows could not recycle it and the user declined to erase it for good: the file stays.
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        await (FileDeleted?.Invoke(path) ?? Task.CompletedTask).ConfigureAwait(true);
    }

    private bool CanDelete(FolderEntry? entry) => Folder?.CanDelete(entry) == true;
}
