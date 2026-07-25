using System.Collections.Frozen;
using ColorCode;

namespace Infrastructure.Markdown;

/// <summary>
/// Resolves a Code Block's fence info string to the ColorCode grammar that tokenizes it — the
/// Highlighting Language of INV-064. An info string no grammar claims resolves to
/// <see langword="null"/>, which shows the Code Block as plain code.
/// </summary>
/// <remarks>
/// ColorCode already answers to a language's own name and its common short forms (<c>cs</c>,
/// <c>c#</c>, <c>py</c>, <c>ts</c>). The <see cref="Aliases"/> table adds the fence tags Markdown
/// authors actually write that ColorCode does not know — GitHub's <c>jsx</c>, <c>yml</c>, and the
/// like — so the same document highlights here as it does on GitHub. Tags for languages ColorCode
/// has no grammar for at all (<c>bash</c>, <c>yaml</c>, <c>go</c>, <c>rust</c>, and <c>mermaid</c>,
/// which is a picture rather than code) are deliberately absent: they stay plain.
/// </remarks>
internal static class HighlightingLanguages
{
    // Fence tags ColorCode does not resolve on its own, mapped to the grammar that fits them. Each
    // entry is a tag whose language ColorCode *does* have a grammar for under another name.
    private static readonly FrozenDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["jsx"] = "javascript",
        ["node"] = "javascript",
        ["mjs"] = "javascript",
        ["cjs"] = "javascript",
        ["tsx"] = "typescript",
        ["c"] = "cpp",
        ["h"] = "cpp",
        ["hpp"] = "cpp",
        ["cc"] = "cpp",
        ["c++"] = "cpp",
        ["objc"] = "cpp",
        ["fsharp"] = "f#",
        ["fs"] = "f#",
        ["csharp"] = "c#",
        ["ps"] = "powershell",
        ["ps1"] = "powershell",
        ["pwsh"] = "powershell",
        ["posh"] = "powershell",
        ["python3"] = "python",
        ["md"] = "markdown",
        ["htm"] = "html",
        ["xhtml"] = "html",
        ["xsl"] = "xml",
        ["xslt"] = "xml",
        ["xsd"] = "xml",
        ["csproj"] = "xml",
        ["svg"] = "xml",
        ["axml"] = "xml",
        ["plsql"] = "sql",
        ["tsql"] = "sql",
        ["mysql"] = "sql",
        ["postgres"] = "sql",
        ["postgresql"] = "sql",
        ["psql"] = "sql",
        ["jsonc"] = "json",
        ["json5"] = "json",
        ["vb"] = "vb.net",
        ["visualbasic"] = "vb.net",
        ["m"] = "matlab",
        ["hs"] = "haskell",
        ["f90"] = "fortran",
        ["f95"] = "fortran",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Resolves <paramref name="infoString"/> to the grammar that tokenizes it.</summary>
    /// <param name="infoString">
    /// The Code Block's fence info string, as the author wrote it. Matched case-insensitively and
    /// ignoring surrounding whitespace, because it is the user's text rather than a compiler's.
    /// </param>
    /// <returns>
    /// The grammar to tokenize with, or <see langword="null"/> when the Code Block has no
    /// Highlighting Language — no info string, or one no grammar claims.
    /// </returns>
    internal static ILanguage? Resolve(string? infoString)
    {
        var tag = infoString?.Trim();
        if (string.IsNullOrEmpty(tag))
        {
            return null;
        }

        // An info string may carry more than the language (```csharp title="a.cs"); the language is
        // its first word, which is the only part that says how to tokenize.
        var firstSpace = tag.IndexOfAny([' ', '\t', ',']);
        if (firstSpace > 0)
        {
            tag = tag[..firstSpace];
        }

        if (Aliases.TryGetValue(tag, out var aliased))
        {
            tag = aliased;
        }

        // FindById is documented to return null for an unknown id, but it has been known to throw on
        // odd input; either way an unrecognised tag means "no Highlighting Language", never a crash
        // in the middle of projecting the user's document.
        try
        {
            return Languages.FindById(tag);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
