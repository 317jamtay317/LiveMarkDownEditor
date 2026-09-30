using System.IO;
using Domain;
using Shouldly;
using UI.Core;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for New File on the <see cref="FolderWorkspaceViewModel"/>: an empty Markdown Document is created
/// in the Save Folder under a usable Entry Name, refreshed into the Folder Tree, and opened in a Tab
/// (INV-086); and New Document asking the Folder Panel to start New File (INV-087).
/// </summary>
public sealed class FolderWorkspaceViewModelNewFileTests
{
    private const string Root = @"C:\vault";

    private readonly StubFolderPicker _picker = new() { FolderResult = Root };
    private readonly FakeMarkdownFolderReader _reader = new();
    private readonly FakeFolderWatcher _watcher = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly FakeEntryCreator _creator = new();
    private readonly StubNewEntryNotice _notice = new();
    private readonly List<string> _events = [];

    [Fact]
    public async Task NewFile_InASelectedFolder_CreatesTheFileThere_INV086()
    {
        var folder = await OpenAsync("sub/note.md");

        await folder.NewFileAsync(Entry(folder, "sub"), "ideas");

        _creator.CreatedFiles.ShouldBe([Path.GetFullPath(@"C:\vault\sub\ideas.md")]);
        _creator.Created.ShouldBeEmpty();
        _notice.Reasons.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewFile_RefreshesTheTree_ThenOpensTheFile_INV086()
    {
        var folder = await OpenAsync("top.md");
        _creator.OnCreate = path =>
        {
            _events.Add($"created {path}");
            _reader.Result = ["ideas.md", "top.md"];
        };

        await folder.NewFileAsync(null, "ideas");

        var ideas = Path.GetFullPath(@"C:\vault\ideas.md");
        _events.ShouldBe([$"created {ideas}", $"opened {ideas} holding File ideas.md"]);
    }

    [Fact]
    public async Task NewFile_OnceOpened_AsksForTheEditor_SoTheUserCanTypeAtOnce_INV086()
    {
        var folder = await OpenAsync("top.md");
        folder.FocusEditor = () => _events.Add("focus editor");

        await folder.NewFileAsync(null, "ideas");

        _events[^1].ShouldBe("focus editor");
        _events[^2].ShouldStartWith("opened");
    }

    [Fact]
    public async Task NewFile_ThatIsGoneBeforeItOpens_OpensNothing_AndDoesNotThrow_INV086()
    {
        var folder = await OpenAsync("top.md");
        folder.OpenFile = _ => Task.FromException(new FileNotFoundException("gone"));
        folder.FocusEditor = () => _events.Add("focus editor");

        await Should.NotThrowAsync(() => folder.NewFileAsync(null, "ideas"));

        _events.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewFolder_DoesNotAskForTheEditor_INV085()
    {
        var folder = await OpenAsync("top.md");
        folder.FocusEditor = () => _events.Add("focus editor");

        await folder.NewFolderAsync(null, "drafts");

        _events.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewFile_ToANameEndingInASeparator_CreatesAFolder_AndOpensNothing_INV086()
    {
        var folder = await OpenAsync("top.md");

        await folder.NewFileAsync(null, "drafts/");

        _creator.Created.ShouldBe([Path.GetFullPath(@"C:\vault\drafts")]);
        _creator.CreatedFiles.ShouldBeEmpty();
        _events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("what?", "character")]
    [InlineData("top", "“top.md”")]
    [InlineData("/ideas", "separator")]
    public async Task NewFile_ToANameThatIsRefused_ExplainsWhy_AndCreatesAndOpensNothing_INV086(string typed, string explained)
    {
        var folder = await OpenAsync("top.md");

        await folder.NewFileAsync(null, typed);

        _notice.Reasons.Single().ShouldContain(explained, Case.Insensitive);
        _notice.Headings.Single().ShouldBe("New file");
        _creator.CreatedFiles.ShouldBeEmpty();
        _events.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewFile_WhenTheDiskRefuses_ExplainsIt_AndOpensNothing_INV086()
    {
        var folder = await OpenAsync("top.md");
        _creator.Failure = new IOException("A file or folder with that name already exists.");

        await folder.NewFileAsync(null, "ideas");

        _notice.Reasons.Single().ShouldContain("ideas.md");
        _events.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewFileCommand_CreatesTheRequestedFile_INV086()
    {
        var folder = await OpenAsync("sub/note.md");

        folder.NewFileCommand.Execute(new NewEntryRequest(Entry(folder, "sub/note.md"), "ideas"));

        _creator.CreatedFiles.ShouldBe([Path.GetFullPath(@"C:\vault\sub\ideas.md")]);
    }

    [Fact]
    public async Task RequestNewFile_AsksTheFolderPanelAgain_EachTime_AndShowsIt_INV087()
    {
        var folder = await OpenAsync("top.md");
        folder.CloseFolderPanel();

        folder.RequestNewFile();
        var first = folder.NewFileRequest;
        folder.NewFileRequest = null; // the panel takes the request
        folder.RequestNewFile();

        first.ShouldNotBeNull();
        folder.NewFileRequest.ShouldNotBeNull();
        folder.NewFileRequest.ShouldNotBeSameAs(first);
        folder.IsFolderPanelVisible.ShouldBeTrue();
    }

    private async Task<FolderWorkspaceViewModel> OpenAsync(params string[] relativePaths)
    {
        _reader.Result = relativePaths;
        FolderWorkspaceViewModel? folder = null;
        folder = new FolderWorkspaceViewModel(
            _picker, _reader, _watcher, _dispatcher, new StubDeleteFilePrompt(), new FakeFileDeleter(),
            new FakeFileRenamer(), new StubRenameFileNotice(), _creator, _notice)
        {
            // Records what the tree held for the file as it opened, proving the refresh came first.
            OpenFile = path =>
            {
                var held = folder!.Folder!.FileFor(path);
                _events.Add($"opened {path} holding {held?.Kind} {held?.RelativePath}");
                return Task.CompletedTask;
            },
        };

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
}
