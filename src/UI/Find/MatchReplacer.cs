using System.Windows.Documents;

namespace UI.Find;

/// <summary>
/// Swaps Matches for a Replacement. Unlike the rest of Find, which is view-only (INV-016), this is
/// the part that edits: it changes the Visual Document, which the editor Captures back into the
/// Markdown Document as canonical Markdown (INV-022).
/// </summary>
/// <remarks>
/// The Replacement is written verbatim into the Match's span, never adapting its case to the Match it
/// replaces, and it inherits the Match's own formatting: replacing a word inside bold text leaves it
/// bold, while a Match straddling a formatting boundary has no single formatting to inherit and comes
/// out plain (INV-022). This is the same mechanism a Spelling Suggestion uses to correct a
/// Misspelling, which is what keeps the two consistent.
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

        var text = replacement ?? string.Empty;
        if (text.Length == 0 || !TryReplaceWithinRun(match, text))
        {
            match.Text = text;
        }
    }

    /// <summary>
    /// Replaces a Match that lies wholly inside one <see cref="Run"/> by inserting the Replacement into
    /// that Run and only then deleting the Match, so the Run — and every formatting element above it —
    /// is kept.
    /// </summary>
    /// <returns><see langword="true"/> when the Match was replaced this way.</returns>
    /// <remarks>
    /// Assigning <see cref="TextRange.Text"/> alone loses the formatting whenever the deletion empties
    /// the Run or leaves the insertion point on its first-character boundary: WPF removes an emptied Run
    /// along with the Bold, Italic, or Span above it, and on a boundary it resolves character formatting
    /// from the *preceding* content — so correcting a bolded word mid-sentence dropped it out of the
    /// bold. Inserting first keeps the Run non-empty throughout, which sidesteps both, and carries the
    /// formatting WPF cannot see as a property with it: a Code Span and a Strikethrough are marked by a
    /// role tag on the element, not by a value re-appliable afterwards.
    /// </remarks>
    private static bool TryReplaceWithinRun(TextRange match, string replacement)
    {
        if (match.Start.Parent is not Run run || !ReferenceEquals(run, match.End.Parent))
        {
            return false;
        }

        var start = run.ContentStart.GetOffsetToPosition(match.Start);
        var end = run.ContentStart.GetOffsetToPosition(match.End);
        if (start < 0 || end > run.Text.Length || end <= start)
        {
            return false;
        }

        // Insert at the Match's end, inside the Run: nothing is deleted yet, so the Run is certain to
        // still be there to take the text. Answering false before the insertion hands the whole
        // replacement back to the caller's TextRange assignment, which costs the formatting but never
        // the text.
        var matchStart = run.ContentStart.GetPositionAtOffset(start);
        if (matchStart is null)
        {
            return false;
        }

        match.End.InsertTextInRun(replacement);

        // The Replacement now sits in the Run from `end` onwards, so deleting the Match cannot empty
        // it. The Match's own end has moved over the inserted text, so measure the span to delete from
        // its start instead — an offset inside a Run is a character count, and the Run only grew.
        var matchEnd = matchStart.GetPositionAtOffset(end - start)!;
        new TextRange(matchStart, matchEnd).Text = string.Empty;
        return true;
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
