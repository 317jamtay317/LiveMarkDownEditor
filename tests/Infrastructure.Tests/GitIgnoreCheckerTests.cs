using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="GitIgnoreChecker"/>, which asks Git — as VS Code's Git extension does, with
/// <c>git check-ignore</c> — which of a Folder Listing's paths are Ignored (INV-084). Run against a real
/// Git repository in a scratch folder.
/// </summary>
public sealed class GitIgnoreCheckerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lmde-git-{Guid.NewGuid():N}");
    private readonly GitIgnoreChecker _checker = new();

    [Fact]
    public async Task IgnoredAsync_NamesAnIgnoredFolder_AndAFileInsideIt_INV084()
    {
        Repository("node_modules/\n");
        Write("node_modules/pkg/readme.md");

        var ignored = await _checker.IgnoredAsync(_root, ["node_modules", "node_modules/pkg", "node_modules/pkg/readme.md"]);

        ignored.ShouldBe(["node_modules", "node_modules/pkg", "node_modules/pkg/readme.md"], ignoreOrder: true);
    }

    [Fact]
    public async Task IgnoredAsync_NamesAnIgnoredFile_AndNotTheOneBesideIt_INV084()
    {
        Repository("*.log.md\n");
        Write("today.log.md");
        Write("note.md");

        var ignored = await _checker.IgnoredAsync(_root, ["today.log.md", "note.md"]);

        ignored.ShouldBe(["today.log.md"]);
    }

    [Fact]
    public async Task IgnoredAsync_DoesNotNameAPathANegationReincludes_INV084()
    {
        Repository("*.md\n!keep.md\n");
        Write("drop.md");
        Write("keep.md");

        var ignored = await _checker.IgnoredAsync(_root, ["drop.md", "keep.md"]);

        ignored.ShouldBe(["drop.md"]);
    }

    [Fact]
    public async Task IgnoredAsync_DoesNotNameAFileGitTracks_INV084()
    {
        Repository(string.Empty);
        Write("tracked.md");
        GitRepository.Run(_root, "add", "tracked.md");
        File.WriteAllText(Path.Combine(_root, ".gitignore"), "tracked.md\n");

        var ignored = await _checker.IgnoredAsync(_root, ["tracked.md"]);

        ignored.ShouldBeEmpty();
    }

    [Fact]
    public async Task IgnoredAsync_ForARootNestedInARepository_AnswersForThePathsBeneathTheRoot_INV084()
    {
        Repository("bin/\n");
        Write("docs/bin/out.md");
        Write("docs/guide.md");
        var nestedRoot = Path.Combine(_root, "docs");

        var ignored = await _checker.IgnoredAsync(nestedRoot, ["bin", "bin/out.md", "guide.md"]);

        ignored.ShouldBe(["bin", "bin/out.md"], ignoreOrder: true);
    }

    [Fact]
    public async Task IgnoredAsync_ForARootThatIsNotARepository_NamesNothing_INV084()
    {
        Write("node_modules/pkg/readme.md");

        var ignored = await _checker.IgnoredAsync(_root, ["node_modules", "node_modules/pkg/readme.md"]);

        ignored.ShouldBeEmpty();
    }

    [Fact]
    public async Task IgnoredAsync_WithoutGit_NamesNothing_INV084()
    {
        Repository("node_modules/\n");
        var withoutGit = new GitIgnoreChecker(gitExecutable: "no-such-git-executable");

        var ignored = await withoutGit.IgnoredAsync(_root, ["node_modules"]);

        ignored.ShouldBeEmpty();
    }

    [Fact]
    public async Task IgnoredAsync_GivenNoPaths_NamesNothing_INV084()
    {
        Repository("*\n");

        (await _checker.IgnoredAsync(_root, [])).ShouldBeEmpty();
    }

    [Fact]
    public async Task IgnoredAsync_ForManyPaths_AnswersForEveryOne_INV084()
    {
        Repository("gen/\n");
        var paths = Enumerable.Range(0, 5000).Select(index => $"gen/file{index}.md").ToList();

        var ignored = await _checker.IgnoredAsync(_root, paths);

        ignored.Count.ShouldBe(5000);
    }

    public void Dispose() => GitRepository.Delete(_root);

    private void Repository(string gitignore)
    {
        GitRepository.Init(_root);
        File.WriteAllText(Path.Combine(_root, ".gitignore"), gitignore);
    }

    private void Write(string relativePath)
    {
        var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "content");
    }
}
