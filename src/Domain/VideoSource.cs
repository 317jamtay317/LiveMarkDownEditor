namespace Domain;

/// <summary>
/// The one decision of what a Video Source is: a Media Source naming a video file (INV-069).
/// </summary>
/// <remarks>
/// A Video is written in Markdown with the Image's own syntax — <c>![alt](clip.mp4)</c> — so the Media
/// Source is the only thing that <em>can</em> tell a Video from an Image. This is deliberately a
/// judgement about the source text and nothing else: it never opens the file, so the projection and the
/// render reach the same answer, and reach it before anything has been read from disk (INV-003). Pure
/// and shared, so the editing surface and the Rendered Output can never disagree about which is which.
/// </remarks>
public static class VideoSource
{
    /// <summary>
    /// The file extensions a Video Source is recognised by, each with its leading dot. They are stated
    /// rather than guessed at — what counts as a Video is a reviewable list, not a heuristic.
    /// </summary>
    public static IReadOnlyList<string> Extensions { get; } =
        [".mp4", ".webm", ".mov", ".m4v", ".mkv", ".avi", ".ogv", ".wmv"];

    /// <summary>
    /// Whether <paramref name="url"/> is a Video Source — a Media Source whose file name ends in one of
    /// the <see cref="Extensions"/>, compared case-insensitively and looking past any query or fragment.
    /// </summary>
    /// <param name="url">The Media Source to classify. <see langword="null"/> and blank name nothing.</param>
    /// <returns><see langword="true"/> when it names a video; otherwise <see langword="false"/>.</returns>
    public static bool IsVideo(string? url)
    {
        var extension = ExtensionOf(url);
        return extension.Length > 0
               && Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    // The extension of the file a Media Source names, with its dot, or the empty string when it names
    // no file with one. A query or fragment is dropped first — neither is part of the file's name, and
    // a URL that merely mentions `demo.mp4` in its query is not a video ("/watch?file=demo.mp4").
    private static string ExtensionOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        var path = url.Trim();
        var end = path.IndexOfAny(['?', '#']);
        if (end >= 0)
        {
            path = path[..end];
        }

        // The dot has to belong to the last segment: a folder named "video.mp4" holding "poster.png"
        // names a picture.
        var lastSeparator = path.LastIndexOfAny(['/', '\\']);
        var dot = path.LastIndexOf('.');
        return dot > lastSeparator + 1 && dot < path.Length - 1 ? path[dot..] : string.Empty;
    }
}
