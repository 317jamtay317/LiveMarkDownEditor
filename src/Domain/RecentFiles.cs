namespace Domain;

/// <summary>
/// The Recent Files — the most-recently-used Watched File paths, newest first. A value object:
/// the list is distinct (compared case-insensitively), capped at <see cref="Capacity"/>, and never
/// holds a null or blank path. Adding a path already present promotes it to the front rather than
/// duplicating it, so the newest use always wins.
/// </summary>
public sealed class RecentFiles
{
    /// <summary>The most entries the list keeps; adding beyond this drops the oldest.</summary>
    public const int Capacity = 10;

    private readonly List<string> _paths;

    private RecentFiles(List<string> paths) => _paths = paths;

    /// <summary>The empty Recent Files list.</summary>
    public static RecentFiles Empty { get; } = new([]);

    /// <summary>The recent Watched File paths, newest first. Never <see langword="null"/>.</summary>
    public IReadOnlyList<string> Paths => _paths;

    /// <summary>
    /// Builds a Recent Files list from existing paths, given newest first. Null or blank entries are
    /// skipped, and the distinct/capacity rules of <see cref="Add"/> apply.
    /// </summary>
    /// <param name="paths">The candidate paths, newest first.</param>
    /// <returns>The resulting Recent Files list.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="paths"/> is <see langword="null"/>.</exception>
    public static RecentFiles From(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var result = Empty;
        // Add oldest first so the first (newest) input path ends up at the front.
        foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Reverse())
        {
            result = result.Add(path);
        }

        return result;
    }

    /// <summary>
    /// Returns a new Recent Files list with <paramref name="path"/> promoted to the front (newest).
    /// Any existing entry for the same path (case-insensitively) is removed first, and the list is
    /// trimmed to <see cref="Capacity"/>.
    /// </summary>
    /// <param name="path">The Watched File path most recently used.</param>
    /// <returns>A new Recent Files list; this instance is unchanged.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is null or blank.</exception>
    public RecentFiles Add(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var updated = new List<string>(_paths.Count + 1) { path };
        updated.AddRange(_paths.Where(existing => !string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)));

        if (updated.Count > Capacity)
        {
            updated.RemoveRange(Capacity, updated.Count - Capacity);
        }

        return new RecentFiles(updated);
    }
}
