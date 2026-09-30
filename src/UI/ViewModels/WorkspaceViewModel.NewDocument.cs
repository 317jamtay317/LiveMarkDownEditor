using System.IO;
using System.Windows.Input;

namespace UI.ViewModels;

/// <summary>
/// New Document, Save and Save As. A new Markdown Document always has its file from the start: New File
/// in place in the Folder Panel while a Folder Entry is selected, otherwise the save prompt asks first
/// (INV-087). Save As saves the Active Session to a file the user picks (INV-088). Whenever a save gives
/// a Tab a different file, the Folder Panel Follows it there (INV-083).
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private const string UntitledFileName = "Untitled.md";

    private object? _editorFocusRequest;

    /// <summary>Save As (Ctrl+Shift+S): saves the Active Session to a file the user picks (INV-088).</summary>
    public ICommand SaveAsCommand { get; }

    /// <summary>
    /// A request for the editor to take keyboard focus, or <see langword="null"/> before the first. The
    /// editor binds it through <see cref="Controls.FocusOnRequest"/>; each new object asks again. It is
    /// asked for when New Document or New File has just opened its file, so the user can type at once
    /// (INV-086, INV-087).
    /// </summary>
    public object? EditorFocusRequest
    {
        get => _editorFocusRequest;
        private set => Set(ref _editorFocusRequest, value);
    }

    /// <summary>Asks the editor to take keyboard focus (see <see cref="EditorFocusRequest"/>).</summary>
    public void RequestEditorFocus() => EditorFocusRequest = new object();

    /// <summary>
    /// New Document (INV-087). With a Folder Workspace open and a Folder Entry selected, it is New File
    /// in the Save Folder: the Folder Panel is shown on its Side Dock tab and asked to open its name editor
    /// there. Otherwise the save prompt opens at once, in the Save Folder (the open root, or none without a
    /// Folder Workspace), and the empty file created where the user picks opens in a new Tab. Cancelling
    /// creates nothing. A picked file another Tab already holds brings that Tab forward instead, and is not
    /// overwritten (INV-009).
    /// </summary>
    public async Task NewDocumentAsync()
    {
        if (Folder.HasFolder && Folder.SelectedEntry is not null)
        {
            Folder.RequestNewFile();
            SideDock.SelectFolderTabCommand.Execute(null);
            return;
        }

        var path = _filePicker.PickSave(suggestedFileName: UntitledFileName, folder: Folder.SaveFolder);
        if (path is null || ActivateHolderOf(path, except: null))
        {
            return;
        }

        var session = _createSession();
        await session.SaveAsync(path).ConfigureAwait(true);

        // The Folder Tree learns of the new file before its Tab becomes the Active Session, so Following
        // it highlights the file rather than finding nothing (INV-083).
        await Folder.RefreshAsync().ConfigureAwait(true);
        AddTab(session);
        ActiveSession = session;
        RequestEditorFocus();
        RememberRecent(path);
        await PersistStateAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Save As (INV-088). The prompt opens in the Active Session's own folder with its file name, or in the
    /// Save Folder with <c>Untitled.md</c> for a Tab without a Watched File. The text is saved to the picked
    /// file, which the Tab then holds and watches; its old file is left as it was. Cancelling saves
    /// nothing, and a picked file another Tab holds brings that Tab forward and saves nothing (INV-009).
    /// </summary>
    public async Task SaveAsActiveAsync()
    {
        if (ActiveSession is not { } session)
        {
            return;
        }

        var path = session.FilePath is { } current
            ? _filePicker.PickSave(Path.GetFileName(current), Path.GetDirectoryName(current))
            : _filePicker.PickSave(UntitledFileName, Folder.SaveFolder);
        if (path is null || ActivateHolderOf(path, except: session))
        {
            return;
        }

        await session.SaveAsync(path).ConfigureAwait(true);
        await FollowSavedAsync(session).ConfigureAwait(true);
        RememberRecent(path);
        await PersistStateAsync().ConfigureAwait(true);
    }

    private async Task<bool> TrySaveAsync(EditorSessionViewModel session)
    {
        // A Tab that has no Watched File yet is saved wherever the user is browsing: the open Folder
        // Workspace's Save Folder (INV-080). One that already has a file is saved where it lives.
        var isNewFile = session.FilePath is null;
        var path = session.FilePath
                   ?? _filePicker.PickSave(suggestedFileName: UntitledFileName, folder: Folder.SaveFolder);
        if (path is null)
        {
            return false;
        }

        await session.SaveAsync(path).ConfigureAwait(true);
        if (isNewFile)
        {
            await FollowSavedAsync(session).ConfigureAwait(true);
        }

        RememberRecent(path);
        await PersistStateAsync().ConfigureAwait(true);
        return true;
    }

    // A save that gave the Tab a different file changes FilePath without changing the Active Session, so
    // the setter that Follows it never runs. Refresh the Folder Tree to hold the new file, then Follow the
    // file explicitly — highlighting it in the tree, or nothing when it lies outside the open root — so
    // the highlight, and the Save Folder the next New Document uses, name where it now lives (INV-083).
    private async Task FollowSavedAsync(EditorSessionViewModel session)
    {
        if (session != ActiveSession)
        {
            return;
        }

        await Folder.RefreshAsync().ConfigureAwait(true);
        Folder.FollowActiveSession(session.FilePath);
    }

    // Brings forward the Tab — other than the given one — that already holds the file at the path, so one
    // file is never open in two Tabs (INV-009). Paths compare without regard to capitals.
    private bool ActivateHolderOf(string path, EditorSessionViewModel? except)
    {
        var holder = Sessions.FirstOrDefault(session =>
            session != except
            && session.FilePath is not null
            && string.Equals(session.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (holder is null)
        {
            return false;
        }

        ActiveSession = holder;
        return true;
    }
}
