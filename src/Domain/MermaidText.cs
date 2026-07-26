using System.Text;
using System.Text.RegularExpressions;

namespace Domain;

/// <summary>
/// The text rules every Diagram Kind's Mermaid format shares: quoting a label so it survives a
/// Round-Trip, and encoding the few characters that would otherwise end the field a label sits in
/// (INV-051). Mermaid's own entity codes are used — <c>#quot;</c> for a quotation mark and
/// <c>#&lt;code&gt;;</c> for anything else — so the encoded source still renders as the author's text.
/// Pure: no I/O, no state.
/// </summary>
internal static partial class MermaidText
{
    /// <summary>Wraps <paramref name="label"/> in quotation marks, encoding any it contains.</summary>
    /// <param name="label">The Node Label or Edge Label to quote.</param>
    /// <returns>The quoted, encoded label.</returns>
    public static string Quote(string label) => "\"" + label.Replace("\"", "#quot;") + "\"";

    /// <summary>
    /// The inverse of <see cref="Quote"/>: strips one surrounding pair of quotation marks, if any, and
    /// decodes Mermaid's entity codes.
    /// </summary>
    /// <param name="text">The raw text read from the source.</param>
    /// <returns>The label it stands for.</returns>
    public static string Unquote(string text)
    {
        text = text.Trim();
        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            text = text.Substring(1, text.Length - 2);
        }

        return Decode(text);
    }

    /// <summary>
    /// Encodes each of <paramref name="reserved"/> as its Mermaid entity code, so a label may hold a
    /// character that would otherwise end the field it is written in (a State's <c>:</c>, say).
    /// </summary>
    /// <param name="text">The label to encode.</param>
    /// <param name="reserved">The characters this field cannot carry literally.</param>
    /// <returns>The encoded label.</returns>
    public static string Encode(string text, string reserved)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (character == '"')
            {
                builder.Append("#quot;");
            }
            else if (reserved.Contains(character, StringComparison.Ordinal))
            {
                builder.Append('#').Append((int)character).Append(';');
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    /// <summary>Decodes Mermaid's entity codes — the inverse of <see cref="Encode"/>.</summary>
    /// <param name="text">The raw text read from the source.</param>
    /// <returns>The label it stands for.</returns>
    public static string Decode(string text) =>
        NumericEntityPattern().Replace(text.Replace("#quot;", "\""), match =>
            int.TryParse(match.Groups["code"].Value, out var code) &&
            code is > 0 and < 0x110000 and (< 0xD800 or > 0xDFFF)
                ? char.ConvertFromUtf32(code)
                : match.Value);

    /// <summary>
    /// The source's lines, trimmed and with the blank ones dropped — every Diagram Kind's format reads
    /// a Mermaid diagram one significant line at a time.
    /// </summary>
    /// <param name="source">The Mermaid source to split.</param>
    /// <returns>The significant lines, in order.</returns>
    public static List<string> Lines(string source) =>
    [
        .. source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0),
    ];

    [GeneratedRegex(@"#(?<code>\d{1,6});")]
    private static partial Regex NumericEntityPattern();
}
