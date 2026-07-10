using Shouldly;
using UI.Spelling;
using Xunit;

namespace UI.Tests.Spelling;

/// <summary>
/// Tests for <see cref="SpellingSuggestions"/>: it takes the raw Spelling Suggestions the Dictionary
/// offers for a Misspelling and tidies them into the list shown to the user — dropping the word
/// itself, removing duplicates, and capping the count.
/// </summary>
public sealed class SpellingSuggestionsTests
{
    // A fake Dictionary whose Spelling Suggestions for a word are whatever the test supplies.
    private sealed class FakeDictionary(params string[] suggestions) : ISpellDictionary
    {
        public bool IsMisspelled(string word) => true;

        public IReadOnlyList<string> Suggest(string word) => suggestions;
    }

    [Fact]
    public void For_ReturnsTheDictionarySuggestions()
    {
        var dictionary = new FakeDictionary("invalid", "invalidate", "involved");

        var suggestions = SpellingSuggestions.For("Invld", dictionary);

        suggestions.ShouldBe(new[] { "invalid", "invalidate", "involved" });
    }

    [Fact]
    public void For_ExcludesTheMisspelledWordItself()
    {
        // A correctly-spelled word can echo back through the speller; never offer it as its own fix.
        var dictionary = new FakeDictionary("colour", "color");

        var suggestions = SpellingSuggestions.For("colour", dictionary);

        suggestions.ShouldBe(new[] { "color" });
    }

    [Fact]
    public void For_RemovesDuplicateSuggestions()
    {
        var dictionary = new FakeDictionary("colour", "Colour", "color");

        var suggestions = SpellingSuggestions.For("wrong", dictionary);

        suggestions.ShouldBe(new[] { "colour", "color" });
    }

    [Fact]
    public void For_CapsTheNumberOfSuggestions()
    {
        var dictionary = new FakeDictionary("a1", "a2", "a3", "a4", "a5");

        var suggestions = SpellingSuggestions.For("wrong", dictionary, maximum: 3);

        suggestions.ShouldBe(new[] { "a1", "a2", "a3" });
    }

    [Fact]
    public void For_WhenTheDictionaryOffersNothing_ReturnsEmpty()
    {
        var dictionary = new FakeDictionary();

        SpellingSuggestions.For("Invld", dictionary).ShouldBeEmpty();
    }
}
