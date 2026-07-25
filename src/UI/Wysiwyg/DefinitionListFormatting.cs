using System.Windows;
using System.Windows.Controls;
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

    /// <summary>
    /// The Toggle Definition List Formatting Action: the paragraphs the selection touches become a
    /// Definition List — the first a Definition Term, the rest its Definition Descriptions — or the
    /// selected Definition List's blocks become plain paragraphs again. Whole blocks are taken: a
    /// <c>:</c> prefix applies to a line, so defining part of a paragraph is not expressible in
    /// Markdown (INV-066).
    /// </summary>
    /// <param name="editor">The editor whose selection is being formatted.</param>
    internal static void Toggle(RichTextBox editor)
    {
        editor.BeginChange();
        try
        {
            if (DefinitionListAt(editor) is { } definitionList)
            {
                Undefine(editor.Document, definitionList);
                return;
            }

            Define(editor);
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// Whether Toggle Definition List can run: the selection sits on a top-level paragraph to define,
    /// or inside a Definition List to undefine. A Code Block is a top-level paragraph too, but its text
    /// is code — defining one would turn its first line into a Term.
    /// </summary>
    /// <param name="editor">The editor whose selection is queried.</param>
    internal static bool CanToggle(RichTextBox editor) =>
        DefinitionListAt(editor) is not null
        || (VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.Start) is Paragraph { Tag: not CodeBlockRole }
            && !FootnoteFormatting.IsInFootnoteSection(editor.Selection.Start));

    // Turns every top-level paragraph the selection touches into one Definition List in its place: the
    // first is the Term, the rest its Descriptions. A lone paragraph becomes a Term with one empty
    // Description, which is where the caret lands — there is nothing else to define it with yet.
    private static void Define(RichTextBox editor)
    {
        var document = editor.Document;
        var blocks = document.Blocks.ToList();
        var start = VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.Start);
        var end = VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.End, LogicalDirection.Backward);
        if (start is null || end is null)
        {
            return;
        }

        var startIndex = blocks.IndexOf(start);
        var endIndex = blocks.IndexOf(end);
        if (startIndex < 0 || endIndex < 0 || blocks[startIndex] is not Paragraph)
        {
            return;
        }

        // A Select All reaches the Footnote Section; taking it in would capture the notes inside the
        // glossary, so the range stops short of it (INV-065).
        endIndex = FootnoteFormatting.TrimToProse(blocks, startIndex, endIndex);
        if (endIndex < 0)
        {
            return;
        }

        var section = new Section();
        ApplyList(section);
        document.Blocks.InsertBefore(blocks[startIndex], section);

        // The paragraphs are moved rather than rebuilt, so their inline formatting survives — the rule
        // Toggle Block Quote is bound by (INV-028), reached for a Definition List.
        for (var i = startIndex; i <= endIndex; i++)
        {
            var block = blocks[i];
            document.Blocks.Remove(block);

            if (i == startIndex && block is Paragraph term)
            {
                ApplyTerm(term);
                section.Blocks.Add(term);
                continue;
            }

            section.Blocks.Add(CreateDescription([block]));
        }

        var caret = section.Blocks.Count > 1
            ? section.Blocks.LastBlock
            : AppendEmptyDescription(section);
        editor.Selection.Select(caret.ContentEnd, caret.ContentEnd);
    }

    // Moves a Definition List's Terms and Descriptions back out to the top level as plain paragraphs.
    private static void Undefine(FlowDocument document, Section definitionList)
    {
        foreach (var block in definitionList.Blocks.ToList())
        {
            definitionList.Blocks.Remove(block);

            if (block is Section { Tag: BlockSemantic.DefinitionDescription } description)
            {
                // A Description holds blocks, so its own content is what comes back out — not the
                // Section wrapping it, which is the Description itself and has no meaning outside one.
                foreach (var inner in description.Blocks.ToList())
                {
                    description.Blocks.Remove(inner);
                    inner.Margin = BodySpacing;
                    document.Blocks.InsertBefore(definitionList, inner);
                }

                continue;
            }

            if (block is Paragraph term)
            {
                term.Tag = null;
                term.Margin = BodySpacing;
            }

            document.Blocks.InsertBefore(definitionList, block);
        }

        document.Blocks.Remove(definitionList);
    }

    private static Block AppendEmptyDescription(Section section)
    {
        var description = CreateDescription([]);
        section.Blocks.Add(description);
        return description;
    }

    // The Definition List enclosing the selection's start, or null when it is not inside one.
    private static Section? DefinitionListAt(RichTextBox editor)
    {
        for (DependencyObject? node = editor.Selection.Start.Parent;
             node is TextElement element;
             node = element.Parent)
        {
            if (element is Section { Tag: BlockSemantic.DefinitionList } section)
            {
                return section;
            }
        }

        return null;
    }

    // A Term sits tight above its Descriptions; the whole list keeps the uniform body spacing.
    private static readonly Thickness BodySpacing = new(0, 0, 0, 6);
    private static readonly Thickness TermSpacing = new(0, 0, 0, 2);
    private static readonly Thickness DescriptionSpacing = new(24, 0, 0, 6);
}
