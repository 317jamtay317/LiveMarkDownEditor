using Application;
using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="FilePdfExportStore"/>, the file-system adapter for
/// <see cref="IPdfExportStore"/> (INV-033).
/// </summary>
public sealed class FilePdfExportStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"lmde-{Guid.NewGuid():N}.pdf");
    private readonly IPdfExportStore _store = new FilePdfExportStore();

    [Fact]
    public async Task SaveAsync_WritesTheBytesVerbatim()
    {
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x00, 0x01, 0x02 };

        await _store.SaveAsync(_path, bytes);

        (await File.ReadAllBytesAsync(_path)).ShouldBe(bytes);
    }

    [Fact]
    public async Task SaveAsync_OverAnExistingFile_ReplacesIt()
    {
        await _store.SaveAsync(_path, [1, 2, 3]);

        await _store.SaveAsync(_path, [4, 5]);

        (await File.ReadAllBytesAsync(_path)).ShouldBe([4, 5]);
    }

    [Fact]
    public async Task SaveAsync_GivenAnEmptyPath_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(() => _store.SaveAsync(string.Empty, [1]));
    }

    [Fact]
    public async Task SaveAsync_GivenNullContent_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => _store.SaveAsync(_path, null!));
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
