using System.Collections.Frozen;
using ColorCode.Common;
using Domain;

namespace Infrastructure.Markdown;

/// <summary>
/// Maps ColorCode's per-language scope names onto the seven language-independent Code Token Kinds
/// the palette colors (INV-064). Every grammar's own vocabulary lands here, so adding a language
/// never adds a color.
/// </summary>
internal static class CodeTokenKinds
{
    private static readonly FrozenDictionary<string, CodeTokenKind> ByScopeName = new Dictionary<string, CodeTokenKind>(StringComparer.Ordinal)
    {
        // Comments, in every dress each grammar puts them in. A C# doc comment's tags travel with
        // the comment rather than as markup, because on screen they read as one muted aside.
        [ScopeName.Comment] = CodeTokenKind.Comment,
        [ScopeName.XmlComment] = CodeTokenKind.Comment,
        [ScopeName.XmlDocComment] = CodeTokenKind.Comment,
        [ScopeName.XmlDocTag] = CodeTokenKind.Comment,
        [ScopeName.HtmlComment] = CodeTokenKind.Comment,

        // Literal text. An attribute's value and its quotes are string-like in exactly the way a
        // quoted literal is, so they take the same color.
        [ScopeName.String] = CodeTokenKind.String,
        [ScopeName.StringCSharpVerbatim] = CodeTokenKind.String,
        [ScopeName.StringEscape] = CodeTokenKind.String,
        [ScopeName.JsonString] = CodeTokenKind.String,
        [ScopeName.XmlAttributeValue] = CodeTokenKind.String,
        [ScopeName.XmlAttributeQuotes] = CodeTokenKind.String,
        [ScopeName.XmlCDataSection] = CodeTokenKind.String,
        [ScopeName.HtmlAttributeValue] = CodeTokenKind.String,
        [ScopeName.HtmlEntity] = CodeTokenKind.String,
        [ScopeName.CssPropertyValue] = CodeTokenKind.String,
        [ScopeName.MarkdownCode] = CodeTokenKind.String,

        [ScopeName.Number] = CodeTokenKind.Number,
        [ScopeName.JsonNumber] = CodeTokenKind.Number,

        // Reserved words, and the fixed values that behave like them (true, null, JSON's constants).
        [ScopeName.Keyword] = CodeTokenKind.Keyword,
        [ScopeName.ControlKeyword] = CodeTokenKind.Keyword,
        [ScopeName.PreprocessorKeyword] = CodeTokenKind.Keyword,
        [ScopeName.PseudoKeyword] = CodeTokenKind.Keyword,
        [ScopeName.JsonConst] = CodeTokenKind.Keyword,
        [ScopeName.BuiltinValue] = CodeTokenKind.Keyword,
        [ScopeName.MarkdownHeader] = CodeTokenKind.Keyword,
        [ScopeName.MarkdownBold] = CodeTokenKind.Keyword,
        [ScopeName.MarkdownEmph] = CodeTokenKind.Keyword,

        // Types, and each grammar's stand-in for the thing being named or declared: an XML element,
        // an HTML tag, a CSS selector.
        [ScopeName.Type] = CodeTokenKind.Type,
        [ScopeName.TypeVariable] = CodeTokenKind.Type,
        [ScopeName.ClassName] = CodeTokenKind.Type,
        [ScopeName.NameSpace] = CodeTokenKind.Type,
        [ScopeName.Predefined] = CodeTokenKind.Type,
        [ScopeName.Intrinsic] = CodeTokenKind.Type,
        [ScopeName.PowerShellType] = CodeTokenKind.Type,
        [ScopeName.XmlName] = CodeTokenKind.Type,
        [ScopeName.HtmlElementName] = CodeTokenKind.Type,
        [ScopeName.CssSelector] = CodeTokenKind.Type,

        // Callables, and the named-thing-applied-to-something that each grammar offers instead: an
        // attribute name, a CSS property, a JSON key.
        [ScopeName.Constructor] = CodeTokenKind.Function,
        [ScopeName.BuiltinFunction] = CodeTokenKind.Function,
        [ScopeName.SqlSystemFunction] = CodeTokenKind.Function,
        [ScopeName.PowerShellCommand] = CodeTokenKind.Function,
        [ScopeName.PowerShellAttribute] = CodeTokenKind.Function,
        [ScopeName.PowerShellParameter] = CodeTokenKind.Function,
        [ScopeName.Attribute] = CodeTokenKind.Function,
        [ScopeName.XmlAttribute] = CodeTokenKind.Function,
        [ScopeName.HtmlAttributeName] = CodeTokenKind.Function,
        [ScopeName.CssPropertyName] = CodeTokenKind.Function,
        [ScopeName.JsonKey] = CodeTokenKind.Function,

        // Operators and the structural marks a grammar gives weight to.
        [ScopeName.Operator] = CodeTokenKind.Operator,
        [ScopeName.Delimiter] = CodeTokenKind.Operator,
        [ScopeName.Brackets] = CodeTokenKind.Operator,
        [ScopeName.Continuation] = CodeTokenKind.Operator,
        [ScopeName.SpecialCharacter] = CodeTokenKind.Operator,
        [ScopeName.PowerShellOperator] = CodeTokenKind.Operator,
        [ScopeName.XmlDelimiter] = CodeTokenKind.Operator,
        [ScopeName.HtmlOperator] = CodeTokenKind.Operator,
        [ScopeName.HtmlTagDelimiter] = CodeTokenKind.Operator,
        [ScopeName.MarkdownListItem] = CodeTokenKind.Operator,

        // Named explicitly so they read as decisions rather than as gaps in the table: a variable
        // and a server-side script block are ordinary code, and plain text is plain by definition.
        [ScopeName.PowerShellVariable] = CodeTokenKind.Plain,
        [ScopeName.HtmlServerSideScript] = CodeTokenKind.Plain,
        [ScopeName.PlainText] = CodeTokenKind.Plain,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Maps a ColorCode scope name to the Code Token Kind that colors it.</summary>
    /// <param name="scopeName">The scope name ColorCode's grammar produced.</param>
    /// <returns>
    /// The matching Code Token Kind, or <see cref="CodeTokenKind.Plain"/> for a scope no entry
    /// claims — an unmapped scope shows as ordinary code, never as a crash or a stray color.
    /// </returns>
    internal static CodeTokenKind From(string? scopeName) =>
        scopeName is not null && ByScopeName.TryGetValue(scopeName, out var kind) ? kind : CodeTokenKind.Plain;
}
