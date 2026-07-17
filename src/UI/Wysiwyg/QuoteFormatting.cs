using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using WpfBlock = System.Windows.Documents.Block;

namespace UI.Wysiwyg;

/// <summary>
/// The Toggle Block Quote Formatting Action, and the one shared definition of what a Block Quote
/// looks like in the Visual Document. The Projector composes a Block Quote through the same
/// <see cref="ApplyQuote"/> method, so Capture treats a user-made Block Quote and a loaded one
/// uniformly (INV-018). The action moves whole blocks in and out of the quote rather than
/// re-creating them, so their content and kind survive (INV-028).
/// </summary>
internal static class QuoteFormatting
{
    /// <summary>
    /// Styles <paramref name="section"/> as a Block Quote — a left rule and muted text — exactly as
    /// the Projector does. The <see cref="BlockSemantic.Quote"/> tag is what Capture keys on to
    /// re-emit the <c>&gt; </c> prefix.
    /// </summary>
    /// <param name="section">The section holding the quoted blocks.</param>
    internal static void ApplyQuote(Section section)
    {
        section.Tag = BlockSemantic.Quote;
        section.BorderThickness = new Thickness(3, 0, 0, 0);
        section.Padding = new Thickness(10, 0, 0, 0);
        section.Margin = BodySpacing;
        section.SetResourceReference(TextElement.ForegroundProperty, "MutedTextBrush");
        section.SetResourceReference(WpfBlock.BorderBrushProperty, "BorderBrush");
    }

    /// <summary>
    /// The Toggle Block Quote Formatting Action: the blocks the selection touches become a Block
    /// Quote, or the selected Block Quote's blocks become plain blocks again. Whole blocks are
    /// quoted — a <c>&gt; </c> prefix applies to a line, so quoting part of a paragraph is not
    /// expressible in Markdown (INV-028).
    /// </summary>
    /// <param name="editor">The editor whose selection is being formatted.</param>
    internal static void Toggle(RichTextBox editor)
    {
        editor.BeginChange();
        try
        {
            if (QuoteAt(editor) is { } quote)
            {
                Unquote(editor.Document, quote);
                return;
            }

            Quote(editor);
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// Whether Toggle Block Quote can run: the selection sits on a top-level block to quote, or
    /// inside a Block Quote to unquote.
    /// </summary>
    /// <param name="editor">The editor whose selection is queried.</param>
    internal static bool CanToggle(RichTextBox editor) =>
        QuoteAt(editor) is not null
        || VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.Start) is not null;

    // Moves every top-level block the selection touches into one new Block Quote, in its place.
    private static void Quote(RichTextBox editor)
    {
        var document = editor.Document;
        var blocks = document.Blocks.ToList();
        var start = VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.Start);
        var end = VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.End);
        if (start is null || end is null)
        {
            return;
        }

        var startIndex = blocks.IndexOf(start);
        var endIndex = blocks.IndexOf(end);
        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        var section = new Section();
        ApplyQuote(section);
        document.Blocks.InsertBefore(blocks[startIndex], section);

        // The blocks are moved, not rebuilt, so their inline formatting and kind survive (INV-028).
        // Removing a block from the document first is what lets it be added to the Section: a Block
        // belongs to one parent at a time.
        for (var i = startIndex; i <= endIndex; i++)
        {
            var block = blocks[i];
            document.Blocks.Remove(block);
            section.Blocks.Add(block);
        }

        editor.Selection.Select(section.ContentStart, section.ContentStart);
    }

    // Moves a Block Quote's blocks back out to the top level, in order, and drops the empty quote.
    private static void Unquote(FlowDocument document, Section quote)
    {
        foreach (var block in quote.Blocks.ToList())
        {
            quote.Blocks.Remove(block);
            document.Blocks.InsertBefore(quote, block);
        }

        document.Blocks.Remove(quote);
    }

    // The Block Quote enclosing the selection's start, or null when it is not inside one.
    private static Section? QuoteAt(RichTextBox editor)
    {
        for (DependencyObject? node = editor.Selection.Start.Parent;
             node is TextElement element;
             node = element.Parent)
        {
            if (element is Section { Tag: BlockSemantic.Quote } section)
            {
                return section;
            }
        }

        return null;
    }

    // The uniform block spacing the Projector gives body blocks.
    private static readonly Thickness BodySpacing = new(0, 0, 0, 6);
}
