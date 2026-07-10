namespace UI.Spelling;

/// <summary>
/// Tidies the raw Spelling Suggestions a <see cref="ISpellDictionary"/> offers for a Misspelling into
/// the short, clean list shown to the user: it drops the Misspelling itself (a speller can echo the
/// input back), removes duplicates, and caps the count so the context menu stays manageable.
/// </summary>
public static class SpellingSuggestions
{
    // A right-click menu with more than a handful of corrections is unwieldy; keep the best few.
    private const int DefaultMaximum = 8;

    /// <summary>The Spelling Suggestions to offer for <paramref name="word"/>, in the Dictionary's order.</summary>
    /// <param name="word">The Misspelling to correct.</param>
    /// <param name="dictionary">The Dictionary that offers the Spelling Suggestions.</param>
    /// <param name="maximum">The most Spelling Suggestions to return.</param>
    /// <returns>The distinct suggested corrections (never the word itself), best first, capped at <paramref name="maximum"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dictionary"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> For(string word, ISpellDictionary dictionary, int maximum = DefaultMaximum)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        if (string.IsNullOrWhiteSpace(word))
        {
            return [];
        }

        return dictionary.Suggest(word)
            .Where(suggestion => !string.IsNullOrWhiteSpace(suggestion)
                && !string.Equals(suggestion, word, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maximum)
            .ToArray();
    }
}
