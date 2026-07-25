namespace Domain;

/// <summary>
/// What a <see cref="CodeToken"/> is: the meaning the palette colors it by. The set is deliberately
/// small and language-independent — every Highlighting Language maps its own grammar onto these
/// kinds, so no language introduces a color of its own (INV-064).
/// </summary>
public enum CodeTokenKind
{
    /// <summary>Code that is none of the other kinds — identifiers, whitespace, punctuation a
    /// language draws no attention to. Shown in the ordinary code foreground.</summary>
    Plain = 0,

    /// <summary>A comment: <c>// like this</c>, <c>/* or this */</c>, <c>&lt;!-- or this --&gt;</c>.</summary>
    Comment = 1,

    /// <summary>A string or character literal, including its quotes and any escapes within it.</summary>
    String = 2,

    /// <summary>A numeric literal.</summary>
    Number = 3,

    /// <summary>A word the language reserves — <c>public</c>, <c>return</c>, <c>SELECT</c>.</summary>
    Keyword = 4,

    /// <summary>A type name, or the language's stand-in for one — a class name, an XML element
    /// name, a CSS selector.</summary>
    Type = 5,

    /// <summary>A function, method, or the language's stand-in for one — a built-in function, a
    /// shell command, an attribute or property name.</summary>
    Function = 6,

    /// <summary>An operator or structural delimiter the language gives weight to.</summary>
    Operator = 7,
}
