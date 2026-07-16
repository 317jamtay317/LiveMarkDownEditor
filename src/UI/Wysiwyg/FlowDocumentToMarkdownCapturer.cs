using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using WpfList = System.Windows.Documents.List;
using WpfTable = System.Windows.Documents.Table;

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
    private readonly record struct Segment(
        string Text, bool Bold, bool Italic, bool Strike, bool Code, bool IsBreak, bool Verbatim)
    {
        public bool SameFormatting(Segment other) =>
            !IsBreak && !other.IsBreak && !Verbatim && !other.Verbatim &&
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
        Paragraph { Tag: CodeBlockRole codeRole } paragraph => CaptureCodeBlock(paragraph, codeRole),
        Paragraph { Tag: BlockSemantic.ThematicBreak } => "---",
        Paragraph paragraph => CaptureInlines(paragraph.Inlines),
        WpfList list => CaptureList(list),
        Section { Tag: BlockSemantic.Quote } quote => CaptureQuote(quote),
        WpfTable table => CaptureTable(table),
        _ => null,
    };

    // Emits canonical Markdown list syntax: "- " before each Unordered item, "N. " (incrementing
    // from the list's StartIndex) before each Ordered item. Content lines after the first are
    // indented to the marker's width so nested content stays inside the item, keeping repeated
    // Round-Trips convergent (INV-005).
    private static string CaptureList(WpfList list)
    {
        var ordered = list.MarkerStyle == TextMarkerStyle.Decimal;
        var number = list.StartIndex;

        var lines = new List<string>();
        foreach (var item in list.ListItems)
        {
            var marker = ordered ? number.ToString(CultureInfo.InvariantCulture) + ". " : "- ";
            var content = CaptureListItem(item);
            var indented = content.Replace("\n", "\n" + new string(' ', marker.Length));
            lines.Add(marker + indented);
            number++;
        }

        return string.Join("\n", lines);
    }

    private static string CaptureListItem(ListItem item)
    {
        var parts = new List<string>();
        foreach (var block in item.Blocks)
        {
            var text = CaptureBlock(block);
            if (text is not null)
            {
                parts.Add(text);
            }
        }

        return string.Join("\n", parts);
    }

    // Emits a block quote by capturing its inner blocks and prefixing every line with "> " (a blank
    // separator line becomes ">"), the canonical form Markdig re-parses to the same quote.
    private static string CaptureQuote(Section section)
    {
        var inner = new FlowDocumentToMarkdownCapturer().Capture(section.Blocks);
        var lines = inner.Split('\n');
        return string.Join("\n", lines.Select(line => line.Length == 0 ? ">" : "> " + line));
    }

    // Emits a fenced code block. The code text is read back from the paragraph's own inlines (Runs
    // separated by LineBreaks) so any edits to the code are captured; the language comes from the role.
    private static string CaptureCodeBlock(Paragraph paragraph, CodeBlockRole role)
    {
        var code = InlineText(paragraph.Inlines);
        return "```" + (role.Language ?? string.Empty) + "\n" + code + "\n```";
    }

    // Emits a GFM pipe table: the header row, an alignment-aware delimiter row, then the body rows.
    private static string CaptureTable(WpfTable table)
    {
        var alignments = (table.Tag as TableRole)?.Alignments ?? [];
        var rows = table.RowGroups.Count > 0 ? table.RowGroups[0].Rows : null;
        if (rows is null || rows.Count == 0)
        {
            return string.Empty;
        }

        var columnCount = rows[0].Cells.Count;
        // The header row is displayed bold; that emphasis is a header convention, not authored inline
        // bold, so it is suppressed when capturing (the delimiter row already marks it as the header).
        var lines = new List<string> { CaptureRow(rows[0], suppressBold: true), DelimiterRow(alignments, columnCount) };
        for (var i = 1; i < rows.Count; i++)
        {
            lines.Add(CaptureRow(rows[i]));
        }

        return string.Join("\n", lines);
    }

    private static string CaptureRow(TableRow row, bool suppressBold = false)
    {
        var cells = row.Cells.Select(cell =>
            cell.Blocks.FirstBlock is Paragraph paragraph ? CaptureInlines(paragraph.Inlines, suppressBold) : string.Empty);
        return "| " + string.Join(" | ", cells) + " |";
    }

    private static string DelimiterRow(IReadOnlyList<ColumnAlignment> alignments, int columnCount)
    {
        var cells = new List<string>();
        for (var i = 0; i < columnCount; i++)
        {
            var alignment = i < alignments.Count ? alignments[i] : ColumnAlignment.None;
            cells.Add(alignment switch
            {
                ColumnAlignment.Left => ":---",
                ColumnAlignment.Center => ":---:",
                ColumnAlignment.Right => "---:",
                _ => "---",
            });
        }

        return "| " + string.Join(" | ", cells) + " |";
    }

    private static string CaptureInlines(InlineCollection inlines, bool suppressBold = false)
    {
        var segments = new List<Segment>();
        foreach (var inline in inlines)
        {
            Flatten(inline, segments);
        }

        if (suppressBold)
        {
            segments = segments
                .Select(segment => segment.Verbatim || segment.IsBreak ? segment : segment with { Bold = false })
                .ToList();
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
            case Run { Tag: ImageRole image }:
                segments.Add(Verbatim(EmitImage(image)));
                break;

            case Run { Tag: TaskMarkerRole task } marker:
            {
                // The marker owns the separator (the Projector strips the one the source carried on
                // the following text), so it emits its own trailing space: "- [ ] todo".
                segments.Add(Verbatim(task.Checked ? "[x] " : "[ ] "));

                // A Task Marker's Run is ordinary editable text, and the caret legitimately sits
                // inside it — a new task item's marker is its only inline, so WPF normalises the
                // caret into it and the first thing typed lands there. That text is the item's
                // content, not the marker, and emitting the marker from its role alone would drop
                // it silently: it would show on screen and never reach the Markdown.
                var typed = TextBeyondGlyph(marker.Text);
                if (typed.Length > 0)
                {
                    segments.Add(new Segment(typed, false, false, false, false, false, Verbatim: false));
                }

                break;
            }

            case Run run when run.Text.Length > 0:
                segments.Add(new Segment(
                    run.Text,
                    Bold: run.FontWeight.ToOpenTypeWeight() >= FontWeights.Bold.ToOpenTypeWeight(),
                    Italic: run.FontStyle == FontStyles.Italic,
                    Strike: HasStrikethrough(run),
                    Code: HasRole(run, InlineSemantic.Code),
                    IsBreak: false,
                    Verbatim: false));
                break;

            case Hyperlink link:
                segments.Add(Verbatim(EmitLink(link)));
                break;

            case LineBreak lineBreak:
                var hard = lineBreak.Tag is InlineSemantic.HardBreak;
                segments.Add(new Segment(hard ? "\\\n" : "\n", false, false, false, false, IsBreak: true, Verbatim: false));
                break;

            case Span span:
                foreach (var child in span.Inlines)
                {
                    Flatten(child, segments);
                }

                break;
        }
    }

    private static Segment Verbatim(string text) => new(text, false, false, false, false, false, Verbatim: true);

    // Whatever a Task Marker's Run holds beyond its checkbox glyph and the single space that
    // separates it from the item's text — that is, text the user typed into the marker's Run.
    private static string TextBeyondGlyph(string markerText)
    {
        var rest = markerText.Length > 0 && markerText[0] is TaskMarkerEditing.UncheckedGlyphChar
            or TaskMarkerEditing.CheckedGlyphChar
            ? markerText[1..]
            : markerText;

        return rest.StartsWith(' ') ? rest[1..] : rest;
    }

    private static string EmitLink(Hyperlink link)
    {
        var inner = CaptureInlines(link.Inlines);
        var role = link.Tag as LinkRole;
        var url = role?.Url ?? link.NavigateUri?.ToString() ?? string.Empty;
        return "[" + inner + "](" + url + TitleSuffix(role?.Title) + ")";
    }

    private static string EmitImage(ImageRole image) =>
        "![" + image.Alt + "](" + image.Url + TitleSuffix(image.Title) + ")";

    private static string TitleSuffix(string? title) =>
        string.IsNullOrEmpty(title) ? string.Empty : " \"" + title + "\"";

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
        if (segment.Verbatim || segment.IsBreak)
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

        if (prefix.Length == 0)
        {
            return segment.Text;
        }

        // An emphasis delimiter must hug its text: `**bold **` and `~~struck ~~` do not close in
        // Markdown, because a closing delimiter preceded by whitespace is not right-flanking. A user
        // selecting a word by double-click or Ctrl+Shift+Right takes its trailing space with it, so
        // the surrounding whitespace is hoisted outside the delimiters rather than emitted inside
        // them — otherwise the Markdown would say "literal tildes" where the Visual Document says
        // "struck through" (INV-018).
        var core = segment.Text.AsSpan().Trim(WhitespaceChars);
        if (core.Length == 0)
        {
            // Whitespace alone carries no emphasis, and `~~ ~~` would be nonsense.
            return segment.Text;
        }

        var leadingLength = segment.Text.Length - segment.Text.AsSpan().TrimStart(WhitespaceChars).Length;
        var leading = segment.Text[..leadingLength];
        var trailing = segment.Text[(leadingLength + core.Length)..];

        return leading + prefix + core.ToString() + suffix + trailing;
    }

    // The whitespace an emphasis delimiter must not sit against. Newlines included: a segment can
    // span a soft line break, and a delimiter left against one closes no better than against a space.
    private static readonly char[] WhitespaceChars = [' ', '\t', '\r', '\n'];

    private static string InlineText(InlineCollection inlines)
    {
        var builder = new StringBuilder();
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case Run run:
                    builder.Append(run.Text);
                    break;
                case LineBreak:
                    builder.Append('\n');
                    break;
            }
        }

        return builder.ToString();
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
