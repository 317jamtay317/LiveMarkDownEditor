using System.IO;
using Domain;
using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for Delete File on the <see cref="FolderWorkspaceViewModel"/>: a File is deleted only when the
/// user confirms, only after its Tab has been closed, and the Folder Tree lets it go (INV-081).
/// </summary>
public sealed class FolderWorkspaceViewModelDeleteFileTests
{
    private const string Root = @"C:\vault";

    private readonly StubFolderPicker _picker = new() { FolderResult = Root };
    private readonly FakeMarkdownFolderReader _reader = new();
    private readonly FakeFolderWatcher _watcher = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly StubDeleteFilePrompt _prompt = new();
    private readonly FakeFileDeleter _deleter = new();
    private readonly List<string> _events = [];

    private static string NotePath => Path.GetFullPath(@"C:\vault\sub\note.md");

    [Fact]
    public async Task Delete_AsksAboutTheFile_ByName_INV081()
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.DeleteAsync(Entry(folder, "sub/note.md"));

        _prompt.FileNames.ShouldBe(["note.md"]);
    }

    [Fact]
    public async Task Delete_WhenTheUserSaysNo_ChangesNothing_INV081()
    {
        var folder = await OpenAsync("sub/note.md", "top.md");
        _prompt.Answer = false;

        await folder.DeleteAsync(Entry(folder, "sub/note.md"));

        _deleter.Deleted.ShouldBeEmpty();
        _events.ShouldBeEmpty(); // no Tab was closed and no deletion was reported
        Flatten(folder.Folder!).ShouldBe(["Folder sub", "File sub/note.md", "File top.md"]);
    }

    [Fact]
    public async Task Delete_WhenTheUserSaysYes_DeletesTheFilesCanonicalPath_INV081()
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.DeleteAsync(Entry(folder, "sub/note.md"));

        _deleter.Deleted.ShouldBe([NotePath]);
    }

    [Fact]
    public async Task Delete_WhenDone_RefreshesTheTree_SoTheFileIsGone_INV081()
    {
        var folder = await OpenAsync("sub/note.md", "top.md");
        _deleter.OnDelete = _ => _reader.Result = ["top.md"];

        await folder.DeleteAsync(Entry(folder, "sub/note.md"));

        Flatten(folder.Folder!).ShouldBe(["File top.md"]);
    }

    [Fact]
    public async Task Delete_ClosesTheFilesTab_BeforeDeletingIt_INV081()
    {
        var folder = await OpenAsync("sub/note.md");
        _deleter.OnDelete = path => _events.Add($"deleted {path}");

        await folder.DeleteAsync(Entry(folder, "sub/note.md"));

        _events.ShouldBe([$"closed {NotePath}", $"deleted {NotePath}", $"reported {NotePath}"]);
    }

    [Fact]
    public async Task Delete_WhenClosingTheTabIsCancelled_DeletesNothing_INV081()
    {
        var folder = await OpenAsync("sub/note.md");
        folder.CloseFile = _ => Task.FromResult(false);

        await folder.DeleteAsync(Entry(folder, "sub/note.md"));

        // Cancel on the unsaved-edits question keeps the Tab, and so keeps the file (INV-010).
        _deleter.Deleted.ShouldBeEmpty();
        _events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_OnAFolder_AsksNothing_AndDeletesNothing_INV081()
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.DeleteAsync(Entry(folder, "sub"));

        _prompt.FileNames.ShouldBeEmpty();
        _deleter.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_WhenTheFileHasAlreadyGone_CountsAsDeleted_INV081()
    {
        var folder = await OpenAsync("sub/note.md", "top.md");
        _reader.Result = ["top.md"];
        _deleter.Failure = new FileNotFoundException("gone", NotePath);

        await Should.NotThrowAsync(() => folder.DeleteAsync(Entry(folder, "sub/note.md")));

        Flatten(folder.Folder!).ShouldBe(["File top.md"]);
        _events.ShouldContain($"reported {NotePath}");
    }

    [Fact]
    public async Task Delete_WhenWindowsDeclinesToEraseIt_KeepsTheFile_INV081()
    {
        var folder = await OpenAsync("sub/note.md");
        _deleter.Failure = new OperationCanceledException("declined");

        await Should.NotThrowAsync(() => folder.DeleteAsync(Entry(folder, "sub/note.md")));

        _events.ShouldNotContain($"reported {NotePath}");
        Flatten(folder.Folder!).ShouldBe(["Folder sub", "File sub/note.md"]);
    }

    [Fact]
    public async Task DeleteEntryCommand_IsAvailableForAFile_ButNotForAFolder_INV081()
    {
        var folder = await OpenAsync("sub/note.md");

        folder.DeleteEntryCommand.CanExecute(Entry(folder, "sub/note.md")).ShouldBeTrue();
        folder.DeleteEntryCommand.CanExecute(Entry(folder, "sub")).ShouldBeFalse();
        folder.DeleteEntryCommand.CanExecute(null).ShouldBeFalse();
    }

    private async Task<FolderWorkspaceViewModel> OpenAsync(params string[] relativePaths)
    {
        _reader.Result = relativePaths;
        var folder = new FolderWorkspaceViewModel(
            _picker, _reader, _watcher, _dispatcher, _prompt, _deleter, new FakeFileRenamer(), new StubRenameFileNotice())
        {
            CloseFile = path =>
            {
                _events.Add($"closed {path}");
                return Task.FromResult(true);
            },
            FileDeleted = path =>
            {
                _events.Add($"reported {path}");
                return Task.CompletedTask;
            },
        };

        await folder.OpenFolderAsync();

        // Only what Delete File itself does is of interest; opening the folder is not.
        _events.Clear();
        return folder;
    }

    private static FolderEntry Entry(FolderWorkspaceViewModel folder, string relativePath)
    {
        FolderEntry? Find(IReadOnlyList<FolderEntry> entries) =>
            entries.FirstOrDefault(entry => entry.RelativePath == relativePath)
            ?? entries.Select(entry => Find(entry.Children)).FirstOrDefault(found => found is not null);

        return Find(folder.Folder!.Entries)
               ?? throw new InvalidOperationException($"No entry '{relativePath}' in the tree.");
    }

    private static IReadOnlyList<string> Flatten(FolderWorkspace workspace)
    {
        var lines = new List<string>();

        void Walk(IReadOnlyList<FolderEntry> entries)
        {
            foreach (var entry in entries)
            {
                lines.Add($"{entry.Kind} {entry.RelativePath}");
                Walk(entry.Children);
            }
        }

        Walk(workspace.Entries);
        return lines;
    }
}
