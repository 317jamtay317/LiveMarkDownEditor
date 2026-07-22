using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace UI.Wysiwyg;

/// <summary>
/// The Toggle Code Formatting Action, and the one shared definition of what "looks like code" in the
/// Visual Document. A selection within a single line becomes a Code Span; a selection spanning
/// multiple whole lines, or covering a whole line, becomes a Code Block; applied where the selection
/// or caret already sits inside code, it removes that code formatting instead. The Projector applies
/// the same formatting through <see cref="ApplyCodeSpan"/> / <see cref="ApplyCodeBlock"/>, so Capture
/// treats user-applied code and code loaded from Markdown uniformly (INV-018).
/// </summary>
/// <remarks>
/// A Code Block is made only out of whole top-level paragraphs, because that is all a fence can
/// replace: inside a List, a Block Quote, or a Table the top-level block is the List, quote, or Table
/// itself, and replacing it with a fence would swallow every sibling item, line, or cell along with
/// the bullets and column separators that hold them apart. There the action makes a Code Span
/// instead — one per line it touches, since a Code Span may not straddle a line break.
/// </remarks>
internal static class CodeFormatting
{
    /// <summary>Applies the Toggle Code Formatting Action at the editor's current selection.</summary>
    /// <param name="editor">The editor whose selection is being formatted.</param>
    internal static void Toggle(RichTextBox editor)
    {
        editor.BeginChange();
        try
        {
            if (CodeBlockAt(editor.Selection) is { } codeBlock)
            {
                RemoveCodeBlock(codeBlock);
                return;
            }

            var codeRuns = CodeSpanRunsIn(editor.Selection);
            if (codeRuns.Count > 0)
            {
                codeRuns.ForEach(RemoveCodeSpan);
                return;
            }

            if (editor.Selection.IsEmpty)
            {
                return;
            }

            var paragraphs = ParagraphsIn(editor.Selection);
            if (paragraphs.Count == 0)
            {
                return;
            }

            var fenceable = paragraphs.TrueForAll(IsTopLevel)
                && (paragraphs.Count > 1 || CoversWholeParagraph(editor.Selection, paragraphs[0]));
            if (!fenceable || !MakeCodeBlock(editor, paragraphs))
            {
                MakeCodeSpans(editor, paragraphs);
            }
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// Whether Toggle Code can run: there is text selected to format, or the selection/caret already
    /// sits inside a Code Span or Code Block to unformat.
    /// </summary>
    /// <param name="editor">The editor whose selection is queried.</param>
    internal static bool CanToggle(RichTextBox editor) =>
        !editor.Selection.IsEmpty
        || CodeBlockAt(editor.Selection) is not null
        || CodeSpanRunsIn(editor.Selection).Count > 0;

    /// <summary>Formats <paramref name="run"/> as a Code Span, exactly as the Projector does.</summary>
    /// <param name="run">The run carrying the code text.</param>
    internal static void ApplyCodeSpan(Run run)
    {
        // Monospace alone reads like body text; an accent colour plus Code Shading make an inline
        // code span visibly code. The Code tag (not the styling) is what Capture keys on, and it is
        // also what the CodeShadingScanner finds to shade the span (INV-017).
        run.Tag = InlineSemantic.Code;
        run.FontFamily = MonospaceFont;
        run.Language = NoProofingLanguage;
        run.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
    }

    /// <summary>Formats <paramref name="paragraph"/> as a Code Block, exactly as the Projector does.</summary>
    /// <param name="paragraph">The paragraph holding the code lines as its inline content.</param>
    /// <param name="language">The fenced code block's info string (language), or <see langword="null"/>.</param>
    internal static void ApplyCodeBlock(Paragraph paragraph, string? language)
    {
        paragraph.Tag = new CodeBlockRole(language);
        paragraph.FontFamily = MonospaceFont;
        paragraph.Language = NoProofingLanguage;
        paragraph.Padding = new Thickness(8);
        paragraph.Margin = BodySpacing;
    }

    // Turns the selection into Code Spans, one per paragraph it touches: the selected text within each
    // is replaced by one Run carrying the Code role, so Capture emits `text`. Later paragraphs are
    // done first so the earlier ranges are still intact when their turn comes.
    private static void MakeCodeSpans(RichTextBox editor, List<Paragraph> paragraphs)
    {
        var selection = editor.Selection;
        var ranges = paragraphs
            .Select(paragraph => new TextRange(
                Later(selection.Start, paragraph.ContentStart),
                Earlier(selection.End, paragraph.ContentEnd)))
            .Where(range => !range.IsEmpty)
            .ToList();

        Run? last = null;
        for (var i = ranges.Count - 1; i >= 0; i--)
        {
            last = MakeCodeSpan(ranges[i]) ?? last;
        }

        if (last is not null)
        {
            editor.Selection.Select(last.ContentEnd, last.ContentEnd);
        }
    }

    // Replaces one range's non-whitespace core with Code Span runs, leaving the whitespace the user's
    // double-click swept up outside them (INV-018). A range holding a line break becomes one run per
    // line, the breaks kept between them, because a Code Span may not straddle one. Returns the last
    // run made, or null when the range is whitespace alone.
    private static Run? MakeCodeSpan(TextRange range)
    {
        var core = VisualDocumentTraversal.WithoutSurroundingWhitespace(range);
        if (core.IsEmpty)
        {
            return null;
        }

        var lines = VisualDocumentTraversal.TextIn(core).Split('\n');
        core.Text = string.Empty;

        var position = core.Start;
        Run? last = null;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length > 0)
            {
                last = new Run(lines[i], position);
                ApplyCodeSpan(last);
                position = last.ElementEnd;
            }

            if (i < lines.Length - 1)
            {
                position = new LineBreak(position).ElementEnd;
            }
        }

        return last;
    }

    private static TextPointer Later(TextPointer left, TextPointer right) =>
        left.CompareTo(right) >= 0 ? left : right;

    private static TextPointer Earlier(TextPointer left, TextPointer right) =>
        left.CompareTo(right) <= 0 ? left : right;

    // Turns the selection into a Code Block: every top-level block the selection touches is replaced
    // by a single code paragraph whose lines are the blocks' text, so Capture emits a fenced block.
    // Reports whether it ran: a block a fence cannot absorb (a Mermaid Diagram, say) lying between the
    // selected paragraphs sends the caller to Code Spans rather than let the fence swallow it.
    //
    // The fence spans the paragraphs the selection actually touches — the ones the caller has already
    // checked are top-level — rather than whatever blocks its end points resolve to. A selection that
    // merely stops at the start of the next block reaches no content in it, so that block is not the
    // user's to fence; and Select All, whose end sits at the document's own edge rather than in any
    // block, spans the whole document exactly as it reads.
    private static bool MakeCodeBlock(RichTextBox editor, List<Paragraph> paragraphs)
    {
        var document = editor.Document;
        var blocks = document.Blocks.ToList();
        var startIndex = blocks.IndexOf(paragraphs[0]);
        var endIndex = blocks.IndexOf(paragraphs[^1]);
        if (startIndex < 0 || endIndex < 0)
        {
            return false;
        }

        for (var i = startIndex; i <= endIndex; i++)
        {
            if (blocks[i] is not Paragraph)
            {
                return false;
            }
        }

        var lines = new List<string>();
        for (var i = startIndex; i <= endIndex; i++)
        {
            var text = new TextRange(blocks[i].ContentStart, blocks[i].ContentEnd).Text;
            lines.AddRange(text.Replace("\r\n", "\n").Split('\n'));
        }

        var codeParagraph = new Paragraph();
        ApplyCodeBlock(codeParagraph, language: null);
        for (var i = 0; i < lines.Count; i++)
        {
            codeParagraph.Inlines.Add(new Run(lines[i]));
            if (i < lines.Count - 1)
            {
                codeParagraph.Inlines.Add(new LineBreak());
            }
        }

        document.Blocks.InsertBefore(blocks[startIndex], codeParagraph);
        for (var i = startIndex; i <= endIndex; i++)
        {
            document.Blocks.Remove(blocks[i]);
        }

        editor.Selection.Select(codeParagraph.ContentEnd, codeParagraph.ContentEnd);
        return true;
    }

    // Reverts a Code Block to a plain paragraph; its lines stay as the paragraph's inline content.
    private static void RemoveCodeBlock(Paragraph paragraph)
    {
        paragraph.Tag = null;
        paragraph.ClearValue(TextElement.FontFamilyProperty);
        paragraph.ClearValue(FrameworkContentElement.LanguageProperty);
        paragraph.ClearValue(Block.PaddingProperty);
    }

    // Reverts a Code Span run to plain prose formatting.
    private static void RemoveCodeSpan(Run run)
    {
        run.Tag = null;
        run.ClearValue(TextElement.FontFamilyProperty);
        run.ClearValue(FrameworkContentElement.LanguageProperty);
        run.ClearValue(TextElement.ForegroundProperty);
    }

    // The Code Block paragraph containing the selection's start, or null when it is not in one.
    private static Paragraph? CodeBlockAt(TextSelection selection) =>
        selection.Start.Paragraph is { Tag: CodeBlockRole } paragraph ? paragraph : null;

    // Every Code Span run the selection touches; for an empty selection, the run holding the caret.
    private static List<Run> CodeSpanRunsIn(TextSelection selection)
    {
        var runs = new List<Run>();
        if (selection.IsEmpty)
        {
            if (selection.Start.Parent is Run run && run.Tag is InlineSemantic.Code)
            {
                runs.Add(run);
            }

            return runs;
        }

        for (var pointer = selection.Start;
             pointer is not null && pointer.CompareTo(selection.End) < 0;
             pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (pointer.Parent is Run run && run.Tag is InlineSemantic.Code && !runs.Contains(run))
            {
                runs.Add(run);
            }
        }

        return runs;
    }

    private static bool CoversWholeParagraph(TextSelection selection, Paragraph paragraph) =>
        selection.Text == new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;

    // Every paragraph the selection touches, in document order. A position strictly inside the
    // selection lies in each of them, so walking to (not through) the end never picks up the
    // paragraph a selection merely stops at the start of.
    private static List<Paragraph> ParagraphsIn(TextSelection selection)
    {
        var paragraphs = new List<Paragraph>();
        for (var pointer = selection.Start;
             pointer is not null && pointer.CompareTo(selection.End) < 0;
             pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (pointer.Paragraph is { } paragraph && !paragraphs.Contains(paragraph))
            {
                paragraphs.Add(paragraph);
            }
        }

        return paragraphs;
    }

    // Whether the paragraph sits directly in the document — the only place a fence can replace it.
    private static bool IsTopLevel(Paragraph paragraph) => paragraph.Parent is FlowDocument;

    private static readonly FontFamily MonospaceFont = new("Consolas, Cascadia Mono, Courier New");

    // BCP-47 "zxx" = "no linguistic content": excludes code from spell check while prose stays checked.
    private static readonly XmlLanguage NoProofingLanguage = XmlLanguage.GetLanguage("zxx");

    // The uniform block spacing the Projector gives body blocks.
    private static readonly Thickness BodySpacing = new(0, 0, 0, 6);
}
