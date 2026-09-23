using Domain;
using Shouldly;
using Xunit;

namespace Domain.Tests;

/// <summary>
/// Tests for <see cref="RecentFiles"/> — the most-recently-used Watched File paths (INV-036): newest
/// first, distinct (case-insensitively), capped, blank-free, and immutable.
/// </summary>
public sealed class RecentFilesTests
{
    [Fact]
    public void Empty_HasNoPaths()
    {
        RecentFiles.Empty.Paths.ShouldBeEmpty();
    }

    [Fact]
    public void Add_PutsThePathAtTheFront()
    {
        var recent = RecentFiles.Empty.Add(@"C:\a.md").Add(@"C:\b.md");

        recent.Paths.ShouldBe([@"C:\b.md", @"C:\a.md"]);
    }

    [Fact]
    public void Add_AnExistingPath_MovesItToTheFront_WithoutDuplicating()
    {
        var recent = RecentFiles.Empty.Add(@"C:\a.md").Add(@"C:\b.md").Add(@"C:\a.md");

        recent.Paths.ShouldBe([@"C:\a.md", @"C:\b.md"]);
    }

    [Fact]
    public void Add_ComparesPathsCaseInsensitively()
    {
        var recent = RecentFiles.Empty.Add(@"C:\Docs\Note.md").Add(@"c:\docs\note.md");

        recent.Paths.Count.ShouldBe(1);
        recent.Paths[0].ShouldBe(@"c:\docs\note.md");
    }

    [Fact]
    public void Add_TrimsToCapacity_DroppingTheOldest()
    {
        var recent = RecentFiles.Empty;
        for (var i = 0; i <= RecentFiles.Capacity; i++)
        {
            recent = recent.Add($@"C:\file{i}.md");
        }

        recent.Paths.Count.ShouldBe(RecentFiles.Capacity);
        recent.Paths.ShouldNotContain(@"C:\file0.md"); // the oldest fell off
        recent.Paths[0].ShouldBe($@"C:\file{RecentFiles.Capacity}.md"); // the newest is at the front
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_GivenNullOrBlank_Throws(string? path)
    {
        Should.Throw<ArgumentException>(() => RecentFiles.Empty.Add(path!));
    }

    [Fact]
    public void Add_DoesNotMutateTheOriginal()
    {
        var original = RecentFiles.Empty.Add(@"C:\a.md");

        original.Add(@"C:\b.md");

        original.Paths.ShouldBe([@"C:\a.md"]);
    }

    [Fact]
    public void From_KeepsTheGivenOrder_NewestFirst()
    {
        var recent = RecentFiles.From([@"C:\a.md", @"C:\b.md", @"C:\c.md"]);

        recent.Paths.ShouldBe([@"C:\a.md", @"C:\b.md", @"C:\c.md"]);
    }

    [Fact]
    public void From_SkipsBlankEntries()
    {
        var recent = RecentFiles.From([@"C:\a.md", "  ", @"C:\b.md"]);

        recent.Paths.ShouldBe([@"C:\a.md", @"C:\b.md"]);
    }

    [Fact]
    public void From_GivenNull_Throws()
    {
        Should.Throw<ArgumentNullException>(() => RecentFiles.From(null!));
    }

    [Fact]
    public void Remove_DropsThePath_KeepingTheRestInOrder_INV081()
    {
        var recent = RecentFiles.From([@"C:\a.md", @"C:\b.md", @"C:\c.md"]);

        recent.Remove(@"C:\b.md").Paths.ShouldBe([@"C:\a.md", @"C:\c.md"]);
    }

    [Fact]
    public void Remove_ComparesPathsCaseInsensitively_INV081()
    {
        var recent = RecentFiles.From([@"C:\Notes\A.md", @"C:\b.md"]);

        recent.Remove(@"c:\notes\a.MD").Paths.ShouldBe([@"C:\b.md"]);
    }

    [Fact]
    public void Remove_APathNotListed_ChangesNothing_INV081()
    {
        var recent = RecentFiles.From([@"C:\a.md"]);

        recent.Remove(@"C:\elsewhere.md").Paths.ShouldBe([@"C:\a.md"]);
    }

    [Fact]
    public void Remove_DoesNotMutateTheOriginal_INV081()
    {
        var original = RecentFiles.From([@"C:\a.md", @"C:\b.md"]);

        original.Remove(@"C:\a.md");

        original.Paths.ShouldBe([@"C:\a.md", @"C:\b.md"]);
    }

    [Fact]
    public void Rename_ReplacesThePath_InTheSamePlace_INV082()
    {
        var recent = RecentFiles.From([@"C:\a.md", @"C:\b.md", @"C:\c.md"]);

        recent.Rename(@"C:\b.md", @"C:\renamed.md").Paths.ShouldBe([@"C:\a.md", @"C:\renamed.md", @"C:\c.md"]);
    }

    [Fact]
    public void Rename_ComparesThePathCaseInsensitively_INV082()
    {
        var recent = RecentFiles.From([@"C:\Notes\A.md", @"C:\b.md"]);

        recent.Rename(@"c:\notes\a.MD", @"C:\Notes\Z.md").Paths.ShouldBe([@"C:\Notes\Z.md", @"C:\b.md"]);
    }

    [Fact]
    public void Rename_ChangingOnlyTheCapitals_KeepsOneEntry_INV082()
    {
        var recent = RecentFiles.From([@"C:\note.md", @"C:\b.md"]);

        recent.Rename(@"C:\note.md", @"C:\Note.md").Paths.ShouldBe([@"C:\Note.md", @"C:\b.md"]);
    }

    [Fact]
    public void Rename_ToAPathAlreadyListed_KeepsItOnceInTheRenamedPlace_INV082()
    {
        var recent = RecentFiles.From([@"C:\a.md", @"C:\b.md", @"C:\c.md"]);

        recent.Rename(@"C:\a.md", @"C:\c.md").Paths.ShouldBe([@"C:\c.md", @"C:\b.md"]);
    }

    [Fact]
    public void Rename_APathNotListed_ChangesNothing_INV082()
    {
        var recent = RecentFiles.From([@"C:\a.md"]);

        recent.Rename(@"C:\elsewhere.md", @"C:\renamed.md").Paths.ShouldBe([@"C:\a.md"]);
    }

    [Fact]
    public void Rename_DoesNotMutateTheOriginal_INV082()
    {
        var original = RecentFiles.From([@"C:\a.md"]);

        original.Rename(@"C:\a.md", @"C:\b.md");

        original.Paths.ShouldBe([@"C:\a.md"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_ToANullOrBlankPath_Throws_INV082(string? newPath)
    {
        Should.Throw<ArgumentException>(() => RecentFiles.From([@"C:\a.md"]).Rename(@"C:\a.md", newPath!));
    }
}
