using System.Globalization;
using System.Windows.Controls;
using System.Windows.Documents;

namespace UI.Wysiwyg;

/// <summary>
/// The Insert Footnote Formatting Action: it cites a new Footnote at the caret — a Footnote Reference
/// there, an empty Footnote Definition in the Footnote Section — and leaves the caret in the Definition,
/// ready for the note (INV-065). It composes both halves through <see cref="FootnoteProjection"/>, the
/// same seam the Projector uses, so a user-cited Footnote and a loaded one are identical to Capture
/// (INV-018).
/// </summary>
internal static class FootnoteFormatting
{
    /// <summary>
    /// Cites a new Footnote at the editor's caret. The Footnote Label is the lowest number not already
    /// a Label in the document, so the new Reference always has a Definition to match — a Reference
    /// without one is not a Footnote at all (INV-065).
    /// </summary>
    /// <param name="editor">The editor whose caret is citing the Footnote.</param>
    internal static void Insert(RichTextBox editor)
    {
        // A citation marks the phrase it follows; it never replaces it, so a selection is cited at its
        // end rather than swallowed.
        var caret = editor.Selection.End;
        if (!CanInsert(editor)
            || VisualDocumentTraversal.TopLevelBlockOf(caret, LogicalDirection.Backward) is not { } anchor
            || VisualDocumentTraversal.AncestorOf<Paragraph>(caret) is not { } paragraph)
        {
            return;
        }

        editor.BeginChange();
        try
        {
            var label = NextLabel(editor.Document);
            InsertReference(paragraph, caret, FootnoteProjection.CreateReference(
                label, NextNumber(editor.Document)));

            var section = FootnoteSectionOf(editor.Document) ?? AppendSection(editor.Document);
            var definition = FootnoteProjection.CreateDefinition(label, NumberIn(section), anchor, []);
            section.Blocks.Add(definition);

            // The caret lands in the note, so the first thing typed is the note (INV-065).
            editor.Selection.Select(definition.ContentEnd, definition.ContentEnd);
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// Whether Insert Footnote can run: the caret sits in prose that can carry a Footnote Reference. A
    /// Code Block's text is code, so a Reference placed there would be code rather than a citation, and
    /// the Footnote Section holds notes rather than the prose that cites them.
    /// </summary>
    /// <param name="editor">The editor whose caret is queried.</param>
    internal static bool CanInsert(RichTextBox editor) =>
        VisualDocumentTraversal.AncestorOf<Paragraph>(editor.Selection.Start) is { Tag: not CodeBlockRole }
        && !IsInFootnoteSection(editor.Selection.Start);

    // The Footnote Section of a Visual Document, or null when it has no Footnotes yet.
    internal static Section? FootnoteSectionOf(FlowDocument document) =>
        document.Blocks.OfType<Section>()
            .FirstOrDefault(block => block.Tag is BlockSemantic.FootnoteSection);

    // Appends a new Footnote Section for a document citing its first Footnote.
    private static Section AppendSection(FlowDocument document)
    {
        var section = FootnoteProjection.CreateEmptySection();
        document.Blocks.Add(section);
        return section;
    }

    // Places the Reference at the caret. Plain text is split so the citation lands exactly where the
    // caret was; inside an inline span — bold, italic, a Link — it goes **beside** the span instead. A
    // citation marks the phrase, not the emphasis carrying it, and `[text[^1]](url)` is not a link at
    // all: a Reference inside a Link's text would break the Link rather than cite it.
    private static void InsertReference(Paragraph paragraph, TextPointer caret, Inline reference)
    {
        if (OutermostInline(paragraph, caret) is not { } outer)
        {
            paragraph.Inlines.Add(reference);
            return;
        }

        if (outer is Run run)
        {
            var offset = run.ContentStart.GetOffsetToPosition(caret);
            if (offset > 0 && offset < run.Text.Length)
            {
                var tail = new Run(run.Text[offset..]);
                run.Text = run.Text[..offset];
                paragraph.Inlines.InsertAfter(run, tail);
                paragraph.Inlines.InsertBefore(tail, reference);
                return;
            }
        }

        if (caret.CompareTo(outer.ContentStart) <= 0)
        {
            paragraph.Inlines.InsertBefore(outer, reference);
            return;
        }

        paragraph.Inlines.InsertAfter(outer, reference);
    }

    // The inline sitting directly in `paragraph` that holds the caret, or null when the caret is in no
    // inline at all (an empty paragraph). Walking out to the paragraph's own child is what keeps the
    // insertion valid: a nested run's sibling collection belongs to its span, not to the paragraph.
    private static Inline? OutermostInline(Paragraph paragraph, TextPointer caret)
    {
        Inline? outermost = null;
        for (System.Windows.DependencyObject? node = caret.Parent;
             node is TextElement element;
             node = element.Parent)
        {
            if (element is Inline inline)
            {
                outermost = inline;
            }

            if (ReferenceEquals(element.Parent, paragraph))
            {
                break;
            }
        }

        return outermost;
    }

    // The lowest number not already a Footnote Label in the document. Numbers are what an author writes
    // when they do not care what the label is, so a new Footnote takes the first free one rather than
    // asking the user about bookkeeping (INV-065).
    private static string NextLabel(FlowDocument document)
    {
        var used = UsedLabels(document);
        for (var candidate = 1; ; candidate++)
        {
            var label = candidate.ToString(CultureInfo.InvariantCulture);
            if (!used.Contains(label))
            {
                return label;
            }
        }
    }

    private static HashSet<string> UsedLabels(FlowDocument document)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in Definitions(document))
        {
            used.Add(((FootnoteDefinitionRole)definition.Tag).Label);
        }

        return used;
    }

    // The Footnote Number a newly cited Footnote shows: one past however many are shown already. The
    // numbering is presentation, and Project recounts it from reference order on the next projection.
    private static int NextNumber(FlowDocument document) => Definitions(document).Count + 1;

    private static int NumberIn(Section section) => section.Blocks.OfType<Section>()
        .Count(definition => definition.Tag is FootnoteDefinitionRole) + 1;

    private static List<Section> Definitions(FlowDocument document) =>
    [
        .. document.Blocks.OfType<Section>()
            .Where(block => block.Tag is BlockSemantic.FootnoteSection)
            .SelectMany(section => section.Blocks.OfType<Section>())
            .Where(definition => definition.Tag is FootnoteDefinitionRole),
    ];

    /// <summary>
    /// Trims a block range so it stops short of the Footnote Section. A Select All reaches it, but
    /// Project composes it at the end of the document rather than the author writing it: quoting it,
    /// listing it, or defining it would capture the notes somewhere they were never written (INV-065).
    /// </summary>
    /// <param name="blocks">The document's top-level blocks, in order.</param>
    /// <param name="startIndex">The first block of the range.</param>
    /// <param name="endIndex">The last block of the range.</param>
    /// <returns>The last block of the trimmed range, or -1 when the range is the Footnote Section alone.</returns>
    internal static int TrimToProse(IReadOnlyList<Block> blocks, int startIndex, int endIndex)
    {
        var trimmed = endIndex;
        while (trimmed >= startIndex && IsFootnoteSection(blocks[trimmed]))
        {
            trimmed--;
        }

        return trimmed < startIndex ? -1 : trimmed;
    }

    /// <summary>Whether <paramref name="block"/> is the Footnote Section.</summary>
    /// <param name="block">The block to test.</param>
    internal static bool IsFootnoteSection(Block block) =>
        block is Section { Tag: BlockSemantic.FootnoteSection };

    /// <summary>
    /// Whether <paramref name="position"/> sits inside the Footnote Section. The Section is composed by
    /// Project rather than authored, so the block-spanning Formatting Actions leave it alone (INV-065).
    /// </summary>
    /// <param name="position">The position to test, or <see langword="null"/>.</param>
    internal static bool IsInFootnoteSection(TextPointer? position)
    {
        for (System.Windows.DependencyObject? node = position?.Parent;
             node is TextElement element;
             node = element.Parent)
        {
            if (element is Section { Tag: BlockSemantic.FootnoteSection })
            {
                return true;
            }
        }

        return false;
    }
}
