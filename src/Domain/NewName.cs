namespace Domain;

/// <summary>
/// The rules a New Name must meet for Rename File, apart from whether the folder already holds it
/// (INV-082). A New Name is tidied as Windows would tidy it, must be a name Windows allows, and stays a
/// Markdown Document by keeping the File's own extension when it has no Markdown extension of its own.
/// Each name of an Entry Name, for New File and New Folder, meets the same rules (see <see cref="EntryName"/>).
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
    public static NameRefusal? Check(string? typed, string keptExtension, out string newName)
    {
        newName = Tidy(typed);
        if (Unusable(newName) is { } refusal)
        {
            return refusal;
        }

        if (!MarkdownFile.IsMarkdown(newName))
        {
            newName += keptExtension;
        }

        return ReservedOrNull(newName);
    }

    /// <summary>
    /// Why a tidied name is unusable before any extension is added: it is blank, or holds a character
    /// Windows forbids. Shared with <see cref="EntryName"/>, which checks each name of a path.
    /// </summary>
    /// <param name="name">A tidied name.</param>
    /// <returns>Why the name cannot be used, or <see langword="null"/> when it can.</returns>
    internal static NameRefusal? Unusable(string name)
    {
        if (name.Length == 0)
        {
            return NameRefusal.Blank;
        }

        return name.Any(character => char.IsControl(character) || Forbidden.Contains(character))
            ? NameRefusal.InvalidCharacter
            : null;
    }

    /// <summary>
    /// Whether Windows reserves the name for a device, whatever extension follows it: <c>CON.md</c> is as
    /// reserved as <c>CON</c>.
    /// </summary>
    /// <param name="name">A tidied name.</param>
    /// <returns><see cref="NameRefusal.ReservedName"/> for a reserved name; otherwise <see langword="null"/>.</returns>
    internal static NameRefusal? ReservedOrNull(string name) =>
        Reserved.Contains(name.Split('.')[0]) ? NameRefusal.ReservedName : null;

    /// <summary>Tidies a name as Windows would: spaces before and after it, and dots after it, are dropped.</summary>
    /// <param name="typed">The name as typed, or <see langword="null"/>.</param>
    /// <returns>The tidied name, empty when nothing is left.</returns>
    internal static string Tidy(string? typed)
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
