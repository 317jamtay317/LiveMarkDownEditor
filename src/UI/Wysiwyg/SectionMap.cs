namespace UI.Wysiwyg;

/// <summary>
/// The Section Body owned by a Section Heading: the contiguous run of blocks that a Fold hides,
/// expressed as a position and length into the block sequence.
/// </summary>
/// <param name="Start">Index of the first Section Body block (the block after the Section Heading).</param>
/// <param name="Count">Number of blocks in the Section Body; zero when the Section has no body.</param>
public readonly record struct SectionBody(int Start, int Count);

/// <summary>
/// Computes Section boundaries over a sequence of blocks. A Section is a heading together with all
/// following blocks up to (but not including) the next heading of equal or higher level, so the
/// Section Body of a heading includes any nested lower-level subsections. This is a pure function of
/// the block levels — the foundation the WYSIWYG editor uses to Fold exactly a Section Body (INV-011).
/// </summary>
public static class SectionMap
{
    /// <summary>Computes the Section Body owned by the Section Heading at <paramref name="headingIndex"/>.</summary>
    /// <param name="blockLevels">
    /// The blocks in document order, each entry the heading level (1–6) of a heading block or
    /// <see langword="null"/> for a non-heading block.
    /// </param>
    /// <param name="headingIndex">The index of the Section Heading whose Section Body is wanted.</param>
    /// <returns>The <see cref="SectionBody"/> the heading owns.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blockLevels"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="headingIndex"/> is outside the sequence.</exception>
    /// <exception cref="ArgumentException">Thrown when the block at <paramref name="headingIndex"/> is not a heading.</exception>
    public static SectionBody FindBody(IReadOnlyList<int?> blockLevels, int headingIndex)
    {
        ArgumentNullException.ThrowIfNull(blockLevels);
        ArgumentOutOfRangeException.ThrowIfNegative(headingIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(headingIndex, blockLevels.Count);

        var headingLevel = blockLevels[headingIndex]
            ?? throw new ArgumentException("Only a Section Heading can be Folded.", nameof(headingIndex));

        var end = headingIndex + 1;
        while (end < blockLevels.Count && !OwnsBoundary(blockLevels[end], headingLevel))
        {
            end++;
        }

        return new SectionBody(headingIndex + 1, end - (headingIndex + 1));
    }

    // A block ends the Section Body when it is a heading of equal or higher level (level number <=).
    private static bool OwnsBoundary(int? level, int headingLevel) => level is int l && l <= headingLevel;
}
