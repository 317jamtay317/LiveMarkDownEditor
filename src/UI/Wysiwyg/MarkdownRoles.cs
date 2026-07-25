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

    /// <summary>
    /// The Footnote Section — the block gathering every Footnote Definition at the end of the Visual
    /// Document. It is composed by Project rather than authored, and is never captured as a block: each
    /// Footnote Definition it holds is captured at its own authored position instead (INV-065).
    /// </summary>
    FootnoteSection,

    /// <summary>A Definition List — captured as its Definition Terms and Descriptions (INV-066).</summary>
    DefinitionList,

    /// <summary>A Definition Term — captured as its own line, flush (INV-066).</summary>
    DefinitionTerm,

    /// <summary>
    /// A Definition Description — captured with a leading <c>:</c> and three spaces, its continuation
    /// lines indented. It holds blocks rather than inlines, because a Description may be more than one
    /// block (INV-066).
    /// </summary>
    DefinitionDescription,
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
/// The role carried by the <see cref="System.Windows.Documents.BlockUIContainer"/> a Mermaid Diagram
/// is projected into, so Capture can reproduce the fenced ```mermaid``` Code Block. The diagram's
/// source is carried here rather than read back from a text surface, because the block shows the
/// diagram's rendered picture, not editable code (INV-047).
/// </summary>
/// <param name="Source">The Mermaid Diagram's source text.</param>
internal sealed record MermaidDiagramRole(string Source);

/// <summary>
/// The role carried by the superscript <see cref="System.Windows.Documents.Run"/> a Footnote Reference
/// is projected into, so Capture can reproduce <c>[^label]</c>. The Run's text is the Footnote Number,
/// which is presentation counted from reference order; the Footnote Label kept here is what Capture
/// writes, so a Footnote is never renumbered into the user's document (INV-065).
/// </summary>
/// <param name="Label">The Footnote Label, without its leading <c>^</c>.</param>
internal sealed record FootnoteReferenceRole(string Label);

/// <summary>
/// The role carried by the leading <see cref="System.Windows.Documents.Run"/> that shows a Footnote
/// Definition's Footnote Number in the Footnote Section. The Number is presentation, so Capture emits
/// nothing for this Run — only the text a user has typed into it (INV-065).
/// </summary>
/// <param name="Label">The Footnote Label of the Definition this Number belongs to.</param>
internal sealed record FootnoteNumberRole(string Label);

/// <summary>
/// The role carried by the <see cref="System.Windows.Documents.Section"/> holding one Footnote
/// Definition's blocks inside the Footnote Section, so Capture can reproduce <c>[^label]: </c> and
/// the note's content.
/// </summary>
/// <remarks>
/// The Footnote Section is shown at the end of the Visual Document, but a Definition is captured where
/// it was **authored** — so it remembers the top-level block it followed in the source. Capture emits
/// the Definition after that block; a Definition whose <paramref name="Anchor"/> is
/// <see langword="null"/> is emitted before every block, and one whose Anchor is no longer in the
/// document (the user deleted it) falls to the end rather than disappearing with it (INV-065).
/// </remarks>
/// <param name="Label">The Footnote Label, without its leading <c>^</c>.</param>
/// <param name="Anchor">The top-level Block this Definition was authored after, or
/// <see langword="null"/> when it was authored before any of them.</param>
internal sealed record FootnoteDefinitionRole(string Label, System.Windows.Documents.Block? Anchor);

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
