using System.IO;
using Domain;
using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for Rename File on the <see cref="FolderWorkspaceViewModel"/>: a File gets a usable New Name
/// in its own folder, a New Name that cannot be used is explained and changes nothing, and the Folder
/// Tree follows the rename (INV-082).
/// </summary>
public sealed class FolderWorkspaceViewModelRenameFileTests
{
    private const string Root = @"C:\vault";

    private readonly StubFolderPicker _picker = new() { FolderResult = Root };
    private readonly FakeMarkdownFolderReader _reader = new();
    private readonly FakeFolderWatcher _watcher = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly FakeFileRenamer _renamer = new();
    private readonly StubRenameFileNotice _notice = new();
    private readonly List<string> _events = [];

    private static string NotePath => Path.GetFullPath(@"C:\vault\sub\note.md");

    private static string IdeasPath => Path.GetFullPath(@"C:\vault\sub\ideas.md");

    [Fact]
    public async Task Rename_RenamesTheFilesCanonicalPath_ToItsNewName_INV082()
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.RenameAsync(Entry(folder, "sub/note.md"), "ideas");

        _renamer.Renamed.ShouldBe([(NotePath, IdeasPath)]);
        _notice.Reasons.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rename_ReportsTheRename_SoTheTabAndRecentFilesCanFollow_INV082()
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.RenameAsync(Entry(folder, "sub/note.md"), "ideas.md");

        _events.ShouldBe([$"renamed {NotePath} to {IdeasPath}"]);
    }

    [Fact]
    public async Task Rename_WhenDone_RefreshesTheTree_SoTheFileHasItsNewName_INV082()
    {
        var folder = await OpenAsync("sub/note.md", "top.md");
        _renamer.OnRename = (_, _) => _reader.Result = ["sub/ideas.md", "top.md"];

        await folder.RenameAsync(Entry(folder, "sub/note.md"), "ideas");

        Flatten(folder.Folder!).ShouldBe(["Folder sub", "File sub/ideas.md", "File top.md"]);
    }

    [Theory]
    [InlineData("", "blank")]
    [InlineData("what?", "character")]
    [InlineData("CON", "CON.md")]
    [InlineData("other", "other.md")]
    public async Task Rename_ToANameThatIsRefused_ExplainsWhy_AndChangesNothing_INV082(string typed, string explained)
    {
        var folder = await OpenAsync("sub/note.md", "sub/other.md");

        await folder.RenameAsync(Entry(folder, "sub/note.md"), typed);

        _notice.Reasons.Count.ShouldBe(1);
        _notice.Reasons[0].ShouldContain(explained, Case.Insensitive);
        _renamer.Renamed.ShouldBeEmpty();
        _events.ShouldBeEmpty();
        Flatten(folder.Folder!).ShouldBe(["Folder sub", "File sub/note.md", "File sub/other.md"]);
    }

    [Theory]
    [InlineData("note.md")]
    [InlineData(" note ")]
    public async Task Rename_ToTheFilesOwnName_DoesNothing_INV082(string typed)
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.RenameAsync(Entry(folder, "sub/note.md"), typed);

        _renamer.Renamed.ShouldBeEmpty();
        _notice.Reasons.ShouldBeEmpty();
        _events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rename_ChangingOnlyTheCapitals_RenamesTheFile_INV082()
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.RenameAsync(Entry(folder, "sub/note.md"), "Note");

        _renamer.Renamed.ShouldBe([(NotePath, Path.GetFullPath(@"C:\vault\sub\Note.md"))]);
    }

    [Fact]
    public async Task Rename_OnAFolder_ExplainsNothing_AndRenamesNothing_INV082()
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.RenameAsync(Entry(folder, "sub"), "renamed");

        _renamer.Renamed.ShouldBeEmpty();
        _notice.Reasons.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rename_ToANameAnotherTabHolds_IsRefused_INV082()
    {
        var folder = await OpenAsync("sub/note.md");
        folder.IsFileOpen = path => string.Equals(path, IdeasPath, StringComparison.OrdinalIgnoreCase);

        await folder.RenameAsync(Entry(folder, "sub/note.md"), "ideas");

        // One file open in two Tabs would break INV-009, so the rename is refused instead.
        _renamer.Renamed.ShouldBeEmpty();
        _notice.Reasons.Single().ShouldContain("ideas.md");
    }

    [Fact]
    public async Task Rename_ChangingOnlyTheCapitals_OfAFileOpenInATab_IsNotRefused_INV082()
    {
        var folder = await OpenAsync("sub/note.md");
        folder.IsFileOpen = path => string.Equals(path, NotePath, StringComparison.OrdinalIgnoreCase);

        await folder.RenameAsync(Entry(folder, "sub/note.md"), "Note.md");

        // The Tab holding it is the File's own, which follows it to the new name.
        _renamer.Renamed.Count.ShouldBe(1);
        _notice.Reasons.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rename_WhenTheDiskRefuses_ExplainsWhy_AndReportsNothing_INV082()
    {
        var folder = await OpenAsync("sub/note.md");
        _renamer.Failure = new IOException("Access is denied.");

        await Should.NotThrowAsync(() => folder.RenameAsync(Entry(folder, "sub/note.md"), "ideas"));

        _notice.Reasons.Single().ShouldContain("note.md");
        _notice.Reasons.Single().ShouldContain("Access is denied.");
        _events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rename_WhenTheFileHasAlreadyGone_ExplainsIt_AndTheTreeLetsItGo_INV082()
    {
        var folder = await OpenAsync("sub/note.md", "top.md");
        _reader.Result = ["top.md"];
        _renamer.Failure = new FileNotFoundException("gone", NotePath);

        await Should.NotThrowAsync(() => folder.RenameAsync(Entry(folder, "sub/note.md"), "ideas"));

        _notice.Reasons.Single().ShouldContain("note.md");
        _events.ShouldBeEmpty();
        Flatten(folder.Folder!).ShouldBe(["File top.md"]);
    }

    [Fact]
    public async Task RenameEntryCommand_RenamesTheRequestedFile_ToTheTypedName_INV082()
    {
        var folder = await OpenAsync("sub/note.md");

        folder.RenameEntryCommand.Execute(new RenameFileRequest(Entry(folder, "sub/note.md"), "ideas"));

        _renamer.Renamed.ShouldBe([(NotePath, IdeasPath)]);
    }

    [Fact]
    public async Task RenameEntryCommand_IsAvailableForAFile_ButNotForAFolder_INV082()
    {
        var folder = await OpenAsync("sub/note.md");

        folder.RenameEntryCommand.CanExecute(new RenameFileRequest(Entry(folder, "sub/note.md"), "x")).ShouldBeTrue();
        folder.RenameEntryCommand.CanExecute(new RenameFileRequest(Entry(folder, "sub"), "x")).ShouldBeFalse();
        folder.RenameEntryCommand.CanExecute(null).ShouldBeFalse();
    }

    private async Task<FolderWorkspaceViewModel> OpenAsync(params string[] relativePaths)
    {
        _reader.Result = relativePaths;
        var folder = new FolderWorkspaceViewModel(
            _picker, _reader, _watcher, _dispatcher, new StubDeleteFilePrompt(), new FakeFileDeleter(), _renamer, _notice)
        {
            FileRenamed = (path, newPath) =>
            {
                _events.Add($"renamed {path} to {newPath}");
                return Task.CompletedTask;
            },
        };

        await folder.OpenFolderAsync();

        // Only what Rename File itself does is of interest; opening the folder is not.
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
