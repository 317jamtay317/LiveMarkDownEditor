using System.IO;
using Domain;
using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for how the <see cref="FolderWorkspaceViewModel"/> Follows the Active Session (INV-083): the
/// File holding the Active Session's Watched File becomes the Selected Folder Entry, anything else
/// leaves nothing selected, and a root opened afterwards follows the same Watched File. Following is
/// browsing — it opens no document (INV-043).
/// </summary>
public sealed class FolderWorkspaceViewModelFollowActiveSessionTests
{
    private const string Root = @"C:\vault";

    private readonly StubFolderPicker _picker = new() { FolderResult = Root };
    private readonly FakeMarkdownFolderReader _reader = new() { Result = ["a.md", "sub/b.md"] };
    private readonly FakeFolderWatcher _watcher = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly List<string> _opened = [];

    [Fact]
    public async Task FollowActiveSession_AWatchedFileInTheFolderTree_HighlightsThatFile_INV083()
    {
        var folder = await OpenFolderAsync();

        folder.FollowActiveSession(PathOf("sub/b.md"));

        folder.SelectedEntry.ShouldNotBeNull();
        folder.SelectedEntry!.Kind.ShouldBe(FolderEntryKind.File);
        folder.SelectedEntry.RelativePath.ShouldBe("sub/b.md");
    }

    [Fact]
    public async Task FollowActiveSession_AWatchedFileInTheFolderTree_OpensNothing_INV083()
    {
        var folder = await OpenFolderAsync();

        folder.FollowActiveSession(PathOf("a.md"));

        // Following highlights a row; activating a File is what opens one (INV-043).
        _opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task FollowActiveSession_AWatchedFileOutsideTheRoot_HighlightsNothing_INV083()
    {
        var folder = await OpenFolderAsync();
        folder.FollowActiveSession(PathOf("a.md"));

        folder.FollowActiveSession(@"C:\elsewhere\away.md");

        folder.SelectedEntry.ShouldBeNull();
    }

    [Fact]
    public async Task FollowActiveSession_AnUnsavedTab_HighlightsNothing_INV083()
    {
        var folder = await OpenFolderAsync();
        folder.FollowActiveSession(PathOf("a.md"));

        // An unsaved Tab has no Watched File, so no row is the document on screen.
        folder.FollowActiveSession(null);

        folder.SelectedEntry.ShouldBeNull();
    }

    [Fact]
    public void FollowActiveSession_WithNoFolderOpen_HighlightsNothing_INV083()
    {
        var folder = Create();

        folder.FollowActiveSession(PathOf("a.md"));

        folder.SelectedEntry.ShouldBeNull();
        folder.SaveFolder.ShouldBeNull();
    }

    [Fact]
    public async Task OpenFolder_HighlightsTheActiveSessionsFile_INV083()
    {
        var folder = Create();
        folder.FollowActiveSession(PathOf("sub/b.md"));

        // Opening the folder a document already lives in shows where it lives at once.
        await folder.OpenFolderAsync();

        folder.SelectedEntry?.RelativePath.ShouldBe("sub/b.md");
    }

    [Fact]
    public async Task Restore_HighlightsTheActiveSessionsFile_INV083()
    {
        var folder = Create();
        folder.FollowActiveSession(PathOf("a.md"));

        await folder.RestoreAsync(Root);

        folder.SelectedEntry?.RelativePath.ShouldBe("a.md");
    }

    [Fact]
    public async Task OpenFolder_WhenTheActiveSessionIsElsewhere_HighlightsNothing_INV083()
    {
        var folder = Create();
        folder.FollowActiveSession(@"C:\elsewhere\away.md");

        await folder.OpenFolderAsync();

        folder.SelectedEntry.ShouldBeNull();
    }

    [Fact]
    public async Task FollowActiveSession_NamesTheFilesFolderAsTheSaveFolder_INV080()
    {
        var folder = await OpenFolderAsync();

        folder.FollowActiveSession(PathOf("sub/b.md"));

        // A new document lands beside the one being edited, as a Selected File always names its own
        // folder (INV-080).
        folder.SaveFolder.ShouldBe(Path.GetFullPath(@"C:\vault\sub"));
    }

    private static string PathOf(string relativePath) =>
        Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private async Task<FolderWorkspaceViewModel> OpenFolderAsync()
    {
        var folder = Create();
        await folder.OpenFolderAsync();
        return folder;
    }

    private FolderWorkspaceViewModel Create() =>
        new(_picker, _reader, _watcher, _dispatcher, new StubDeleteFilePrompt(), new FakeFileDeleter(), new FakeFileRenamer(), new StubRenameFileNotice())
        {
            OpenFile = path =>
            {
                _opened.Add(path);
                return Task.CompletedTask;
            },
        };
}
