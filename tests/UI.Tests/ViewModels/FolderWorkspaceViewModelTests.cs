using Domain;
using Shouldly;
using UI.Tests.TestDoubles;
using UI.ViewModels;
using Xunit;

namespace UI.Tests.ViewModels;

/// <summary>
/// Tests for <see cref="FolderWorkspaceViewModel"/> — the Folder Workspace shell: opening a folder and
/// browsing it are view-only, activating a File opens it (INV-043); the tree tracks the disk live
/// (INV-044); and the open folder is restored across runs (INV-045).
/// </summary>
public sealed class FolderWorkspaceViewModelTests
{
    private const string Root = @"C:\vault";

    private readonly StubFolderPicker _picker = new();
    private readonly FakeMarkdownFolderReader _reader = new();
    private readonly FakeFolderWatcher _watcher = new();
    private readonly InlineUiDispatcher _dispatcher = new();
    private readonly List<string> _opened = [];

    private FolderWorkspaceViewModel Create()
    {
        var folder = new FolderWorkspaceViewModel(_picker, _reader, _watcher, _dispatcher)
        {
            OpenFile = path =>
            {
                _opened.Add(path);
                return Task.CompletedTask;
            },
        };
        return folder;
    }

    private static FolderEntry FirstFile(FolderWorkspace workspace)
    {
        FolderEntry? Find(IReadOnlyList<FolderEntry> entries)
        {
            foreach (var entry in entries)
            {
                if (entry.Kind == FolderEntryKind.File)
                {
                    return entry;
                }

                var nested = Find(entry.Children);
                if (nested is not null)
                {
                    return nested;
                }
            }

            return null;
        }

        return Find(workspace.Entries) ?? throw new InvalidOperationException("No File entry in the tree.");
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

    [Fact]
    public async Task OpenFolder_BuildsTheTree_ShowsThePanel_AndWatchesTheRoot_INV043()
    {
        _picker.FolderResult = Root;
        _reader.Result = ["a.md", "sub/b.md"];
        var folder = Create();

        await folder.OpenFolderAsync();

        folder.Folder.ShouldNotBeNull();
        folder.Folder!.RootPath.ShouldBe(Root);
        Flatten(folder.Folder).ShouldBe(["Folder sub", "File sub/b.md", "File a.md"]);
        folder.IsFolderPanelVisible.ShouldBeTrue();
        _watcher.WatchedRoot.ShouldBe(Root);
    }

    [Fact]
    public async Task OpenFolder_WhenCancelled_OpensNoFolder_INV043()
    {
        _picker.FolderResult = null;
        var folder = Create();

        await folder.OpenFolderAsync();

        folder.Folder.ShouldBeNull();
        folder.IsFolderPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task Activate_OnAFile_OpensItThroughTheCallback_WithItsCanonicalPath_INV043()
    {
        _picker.FolderResult = Root;
        _reader.Result = ["sub/note.md"];
        var folder = Create();
        await folder.OpenFolderAsync();
        var file = FirstFile(folder.Folder!);

        await folder.ActivateAsync(file);

        _opened.ShouldHaveSingleItem();
        _opened[0].ShouldBe(folder.Folder!.AbsolutePathOf(file));
        _opened[0].ShouldBe(System.IO.Path.GetFullPath(@"C:\vault\sub\note.md"));
    }

    [Fact]
    public async Task Activate_OnAFolder_OpensNothing_INV043()
    {
        _picker.FolderResult = Root;
        _reader.Result = ["sub/note.md"];
        var folder = Create();
        await folder.OpenFolderAsync();
        var folderEntry = folder.Folder!.Entries[0];
        folderEntry.Kind.ShouldBe(FolderEntryKind.Folder);

        await folder.ActivateAsync(folderEntry);

        _opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task Activate_WhenTheFileHasGone_OpensNothing_INV043()
    {
        _picker.FolderResult = Root;
        _reader.Result = ["note.md"];
        var folder = new FolderWorkspaceViewModel(_picker, _reader, _watcher, _dispatcher)
        {
            OpenFile = _ => Task.FromException(new System.IO.IOException("gone")),
        };
        await folder.OpenFolderAsync();
        var file = FirstFile(folder.Folder!);

        // A file deleted between enumeration and the click must not crash — it simply opens nothing.
        await Should.NotThrowAsync(() => folder.ActivateAsync(file));
    }

    [Fact]
    public void ToggleFolderPanel_TogglesItsVisibility_INV043()
    {
        var folder = Create();

        folder.ToggleFolderPanelCommand.Execute(null);
        folder.IsFolderPanelVisible.ShouldBeTrue();

        folder.ToggleFolderPanelCommand.Execute(null);
        folder.IsFolderPanelVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task WatcherChanged_ReReadsTheFolder_AndUpdatesTheTree_INV044()
    {
        _picker.FolderResult = Root;
        _reader.Result = ["a.md"];
        var folder = Create();
        await folder.OpenFolderAsync();
        Flatten(folder.Folder!).ShouldBe(["File a.md"]);

        // A Markdown Document appears on disk; the watcher fires and the tree tracks it (INV-044).
        _reader.Result = ["a.md", "b.md"];
        _watcher.RaiseChanged();

        Flatten(folder.Folder!).ShouldBe(["File a.md", "File b.md"]);
        _opened.ShouldBeEmpty(); // tracking the disk opens/edits nothing
    }

    [Fact]
    public async Task Refresh_ReReadsTheOpenFolder_INV044()
    {
        _picker.FolderResult = Root;
        _reader.Result = ["a.md"];
        var folder = Create();
        await folder.OpenFolderAsync();

        _reader.Result = ["a.md", "c.md"];
        await folder.RefreshAsync();

        Flatten(folder.Folder!).ShouldBe(["File a.md", "File c.md"]);
    }

    [Fact]
    public async Task OpenFolder_PersistsTheState_INV045()
    {
        var persisted = 0;
        _picker.FolderResult = Root;
        _reader.Result = ["a.md"];
        var folder = new FolderWorkspaceViewModel(_picker, _reader, _watcher, _dispatcher)
        {
            OpenFile = _ => Task.CompletedTask,
            PersistState = () => { persisted++; return Task.CompletedTask; },
        };

        await folder.OpenFolderAsync();

        persisted.ShouldBe(1);
    }

    [Fact]
    public async Task Restore_ReopensASavedFolder_WithoutRePersisting_INV045()
    {
        var persisted = 0;
        _reader.Result = ["a.md"];
        var folder = new FolderWorkspaceViewModel(_picker, _reader, _watcher, _dispatcher)
        {
            OpenFile = _ => Task.CompletedTask,
            PersistState = () => { persisted++; return Task.CompletedTask; },
        };

        await folder.RestoreAsync(Root);

        folder.Folder.ShouldNotBeNull();
        folder.Folder!.RootPath.ShouldBe(Root);
        Flatten(folder.Folder).ShouldBe(["File a.md"]);
        folder.IsFolderPanelVisible.ShouldBeTrue();
        _watcher.WatchedRoot.ShouldBe(Root);
        persisted.ShouldBe(0); // restoring is not itself an open, so it does not re-persist (INV-037)
    }

    [Fact]
    public async Task Restore_WhenTheFolderHasGone_OpensNothing_INV045()
    {
        _reader.MissingRoots.Add(@"C:\gone");
        var folder = Create();

        await folder.RestoreAsync(@"C:\gone");

        folder.Folder.ShouldBeNull();
    }

    [Fact]
    public async Task Restore_GivenNoSavedFolder_OpensNothing_INV045()
    {
        var folder = Create();

        await folder.RestoreAsync(null);

        folder.Folder.ShouldBeNull();
    }
}
