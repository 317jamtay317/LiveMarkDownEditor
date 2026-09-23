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

    /// <summary>
    /// Returns a new Recent Files list without <paramref name="path"/> (compared case-insensitively),
    /// the rest keeping their order. Used when a Recent File is known to be gone: it failed to open, or
    /// the user deleted it with Delete File (INV-081). A path not in the list changes nothing.
    /// </summary>
    /// <param name="path">The path to drop.</param>
    /// <returns>A new Recent Files list; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    public RecentFiles Remove(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        return new RecentFiles(
            [.. _paths.Where(existing => !string.Equals(existing, path, StringComparison.OrdinalIgnoreCase))]);
    }

    /// <summary>
    /// Returns a new Recent Files list with <paramref name="path"/> (compared case-insensitively)
    /// replaced by <paramref name="newPath"/> in the same place, because Rename File moved the file there
    /// (INV-082). The list stays distinct, so an entry already listed for <paramref name="newPath"/> is
    /// dropped in favour of the renamed one. A path not in the list changes nothing.
    /// </summary>
    /// <param name="path">The path the file had.</param>
    /// <param name="newPath">The path the file has now.</param>
    /// <returns>A new Recent Files list; this instance is unchanged.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="newPath"/> is null or blank.</exception>
    public RecentFiles Rename(string path, string newPath)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(newPath);

        if (!_paths.Any(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)))
        {
            return this;
        }

        return new RecentFiles(
        [
            .. _paths
                .Where(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)
                                   || !string.Equals(existing, newPath, StringComparison.OrdinalIgnoreCase))
                .Select(existing => string.Equals(existing, path, StringComparison.OrdinalIgnoreCase) ? newPath : existing),
        ]);
    }
}
