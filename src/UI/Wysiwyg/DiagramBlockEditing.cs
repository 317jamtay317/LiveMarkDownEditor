using System.Windows.Controls;
using System.Windows.Documents;

namespace UI.Wysiwyg;

/// <summary>
/// Writes a Mermaid Diagram into the Visual Document for the Flowchart Builder's Insert (INV-053). It
/// composes a <c>mermaid</c> Code Block through the same <see cref="CodeFormatting.ApplyCodeBlock"/>
/// seam the Projector and Toggle Code use, so the inserted block Captures as canonical Markdown exactly
/// as a hand-typed one does (INV-018) — replacing the Mermaid Diagram at the caret when there is one,
/// otherwise inserting a new Code Block at the caret.
/// </summary>
internal static class DiagramBlockEditing
{
    /// <summary>
    /// Writes <paramref name="mermaidSource"/> as a Mermaid Diagram at the caret: replacing the caret's
    /// Mermaid Diagram Code Block when it is on one, else inserting a new Code Block after the caret's
    /// block. The whole edit is one <c>BeginChange</c> unit, so a single undo takes it back and the
    /// change flows through the ordinary Capture path (INV-053).
    /// </summary>
    /// <param name="editor">The editing surface to write into.</param>
    /// <param name="mermaidSource">The Mermaid source to insert.</param>
    internal static void InsertOrReplaceDiagramAtCaret(RichTextBox editor, string mermaidSource)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(mermaidSource);

        editor.BeginChange();
        try
        {
            var paragraph = BuildMermaidBlock(mermaidSource);
            var document = editor.Document;

            if (editor.CaretPosition?.Paragraph is { Tag: CodeBlockRole role } existing &&
                MermaidDiagram.IsMermaidLanguage(role.Language))
            {
                // The caret is inside a Mermaid Diagram: replace that block in place.
                document.Blocks.InsertBefore(existing, paragraph);
                document.Blocks.Remove(existing);
            }
            else if (VisualDocumentTraversal.TopLevelBlockOf(editor.CaretPosition) is { } anchor)
            {
                document.Blocks.InsertAfter(anchor, paragraph);
            }
            else
            {
                document.Blocks.Add(paragraph);
            }

            editor.CaretPosition = paragraph.ContentEnd;
        }
        finally
        {
            editor.EndChange();
        }
    }

    // Builds a Mermaid Code Block paragraph: one Run per source line, separated by LineBreaks, exactly
    // as the Projector lays out a fenced Code Block — so MermaidDiagram.SourceAt reads it back and
    // Capture emits the fenced block (INV-004/INV-047).
    private static Paragraph BuildMermaidBlock(string source)
    {
        var paragraph = new Paragraph();
        CodeFormatting.ApplyCodeBlock(paragraph, MermaidDiagram.Language);

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            paragraph.Inlines.Add(new Run(lines[i]));
            if (i < lines.Length - 1)
            {
                paragraph.Inlines.Add(new LineBreak());
            }
        }

        return paragraph;
    }
}
