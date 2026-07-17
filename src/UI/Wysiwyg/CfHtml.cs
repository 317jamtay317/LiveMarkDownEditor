using System.Text;

namespace UI.Wysiwyg;

/// <summary>
/// Wraps an HTML fragment in the Windows "HTML Format" (CF_HTML) the clipboard uses, so a copied
/// selection pastes as formatted HTML into web editors (Gmail, Google Docs, Slack). The format is a
/// header of byte offsets followed by the HTML; the offsets index into the UTF-8 bytes of the result.
/// </summary>
public static class CfHtml
{
    // The offset fields are fixed-width (D10), so formatting the real values never changes the
    // header's length — the offsets computed against the placeholder header stay correct.
    private const string Header =
        "Version:0.9\r\n" +
        "StartHTML:{0:D10}\r\n" +
        "EndHTML:{1:D10}\r\n" +
        "StartFragment:{2:D10}\r\n" +
        "EndFragment:{3:D10}\r\n";

    private const string DocumentStart = "<html><body>";
    private const string FragmentStart = "<!--StartFragment-->";
    private const string FragmentEnd = "<!--EndFragment-->";
    private const string DocumentEnd = "</body></html>";

    /// <summary>Wraps an HTML fragment as a CF_HTML clipboard string.</summary>
    /// <param name="htmlFragment">The HTML to place between the fragment markers.</param>
    /// <returns>The CF_HTML string, suitable for <c>DataFormats.Html</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="htmlFragment"/> is <see langword="null"/>.</exception>
    public static string Wrap(string htmlFragment)
    {
        ArgumentNullException.ThrowIfNull(htmlFragment);

        var startHtml = Utf8Length(string.Format(Header, 0, 0, 0, 0));
        var startFragment = startHtml + Utf8Length(DocumentStart + FragmentStart);
        var endFragment = startFragment + Utf8Length(htmlFragment);
        var endHtml = endFragment + Utf8Length(FragmentEnd + DocumentEnd);

        var header = string.Format(Header, startHtml, endHtml, startFragment, endFragment);
        return header + DocumentStart + FragmentStart + htmlFragment + FragmentEnd + DocumentEnd;
    }

    private static int Utf8Length(string text) => Encoding.UTF8.GetByteCount(text);
}
