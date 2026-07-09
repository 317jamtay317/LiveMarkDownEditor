namespace UI.Wysiwyg;

/// <summary>
/// The pure computation behind the Navigation Panel's Collapse/Expand. Given the Outline Entries as
/// their heading levels in document order and a parallel list of Collapsed flags, it decides which
/// entries lead nested entries and which are hidden because an ancestor Outline Entry is Collapsed. A
/// pure function of levels and flags — it holds no view state and never touches the editor, so
/// Collapse/Expand cannot change any document (INV-012).
/// </summary>
public static class OutlineView
{
    /// <summary>
    /// Whether the Outline Entry at <paramref name="index"/> leads nested Outline Entries — i.e. the
    /// next entry is of a deeper (lower) heading level, so the entry can be Collapsed.
    /// </summary>
    /// <param name="levels">The Outline Entries' heading levels (1–6) in document order.</param>
    /// <param name="index">The entry to test.</param>
    /// <returns><see langword="true"/> if the entry leads at least one nested entry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="levels"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside the sequence.</exception>
    public static bool HasNestedEntries(IReadOnlyList<int> levels, int index)
    {
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, levels.Count);

        return index + 1 < levels.Count && levels[index + 1] > levels[index];
    }

    /// <summary>
    /// Computes which Outline Entries are visible in the Navigation Panel: an entry is hidden when it
    /// is nested under a Collapsed ancestor (an earlier entry of a higher level whose Collapsed flag
    /// is set). A Collapsed flag on an entry that leads no nested entries has no effect.
    /// </summary>
    /// <param name="levels">The Outline Entries' heading levels (1–6) in document order.</param>
    /// <param name="collapsed">Each entry's Collapsed flag, parallel to <paramref name="levels"/>.</param>
    /// <returns>A parallel list where each element is whether that Outline Entry is visible.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the two lists differ in length.</exception>
    public static IReadOnlyList<bool> VisibleEntries(IReadOnlyList<int> levels, IReadOnlyList<bool> collapsed)
    {
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(collapsed);
        if (levels.Count != collapsed.Count)
        {
            throw new ArgumentException("Levels and collapsed flags must be the same length.", nameof(collapsed));
        }

        var visible = new bool[levels.Count];

        // The shallowest level currently collapsing its descendants, or null when nothing is hiding.
        // A collapse region ends at the first entry whose level is at or above (level number <=) the
        // collapsing level; a deeper collapse inside a hidden region is irrelevant because those
        // entries are already hidden.
        int? activeCollapseLevel = null;
        for (var index = 0; index < levels.Count; index++)
        {
            if (activeCollapseLevel is int hidingLevel && levels[index] > hidingLevel)
            {
                visible[index] = false;
                continue;
            }

            activeCollapseLevel = null;
            visible[index] = true;
            if (collapsed[index] && HasNestedEntries(levels, index))
            {
                activeCollapseLevel = levels[index];
            }
        }

        return visible;
    }
}
