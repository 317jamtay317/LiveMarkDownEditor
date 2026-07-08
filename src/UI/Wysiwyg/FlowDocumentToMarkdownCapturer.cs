using System.Text;
using System.Windows;
using System.Windows.Documents;

namespace UI.Wysiwyg;

/// <summary>
/// Captures a Visual Document (a <see cref="FlowDocument"/>) back into canonical Markdown Document
/// source text. The inverse of <see cref="MarkdownToFlowDocumentProjector"/>.
/// </summary>
/// <remarks>
/// Formatting is detected from each leaf <see cref="Run"/>'s <em>effective</em> properties
/// (<see cref="TextElement.FontWeight"/>, <see cref="TextElement.FontStyle"/>,
/// <see cref="Inline.TextDecorations"/>) and role tags, so both formatting loaded from Markdown and
/// formatting applied by the user through the editor's toolbar are captured uniformly. Adjacent
/// runs sharing the same formatting are merged and canonical delimiters are emitted, so repeated
/// Round-Trips converge (INV-005) and the captured text renders identically to the original
/// (INV-004).
/// </remarks>
public sealed class FlowDocumentToMarkdownCapturer
{
    private readonly record struct Segment(string Text, bool Bold, bool Italic, bool Strike, bool Code, bool IsBreak)
    {
        public bool SameFormatting(Segment other) =>
            !IsBreak && !other.IsBreak &&
            Bold == other.Bold && Italic == other.Italic && Strike == other.Strike && Code == other.Code;
    }

    /// <summary>Captures the Visual Document as canonical Markdown source text.</summary>
    /// <param name="document">The Visual Document to serialise.</param>
    /// <returns>The canonical Markdown source text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is <see langword="null"/>.</exception>
    public string Capture(FlowDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return Capture(document.Blocks);
    }

    /// <summary>Captures an explicit sequence of blocks as canonical Markdown source text.</summary>
    /// <remarks>
    /// Used to capture the full logical document even when some Section Bodies are Folded: the editor
    /// supplies the visible blocks with each Folded body spliced back in at its Section Heading, so a
    /// Fold never changes the captured source (INV-011).
    /// </remarks>
    /// <param name="blocks">The blocks to serialise, in document order.</param>
    /// <returns>The canonical Markdown source text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="blocks"/> is <see langword="null"/>.</exception>
    public string Capture(IEnumerable<Block> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);

        var captured = new List<string>();
        foreach (var block in blocks)
        {
            var text = CaptureBlock(block);
            if (text is not null)
            {
                captured.Add(text);
            }
        }

        return string.Join("\n\n", captured);
    }

    private static string? CaptureBlock(Block block) => block switch
    {
        Paragraph { Tag: HeadingRole heading } paragraph =>
            new string('#', heading.Level) + " " + CaptureInlines(paragraph.Inlines),
        Paragraph paragraph => CaptureInlines(paragraph.Inlines),
        _ => null,
    };

    private static string CaptureInlines(InlineCollection inlines)
    {
        var segments = new List<Segment>();
        foreach (var inline in inlines)
        {
            Flatten(inline, segments);
        }

        var merged = Merge(segments);

        var builder = new StringBuilder();
        foreach (var segment in merged)
        {
            builder.Append(Emit(segment));
        }

        return builder.ToString();
    }

    private static void Flatten(Inline inline, List<Segment> segments)
    {
        switch (inline)
        {
            case Run run when run.Text.Length > 0:
                segments.Add(new Segment(
                    run.Text,
                    Bold: run.FontWeight.ToOpenTypeWeight() >= FontWeights.Bold.ToOpenTypeWeight(),
                    Italic: run.FontStyle == FontStyles.Italic,
                    Strike: HasStrikethrough(run),
                    Code: HasRole(run, InlineSemantic.Code),
                    IsBreak: false));
                break;

            case LineBreak:
                segments.Add(new Segment("\n", false, false, false, false, IsBreak: true));
                break;

            case Span span:
                foreach (var child in span.Inlines)
                {
                    Flatten(child, segments);
                }

                break;
        }
    }

    private static List<Segment> Merge(List<Segment> segments)
    {
        var merged = new List<Segment>();
        foreach (var segment in segments)
        {
            if (merged.Count > 0 && merged[^1].SameFormatting(segment))
            {
                merged[^1] = merged[^1] with { Text = merged[^1].Text + segment.Text };
            }
            else
            {
                merged.Add(segment);
            }
        }

        return merged;
    }

    private static string Emit(Segment segment)
    {
        if (segment.IsBreak)
        {
            return segment.Text;
        }

        if (segment.Code)
        {
            return "`" + segment.Text + "`";
        }

        var prefix = (segment.Strike ? "~~" : string.Empty)
            + (segment.Bold ? "**" : string.Empty)
            + (segment.Italic ? "*" : string.Empty);
        var suffix = (segment.Italic ? "*" : string.Empty)
            + (segment.Bold ? "**" : string.Empty)
            + (segment.Strike ? "~~" : string.Empty);

        return prefix + segment.Text + suffix;
    }

    private static bool HasStrikethrough(Inline inline)
    {
        for (DependencyObject? node = inline; node is TextElement element; node = element.Parent)
        {
            if (element is Inline { TextDecorations.Count: > 0 } styled
                && styled.TextDecorations.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough))
            {
                return true;
            }

            if (element.Tag is InlineSemantic.Strikethrough)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRole(Inline inline, InlineSemantic role)
    {
        for (DependencyObject? node = inline; node is TextElement element; node = element.Parent)
        {
            if (element.Tag is InlineSemantic tag && tag == role)
            {
                return true;
            }
        }

        return false;
    }
}
