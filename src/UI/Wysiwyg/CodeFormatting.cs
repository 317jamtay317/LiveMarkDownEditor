using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace UI.Wysiwyg;

/// <summary>
/// The Toggle Code Formatting Action, and the one shared definition of what "looks like code" in the
/// Visual Document. A selection within a single line becomes a Code Span; a selection spanning
/// multiple lines, or covering a whole line, becomes a Code Block; applied where the selection or
/// caret already sits inside code, it removes that code formatting instead. The Projector applies
/// the same formatting through <see cref="ApplyCodeSpan"/> / <see cref="ApplyCodeBlock"/>, so Capture
/// treats user-applied code and code loaded from Markdown uniformly (INV-018).
/// </summary>
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

            var start = editor.Selection.Start.Paragraph;
            var end = editor.Selection.End.Paragraph;
            if (start is null || end is null)
            {
                return;
            }

            if (!ReferenceEquals(start, end) || CoversWholeParagraph(editor.Selection, start))
            {
                MakeCodeBlock(editor);
            }
            else
            {
                MakeCodeSpan(editor.Selection);
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

    // Turns the (single-paragraph, partial) selection into a Code Span: the selected text is replaced
    // by one Run carrying the Code role, so Capture emits `text`.
    private static void MakeCodeSpan(TextSelection selection)
    {
        var text = selection.Text;
        selection.Text = string.Empty;

        var run = new Run(text, selection.Start);
        ApplyCodeSpan(run);
        selection.Select(run.ContentEnd, run.ContentEnd);
    }

    // Turns the selection into a Code Block: every top-level block the selection touches is replaced
    // by a single code paragraph whose lines are the blocks' text, so Capture emits a fenced block.
    private static void MakeCodeBlock(RichTextBox editor)
    {
        var document = editor.Document;
        var blocks = document.Blocks.ToList();
        var startIndex = blocks.IndexOf(TopLevelBlockOf(editor.Selection.Start)!);
        var endIndex = blocks.IndexOf(TopLevelBlockOf(editor.Selection.End)!);
        if (startIndex < 0 || endIndex < 0)
        {
            return;
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

    // The block that sits directly in the FlowDocument and contains the given position.
    private static Block? TopLevelBlockOf(TextPointer position)
    {
        for (DependencyObject? node = position.Parent; node is TextElement element; node = element.Parent)
        {
            if (element is Block block && block.Parent is FlowDocument)
            {
                return block;
            }
        }

        return null;
    }

    private static readonly FontFamily MonospaceFont = new("Consolas, Cascadia Mono, Courier New");

    // BCP-47 "zxx" = "no linguistic content": excludes code from spell check while prose stays checked.
    private static readonly XmlLanguage NoProofingLanguage = XmlLanguage.GetLanguage("zxx");

    // The uniform block spacing the Projector gives body blocks.
    private static readonly Thickness BodySpacing = new(0, 0, 0, 6);
}
