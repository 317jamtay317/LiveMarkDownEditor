using System.IO;
using Domain;
using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for New Folder on the <see cref="FolderWorkspaceViewModel"/>: an empty folder is created in the
/// Save Folder under a usable Entry Name, an Entry Name that cannot be used is explained and creates
/// nothing, and the Folder Tree shows the new Folder (INV-085).
/// </summary>
public sealed class FolderWorkspaceViewModelNewFolderTests
{
    private const string Root = @"C:\vault";

    private readonly StubFolderPicker _picker = new() { FolderResult = Root };
    private readonly FakeMarkdownFolderReader _reader = new();
    private readonly FakeFolderWatcher _watcher = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly FakeEntryCreator _creator = new();
    private readonly StubNewEntryNotice _notice = new();

    [Fact]
    public async Task NewFolder_WithNothingSelected_CreatesTheFolderInTheRoot_INV085()
    {
        var folder = await OpenAsync("top.md");

        await folder.NewFolderAsync(null, "drafts");

        _creator.Created.ShouldBe([Path.GetFullPath(@"C:\vault\drafts")]);
        _notice.Reasons.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewFolder_InASelectedFolder_CreatesItInside_INV085()
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.NewFolderAsync(Entry(folder, "sub"), "drafts");

        _creator.Created.ShouldBe([Path.GetFullPath(@"C:\vault\sub\drafts")]);
    }

    [Fact]
    public async Task NewFolder_BesideASelectedFile_CreatesItInTheFolderHoldingIt_INV085()
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.NewFolderAsync(Entry(folder, "sub/note.md"), "  drafts. ");

        _creator.Created.ShouldBe([Path.GetFullPath(@"C:\vault\sub\drafts")]);
    }

    [Fact]
    public async Task NewFolder_WhenDone_RefreshesTheTree_SoTheEmptyFolderIsShown_INV085()
    {
        var folder = await OpenAsync("top.md");
        _creator.OnCreate = _ => _reader.Folders = ["drafts"];

        await folder.NewFolderAsync(null, "drafts");

        Flatten(folder.Folder!).ShouldBe(["Folder drafts", "File top.md"]);
    }

    [Theory]
    [InlineData("", "must be provided")]
    [InlineData("what?", "character")]
    [InlineData("CON", "CON")]
    [InlineData("sub", "“sub”")]
    [InlineData("top.md", "“top.md”")]
    public async Task NewFolder_ToAFolderNameThatIsRefused_ExplainsWhy_AndCreatesNothing_INV085(string typed, string explained)
    {
        var folder = await OpenAsync("sub/note.md", "top.md");

        await folder.NewFolderAsync(null, typed);

        _notice.Reasons.Count.ShouldBe(1);
        _notice.Reasons[0].ShouldContain(explained, Case.Insensitive);
        _creator.Created.ShouldBeEmpty();
        Flatten(folder.Folder!).ShouldBe(["Folder sub", "File sub/note.md", "File top.md"]);
    }

    [Fact]
    public async Task NewFolder_WhenTheDiskRefuses_ExplainsIt_AndCreatesNothing_INV085()
    {
        var folder = await OpenAsync("top.md");
        _creator.Failure = new IOException("A file or folder with that name already exists.");

        await Should.NotThrowAsync(() => folder.NewFolderAsync(null, "drafts"));

        _notice.Reasons.Single().ShouldContain("drafts");
        _notice.Reasons.Single().ShouldContain("already exists");
    }

    [Fact]
    public async Task NewFolder_WithNoFolderWorkspaceOpen_CreatesNothing_INV085()
    {
        var folder = Build();

        await folder.NewFolderAsync(null, "drafts");

        _creator.Created.ShouldBeEmpty();
        _notice.Reasons.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewFolderCommand_CreatesTheRequestedFolder_INV085()
    {
        var folder = await OpenAsync("sub/note.md");

        folder.NewFolderCommand.Execute(new NewEntryRequest(Entry(folder, "sub"), "drafts"));

        _creator.Created.ShouldBe([Path.GetFullPath(@"C:\vault\sub\drafts")]);
    }

    [Fact]
    public async Task NewFolderCommand_IsAvailableOnlyWithAFolderWorkspaceOpen_INV085()
    {
        var folder = Build();
        folder.NewFolderCommand.CanExecute(new NewEntryRequest(null, "drafts")).ShouldBeFalse();

        await folder.OpenFolderAsync();

        folder.NewFolderCommand.CanExecute(new NewEntryRequest(null, "drafts")).ShouldBeTrue();
        folder.NewFolderCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task OpeningAFolder_ShowsItsEmptyAndIgnoredFolders_INV042_INV084()
    {
        _reader.Folders = ["drafts", "node_modules"];
        _reader.Ignored = ["node_modules"];
        var folder = await OpenAsync("top.md");

        Flatten(folder.Folder!).ShouldBe(["Folder drafts", "Folder node_modules", "File top.md"]);
        Entry(folder, "node_modules").IsIgnored.ShouldBeTrue();
        Entry(folder, "drafts").IsIgnored.ShouldBeFalse();
    }

    private FolderWorkspaceViewModel Build() =>
        new(_picker, _reader, _watcher, _dispatcher, new StubDeleteFilePrompt(), new FakeFileDeleter(),
            new FakeFileRenamer(), new StubRenameFileNotice(), _creator, _notice);

    private async Task<FolderWorkspaceViewModel> OpenAsync(params string[] relativePaths)
    {
        _reader.Result = relativePaths;
        var folder = Build();
        await folder.OpenFolderAsync();
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
