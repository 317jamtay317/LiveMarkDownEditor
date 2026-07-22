using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ReverseMarkdown;

namespace UI.Wysiwyg;

/// <summary>
/// Converts an HTML fragment to Markdown, for Smart Paste: HTML copied from a web page pastes as clean
/// Markdown rather than as raw markup or plain text (INV-041). A pure function of the HTML.
/// </summary>
/// <remarks>
/// Preformatted HTML — a <c>&lt;pre&gt;</c>, or any element declaring <c>white-space: pre</c>, which is
/// what a code editor puts on the clipboard — is whitespace-significant, so it converts to a fenced
/// Code Block holding its lines verbatim. HTML's ordinary whitespace rules would collapse a code
/// snippet's indentation away, and a Markdown paragraph cannot carry leading spaces either: a fence is
/// the only place indentation survives a Round-Trip (INV-041).
/// </remarks>
public static partial class HtmlToMarkdown
{
    /// <summary>Converts the given HTML fragment to Markdown.</summary>
    /// <param name="html">The HTML fragment; the empty string for none.</param>
    /// <returns>The Markdown equivalent, trimmed of surrounding whitespace.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="html"/> is <see langword="null"/>.</exception>
    public static string Convert(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        // Line endings are normalised because a Markdown Document is "\n"-separated throughout — the
        // Capturer joins its blocks with "\n" — and pasted source joins the same document.
        return Converter.Convert(WithPreformattedAsCode(html)).Replace("\r\n", "\n").Trim();
    }

    // Rewrites every outermost run of Preformatted HTML as a <pre> holding its lines as literal text,
    // which the converter then emits as a fenced Code Block. Reconstructing the lines here is what
    // keeps the indentation: a code editor writes one element per line and leans on white-space: pre to
    // hold the leading spaces, and both of those are gone by the time HTML's own whitespace collapsing
    // has run. HTML with nothing preformatted in it is handed on untouched.
    private static string WithPreformattedAsCode(string html)
    {
        var document = Parser.ParseDocument(html);
        var preformatted = document.All
            .Where(IsPreformatted)
            .Where(element => !element.Ancestors<IElement>().Any(IsPreformatted))
            .ToList();

        if (preformatted.Count == 0)
        {
            return html;
        }

        foreach (var element in preformatted)
        {
            var block = document.CreateElement("pre");
            block.TextContent = CodeIn(element);
            element.Replace(block);
        }

        return document.Body?.InnerHtml ?? html;
    }

    // Whether the element's content is whitespace-significant: a <pre>, or one styled white-space: pre.
    // "pre-wrap" is deliberately excluded — chat clients set it on ordinary prose, which is not code.
    private static bool IsPreformatted(IElement element) =>
        element.LocalName == "pre" || WhiteSpacePre().IsMatch(element.GetAttribute("style") ?? string.Empty);

    // The code an element holds, as lines. Trailing line breaks are dropped because the fence supplies
    // the final one; leading ones go too, since HTML drops the newline that follows a <pre> tag.
    private static string CodeIn(IElement element)
    {
        var builder = new StringBuilder();
        AppendCode(element, builder);
        return builder.ToString().Trim('\n');
    }

    // Walks the element's content in document order, taking text verbatim. Every element that lays its
    // content out as its own line ends one, so the lines a code editor writes as sibling <div>s stay
    // apart — the converter reads a <pre>'s text, not its markup, and would otherwise run them together.
    private static void AppendCode(INode node, StringBuilder builder)
    {
        foreach (var child in node.ChildNodes)
        {
            switch (child)
            {
                case { NodeType: NodeType.Text }:
                    // A non-breaking space is how HTML writes a space it means to keep; as code it is
                    // one column of indentation like any other.
                    builder.Append(child.TextContent.Replace('\u00a0', ' '));
                    break;
                case IElement { LocalName: "br" }:
                    builder.Append('\n');
                    break;
                case IElement element:
                    AppendCode(element, builder);
                    if (LineElements.Contains(element.LocalName))
                    {
                        builder.Append('\n');
                    }

                    break;
            }
        }
    }

    // GitHub Flavored so pasted tables and strikethrough survive; unknown tags pass through (their
    // default), keeping the text of a div or span the conversion does not otherwise map.
    private static readonly Converter Converter = new(new Config { GithubFlavored = true });

    private static readonly HtmlParser Parser = new();

    // The elements a code editor writes one of per line.
    private static readonly HashSet<string> LineElements = ["div", "p", "li", "tr"];

    [GeneratedRegex(@"white-space\s*:\s*pre\s*(;|$)", RegexOptions.IgnoreCase)]
    private static partial Regex WhiteSpacePre();
}
