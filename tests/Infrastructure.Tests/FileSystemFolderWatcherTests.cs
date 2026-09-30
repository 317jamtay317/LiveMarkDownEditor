using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="FileSystemFolderWatcher.IsTreeChange"/>, the rule deciding which file-system
/// events rebuild the Folder Tree (INV-044): what is added, removed or renamed in the tree, and what
/// changes what Git ignores (INV-084) — but not a saved document, nor Git's own bookkeeping inside
/// <c>.git</c>.
/// </summary>
public sealed class FileSystemFolderWatcherTests
{
    [Theory]
    [InlineData(WatcherChangeTypes.Created, "notes/a.md")]
    [InlineData(WatcherChangeTypes.Deleted, "notes/a.md")]
    [InlineData(WatcherChangeTypes.Renamed, "notes/b.md")]
    [InlineData(WatcherChangeTypes.Created, "drafts")]
    [InlineData(WatcherChangeTypes.Deleted, "a/b/empty")]
    [InlineData(WatcherChangeTypes.Renamed, "renamed-folder")]
    public void IsTreeChange_AnEntryAddedRemovedOrRenamed_RebuildsTheTree_INV044(WatcherChangeTypes change, string path)
    {
        FileSystemFolderWatcher.IsTreeChange(change, path).ShouldBeTrue();
    }

    [Theory]
    [InlineData(WatcherChangeTypes.Changed, ".gitignore")]
    [InlineData(WatcherChangeTypes.Changed, "sub/.gitignore")]
    [InlineData(WatcherChangeTypes.Changed, ".GITIGNORE")]
    [InlineData(WatcherChangeTypes.Changed, ".git/info/exclude")]
    [InlineData(WatcherChangeTypes.Created, ".git/info/exclude")]
    public void IsTreeChange_WhatGitIgnoresChanging_RebuildsTheTree_INV044(WatcherChangeTypes change, string path)
    {
        FileSystemFolderWatcher.IsTreeChange(change, path).ShouldBeTrue();
    }

    [Theory]
    [InlineData(WatcherChangeTypes.Changed, "notes/a.md")]
    [InlineData(WatcherChangeTypes.Changed, "notes")]
    public void IsTreeChange_ADocumentSavedInPlace_RebuildsNothing_INV044(WatcherChangeTypes change, string path)
    {
        FileSystemFolderWatcher.IsTreeChange(change, path).ShouldBeFalse();
    }

    [Theory]
    [InlineData(WatcherChangeTypes.Created, ".git/index.lock")]
    [InlineData(WatcherChangeTypes.Deleted, ".git/index.lock")]
    [InlineData(WatcherChangeTypes.Renamed, ".git/refs/heads/main")]
    [InlineData(WatcherChangeTypes.Created, ".git/objects/ab")]
    [InlineData(WatcherChangeTypes.Changed, ".git/HEAD")]
    [InlineData(WatcherChangeTypes.Created, "vendor/lib/.git/index.lock")]
    [InlineData(WatcherChangeTypes.Created, ".hg/store/data")]
    public void IsTreeChange_BookkeepingInsideAnExcludedFolder_RebuildsNothing_INV044(WatcherChangeTypes change, string path)
    {
        FileSystemFolderWatcher.IsTreeChange(change, path).ShouldBeFalse();
    }

    [Fact]
    public void IsTreeChange_TakesEitherSeparator_INV044()
    {
        FileSystemFolderWatcher.IsTreeChange(WatcherChangeTypes.Created, @".git\index.lock").ShouldBeFalse();
        FileSystemFolderWatcher.IsTreeChange(WatcherChangeTypes.Changed, @"sub\.gitignore").ShouldBeTrue();
    }

    [Fact]
    public void IgnoreSourcesAbove_ARootNestedInARepository_NamesEveryGitignoreAboveIt_AndTheRepositorysExclude_INV084()
    {
        using var scratch = new Scratch();
        var repository = scratch.Repository();
        var root = scratch.Folder(@"docs\notes");

        var sources = FileSystemFolderWatcher.IgnoreSourcesAbove(root);

        sources.ShouldBe(
        [
            (Path.Combine(repository, "docs"), ".gitignore"),
            (repository, ".gitignore"),
            (Path.Combine(repository, ".git", "info"), "exclude"),
        ]);
    }

    [Fact]
    public void IgnoreSourcesAbove_TheRepositoryRootItself_NamesNothing_ItsOwnWatcherCoversThem_INV084()
    {
        using var scratch = new Scratch();
        var repository = scratch.Repository();

        FileSystemFolderWatcher.IgnoreSourcesAbove(repository).ShouldBeEmpty();
    }

    [Fact]
    public void IgnoreSourcesAbove_ARootOutsideAnyRepository_NamesNothing_INV084()
    {
        using var scratch = new Scratch();

        FileSystemFolderWatcher.IgnoreSourcesAbove(scratch.Folder(@"docs\notes")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(".gitignore")]
    [InlineData(@"docs\.gitignore")]
    [InlineData(@".git\info\exclude")]
    public void Watch_ARootNestedInARepository_RebuildsWhenAnIgnoreRuleAboveItChanges_INV044(string source)
    {
        using var scratch = new Scratch();
        var repository = scratch.Repository();
        var root = scratch.Folder(@"docs\notes");
        var file = Path.Combine(repository, source);
        File.WriteAllText(file, "bin/\n");
        using var watcher = new FileSystemFolderWatcher();
        using var changed = new ManualResetEventSlim();
        watcher.Changed += (_, _) => changed.Set();
        watcher.Watch(root);

        File.AppendAllText(file, "obj/\n");

        changed.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
    }

    [Fact]
    public void Watch_ARootNestedInARepository_IgnoresOtherFilesAboveIt_INV044()
    {
        using var scratch = new Scratch();
        var repository = scratch.Repository();
        var root = scratch.Folder(@"docs\notes");
        var other = Path.Combine(repository, "README.md");
        File.WriteAllText(other, "# Repo");
        using var watcher = new FileSystemFolderWatcher();
        using var changed = new ManualResetEventSlim();
        watcher.Changed += (_, _) => changed.Set();
        watcher.Watch(root);

        File.AppendAllText(other, "more\n");

        changed.Wait(TimeSpan.FromSeconds(1)).ShouldBeFalse();
    }

    /// <summary>A scratch folder, deleted afterwards, that can be made a repository's root.</summary>
    private sealed class Scratch : IDisposable
    {
        private readonly string _path = Directory.CreateTempSubdirectory("lmde-watch-").FullName;

        // A repository as the watcher finds one: a .git folder, with the info folder Git makes.
        public string Repository()
        {
            Directory.CreateDirectory(Path.Combine(_path, ".git", "info"));
            return _path;
        }

        public string Folder(string relative) => Directory.CreateDirectory(Path.Combine(_path, relative)).FullName;

        public void Dispose() => Directory.Delete(_path, recursive: true);
    }
}
