using Application;
using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="FileSystemFileRenamer"/>, the adapter for <see cref="IFileRenamer"/> that gives a
/// File its New Name on disk and never overwrites another file (INV-082).
/// </summary>
public sealed class FileSystemFileRenamerTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("lmde-rename-").FullName;
    private readonly IFileRenamer _renamer = new FileSystemFileRenamer();

    private string NotePath => Path.Combine(_folder, "note.md");

    private string IdeasPath => Path.Combine(_folder, "ideas.md");

    [Fact]
    public async Task RenameAsync_MovesTheFileToItsNewName_KeepingItsText_INV082()
    {
        await File.WriteAllTextAsync(NotePath, "# Note");

        await _renamer.RenameAsync(NotePath, IdeasPath);

        File.Exists(NotePath).ShouldBeFalse();
        (await File.ReadAllTextAsync(IdeasPath)).ShouldBe("# Note");
    }

    [Fact]
    public async Task RenameAsync_ChangingOnlyTheCapitals_RenamesTheFile_INV082()
    {
        await File.WriteAllTextAsync(NotePath, "# Note");
        var capitalized = Path.Combine(_folder, "Note.md");

        await _renamer.RenameAsync(NotePath, capitalized);

        // The file system ignores capitals when it looks a name up, so ask it for the name it stores.
        Directory.GetFiles(_folder).Select(Path.GetFileName).ShouldBe(["Note.md"]);
    }

    [Fact]
    public async Task RenameAsync_ToANameAnotherFileHas_NeverOverwritesIt_INV082()
    {
        await File.WriteAllTextAsync(NotePath, "# Note");
        await File.WriteAllTextAsync(IdeasPath, "# Ideas");

        await Should.ThrowAsync<IOException>(() => _renamer.RenameAsync(NotePath, IdeasPath));

        (await File.ReadAllTextAsync(IdeasPath)).ShouldBe("# Ideas");
        (await File.ReadAllTextAsync(NotePath)).ShouldBe("# Note");
    }

    [Fact]
    public async Task RenameAsync_ToTheNameOfAFolder_FailsAndKeepsTheFile_INV082()
    {
        await File.WriteAllTextAsync(NotePath, "# Note");
        Directory.CreateDirectory(IdeasPath);

        await Should.ThrowAsync<IOException>(() => _renamer.RenameAsync(NotePath, IdeasPath));

        File.Exists(NotePath).ShouldBeTrue();
    }

    [Fact]
    public async Task RenameAsync_GivenAMissingFile_ThrowsFileNotFound_INV082()
    {
        // A File that has already gone is reported as such, so the caller can say so and let it go.
        await Should.ThrowAsync<FileNotFoundException>(() => _renamer.RenameAsync(NotePath, IdeasPath));
    }

    /// <summary>Removes the temporary folder and whatever a test left in it.</summary>
    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
