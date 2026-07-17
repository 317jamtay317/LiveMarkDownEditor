using System.Net;

namespace Domain;

/// <summary>
/// Composes a <see cref="RenderedOutput"/> into the <see cref="ExportShape"/> an Export as HTML
/// writes. Pure and deterministic: <see cref="Compose"/> is a function of its three inputs and
/// holds no state (INV-032).
/// </summary>
/// <remarks>
/// This is the one place the Standalone Page's wrapper lives. A Standalone Page is deliberately an
/// HTML Fragment <em>plus</em> a fixed wrapper — the two never differ in the content they carry — so
/// choosing an Export Shape is a choice of packaging and can never be a choice of document.
/// </remarks>
public static class HtmlExport
{
    /// <summary>
    /// The Standalone Page's embedded stylesheet. It echoes the editor's own palette so an exported
    /// page reads like the document the user was looking at, and it is embedded rather than linked
    /// so the file stands on its own when opened or emailed.
    /// </summary>
    private const string Stylesheet = """
            :root {
              color-scheme: light dark;
              --text: #1F2328;
              --muted: #6E7781;
              --background: #FFFFFF;
              --border: #E2E4E8;
              --accent: #4F46E5;
              --code-shading: rgba(0, 0, 0, 0.09);
            }
            @media (prefers-color-scheme: dark) {
              :root {
                --text: #E6E6E6;
                --muted: #9198A1;
                --background: #1B1D21;
                --border: #30343A;
                --accent: #A5B4FC;
                --code-shading: rgba(255, 255, 255, 0.09);
              }
            }
            body {
              margin: 0 auto;
              padding: 3rem 1.5rem;
              max-width: 46rem;
              background: var(--background);
              color: var(--text);
              font-family: "Segoe UI", system-ui, -apple-system, sans-serif;
              font-size: 16px;
              line-height: 1.6;
            }
            h1, h2, h3, h4, h5, h6 { line-height: 1.25; margin: 1.6em 0 0.6em; font-weight: 600; }
            h1 { font-size: 2em; }
            h2 { font-size: 1.5em; }
            h3 { font-size: 1.25em; }
            a { color: var(--accent); }
            code, pre { font-family: Consolas, "Cascadia Mono", monospace; font-size: 0.9em; }
            code { background: var(--code-shading); padding: 0.15em 0.35em; border-radius: 4px; }
            pre { background: var(--code-shading); padding: 1em; border-radius: 6px; overflow-x: auto; }
            pre code { background: none; padding: 0; }
            blockquote {
              margin: 1em 0;
              padding: 0 1em;
              border-left: 4px solid var(--border);
              color: var(--muted);
            }
            table { border-collapse: collapse; display: block; overflow-x: auto; }
            th, td { border: 1px solid var(--border); padding: 0.4em 0.8em; }
            th { background: var(--code-shading); }
            hr { border: none; border-top: 1px solid var(--border); margin: 2em 0; }
            img { max-width: 100%; }
            ul.contains-task-list { list-style: none; padding-left: 1.2em; }
        """;

    /// <summary>
    /// Composes the given Rendered Output into the chosen Export Shape.
    /// </summary>
    /// <param name="output">The Rendered Output to export. Carried verbatim by both Export Shapes.</param>
    /// <param name="shape">The Export Shape to write.</param>
    /// <param name="title">
    /// The title a Standalone Page carries. It comes from a file name the user chose, so it is
    /// HTML-escaped rather than trusted. Ignored by <see cref="ExportShape.HtmlFragment"/>.
    /// </param>
    /// <returns>The HTML to write to the export file.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="output"/> or <paramref name="title"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="shape"/> is not a defined <see cref="ExportShape"/>.
    /// </exception>
    public static string Compose(RenderedOutput output, ExportShape shape, string title)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(title);

        return shape switch
        {
            ExportShape.HtmlFragment => output.Html,
            ExportShape.StandalonePage => StandalonePage(output, title),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown Export Shape."),
        };
    }

    private static string StandalonePage(RenderedOutput output, string title) =>
        $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>{WebUtility.HtmlEncode(title)}</title>
        <style>
        {Stylesheet}
        </style>
        </head>
        <body>
        {output.Html}
        </body>
        </html>
        """;
}
