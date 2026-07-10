namespace UI.Spelling;

/// <summary>
/// A dictionary that decides whether a single word is misspelled. The port the spell checker depends
/// on; an adapter backs it with a real dictionary (the operating system's speller).
/// </summary>
public interface ISpellDictionary
{
    /// <summary>Whether <paramref name="word"/> is not a recognised word.</summary>
    /// <param name="word">A single word (no surrounding punctuation).</param>
    /// <returns><see langword="true"/> if the word is misspelled; otherwise <see langword="false"/>.</returns>
    bool IsMisspelled(string word);

    /// <summary>The Spelling Suggestions the Dictionary offers as replacements for <paramref name="word"/>.</summary>
    /// <param name="word">A single word (no surrounding punctuation), usually a Misspelling.</param>
    /// <returns>
    /// The suggested corrections, best first; empty when the Dictionary offers none or is unavailable.
    /// </returns>
    IReadOnlyList<string> Suggest(string word);
}
