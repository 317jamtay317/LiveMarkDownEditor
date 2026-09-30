using Application;
using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="FileSystemEntryCreator"/>, the adapter for <see cref="IEntryCreator"/> that makes
/// New Folder's empty folder (INV-085) and New File's empty Markdown Document (INV-086) on disk, with any
/// folders on the way, and never overwrites anything already there.
/// </summary>
public sealed class FileSystemEntryCreatorTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("lmde-newentry-").FullName;
    private readonly IEntryCreator _creator = new FileSystemEntryCreator();

    private string DraftsPath => Path.Combine(_folder, "drafts");

    private string IdeasPath => Path.Combine(_folder, "ideas.md");

    [Fact]
    public async Task CreateFolderAsync_MakesAnEmptyFolder_INV085()
    {
        await _creator.CreateFolderAsync(DraftsPath);

        Directory.Exists(DraftsPath).ShouldBeTrue();
        Directory.EnumerateFileSystemEntries(DraftsPath).ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateFolderAsync_ForAPath_MakesTheFoldersOnTheWay_INV085()
    {
        var nested = Path.Combine(DraftsPath, "2026", "q3");

        await _creator.CreateFolderAsync(nested);

        Directory.Exists(nested).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateFolderAsync_WhenAFolderAlreadyHasTheName_Refuses_AndLeavesItAlone_INV085()
    {
        Directory.CreateDirectory(DraftsPath);
        await File.WriteAllTextAsync(Path.Combine(DraftsPath, "kept.md"), "# Kept");

        await Should.ThrowAsync<IOException>(() => _creator.CreateFolderAsync(DraftsPath));

        File.Exists(Path.Combine(DraftsPath, "kept.md")).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateFolderAsync_WhenAFileAlreadyHasTheName_Refuses_AndLeavesItAlone_INV085()
    {
        await File.WriteAllTextAsync(DraftsPath, "not a folder");

        await Should.ThrowAsync<IOException>(() => _creator.CreateFolderAsync(DraftsPath));

        (await File.ReadAllTextAsync(DraftsPath)).ShouldBe("not a folder");
    }

    [Fact]
    public async Task CreateFileAsync_MakesAnEmptyFile_INV086()
    {
        await _creator.CreateFileAsync(IdeasPath);

        File.Exists(IdeasPath).ShouldBeTrue();
        new FileInfo(IdeasPath).Length.ShouldBe(0);
    }

    [Fact]
    public async Task CreateFileAsync_ForAPath_MakesTheFoldersOnTheWay_INV086()
    {
        var nested = Path.Combine(DraftsPath, "2026", "ideas.md");

        await _creator.CreateFileAsync(nested);

        File.Exists(nested).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateFileAsync_WhenAFileAlreadyHasTheName_Refuses_AndLeavesItAlone_INV086()
    {
        await File.WriteAllTextAsync(IdeasPath, "# Kept");

        await Should.ThrowAsync<IOException>(() => _creator.CreateFileAsync(IdeasPath));

        (await File.ReadAllTextAsync(IdeasPath)).ShouldBe("# Kept");
    }

    [Fact]
    public async Task CreateFileAsync_WhenAFolderAlreadyHasTheName_Refuses_INV086()
    {
        Directory.CreateDirectory(IdeasPath);

        await Should.ThrowAsync<IOException>(() => _creator.CreateFileAsync(IdeasPath));

        Directory.Exists(IdeasPath).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_GivenABlankPath_Throws_INV085_INV086(string? path)
    {
        await Should.ThrowAsync<ArgumentException>(() => _creator.CreateFolderAsync(path!));
        await Should.ThrowAsync<ArgumentException>(() => _creator.CreateFileAsync(path!));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
