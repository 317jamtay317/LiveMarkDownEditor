namespace UI.Spelling;

/// <summary>
/// Segments text into spell-checkable words. Unlike a plain whitespace split, it breaks on all
/// non-letter characters <em>and</em> on camelCase boundaries, so a code-like identifier such as
/// <c>this.ShouldBe().Invld()</c> yields the sub-words <c>this</c>, <c>Should</c>, <c>Be</c>,
/// <c>Invld</c> — each of which can be checked against the dictionary on its own. That is what lets
/// spell check flag only a genuinely misspelled part instead of the whole identifier.
/// </summary>
public static class WordTokenizer
{
    /// <summary>Segments <paramref name="text"/> into words, in order, with their positions.</summary>
    /// <param name="text">The text to segment. <see langword="null"/> is treated as empty.</param>
    /// <returns>The words found, each carrying its start offset and length within <paramref name="text"/>.</returns>
    public static IEnumerable<Word> Tokenize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        var index = 0;
        while (index < text.Length)
        {
            if (!IsWordCharacter(text, index))
            {
                index++;
                continue;
            }

            // Consume a maximal run of letters (apostrophes count only between letters, so
            // contractions like "don't" stay whole), then split that run at camelCase boundaries.
            var runStart = index;
            while (index < text.Length && IsWordCharacter(text, index))
            {
                index++;
            }

            foreach (var word in SplitCamelCase(text, runStart, index))
            {
                yield return word;
            }
        }
    }

    // A word character is a letter, or an apostrophe that sits between two letters (a contraction).
    private static bool IsWordCharacter(string text, int index)
    {
        var c = text[index];
        if (char.IsLetter(c))
        {
            return true;
        }

        if (c is '\'' or '’')
        {
            return index > 0 && char.IsLetter(text[index - 1])
                && index + 1 < text.Length && char.IsLetter(text[index + 1]);
        }

        return false;
    }

    // Splits [start, end) into sub-words at camelCase boundaries: a lower→upper transition
    // (should|Be), and the end of an acronym that runs into a word (XML|Http).
    private static IEnumerable<Word> SplitCamelCase(string text, int start, int end)
    {
        var wordStart = start;
        for (var i = start + 1; i < end; i++)
        {
            var previous = text[i - 1];
            var current = text[i];

            var lowerToUpper = char.IsLower(previous) && char.IsUpper(current);
            var acronymBoundary = char.IsUpper(previous) && char.IsUpper(current)
                && i + 1 < end && char.IsLower(text[i + 1]);

            if (lowerToUpper || acronymBoundary)
            {
                yield return new Word(text[wordStart..i], wordStart, i - wordStart);
                wordStart = i;
            }
        }

        yield return new Word(text[wordStart..end], wordStart, end - wordStart);
    }
}
