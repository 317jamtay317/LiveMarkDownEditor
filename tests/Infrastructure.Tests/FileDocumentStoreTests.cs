using Application;
using Domain;
using Infrastructure.Storage;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="FileDocumentStore"/>, the file-system adapter for <see cref="IDocumentStore"/>.
/// </summary>
public sealed class FileDocumentStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"lmde-{Guid.NewGuid():N}.md");
    private readonly IDocumentStore _store = new FileDocumentStore();

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsTheSourceText()
    {
        await _store.SaveAsync(_path, new MarkdownDocument("# Hello\n\nWorld."));

        var loaded = await _store.LoadAsync(_path);

        loaded.Source.Text.ShouldBe("# Hello\n\nWorld.");
    }

    [Fact]
    public async Task LoadAsync_GivenMissingFile_Throws()
    {
        await Should.ThrowAsync<FileNotFoundException>(() => _store.LoadAsync(_path));
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
