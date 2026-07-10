using Shouldly;
using UI.Spelling;
using Xunit;

namespace UI.Tests.Spelling;

/// <summary>
/// Tests for <see cref="SpellCheckScanner"/>: it tokenizes text and keeps only the sub-words a
/// dictionary rejects, so a code-like identifier flags only its genuinely misspelled part.
/// </summary>
public sealed class SpellCheckScannerTests
{
    // A fake dictionary: every listed word is correct; everything else is misspelled.
    private sealed class FakeDictionary(params string[] known) : ISpellDictionary
    {
        private readonly HashSet<string> _known = new(known, StringComparer.OrdinalIgnoreCase);

        public bool IsMisspelled(string word) => !_known.Contains(word);
    }

    [Fact]
    public void FindMisspellings_MethodChain_FlagsOnlyTheUnknownSubWord()
    {
        var dictionary = new FakeDictionary("this", "should", "be");

        var flagged = SpellCheckScanner.FindMisspellings("this.ShouldBe().Invld()", dictionary).ToArray();

        flagged.Length.ShouldBe(1);
        flagged[0].Text.ShouldBe("Invld");
        flagged[0].Start.ShouldBe(16);
    }

    [Fact]
    public void FindMisspellings_AllKnown_FlagsNothing()
    {
        var dictionary = new FakeDictionary("hello", "world");

        SpellCheckScanner.FindMisspellings("hello world", dictionary).ShouldBeEmpty();
    }

    [Fact]
    public void FindMisspellings_SkipsSingleLetterTokens()
    {
        // A lone letter (a loop variable, an initial) is never treated as a misspelling.
        var dictionary = new FakeDictionary();

        SpellCheckScanner.FindMisspellings("x", dictionary).ShouldBeEmpty();
    }
}
