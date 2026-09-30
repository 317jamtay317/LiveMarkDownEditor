using System.IO;
using System.Windows.Input;
using Domain;
using UI.Core;

namespace UI.ViewModels;

/// <summary>
/// New File and New Folder: the Folder Panel actions that create an empty Markdown Document or an empty
/// folder on disk, in the Save Folder, as VS Code's Explorer does. An Entry Name that cannot be used is
/// explained and creates nothing, nothing is ever overwritten, the Folder Tree shows the new entry, and a
/// new File opens in a Tab (INV-085, INV-086). New Document starts New File here through
/// <see cref="RequestNewFile"/> (INV-087).
/// </summary>
public sealed partial class FolderWorkspaceViewModel
{
    private object? _newFileRequest;

    /// <summary>
    /// Moves keyboard focus to the editor — the Workspace wires this to its <c>EditorFocusRequest</c>, so
    /// a new File opens ready to type into, as VS Code's does (INV-086). Left null in isolation.
    /// </summary>
    public Action? FocusEditor { get; set; }

    /// <summary>
    /// New Folder: creates the requested Folder in the Save Folder. Available only while a Folder
    /// Workspace is open (INV-085). Parameter: a <see cref="NewEntryRequest"/>.
    /// </summary>
    public ICommand NewFolderCommand { get; }

    /// <summary>
    /// New File: creates the requested Markdown Document in the Save Folder and opens it. Available only
    /// while a Folder Workspace is open (INV-086). Parameter: a <see cref="NewEntryRequest"/>.
    /// </summary>
    public ICommand NewFileCommand { get; }

    /// <summary>
    /// A request, still to be taken up, for the Folder Panel to start New File — to open its name editor
    /// in the Save Folder — or <see langword="null"/> when there is none. New Document sets it through
    /// <see cref="RequestNewFile"/>; the panel binds it two ways, starts New File when a request arrives,
    /// and hands it back as <see langword="null"/> (INV-087). Each request is a new object, so asking twice
    /// is two requests.
    /// </summary>
    public object? NewFileRequest
    {
        get => _newFileRequest;
        set => Set(ref _newFileRequest, value);
    }

    /// <summary>
    /// Asks the Folder Panel to start New File in the Save Folder, showing the panel first — New Document
    /// with a Folder Entry selected (INV-087).
    /// </summary>
    public void RequestNewFile()
    {
        IsFolderPanelVisible = true;
        NewFileRequest = new object();
    }

    /// <summary>
    /// New Folder (INV-085). The Entry Name is tidied and checked by the Folder Tree's own rule, in the
    /// Save Folder for <paramref name="selected"/>: a refused one is explained through the
    /// <see cref="INewEntryNotice"/> and creates nothing. Otherwise the folder, and any on the way, is
    /// created through the <see cref="Application.IEntryCreator"/>, never overwriting anything, and the
    /// Folder Tree is refreshed so it shows the new, empty Folder. A creation the disk will not carry out
    /// is explained and changes nothing. With no Folder Workspace open, nothing happens.
    /// </summary>
    /// <param name="selected">The Selected Folder Entry when New Folder began, or <see langword="null"/>.</param>
    /// <param name="entryName">The Entry Name the user typed.</param>
    public Task NewFolderAsync(FolderEntry? selected, string? entryName) =>
        Folder is { } folder ? CreateAsync("New folder", folder.NewFolder(selected, entryName)) : Task.CompletedTask;

    /// <summary>
    /// New File (INV-086): exactly New Folder's steps for the Folder Tree's New File rule, after which the
    /// new file opens in a Tab through <see cref="OpenFile"/> — once the Folder Tree has been refreshed to
    /// hold it, so it is highlighted as the Active Session. An Entry Name ending in a separator creates a
    /// Folder instead, and opens nothing.
    /// </summary>
    /// <param name="selected">The Selected Folder Entry when New File began, or <see langword="null"/>.</param>
    /// <param name="entryName">The Entry Name the user typed.</param>
    public Task NewFileAsync(FolderEntry? selected, string? entryName) =>
        Folder is { } folder ? CreateAsync("New file", folder.NewFile(selected, entryName)) : Task.CompletedTask;

    private async Task CreateAsync(string heading, EntryCreation creation)
    {
        if (Folder is not { } folder)
        {
            return;
        }

        if (creation.Created is not { } created)
        {
            _newEntryNotice.Explain(heading, ReasonFor(creation));
            return;
        }

        var path = folder.AbsolutePathOf(created);
        var isFile = created.Kind == FolderEntryKind.File;
        try
        {
            await (isFile ? _entryCreator.CreateFileAsync(path) : _entryCreator.CreateFolderAsync(path)).ConfigureAwait(true);
        }
        catch (IOException exception)
        {
            _newEntryNotice.Explain(heading, $"“{creation.Name}” could not be created. {exception.Message}");
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
        if (!isFile)
        {
            return;
        }

        try
        {
            await (OpenFile?.Invoke(path) ?? Task.CompletedTask).ConfigureAwait(true);
        }
        catch (IOException)
        {
            // A new File that has gone again before it could open opens nothing, as activating one does.
            return;
        }

        FocusEditor?.Invoke();
    }

    private bool CanCreate(NewEntryRequest? request) => request is not null && Folder is not null;

    private Task NewFolderAsync(NewEntryRequest? request) => NewFolderAsync(request?.Selected, request?.EntryName);

    private Task NewFileAsync(NewEntryRequest? request) => NewFileAsync(request?.Selected, request?.EntryName);

    private static string ReasonFor(EntryCreation creation) => creation.Refusal switch
    {
        NameRefusal.Blank => "A file or folder name must be provided.",
        NameRefusal.StartsWithSeparator => "A file or folder name can’t start with a separator (/ or \\).",
        NameRefusal.InvalidCharacter =>
            "A file or folder name can’t contain any of these characters: : * ? \" < > |",
        NameRefusal.ReservedName =>
            $"“{creation.Name}” uses a name Windows keeps for itself, so no file or folder can have it.",
        NameRefusal.NameTaken => $"There is already a file or folder named “{creation.Name}” here.",
        _ => $"“{creation.Name}” could not be created.",
    };
}
