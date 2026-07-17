using System.IO;
using Shouldly;
using UI.Spelling;
using Xunit;

namespace UI.Tests.Spelling;

/// <summary>
/// Tests for <see cref="FileUserDictionary"/> (INV-040): accepted words are held, compared
/// case-insensitively, and persisted across instances; a missing file is an empty dictionary.
/// </summary>
public sealed class FileUserDictionaryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"lmde-{Guid.NewGuid():N}.txt");

    [Fact]
    public void Add_ThenContains_TheWord()
    {
        var dictionary = new FileUserDictionary(_path);

        dictionary.Add("Shouldly");

        dictionary.Contains("Shouldly").ShouldBeTrue();
    }

    [Fact]
    public void Contains_IsCaseInsensitive()
    {
        var dictionary = new FileUserDictionary(_path);
        dictionary.Add("Shouldly");

        dictionary.Contains("shouldly").ShouldBeTrue();
    }

    [Fact]
    public void Add_PersistsTheWord_AcrossInstances_INV040()
    {
        new FileUserDictionary(_path).Add("Markdig");

        // A fresh dictionary over the same file still knows the accepted word.
        new FileUserDictionary(_path).Contains("Markdig").ShouldBeTrue();
    }

    [Fact]
    public void Contains_OnAMissingFile_IsFalse()
    {
        new FileUserDictionary(_path).Contains("anything").ShouldBeFalse();
    }

    [Fact]
    public void Add_GivenABlankWord_Throws()
    {
        Should.Throw<ArgumentException>(() => new FileUserDictionary(_path).Add("   "));
    }

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }
}
