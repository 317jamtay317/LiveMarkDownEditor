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

    /// <summary>A hard line break — captured as a trailing backslash before the newline.</summary>
    HardBreak,
}

/// <summary>
/// The Markdown semantic role carried by a block-level element of a Visual Document that has no
/// associated data, stored on its <c>Tag</c> so Capture can reproduce its Markdown syntax.
/// </summary>
internal enum BlockSemantic
{
    /// <summary>Block quote — captured with a <c>&gt; </c> prefix on every line.</summary>
    Quote,

    /// <summary>Thematic break (horizontal rule) — captured as <c>---</c>.</summary>
    ThematicBreak,
}

/// <summary>
/// The Markdown semantic role carried by a heading paragraph of a Visual Document, stored on its
/// <c>Tag</c> so that Capture can reproduce the heading's <c>#</c> prefix at the correct level.
/// </summary>
/// <param name="Level">The heading level, 1–6.</param>
internal sealed record HeadingRole(int Level);

/// <summary>
/// The role carried by a <see cref="System.Windows.Documents.Hyperlink"/> projected from a Markdown
/// link, so Capture can reproduce <c>[text](url)</c>.
/// </summary>
/// <param name="Url">The link's destination URL.</param>
/// <param name="Title">The optional link title, or <see langword="null"/>.</param>
internal sealed record LinkRole(string Url, string? Title);

/// <summary>
/// The role carried by a <see cref="System.Windows.Documents.Run"/> projected from a Markdown image,
/// so Capture can reproduce <c>![alt](url)</c>.
/// </summary>
/// <param name="Url">The image's source URL.</param>
/// <param name="Alt">The image's alt text.</param>
/// <param name="Title">The optional image title, or <see langword="null"/>.</param>
internal sealed record ImageRole(string Url, string Alt, string? Title);

/// <summary>
/// The role carried by a <see cref="System.Windows.Documents.Run"/> projected from a task-list
/// marker, so Capture can reproduce <c>[ ]</c> or <c>[x]</c>.
/// </summary>
/// <param name="Checked">Whether the task is checked.</param>
internal sealed record TaskMarkerRole(bool Checked);

/// <summary>
/// The role carried by a code-block paragraph of a Visual Document, so Capture can reproduce a fenced
/// code block. The code text itself is the paragraph's own inline content.
/// </summary>
/// <param name="Language">The fenced code block's info string (language), or <see langword="null"/>.</param>
internal sealed record CodeBlockRole(string? Language);

/// <summary>
/// The role carried by a <see cref="System.Windows.Documents.Table"/> projected from a Markdown
/// pipe table, recording each column's alignment so Capture can reproduce the delimiter row.
/// </summary>
/// <param name="Alignments">The alignment of each column, left to right.</param>
internal sealed record TableRole(IReadOnlyList<ColumnAlignment> Alignments);

/// <summary>The alignment of a Markdown table column, as declared by its delimiter-row colons.</summary>
internal enum ColumnAlignment
{
    /// <summary>No explicit alignment — captured as <c>---</c>.</summary>
    None,

    /// <summary>Left-aligned — captured as <c>:---</c>.</summary>
    Left,

    /// <summary>Centre-aligned — captured as <c>:---:</c>.</summary>
    Center,

    /// <summary>Right-aligned — captured as <c>---:</c>.</summary>
    Right,
}
