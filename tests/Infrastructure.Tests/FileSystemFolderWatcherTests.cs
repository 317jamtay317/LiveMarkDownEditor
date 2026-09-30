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
}
