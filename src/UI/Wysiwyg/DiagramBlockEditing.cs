using System.Windows.Controls;
using System.Windows.Documents;

namespace UI.Wysiwyg;

/// <summary>
/// Writes a Mermaid Diagram into the Visual Document for the Flowchart Builder's Insert (INV-053). It
/// composes the diagram's atomic block through the same <see cref="MermaidDiagram.CreateDiagramBlock"/>
/// seam the Projector uses, so an inserted diagram is shown as its picture and Captures as the fenced
/// ```mermaid``` block exactly as a projected one does (INV-047) — replacing the Mermaid Diagram at the
/// caret when there is one, otherwise inserting a new block after the caret's block. The diagram is a
/// Block Island, so a line always follows it and the caret lands in that line rather than in the
/// diagram itself (INV-055).
/// </summary>
internal static class DiagramBlockEditing
{
    /// <summary>
    /// Writes <paramref name="mermaidSource"/> as a Mermaid Diagram at the caret: replacing the caret's
    /// Mermaid Diagram block when it is on one, else inserting a new block after the caret's block. The
    /// whole edit is one <c>BeginChange</c> unit, so a single undo takes it back and the change flows
    /// through the ordinary Capture path (INV-053). The caret is left in the line below the diagram, so
    /// the user can carry on typing after it (INV-055).
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
            var block = MermaidDiagram.CreateDiagramBlock(mermaidSource);
            var document = editor.Document;
            var caretBlock = VisualDocumentTraversal.TopLevelBlockOf(editor.CaretPosition);

            if (caretBlock is BlockUIContainer { Tag: MermaidDiagramRole })
            {
                // The caret is on a Mermaid Diagram: replace that block in place.
                document.Blocks.InsertBefore(caretBlock, block);
                document.Blocks.Remove(caretBlock);
            }
            else if (caretBlock is not null)
            {
                document.Blocks.InsertAfter(caretBlock, block);
            }
            else
            {
                document.Blocks.Add(block);
            }

            // A Mermaid Diagram is a Block Island: its BlockUIContainer hosts a picture and no text
            // at all, so a caret left inside it swallows Enter and — when the diagram is the last
            // block — has nowhere below to go. Park the caret in the line after it instead (INV-055).
            var trailing = VisualDocumentTraversal.EnsureParagraphAfter(document, block);
            editor.CaretPosition = trailing is not null ? trailing.ContentStart : block.ContentEnd;
        }
        finally
        {
            editor.EndChange();
        }
    }
}
