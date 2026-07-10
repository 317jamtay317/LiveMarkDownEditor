namespace UI.Find;

/// <summary>
/// One occurrence found by a Find: a contiguous span of the searched text equal to the query,
/// identified by its zero-based <paramref name="Start"/> offset and its <paramref name="Length"/>.
/// </summary>
/// <param name="Start">The zero-based offset of the Match within the searched text.</param>
/// <param name="Length">The number of characters the Match spans (the query's length).</param>
public readonly record struct Match(int Start, int Length);
