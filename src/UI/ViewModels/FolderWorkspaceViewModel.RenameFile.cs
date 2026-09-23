using System.IO;
using System.Windows.Input;
using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// Rename File: the Folder Panel action that gives a File a New Name on disk, in the folder it already
/// sits in. A New Name that cannot be used is explained and changes nothing, a file is never
/// overwritten, and an open File's Tab follows it to its New Name (INV-082).
/// </summary>
public sealed partial class FolderWorkspaceViewModel
{
    /// <summary>
    /// Says whether a Tab holds the Watched File at an absolute path. The Workspace wires this to its
    /// open Tabs, so a New Name another Tab already holds is refused rather than opening one file in
    /// two Tabs (INV-009). Left null in isolation, no Tab holds anything.
    /// </summary>
    public Func<string, bool>? IsFileOpen { get; set; }

    /// <summary>
    /// Reports a File that Rename File has renamed, by the absolute path it had and the one it has now.
    /// The Workspace wires this to point the File's Tab, and its Recent Files entry, at the new path
    /// (INV-082).
    /// </summary>
    public Func<string, string, Task>? FileRenamed { get; set; }

    /// <summary>
    /// Rename File: gives the requested File the typed New Name. Available only for a File the Folder
    /// Tree holds, never a Folder (INV-082). Parameter: a <see cref="RenameFileRequest"/>.
    /// </summary>
    public ICommand RenameEntryCommand { get; }

    /// <summary>
    /// Rename File (INV-082). The New Name is tidied and checked by the Folder Tree's own rule: a name
    /// that is exactly the File's current one does nothing, and a refused one is explained through the
    /// <see cref="IRenameFileNotice"/> and changes nothing. So is a New Name another Tab holds, which
    /// would open one file in two Tabs (INV-009). Otherwise the file is renamed on disk, never
    /// overwriting anything; <see cref="FileRenamed"/> reports it so the File's Tab and Recent Files
    /// entry follow; and the Folder Tree is refreshed. A rename the disk will not carry out is
    /// explained and changes nothing, and a File already gone from disk is explained and drops out of
    /// the tree. A Folder, or an entry the tree no longer holds, is ignored.
    /// </summary>
    /// <param name="entry">The File the user is renaming.</param>
    /// <param name="newName">The New Name the user typed.</param>
    public async Task RenameAsync(FolderEntry? entry, string? newName)
    {
        if (Folder is not { } folder || !folder.CanRename(entry))
        {
            return;
        }

        var rename = folder.Rename(entry, newName);
        if (rename.IsUnchanged)
        {
            return;
        }

        if (rename.Renamed is null)
        {
            _renameNotice.Explain(ReasonFor(rename));
            return;
        }

        var path = folder.AbsolutePathOf(entry);
        var newPath = folder.AbsolutePathOf(rename.Renamed);
        if (!string.Equals(path, newPath, StringComparison.OrdinalIgnoreCase) && IsFileOpen?.Invoke(newPath) == true)
        {
            _renameNotice.Explain($"“{rename.NewName}” is open in another tab. Close that tab first.");
            return;
        }

        try
        {
            await _renamer.RenameAsync(path, newPath).ConfigureAwait(true);
        }
        catch (FileNotFoundException)
        {
            _renameNotice.Explain($"“{entry.Name}” could not be renamed because it is no longer on disk.");
            await RefreshAsync().ConfigureAwait(true);
            return;
        }
        catch (IOException exception)
        {
            _renameNotice.Explain($"“{entry.Name}” could not be renamed. {exception.Message}");
            return;
        }

        // The Tab follows first, so it is watching the new path before anything else looks at the disk.
        await (FileRenamed?.Invoke(path, newPath) ?? Task.CompletedTask).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private bool CanRename(RenameFileRequest? request) => Folder?.CanRename(request?.File) == true;

    private Task RenameAsync(RenameFileRequest? request) => RenameAsync(request?.File, request?.NewName);

    private static string ReasonFor(FileRename rename) => rename.Refusal switch
    {
        RenameRefusal.Blank => "A file name can’t be blank.",
        RenameRefusal.InvalidCharacter =>
            "A file name can’t contain any of these characters: \\ / : * ? \" < > |",
        RenameRefusal.ReservedName => $"“{rename.NewName}” is a name Windows keeps for itself, so no file can have it.",
        RenameRefusal.NameTaken => $"There is already a file or folder named “{rename.NewName}” here.",
        _ => $"“{rename.File.Name}” could not be renamed.",
    };
}
