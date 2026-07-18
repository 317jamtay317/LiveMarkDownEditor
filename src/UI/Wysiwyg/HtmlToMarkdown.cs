using ReverseMarkdown;

namespace UI.Wysiwyg;

/// <summary>
/// Converts an HTML fragment to Markdown, for Smart Paste: HTML copied from a web page pastes as clean
/// Markdown rather than as raw markup or plain text (INV-041). A pure function of the HTML.
/// </summary>
public static class HtmlToMarkdown
{
    // GitHub Flavored so pasted tables and strikethrough survive; unknown tags pass through (their
    // default), keeping the text of a div or span the conversion does not otherwise map.
    private static readonly Converter Converter = new(new Config { GithubFlavored = true });

    /// <summary>Converts the given HTML fragment to Markdown.</summary>
    /// <param name="html">The HTML fragment; the empty string for none.</param>
    /// <returns>The Markdown equivalent, trimmed of surrounding whitespace.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> is <see langword="null"/>.</exception>
    public static string Convert(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return Converter.Convert(html).Trim();
    }
}
