namespace Domain;

/// <summary>
/// Computes the <see cref="DocumentStatistics"/> of a document's prose. A pure function of the text:
/// the same text always yields the same statistics (INV-039).
/// </summary>
public static class TextStatistics
{
    /// <summary>A commonly-used average adult reading speed for prose, in words per minute.</summary>
    public const double WordsPerMinute = 200.0;

    /// <summary>Computes the statistics of the given text.</summary>
    /// <param name="text">The prose to measure; the empty string for none.</param>
    /// <returns>The document's <see cref="DocumentStatistics"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static DocumentStatistics Compute(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var words = CountWords(text);
        var readingTime = words == 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(words / WordsPerMinute);
        return new DocumentStatistics(words, text.Length, readingTime);
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                count++;
            }
        }

        return count;
    }
}
