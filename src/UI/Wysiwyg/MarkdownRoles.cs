namespace UI.Wysiwyg;

/// <summary>
/// The Markdown semantic role carried by an inline element of a Visual Document, stored on its
/// <c>Tag</c> so that Capture can reproduce the exact Markdown syntax that produced it.
/// </summary>
internal enum InlineSemantic
{
    /// <summary>Inline code span — captured as <c>`code`</c>.</summary>
    Code,

    /// <summary>Strikethrough span — captured as <c>~~text~~</c>.</summary>
    Strikethrough,
}

/// <summary>
/// The Markdown semantic role carried by a heading paragraph of a Visual Document, stored on its
/// <c>Tag</c> so that Capture can reproduce the heading's <c>#</c> prefix at the correct level.
/// </summary>
/// <param name="Level">The heading level, 1–6.</param>
internal sealed record HeadingRole(int Level);
