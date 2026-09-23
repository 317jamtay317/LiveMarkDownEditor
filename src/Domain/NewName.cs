namespace Domain;

/// <summary>
/// The rules a New Name must meet for Rename File, apart from whether the folder already holds it
/// (INV-082). A New Name is tidied as Windows would tidy it, must be a name Windows allows, and stays a
/// Markdown Document by keeping the File's own extension when it has no Markdown extension of its own.
/// </summary>
internal static class NewName
{
    // The characters Windows forbids in a file name, besides the control characters.
    private static readonly char[] Forbidden = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    private static readonly HashSet<string> Reserved = new(
        ["CON", "PRN", "AUX", "NUL", .. Numbered("COM"), .. Numbered("LPT")],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Tidies <paramref name="typed"/> into a New Name and checks it. Spaces before and after it, and
    /// dots after it, are dropped. A name that is otherwise usable but has no Markdown extension gets
    /// <paramref name="keptExtension"/>, so <c>Ideas</c> becomes <c>Ideas.md</c>.
    /// </summary>
    /// <param name="typed">The name the user typed, or <see langword="null"/>.</param>
    /// <param name="keptExtension">The File's own extension, including its dot (e.g. <c>.md</c>).</param>
    /// <param name="newName">The tidied New Name, which is empty when the name was blank.</param>
    /// <returns>Why the New Name cannot be used, or <see langword="null"/> when it can.</returns>
    public static RenameRefusal? Check(string? typed, string keptExtension, out string newName)
    {
        newName = Tidy(typed);
        if (newName.Length == 0)
        {
            return RenameRefusal.Blank;
        }

        if (newName.Any(character => char.IsControl(character) || Forbidden.Contains(character)))
        {
            return RenameRefusal.InvalidCharacter;
        }

        if (!MarkdownFile.IsMarkdown(newName))
        {
            newName += keptExtension;
        }

        // Windows reserves a device name whatever extension follows it: CON.md is as reserved as CON.
        var stem = newName.Split('.')[0];
        return Reserved.Contains(stem) ? RenameRefusal.ReservedName : null;
    }

    private static string Tidy(string? typed)
    {
        var name = (typed ?? string.Empty).Trim();
        while (name.EndsWith('.'))
        {
            name = name.TrimEnd('.').TrimEnd();
        }

        return name;
    }

    private static IEnumerable<string> Numbered(string device) =>
        Enumerable.Range(1, 9).Select(number => $"{device}{number}");
}
