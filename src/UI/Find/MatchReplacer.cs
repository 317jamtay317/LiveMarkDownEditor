using System.Windows.Documents;

namespace UI.Find;

/// <summary>
/// Swaps Matches for a Replacement. Unlike the rest of Find, which is view-only (INV-016), this is
/// the part that edits: it changes the Visual Document, which the editor Captures back into the
/// Markdown Document as canonical Markdown (INV-022).
/// </summary>
/// <remarks>
/// The Replacement is written verbatim into the Match's span, so it carries the formatting in effect
/// at the start of the Match and never adapts its case to the Match it replaces. This is the same
/// mechanism a Spelling Suggestion uses to correct a Misspelling.
/// </remarks>
public static class MatchReplacer
{
    /// <summary>
    /// Swaps <paramref name="match"/>'s span for <paramref name="replacement"/>. An empty
    /// Replacement deletes the Match.
    /// </summary>
    /// <param name="match">The Match to replace.</param>
    /// <param name="replacement">The Replacement text, inserted verbatim.</param>
    /// <remarks>
    /// Replacing needs no layout — it edits the document, not what is drawn — so a Match is replaced
    /// whether or not the editor has been rendered. Callers pass ranges from a scan of the current
    /// Visual Document, so there are no stale pointers to guard against.
    /// </remarks>
    public static void Replace(TextRange match, string? replacement)
    {
        ArgumentNullException.ThrowIfNull(match);

        match.Text = replacement ?? string.Empty;
    }

    /// <summary>
    /// Swaps every Match in <paramref name="matches"/> for <paramref name="replacement"/>. Each is
    /// replaced exactly once, so a Replacement that itself contains the query cannot cascade
    /// (INV-022).
    /// </summary>
    /// <param name="matches">The Matches to replace, as taken before the first edit.</param>
    /// <param name="replacement">The Replacement text, inserted verbatim.</param>
    /// <remarks>
    /// WPF <see cref="TextPointer"/>s track edits, so replacing one Match leaves the remaining
    /// Matches' ranges pointing at the right spans.
    /// </remarks>
    public static void ReplaceAll(IReadOnlyList<TextRange> matches, string? replacement)
    {
        ArgumentNullException.ThrowIfNull(matches);

        foreach (var match in matches)
        {
            Replace(match, replacement);
        }
    }
}
