using System.IO;
using Shouldly;
using UI.Core;
using UI.Platform;
using Xunit;

namespace UI.Tests.Platform;

/// <summary>
/// Tests for <see cref="JsonPageSetupStore"/>: the Page Setup is remembered across runs as an
/// editor-wide preference, and a missing or unreadable persisted setup loads as the default — never
/// stopping the app from starting (INV-061).
/// </summary>
public sealed class JsonPageSetupStoreTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "LiveMarkDownEditor.Tests", Guid.NewGuid().ToString("N"));

    private string StorePath => Path.Combine(_directory, "page-setup.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsThePageSetup_INV061()
    {
        var store = new JsonPageSetupStore(StorePath);
        var setup = new PageSetup(
            PageOrientation.Landscape,
            new PrintMargins(left: 30d, top: 40d, right: 30d, bottom: 40d));

        await store.SaveAsync(setup);
        var loaded = new JsonPageSetupStore(StorePath).Load();

        loaded.ShouldBe(setup);
    }

    [Fact]
    public void Load_GivenNoFile_IsTheDefault_INV061()
    {
        var store = new JsonPageSetupStore(StorePath);

        store.Load().ShouldBe(PageSetup.Default);
    }

    [Fact]
    public void Load_GivenACorruptFile_IsTheDefault_INV061()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(StorePath, "not json at all {");

        new JsonPageSetupStore(StorePath).Load().ShouldBe(PageSetup.Default);
    }

    [Fact]
    public void Load_GivenMarginsThatViolateTheGuard_IsTheDefault_INV061()
    {
        // Well-formed JSON whose values no PrintMargins may hold (a negative margin): the guarded
        // value object refuses it, and the store falls back to the default rather than crashing.
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            StorePath,
            """{ "Orientation": "Portrait", "Left": -5, "Top": 96, "Right": 96, "Bottom": 96 }""");

        new JsonPageSetupStore(StorePath).Load().ShouldBe(PageSetup.Default);
    }
}
