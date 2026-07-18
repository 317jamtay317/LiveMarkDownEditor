namespace Domain;

/// <summary>
/// A read-only summary of a document's prose: its word count, character count, and estimated reading
/// time. A value object — two summaries with the same counts are equal.
/// </summary>
/// <param name="WordCount">The number of words (whitespace-separated runs).</param>
/// <param name="CharacterCount">The number of characters, whitespace included.</param>
/// <param name="ReadingTime">The estimated time to read the prose at a fixed reading speed.</param>
public sealed record DocumentStatistics(int WordCount, int CharacterCount, TimeSpan ReadingTime)
{
    /// <summary>The statistics of empty text.</summary>
    public static DocumentStatistics Empty { get; } = new(0, 0, TimeSpan.Zero);
}
