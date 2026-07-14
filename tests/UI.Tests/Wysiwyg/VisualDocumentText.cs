using System.Windows.Controls;
using System.Windows.Documents;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Locates literal text inside a Visual Document so tests can place selections and carets the way a
/// user would — by what they see, not by document structure.
/// </summary>
internal static class VisualDocumentText
{
    /// <summary>Selects the first occurrence of <paramref name="text"/> (within a single text run).</summary>
    /// <param name="editor">The editor whose Visual Document is searched.</param>
    /// <param name="text">The literal text to select.</param>
    internal static void SelectText(RichTextBox editor, string text)
    {
        var range = FindRange(editor.Document, text);
        editor.Selection.Select(range.Start, range.End);
    }

    /// <summary>Collapses the selection to a caret at the start of the first occurrence of <paramref name="text"/>.</summary>
    /// <param name="editor">The editor whose Visual Document is searched.</param>
    /// <param name="text">The literal text to place the caret in.</param>
    internal static void PlaceCaretIn(RichTextBox editor, string text)
    {
        var range = FindRange(editor.Document, text);
        editor.Selection.Select(range.Start, range.Start);
    }

    private static TextRange FindRange(FlowDocument document, string text)
    {
        for (var pointer = document.ContentStart;
             pointer is not null;
             pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            if (pointer.GetPointerContext(LogicalDirection.Forward) != TextPointerContext.Text)
            {
                continue;
            }

            var runText = pointer.GetTextInRun(LogicalDirection.Forward);
            var index = runText.IndexOf(text, StringComparison.Ordinal);
            if (index < 0)
            {
                continue;
            }

            return new TextRange(
                pointer.GetPositionAtOffset(index)!,
                pointer.GetPositionAtOffset(index + text.Length)!);
        }

        throw new InvalidOperationException($"Text '{text}' was not found in the Visual Document.");
    }
}
