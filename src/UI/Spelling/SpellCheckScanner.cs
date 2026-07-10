namespace UI.Spelling;

/// <summary>
/// Finds the misspelled words in a piece of text by segmenting it with the
/// <see cref="WordTokenizer"/> and keeping only the words an <see cref="ISpellDictionary"/> rejects.
/// Because segmentation is camelCase-aware, only the genuinely misspelled sub-word of a code-like
/// identifier is returned.
/// </summary>
public static class SpellCheckScanner
{
    // A lone letter is a loop variable or an initial, never a misspelling worth flagging.
    private const int MinimumWordLength = 2;

    /// <summary>Finds the misspelled words in <paramref name="text"/>, with their positions.</summary>
    /// <param name="text">The text to check. <see langword="null"/> is treated as empty.</param>
    /// <param name="dictionary">The dictionary that judges each word.</param>
    /// <returns>The misspelled words, in order, each carrying its offset within <paramref name="text"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dictionary"/> is <see langword="null"/>.</exception>
    public static IEnumerable<Word> FindMisspellings(string? text, ISpellDictionary dictionary)
    {
        ArgumentNullException.ThrowIfNull(dictionary);

        return WordTokenizer.Tokenize(text)
            .Where(word => word.Length >= MinimumWordLength && dictionary.IsMisspelled(word.Text));
    }
}
