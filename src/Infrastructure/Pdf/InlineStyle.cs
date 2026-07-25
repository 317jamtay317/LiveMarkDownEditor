using Markdig.Syntax.Inlines;

namespace Infrastructure.Pdf;

/// <summary>The accumulated inline formatting applied to a run of text.</summary>
/// <param name="Bold">Whether the run is bold.</param>
/// <param name="Italic">Whether the run is italic.</param>
/// <param name="Code">Whether the run is a Code Span, and so set monospaced.</param>
/// <param name="Link">Whether the run is a Link, and so colored and underlined.</param>
/// <param name="Superscript">Whether the run is raised, as a Footnote Reference is (INV-065).</param>
internal readonly record struct InlineStyle(
    bool Bold, bool Italic, bool Code, bool Link, bool Superscript = false)
{
    /// <summary>Returns this style with the formatting the given emphasis run adds.</summary>
    /// <param name="emphasis">The emphasis run being entered.</param>
    /// <returns>The style its children are written with.</returns>
    public InlineStyle WithEmphasis(EmphasisInline emphasis) => emphasis switch
    {
        // Strikethrough (~~) has no MigraDoc equivalent; its text is kept without the rule.
        { DelimiterChar: '~' } => this,
        { DelimiterCount: >= 2 } => this with { Bold = true },
        _ => this with { Italic = true },
    };
}
