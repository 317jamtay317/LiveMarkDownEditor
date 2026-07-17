using System.Text;
using Application;
using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="FileHtmlExportStore"/>, the file-system adapter for
/// <see cref="IHtmlExportStore"/> (INV-032).
/// </summary>
public sealed class FileHtmlExportStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"lmde-{Guid.NewGuid():N}.html");
    private readonly IHtmlExportStore _store = new FileHtmlExportStore();

    [Fact]
    public async Task SaveAsync_WritesTheHtmlVerbatim()
    {
        await _store.SaveAsync(_path, "<h1>Hello</h1>");

        (await File.ReadAllTextAsync(_path)).ShouldBe("<h1>Hello</h1>");
    }

    [Fact]
    public async Task SaveAsync_WritesUtf8WithoutAByteOrderMark()
    {
        // A Standalone Page declares <meta charset="utf-8">, and a BOM would be the one thing that
        // could make the bytes disagree with that declaration.
        await _store.SaveAsync(_path, "<p>café</p>");

        var bytes = await File.ReadAllBytesAsync(_path);

        bytes.Take(3).ShouldNotBe(new byte[] { 0xEF, 0xBB, 0xBF });
        Encoding.UTF8.GetString(bytes).ShouldBe("<p>café</p>");
    }

    [Fact]
    public async Task SaveAsync_OverAnExistingFile_ReplacesIt()
    {
        await _store.SaveAsync(_path, "<p>first</p>");

        await _store.SaveAsync(_path, "<p>second</p>");

        (await File.ReadAllTextAsync(_path)).ShouldBe("<p>second</p>");
    }

    [Fact]
    public async Task SaveAsync_GivenAnEmptyPath_Throws()
    {
        await Should.ThrowAsync<ArgumentException>(() => _store.SaveAsync(string.Empty, "<p></p>"));
    }

    [Fact]
    public async Task SaveAsync_GivenNullHtml_Throws()
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
