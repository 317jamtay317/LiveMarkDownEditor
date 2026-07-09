using System.Windows.Documents;

namespace UI.Controls;

/// <summary>
/// One Section Heading as it appears in a <see cref="MarkdownRichEditor.Outline"/>: an Outline Entry
/// carrying the heading's level and text. Activating it Navigates the editor to the heading. The
/// Outline lists every Section Heading — including ones inside a Folded Section Body — so a
/// <see cref="SectionHeading"/> may reference a block that is currently hidden.
/// </summary>
public sealed class SectionHeading
{
    /// <summary>Creates an Outline Entry for a Section Heading block.</summary>
    /// <param name="level">The heading level, 1–6.</param>
    /// <param name="text">The heading's plain text, shown as the Outline Entry's label.</param>
    /// <param name="block">The Visual Document block the heading is, used to Navigate to it.</param>
    internal SectionHeading(int level, string text, Block block)
    {
        Level = level;
        Text = text;
        Block = block;
    }

    /// <summary>The heading level, 1–6 — used to indent the Outline Entry by document depth.</summary>
    public int Level { get; }

    /// <summary>The heading's plain text, shown as the Outline Entry's label.</summary>
    public string Text { get; }

    /// <summary>
    /// The Visual Document block this heading is. Retained across Folds so Navigation can reveal and
    /// select it even while it is hidden inside a Folded Section Body.
    /// </summary>
    internal Block Block { get; }
}
