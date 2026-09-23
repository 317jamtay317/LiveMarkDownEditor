using Application;
using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="RecycleBinFileDeleter"/>, the adapter for <see cref="IFileDeleter"/> that sends
/// a deleted File to the Recycle Bin rather than erasing it (INV-081).
/// </summary>
/// <remarks>
/// The delete is real: each run leaves one small temporary file in the Recycle Bin. That is the only
/// way to prove the file is recycled rather than erased.
/// </remarks>
public sealed class RecycleBinFileDeleterTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"lmde-delete-{Guid.NewGuid():N}.md");
    private readonly IFileDeleter _deleter = new RecycleBinFileDeleter();

    [Fact]
    public async Task DeleteAsync_RemovesTheFileFromItsFolder_INV081()
    {
        await File.WriteAllTextAsync(_path, "# Doomed");

        await _deleter.DeleteAsync(_path);

        File.Exists(_path).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_GivenAMissingFile_ThrowsFileNotFound_INV081()
    {
        // A File that has already gone is reported as such, so the caller can treat it as deleted.
        await Should.ThrowAsync<FileNotFoundException>(() => _deleter.DeleteAsync(_path));
    }

    /// <summary>Removes the temporary file if a failing test left it behind.</summary>
    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
