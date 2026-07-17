using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace UI.Wysiwyg;

/// <summary>
/// The Set Heading Level Formatting Action, and the one shared definition of what a Heading looks
/// like in the Visual Document. The Projector styles a Heading through the same
/// <see cref="ApplyHeading"/> method, so Capture treats a user-made Heading and a loaded one
/// uniformly (INV-018). Set Heading Level relevels the caret's existing paragraph in place, so its
/// content and inline formatting survive the change (INV-027).
/// </summary>
internal static class HeadingFormatting
{
    /// <summary>
    /// The <see cref="SetLevel"/> level that means "not a Heading" — the Heading Level Picker's
    /// Paragraph choice, which turns a Heading back into a plain paragraph.
    /// </summary>
    internal const int ParagraphLevel = 0;

    /// <summary>The lowest Heading Level; <c>#</c> repeats once per level.</summary>
    internal const int MinLevel = 1;

    /// <summary>The highest Heading Level — a seventh <c>#</c> is not a heading in Markdown at all.</summary>
    internal const int MaxLevel = 6;

    /// <summary>
    /// Styles <paramref name="paragraph"/> as a Heading at <paramref name="level"/>, exactly as the
    /// Projector does. A Heading is distinguished by size alone and never by weight: an inherited
    /// bold weight would make Capture read every Heading run as inline-bold (<c># **x**</c>).
    /// </summary>
    /// <param name="paragraph">The paragraph holding the Heading's inline content.</param>
    /// <param name="level">The Heading Level, <see cref="MinLevel"/>–<see cref="MaxLevel"/>.</param>
    internal static void ApplyHeading(Paragraph paragraph, int level)
    {
        paragraph.Tag = new HeadingRole(level);
        paragraph.FontSize = HeadingFontSize(level);
        paragraph.Margin = HeadingSpacing;
    }

    /// <summary>
    /// The Set Heading Level Formatting Action: makes the block at the editor's caret a Heading of
    /// <paramref name="level"/>, or — given <see cref="ParagraphLevel"/> — a plain paragraph again.
    /// It sets rather than toggles, so choosing a Heading's current level leaves it unchanged, and a
    /// level outside the supported range is ignored rather than written (INV-027).
    /// </summary>
    /// <param name="editor">The editor whose caret's block is being relevelled.</param>
    /// <param name="level">The Heading Level to set, or <see cref="ParagraphLevel"/> for Paragraph.</param>
    internal static void SetLevel(RichTextBox editor, int level)
    {
        if (level is not ParagraphLevel and (< MinLevel or > MaxLevel))
        {
            return;
        }

        if (ParagraphAt(editor) is not { } paragraph)
        {
            return;
        }

        editor.BeginChange();
        try
        {
            // The paragraph is relevelled in place rather than re-created from its text, so its
            // inline formatting cannot be flattened by a change of level (INV-027).
            if (level is ParagraphLevel)
            {
                ClearHeading(paragraph);
            }
            else
            {
                ApplyHeading(paragraph, level);
            }
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// Whether Set Heading Level can run: the caret sits in a paragraph that can carry a Heading
    /// Level. A Heading is a top-level block, so a paragraph nested in a List Item or a Table cell
    /// is not one.
    /// </summary>
    /// <param name="editor">The editor whose caret is queried.</param>
    internal static bool CanSetLevel(RichTextBox editor) => ParagraphAt(editor) is not null;

    /// <summary>
    /// The Heading Level of the block at the editor's caret, or <see cref="ParagraphLevel"/> when it
    /// is not a Heading. Lets the Heading Level Picker show which level the caret is on.
    /// </summary>
    /// <param name="editor">The editor whose caret is queried.</param>
    internal static int LevelAt(RichTextBox editor) =>
        ParagraphAt(editor)?.Tag is HeadingRole role ? role.Level : ParagraphLevel;

    // Reverts a Heading to a plain body paragraph; its inlines stay exactly as they are.
    private static void ClearHeading(Paragraph paragraph)
    {
        paragraph.Tag = null;
        paragraph.ClearValue(TextElement.FontSizeProperty);
        paragraph.Margin = BodySpacing;
    }

    // The top-level prose paragraph holding the caret, or null when the caret is not in one. Only a
    // top-level paragraph can be a Heading: a List Item's or a Table cell's paragraph cannot. A Code
    // Block is a top-level paragraph too, but its text is code — relevelling one into a Heading would
    // turn its first line into prose.
    private static Paragraph? ParagraphAt(RichTextBox editor) =>
        VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.Start)
            is Paragraph { Tag: not CodeBlockRole } paragraph
            ? paragraph
            : null;

    private static double HeadingFontSize(int level) => level switch
    {
        1 => 28,
        2 => 24,
        3 => 20,
        4 => 17,
        5 => 15,
        _ => 13,
    };

    // The uniform spacing the Projector gives Headings and body blocks.
    private static readonly Thickness HeadingSpacing = new(0, 12, 0, 4);
    private static readonly Thickness BodySpacing = new(0, 0, 0, 6);
}
