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

        var ordered = blocks.ToList();

        // The Footnote Section is shown at the end of the Visual Document, but each Footnote Definition
        // it holds is written where it was authored — so the Definitions are lifted out and spliced in
        // after the blocks they remember, and the Section itself is never emitted (INV-065).
        var pending = FootnoteDefinitions(ordered);

        var captured = new List<string>();
        captured.AddRange(TakeAnchoredTo(pending, anchor: null));

        foreach (var block in ordered)
        {
            if (block is Section { Tag: BlockSemantic.FootnoteSection })
            {
                continue;
            }

            var text = CaptureBlock(block);
            if (text is not null)
            {
                captured.Add(text);
            }

            captured.AddRange(TakeAnchoredTo(pending, block));
        }

        // A Block Island (a Table, a Mermaid Diagram) is always followed by an empty paragraph so the
        // caret can reach the line below it (INV-055). That paragraph is a caret affordance, not
        // content: emitting it would append a blank line the user never typed, which Markdown drops
        // on the way back in — so the source would not converge on Round-Trip (INV-005).
        while (captured.Count > 0 && captured[^1].Length == 0)
        {
            captured.RemoveAt(captured.Count - 1);
        }

        // A Definition whose remembered block is no longer in the document — the user deleted it —
        // falls to the end rather than disappearing along with it (INV-065).
        captured.AddRange(pending.Select(Emit));

        return string.Join("\n\n", captured);
    }

    // Every Footnote Definition held by a Footnote Section among `blocks`, in the order it is shown.
    private static List<Section> FootnoteDefinitions(List<Block> blocks) =>
    [
        .. blocks
            .OfType<Section>()
            .Where(section => section.Tag is BlockSemantic.FootnoteSection)
            .SelectMany(section => section.Blocks.OfType<Section>())
            .Where(definition => definition.Tag is FootnoteDefinitionRole),
    ];

    // Emits and removes the Definitions authored after `anchor`, so each is written exactly once.
    private static List<string> TakeAnchoredTo(List<Section> pending, Block? anchor)
    {
        var due = pending.Where(definition => Role(definition).Anchor == anchor).ToList();
        due.ForEach(definition => pending.Remove(definition));
        return [.. due.Select(Emit)];
    }

    // Emits one Footnote Definition: `[^label]: ` then the note's own blocks, their continuation lines
    // indented so they stay inside the note. A blank separator line stays blank rather than becoming
    // indented whitespace, so repeated Round-Trips converge (INV-005).
    private static string Emit(Section definition) =>
        "[^" + Role(definition).Label + "]: " + Indent(
            new FlowDocumentToMarkdownCapturer().Capture(definition.Blocks),
            FootnoteContinuationIndent);

    private static FootnoteDefinitionRole Role(Section definition) => (FootnoteDefinitionRole)definition.Tag;

    // What a Footnote Definition's continuation lines are indented by, so they stay inside the note.
    private const string FootnoteContinuationIndent = "    ";

    // A Definition Description's colon and the three spaces that make it one (INV-066), and the indent
    // its continuation lines carry so they stay inside the Description.
    private const string DefinitionDescriptionMarker = ":   ";
    private const string DefinitionContinuationIndent = "    ";

    private static string? CaptureBlock(Block block) => block switch
    {
        Paragraph { Tag: HeadingRole heading } paragraph =>
            new string('#', heading.Level) + " " + CaptureInlines(paragraph.Inlines),
        Paragraph { Tag: CodeBlockRole codeRole } paragraph => CaptureCodeBlock(paragraph, codeRole),
        Paragraph { Tag: BlockSemantic.ThematicBreak } => "---",
        Paragraph paragraph => CaptureInlines(paragraph.Inlines),
        BlockUIContainer { Tag: MermaidDiagramRole diagram } => CaptureMermaidDiagram(diagram),
        WpfList list => CaptureList(list),
        Section { Tag: BlockSemantic.Quote } quote => CaptureQuote(quote),
        Section { Tag: BlockSemantic.DefinitionList } definitionList => CaptureDefinitionList(definitionList),
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

    // Emits a Definition List: each Definition Term on its own line, each Definition Description behind
    // a colon and three spaces with its continuation lines indented. The canonical form is exact
    // because the syntax is unforgiving (INV-066): a colon followed by fewer than three spaces is a
    // paragraph rather than a definition list, and an Item that has a Term needs the blank line before
    // it — without one its Term is absorbed into the Description above — while a term-less one must
    // *not* have it, because a blank line there makes that Description loose and changes the render.
    private static string CaptureDefinitionList(Section definitionList)
    {
        var lines = new List<string>();
        var afterDescription = false;

        foreach (var block in definitionList.Blocks)
        {
            if (block is Section { Tag: BlockSemantic.DefinitionDescription } description)
            {
                lines.Add(DefinitionDescriptionMarker + Indent(
                    new FlowDocumentToMarkdownCapturer().Capture(description.Blocks),
                    DefinitionContinuationIndent));
                afterDescription = true;
                continue;
            }

            if (block is Paragraph term)
            {
                // A Term following a Description begins a new Item; consecutive Terms share one.
                if (afterDescription)
                {
                    lines.Add(string.Empty);
                }

                lines.Add(CaptureInlines(term.Inlines));
                afterDescription = false;
            }
        }

        return string.Join("\n", lines);
    }

    // Indents every line of `text` after the first, leaving blank separator lines blank so repeated
    // Round-Trips converge (INV-005). Shared by a Footnote Definition and a Definition Description,
    // which carry their continuation lines the same way.
    private static string Indent(string text, string indent) =>
        string.Join("\n", text.Split('\n').Select((line, index) =>
            index == 0 || line.Length == 0 ? line : indent + line));

    // Emits a fenced code block. The code text is read back from the paragraph's own inlines (Runs
    // separated by LineBreaks) so any edits to the code are captured; the language comes from the role.
    private static string CaptureCodeBlock(Paragraph paragraph, CodeBlockRole role)
    {
        var code = InlineText(paragraph.Inlines);
        return "```" + (role.Language ?? string.Empty) + "\n" + code + "\n```";
    }

    // Emits a Mermaid Diagram as its fenced ```mermaid``` block, from the source carried on the diagram
    // block's role — the block shows the rendered picture, so the source lives on the role (INV-047).
    private static string CaptureMermaidDiagram(MermaidDiagramRole diagram) =>
        "```mermaid\n" + diagram.Source + "\n```";

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
            // An Image, whichever way it is being shown: as its picture (an InlineUIContainer) or as
            // its alt text (a Run). Both carry the same ImageRole, so both re-emit the Image Source
            // their author wrote rather than the absolute path it resolved to (INV-031).
            case InlineUIContainer { Tag: ImageRole picture }:
                segments.Add(Verbatim(EmitImage(picture)));
                break;

            case Run { Tag: ImageRole image }:
                segments.Add(Verbatim(EmitImage(image)));
                break;

            // A Footnote Reference shows a Footnote Number but is captured from its Footnote Label, so a
            // Footnote is never renumbered into the user's document (INV-065). The number is the Run's
            // whole text; anything the user typed against it is their content, and is kept.
            case Run { Tag: FootnoteReferenceRole reference } cited:
                segments.Add(Verbatim("[^" + reference.Label + "]"));
                AddTyped(TextBeyondDigits(cited.Text), segments);
                break;

            // A Footnote Definition's Footnote Number is presentation: it emits nothing at all, so the
            // number can never reach the Markdown Document (INV-065).
            case Run { Tag: FootnoteNumberRole } numbered:
                AddTyped(TextBeyondMarker(numbered.Text), segments);
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

    // Text a user typed into a presentation-only Run — a Footnote Reference's or Footnote Number's — is
    // their content, and is captured as ordinary prose rather than swallowed by the marker.
    private static void AddTyped(string typed, List<Segment> segments)
    {
        if (typed.Length > 0)
        {
            segments.Add(new Segment(typed, false, false, false, false, false, Verbatim: false));
        }
    }

    // Whatever a Footnote Reference's Run holds beyond the Footnote Number it shows.
    private static string TextBeyondDigits(string text) => text.TrimStart(Digits);

    // Whatever a Footnote Definition's marker Run holds beyond the "N. " it shows.
    private static string TextBeyondMarker(string text)
    {
        var rest = text.TrimStart(Digits);
        rest = rest.StartsWith('.') ? rest[1..] : rest;
        return rest.StartsWith(' ') ? rest[1..] : rest;
    }

    private static readonly char[] Digits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];

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
            // Unlike emphasis, a Code Span can legitimately be nothing but whitespace, so a blank one
            // keeps the backticks it came in with rather than dissolving into a bare space.
            return Hug(segment.Text, "`", "`", whenBlank: "`" + segment.Text + "`");
        }

        var prefix = (segment.Strike ? "~~" : string.Empty)
            + (segment.Bold ? "**" : string.Empty)
            + (segment.Italic ? "*" : string.Empty);
        var suffix = (segment.Italic ? "*" : string.Empty)
            + (segment.Bold ? "**" : string.Empty)
            + (segment.Strike ? "~~" : string.Empty);

        // Whitespace alone carries no emphasis, and `~~ ~~` would be nonsense.
        return prefix.Length == 0 ? segment.Text : Hug(segment.Text, prefix, suffix, whenBlank: segment.Text);
    }

    // A delimiter must hug its text: `**bold **` and `~~struck ~~` do not close in Markdown, because a
    // closing delimiter preceded by whitespace is not right-flanking, and `` `fast `now `` shades the
    // separator as code and leaves the next word butted against it. A user selecting a word by
    // double-click or Ctrl+Shift+Right takes its trailing space with it, so the surrounding whitespace
    // is hoisted outside the delimiters rather than emitted inside them — otherwise the Markdown would
    // say "literal tildes" where the Visual Document says "struck through" (INV-018).
    private static string Hug(string text, string prefix, string suffix, string whenBlank)
    {
        var core = text.AsSpan().Trim(WhitespaceChars);
        if (core.Length == 0)
        {
            return whenBlank;
        }

        var leadingLength = text.Length - text.AsSpan().TrimStart(WhitespaceChars).Length;
        return text[..leadingLength] + prefix + core.ToString() + suffix + text[(leadingLength + core.Length)..];
    }

    // The whitespace a delimiter must not sit against. Newlines included: a segment can
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
