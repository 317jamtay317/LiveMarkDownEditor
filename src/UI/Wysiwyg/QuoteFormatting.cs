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
        || (VisualDocumentTraversal.TopLevelBlockOf(editor.Selection.Start) is not null
            && !FootnoteFormatting.IsInFootnoteSection(editor.Selection.Start));

    /// <summary>
    /// Answers Enter on an empty line inside a Block Quote: the line leaves the quote and becomes a
    /// plain paragraph below it, with the caret on it. A Block Quote has no closing mark to type past,
    /// so this is the way out of one from the keyboard — two Enters at the end of a quoted paragraph
    /// stop the quoting and carry on writing below it (INV-078). A line in the middle of a quote
    /// splits it, the blocks below staying quoted as a second Block Quote, and a quote the line was
    /// alone in is removed. The blocks are moved, never rebuilt, so they survive exactly as they do
    /// under <see cref="Toggle"/> (INV-028).
    /// </summary>
    /// <param name="editor">The editor whose caret Enter was pressed at.</param>
    /// <returns>
    /// <see langword="true"/> when the line left the quote; <see langword="false"/> when the caret is
    /// not on an empty line of a Block Quote, so Enter behaves as it normally does.
    /// </returns>
    internal static bool TryLeaveQuote(RichTextBox editor)
    {
        if (VisualDocumentTraversal.AncestorOf<Paragraph>(editor.CaretPosition) is not { } line
            || line.Parent is not Section { Tag: BlockSemantic.Quote } quote
            || !IsEmpty(line)
            || BlocksAround(quote) is not { } siblings)
        {
            return false;
        }

        editor.BeginChange();
        try
        {
            var below = quote.Blocks.SkipWhile(block => block != line).Skip(1).ToList();

            quote.Blocks.Remove(line);
            line.Margin = BodySpacing;
            siblings.InsertAfter(quote, line);

            // The blocks below the line were quoted before Enter and are still quoted after it, so
            // they carry on in a Block Quote of their own beneath the line that left.
            if (below.Count > 0)
            {
                var rest = new Section();
                ApplyQuote(rest);
                siblings.InsertAfter(line, rest);
                foreach (var block in below)
                {
                    quote.Blocks.Remove(block);
                    rest.Blocks.Add(block);
                }
            }

            // A quote the line was alone in is a left rule around nothing, and Capture would emit a
            // stray marker for it.
            if (quote.Blocks.Count == 0)
            {
                siblings.Remove(quote);
            }

            editor.Selection.Select(line.ContentStart, line.ContentStart);
        }
        finally
        {
            editor.EndChange();
        }

        return true;
    }

    // Whether the line holds nothing at all. It is walked rather than read through its Inlines
    // because WPF's own paragraph break carries the formatting across as empty runs — and nests them,
    // so a line that looks empty can hold a Span holding a Run — while an Image's picture, a Video
    // Player and a soft break are content the walk must still find.
    private static bool IsEmpty(Paragraph line)
    {
        for (var pointer = line.ContentStart;
             pointer is not null && pointer.CompareTo(line.ContentEnd) < 0;
             pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (pointer.GetAdjacentElement(LogicalDirection.Forward) is LineBreak or InlineUIContainer)
            {
                return false;
            }

            if (pointer.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text
                && pointer.GetTextInRun(LogicalDirection.Forward).Length > 0)
            {
                return false;
            }
        }

        return true;
    }

    // The blocks the Block Quote itself sits among — where the line that leaves it lands. A quote is
    // usually a top-level block, but it can be nested in another quote or held by a List Item, and
    // the line leaves one level: into whichever of them holds the quote.
    private static BlockCollection? BlocksAround(Section quote) => quote.Parent switch
    {
        FlowDocument document => document.Blocks,
        Section section => section.Blocks,
        ListItem item => item.Blocks,
        _ => null,
    };

    // Moves every top-level block the selection touches into one new Block Quote, in its place.
    private static void Quote(RichTextBox editor)
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
        if (startIndex < 0 || endIndex < 0)
        {
            return;
        }

        // A Select All reaches the Footnote Section, which Project composes rather than the author
        // writing it: quoting it would capture every note behind a "> " it was never written with
        // (INV-065).
        endIndex = FootnoteFormatting.TrimToProse(blocks, startIndex, endIndex);
        if (endIndex < 0)
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
