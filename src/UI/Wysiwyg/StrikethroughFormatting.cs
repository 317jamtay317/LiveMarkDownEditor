using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace UI.Wysiwyg;

/// <summary>
/// The Toggle Strikethrough Formatting Action, and the one shared definition of what struck-through
/// prose looks like in the Visual Document. The Projector strikes a span through with the same
/// <see cref="ApplyStrikethrough"/> method, so Capture treats a user-struck span and a loaded one
/// uniformly (INV-018), and the action can remove either alike (INV-029).
/// </summary>
/// <remarks>
/// Unlike bold and italic — which ride on <c>FontWeight</c> / <c>FontStyle</c>, inherited properties
/// an inner Run can simply override — a Strikethrough rides on <c>TextDecorations</c>, which Capture
/// reads by walking a Run's ancestors. Clearing the decoration on the selected Run alone would leave
/// an enclosing struck Span still striking it: the text would look plain and still Capture as
/// <c>~~text~~</c>. So the Strikethrough is removed where it lives, the way
/// <see cref="CodeFormatting"/> removes a Code Span.
/// </remarks>
internal static class StrikethroughFormatting
{
    /// <summary>Applies the Toggle Strikethrough Formatting Action at the editor's current selection.</summary>
    /// <param name="editor">The editor whose selection is being formatted.</param>
    internal static void Toggle(RichTextBox editor)
    {
        editor.BeginChange();
        try
        {
            var struck = StruckSpansIn(editor.Selection);
            if (struck.Count > 0)
            {
                struck.ForEach(RemoveStrikethrough);
                return;
            }

            if (editor.Selection.IsEmpty)
            {
                return;
            }

            // Wrapping the selected range moves the existing inlines into the Span, so their other
            // formatting (bold, italic, a Code Span) survives the strike (INV-029).
            var span = new Span(editor.Selection.Start, editor.Selection.End);
            ApplyStrikethrough(span);
        }
        finally
        {
            editor.EndChange();
        }
    }

    /// <summary>
    /// Whether Toggle Strikethrough can run: there is text selected to strike through, or the
    /// selection already touches struck-through prose to restore.
    /// </summary>
    /// <param name="editor">The editor whose selection is queried.</param>
    internal static bool CanToggle(RichTextBox editor) =>
        !editor.Selection.IsEmpty || StruckSpansIn(editor.Selection).Count > 0;

    /// <summary>Strikes <paramref name="span"/> through, exactly as the Projector does.</summary>
    /// <param name="span">The span holding the struck-through inlines.</param>
    internal static void ApplyStrikethrough(Span span)
    {
        // Both the role and the decoration are set: Capture reads either, and the role is what
        // survives a user clearing the decoration by other means.
        span.Tag = InlineSemantic.Strikethrough;
        span.TextDecorations = TextDecorations.Strikethrough;
    }

    // Reverts a struck Span to plain prose, leaving its inlines (and their other formatting) in place.
    private static void RemoveStrikethrough(Span span)
    {
        span.Tag = null;
        span.TextDecorations = null;
    }

    // Every struck Span the selection touches, found the same two ways Capture reads a Strikethrough
    // — by its role or by its decoration — so a loaded Strikethrough and a toggled one both turn up.
    // For an empty selection, the struck Span enclosing the caret.
    private static List<Span> StruckSpansIn(TextSelection selection)
    {
        var spans = new List<Span>();
        if (selection.IsEmpty)
        {
            AddStruckAncestors(selection.Start.Parent, spans);
            return spans;
        }

        for (var pointer = selection.Start;
             pointer is not null && pointer.CompareTo(selection.End) < 0;
             pointer = pointer.GetNextContextPosition(LogicalDirection.Forward))
        {
            AddStruckAncestors(pointer.Parent, spans);
        }

        return spans;
    }

    // The strike may sit on any ancestor of the pointer's own element, so the chain is walked up.
    private static void AddStruckAncestors(DependencyObject? node, List<Span> spans)
    {
        for (; node is TextElement element; node = element.Parent)
        {
            if (element is Span span && IsStruck(span) && !spans.Contains(span))
            {
                spans.Add(span);
            }
        }
    }

    private static bool IsStruck(Span span) =>
        span.Tag is InlineSemantic.Strikethrough
        || (span.TextDecorations is { Count: > 0 } decorations
            && decorations.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough));
}
