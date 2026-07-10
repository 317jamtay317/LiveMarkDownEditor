namespace UI.Find;

/// <summary>
/// The pure text search behind Find: locates every occurrence of a query within a text snapshot and
/// computes wrap-around movement between the resulting Matches. It has no reference to the Visual
/// Document — it works on a plain-text snapshot and returns offsets — so finding can never change the
/// Markdown Document (INV-016).
/// </summary>
public static class MatchFinder
{
    /// <summary>
    /// Finds every occurrence of <paramref name="query"/> in <paramref name="text"/>, in document
    /// order, compared case-insensitively and never overlapping (the search resumes past the end of
    /// each Match).
    /// </summary>
    /// <param name="text">The text to search; a Match spans a run of it equal to the query.</param>
    /// <param name="query">The query to find; an empty or null query yields no Matches.</param>
    /// <returns>The Matches in ascending <see cref="Match.Start"/> order; empty when there are none.</returns>
    public static IReadOnlyList<Match> FindMatches(string? text, string? query)
    {
        var matches = new List<Match>();
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(query))
        {
            return matches;
        }

        var from = 0;
        while (from <= text.Length - query.Length)
        {
            var at = text.IndexOf(query, from, StringComparison.CurrentCultureIgnoreCase);
            if (at < 0)
            {
                break;
            }

            matches.Add(new Match(at, query.Length));
            from = at + query.Length;
        }

        return matches;
    }

    /// <summary>
    /// Moves a Current Match index by <paramref name="delta"/> positions over <paramref name="count"/>
    /// Matches, wrapping around either end so Find Next past the last Match returns to the first and
    /// Find Previous past the first returns to the last.
    /// </summary>
    /// <param name="index">The current index; <c>-1</c> when there is no Current Match.</param>
    /// <param name="delta">The step, typically <c>+1</c> (Find Next) or <c>-1</c> (Find Previous).</param>
    /// <param name="count">The number of Matches to move within.</param>
    /// <returns>The new index in <c>[0, count)</c>, or <c>-1</c> when there are no Matches.</returns>
    public static int Advance(int index, int delta, int count)
    {
        if (count <= 0)
        {
            return -1;
        }

        var start = index < 0 ? 0 : index;
        return ((start + delta) % count + count) % count;
    }
}
