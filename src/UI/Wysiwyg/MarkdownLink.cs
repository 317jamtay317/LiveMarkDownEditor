using System.IO;

namespace UI.Wysiwyg;

/// <summary>The kind of destination a Link points at, for a Ctrl+Click follow (INV-038).</summary>
public enum LinkKind
{
    /// <summary>Nothing to follow — an unsupported or unresolvable destination.</summary>
    None,

    /// <summary>A web address, opened in the default browser.</summary>
    Web,

    /// <summary>A Markdown file, opened in a new Tab.</summary>
    MarkdownFile,
}

/// <summary>A resolved Link destination: what kind it is and the value to act on.</summary>
/// <param name="Kind">The kind of destination.</param>
/// <param name="Value">The absolute URL (Web) or absolute file path (MarkdownFile); empty for None.</param>
public readonly record struct LinkTarget(LinkKind Kind, string Value)
{
    /// <summary>The "nothing to follow" target.</summary>
    public static LinkTarget None { get; } = new(LinkKind.None, string.Empty);
}

/// <summary>
/// Classifies a Link's destination for a Ctrl+Click follow: a web address opens in the browser, a
/// relative Markdown file opens in a new Tab, and anything else is left alone (INV-038).
/// </summary>
public static class MarkdownLink
{
    /// <summary>Classifies <paramref name="uri"/> against the document's Base Directory.</summary>
    /// <param name="uri">The Link's destination, as its <c>NavigateUri</c>.</param>
    /// <param name="baseDirectory">The folder relative paths resolve against, or null when unsaved.</param>
    /// <returns>The resolved <see cref="LinkTarget"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    public static LinkTarget Classify(Uri uri, string? baseDirectory)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (uri.IsAbsoluteUri &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto))
        {
            return new LinkTarget(LinkKind.Web, uri.AbsoluteUri);
        }

        var path = ResolveFilePath(uri, baseDirectory);
        return path is not null && IsMarkdown(path)
            ? new LinkTarget(LinkKind.MarkdownFile, path)
            : LinkTarget.None;
    }

    private static string? ResolveFilePath(Uri uri, string? baseDirectory)
    {
        if (uri.IsAbsoluteUri)
        {
            return uri.IsFile ? uri.LocalPath : null;
        }

        // A relative link names a file beside the Markdown Document; drop any #fragment first.
        var relative = uri.OriginalString;
        var fragment = relative.IndexOf('#');
        if (fragment >= 0)
        {
            relative = relative[..fragment];
        }

        relative = Uri.UnescapeDataString(relative);
        if (relative.Length == 0)
        {
            return null; // a pure fragment such as "#section" points nowhere to open
        }

        if (Path.IsPathRooted(relative))
        {
            return relative;
        }

        return string.IsNullOrWhiteSpace(baseDirectory)
            ? null
            : Path.GetFullPath(Path.Combine(baseDirectory, relative));
    }

    private static bool IsMarkdown(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase);
    }
}
