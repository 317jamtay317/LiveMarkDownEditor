using System.Windows;
using System.Windows.Documents;

namespace UI.Wysiwyg;

/// <summary>
/// The one shared definition of what a Definition List looks like in the Visual Document: each
/// Definition Term flush against the margin, with its Definition Descriptions indented beneath it
/// (INV-066). The Projector and the Toggle Definition List Formatting Action compose Definition Lists
/// through the same methods here, so Capture treats a loaded Definition List and a user-made one
/// identically (INV-018).
/// </summary>
internal static class DefinitionListFormatting
{
    /// <summary>Styles <paramref name="section"/> as a Definition List holding Terms and Descriptions.</summary>
    /// <param name="section">The section holding the Definition List's blocks.</param>
    internal static void ApplyList(Section section)
    {
        section.Tag = BlockSemantic.DefinitionList;
        section.Margin = BodySpacing;
    }

    /// <summary>
    /// Styles <paramref name="paragraph"/> as a Definition Term: flush against the margin, so the
    /// indented Descriptions beneath it read as belonging to it.
    /// </summary>
    /// <param name="paragraph">The paragraph holding the Term's inline content.</param>
    internal static void ApplyTerm(Paragraph paragraph)
    {
        paragraph.Tag = BlockSemantic.DefinitionTerm;
        paragraph.Margin = TermSpacing;
    }

    /// <summary>
    /// Composes a Definition Description: a section indented beneath its Terms, holding the
    /// Description's own blocks. It is a section rather than a paragraph because a Description may hold
    /// more than one block — a further paragraph, a List, a Code Block (INV-066).
    /// </summary>
    /// <param name="blocks">The Description's content; an empty one gets a paragraph to type into.</param>
    /// <returns>The Definition Description to place in the Definition List.</returns>
    internal static Section CreateDescription(IEnumerable<Block> blocks)
    {
        var description = new Section
        {
            Tag = BlockSemantic.DefinitionDescription,
            Margin = DescriptionSpacing,
        };

        foreach (var block in blocks)
        {
            description.Blocks.Add(block);
        }

        if (description.Blocks.Count == 0)
        {
            description.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
        }

        return description;
    }

    // A Term sits tight above its Descriptions; the whole list keeps the uniform body spacing.
    private static readonly Thickness BodySpacing = new(0, 0, 0, 6);
    private static readonly Thickness TermSpacing = new(0, 0, 0, 2);
    private static readonly Thickness DescriptionSpacing = new(24, 0, 0, 6);
}
