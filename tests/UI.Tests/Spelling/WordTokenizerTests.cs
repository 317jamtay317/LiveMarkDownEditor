using Shouldly;
using UI.Spelling;
using Xunit;

namespace UI.Tests.Spelling;

/// <summary>
/// Tests for <see cref="WordTokenizer"/>: the punctuation- and camelCase-aware segmentation that lets
/// spell check evaluate each sub-word of a code-like identifier individually (so that, e.g., only the
/// genuinely misspelled part of <c>this.ShouldBe().Invld()</c> can be flagged).
/// </summary>
public sealed class WordTokenizerTests
{
    private static (string Text, int Start)[] Tokens(string text) =>
        WordTokenizer.Tokenize(text).Select(word => (word.Text, word.Start)).ToArray();

    [Fact]
    public void Tokenize_PlainSentence_SplitsOnWhitespace()
    {
        Tokens("hello world").ShouldBe([("hello", 0), ("world", 6)]);
    }

    [Fact]
    public void Tokenize_MethodChain_SplitsPunctuationAndCamelCase()
    {
        // The motivating case: this.ShouldBe().Invld() -> this / Should / Be / Invld.
        Tokens("this.ShouldBe().Invld()").ShouldBe([("this", 0), ("Should", 5), ("Be", 11), ("Invld", 16)]);
    }

    [Fact]
    public void Tokenize_Acronym_BreaksBeforeTrailingWord()
    {
        Tokens("XMLHttpRequest").ShouldBe([("XML", 0), ("Http", 3), ("Request", 7)]);
    }

    [Fact]
    public void Tokenize_Contraction_IsKeptWhole()
    {
        Tokens("don't stop").ShouldBe([("don't", 0), ("stop", 6)]);
    }

    [Fact]
    public void Tokenize_DigitsSeparateWords()
    {
        Tokens("utf8bom").ShouldBe([("utf", 0), ("bom", 4)]);
    }

    [Fact]
    public void Tokenize_ReportsAccurateOffsets()
    {
        var word = WordTokenizer.Tokenize("  ab.CdEf").Single(w => w.Text == "Ef");
        word.Start.ShouldBe(7);
        word.Length.ShouldBe(2);
    }
}
