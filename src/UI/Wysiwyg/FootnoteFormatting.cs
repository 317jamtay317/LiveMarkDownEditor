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
        if (VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.Start) is not { } anchor
            || !CanInsert(editor))
        {
            return;
        }

        editor.BeginChange();
        try
        {
            var label = NextLabel(editor.Document);

            // The Reference replaces the selection, the way any insertion at a selection does.
            var reference = FootnoteProjection.CreateReference(label, NextNumber(editor.Document));
            var paragraph = VisualDocumentTraversal.AncestorOf<Paragraph>(editor.Selection.Start);
            if (paragraph is null)
            {
                return;
            }

            editor.Selection.Text = string.Empty;
            paragraph.Inlines.InsertBefore(SplitAt(paragraph, editor.Selection.Start), reference);

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

    // The inline the Reference is inserted before: the caret splits the run it sits inside, so a
    // citation lands exactly where the caret was rather than at the end of the word it was in.
    private static Inline SplitAt(Paragraph paragraph, TextPointer caret)
    {
        if (VisualDocumentTraversal.AncestorOf<Run>(caret) is not { } run)
        {
            return paragraph.Inlines.FirstInline;
        }

        var offset = run.ContentStart.GetOffsetToPosition(caret);
        var text = run.Text;
        if (offset <= 0)
        {
            return run;
        }

        if (offset >= text.Length)
        {
            // At the run's end there is nothing to split: the Reference goes after it.
            var following = run.NextInline;
            if (following is null)
            {
                var empty = new Run(string.Empty);
                paragraph.Inlines.Add(empty);
                return empty;
            }

            return following;
        }

        run.Text = text[..offset];
        var tail = new Run(text[offset..]);
        paragraph.Inlines.InsertAfter(run, tail);
        return tail;
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

    private static bool IsInFootnoteSection(TextPointer? position)
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
