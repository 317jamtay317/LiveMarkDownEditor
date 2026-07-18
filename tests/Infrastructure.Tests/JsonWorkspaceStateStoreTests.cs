using Application;
using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="JsonWorkspaceStateStore"/>, the file-system adapter for
/// <see cref="IWorkspaceStateStore"/> (INV-037): it round-trips the Workspace State and never lets a
/// missing or corrupt file stop the app from starting.
/// </summary>
public sealed class JsonWorkspaceStateStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"lmde-{Guid.NewGuid():N}");
    private readonly string _path;

    public JsonWorkspaceStateStoreTests()
    {
        _path = Path.Combine(_directory, "workspace.json");
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTripsTheState()
    {
        var store = new JsonWorkspaceStateStore(_path);
        var state = new WorkspaceState([@"C:\a.md", @"C:\b.md"], [@"C:\b.md", @"C:\a.md"]);

        await store.SaveAsync(state);
        var loaded = store.Load();

        loaded.OpenDocuments.ShouldBe([@"C:\a.md", @"C:\b.md"]);
        loaded.RecentFiles.ShouldBe([@"C:\b.md", @"C:\a.md"]);
    }

    [Fact]
    public void Load_WhenNoFileExists_ReturnsEmpty()
    {
        new JsonWorkspaceStateStore(_path).Load().ShouldBe(WorkspaceState.Empty);
    }

    [Fact]
    public async Task Load_WhenTheFileIsCorrupt_ReturnsEmpty()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_path, "{ this is not valid json");

        new JsonWorkspaceStateStore(_path).Load().ShouldBe(WorkspaceState.Empty);
    }

    [Fact]
    public async Task SaveAsync_CreatesTheDirectory_WhenItDoesNotExist()
    {
        Directory.Exists(_directory).ShouldBeFalse();

        await new JsonWorkspaceStateStore(_path).SaveAsync(WorkspaceState.Empty);

        File.Exists(_path).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_ThenLoad_RoundTripsTheWorkspaceFolder_INV045()
    {
        var store = new JsonWorkspaceStateStore(_path);
        var state = new WorkspaceState([@"C:\a.md"], [@"C:\a.md"], WorkspaceFolder: @"C:\vault");

        await store.SaveAsync(state);

        store.Load().WorkspaceFolder.ShouldBe(@"C:\vault");
    }

    [Fact]
    public async Task Load_WhenStateHasNoWorkspaceFolderField_LoadsWithNoFolder_INV045()
    {
        // State written by an earlier version — before the Folder Workspace — must still load, with no
        // Folder Workspace open rather than failing.
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_path, """{ "OpenDocuments": ["C:\\a.md"], "RecentFiles": [] }""");

        var loaded = new JsonWorkspaceStateStore(_path).Load();

        loaded.OpenDocuments.ShouldBe([@"C:\a.md"]);
        loaded.WorkspaceFolder.ShouldBeNull();
    }

    [Fact]
    public void Constructor_GivenABlankPath_Throws()
    {
        Should.Throw<ArgumentException>(() => new JsonWorkspaceStateStore("  "));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
