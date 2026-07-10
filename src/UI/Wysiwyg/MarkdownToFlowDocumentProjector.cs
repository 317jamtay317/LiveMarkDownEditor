using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Infrastructure.Markdown;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MarkdigTable = Markdig.Extensions.Tables.Table;
using MarkdigTableRow = Markdig.Extensions.Tables.TableRow;
using MarkdigTableCell = Markdig.Extensions.Tables.TableCell;
using MarkdigTableColumnAlign = Markdig.Extensions.Tables.TableColumnAlign;
using MarkdigTaskList = Markdig.Extensions.TaskLists.TaskList;
using WpfInline = System.Windows.Documents.Inline;
using WpfBlock = System.Windows.Documents.Block;

namespace UI.Wysiwyg;

/// <summary>
/// Projects a Markdown Document's source text into a Visual Document (a <see cref="FlowDocument"/>)
/// for single-pane WYSIWYG editing. Formatting is expressed as real <see cref="FlowDocument"/>
/// elements — a heading looks like a heading, bold is bold — so raw Markdown syntax is never shown.
/// </summary>
/// <remarks>
/// Parsing uses the shared <see cref="GfmPipeline"/>, the same pipeline the HTML renderer uses, so
/// the editing surface and any exported HTML agree on the GFM feature set. Each element is tagged
/// with its <see cref="InlineSemantic"/> / role so the inverse <see cref="FlowDocumentToMarkdownCapturer"/>
/// can reconstruct the original Markdown. Satisfies INV-003: the projection is a pure function of the
/// source text.
/// </remarks>
public sealed class MarkdownToFlowDocumentProjector
{
    private static readonly FontFamily MonospaceFont = new("Consolas, Cascadia Mono, Courier New");

    // BCP-47 "zxx" = "no linguistic content". The WYSIWYG editor enables spell check for prose, but
    // code is not prose: identifiers like this.Should().NotBe() are not misspellings. WPF's speller
    // skips any run whose Language has no dictionary, so tagging code runs "zxx" excludes just them
    // while leaving the surrounding prose checked.
    private static readonly XmlLanguage NoProofingLanguage = XmlLanguage.GetLanguage("zxx");

    // WPF's default Paragraph margin double-spaces the Visual Document, which reads as large gaps
    // between lines. A small, uniform block spacing looks like a real editor; a heading gets a little
    // extra room above so it stands off the preceding Section.
    private static readonly Thickness BodySpacing = new(0, 0, 0, 6);
    private static readonly Thickness HeadingSpacing = new(0, 12, 0, 4);

    /// <summary>Projects Markdown source text into a Visual Document.</summary>
    /// <param name="markdown">The Markdown source text. <see langword="null"/> is treated as empty.</param>
    /// <returns>A <see cref="FlowDocument"/> presenting the formatted content.</returns>
    public FlowDocument Project(string markdown)
    {
        var pipeline = GfmPipeline.Create();
        var ast = Markdig.Markdown.Parse(markdown ?? string.Empty, pipeline);

        var document = new FlowDocument();
        foreach (var block in ast)
        {
            var projected = ProjectBlock(block);
            if (projected is not null)
            {
                document.Blocks.Add(projected);
            }
        }

        return document;
    }

    private static WpfBlock? ProjectBlock(Markdig.Syntax.Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
            {
                // Headings are distinguished visually by size only, not FontWeight: an inherited
                // bold weight would make Capture treat every heading run as inline-bold (# **x**).
                var paragraph = new Paragraph
                {
                    Tag = new HeadingRole(heading.Level),
                    FontSize = HeadingFontSize(heading.Level),
                    Margin = HeadingSpacing,
                };
                AppendInlines(paragraph.Inlines, heading.Inline);
                return paragraph;
            }

            case ParagraphBlock paragraphBlock:
            {
                var paragraph = new Paragraph { Margin = BodySpacing };
                AppendInlines(paragraph.Inlines, paragraphBlock.Inline);
                return paragraph;
            }

            case ListBlock listBlock:
                return ProjectList(listBlock);

            case QuoteBlock quoteBlock:
                return ProjectQuote(quoteBlock);

            case FencedCodeBlock fenced:
                return ProjectCodeBlock(fenced, fenced.Info);

            case CodeBlock codeBlock:
                return ProjectCodeBlock(codeBlock, language: null);

            case ThematicBreakBlock:
                return ProjectThematicBreak();

            case MarkdigTable table:
                return ProjectTable(table);

            default:
                return null;
        }
    }

    // A Markdown List becomes a WPF List whose marker mirrors the source: a bullet (Disc) for an
    // Unordered List, incrementing numbers (Decimal) for an Ordered List, starting at the source's
    // own start number. Each List Item's own blocks are projected in turn, so an item's paragraph —
    // and any nested List — is shown rather than dropped.
    private static WpfBlock ProjectList(ListBlock listBlock)
    {
        var list = new List
        {
            MarkerStyle = listBlock.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
            Margin = BodySpacing,
        };

        if (listBlock.IsOrdered && int.TryParse(listBlock.OrderedStart, out var start))
        {
            list.StartIndex = start;
        }

        foreach (var child in listBlock)
        {
            if (child is not ListItemBlock itemBlock)
            {
                continue;
            }

            var listItem = new ListItem();
            foreach (var itemChild in itemBlock)
            {
                var projected = ProjectBlock(itemChild);
                if (projected is not null)
                {
                    listItem.Blocks.Add(projected);
                }
            }

            // A ListItem must hold at least one block to be a valid FlowDocument element.
            if (listItem.Blocks.Count == 0)
            {
                listItem.Blocks.Add(new Paragraph());
            }

            list.ListItems.Add(listItem);
        }

        return list;
    }

    // A block quote becomes a Section with a left rule and muted text, holding the projected child
    // blocks. The BlockSemantic.Quote tag lets Capture re-emit the "> " prefix.
    private static WpfBlock ProjectQuote(QuoteBlock quoteBlock)
    {
        var section = new Section
        {
            Tag = BlockSemantic.Quote,
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 0, 0, 0),
            Margin = BodySpacing,
        };
        section.SetResourceReference(TextElement.ForegroundProperty, "MutedTextBrush");
        section.SetResourceReference(WpfBlock.BorderBrushProperty, "BorderBrush");

        foreach (var child in quoteBlock)
        {
            var projected = ProjectBlock(child);
            if (projected is not null)
            {
                section.Blocks.Add(projected);
            }
        }

        if (section.Blocks.Count == 0)
        {
            section.Blocks.Add(new Paragraph());
        }

        return section;
    }

    // A code block becomes a monospace paragraph whose inline content is the code text (one Run per
    // line, separated by LineBreaks). The CodeBlockRole tag carries the fence language so Capture can
    // re-emit a fenced block; the code itself is read back from the paragraph's inlines, so edits to
    // the code survive a Round-Trip. Its shaded panel is Code Shading, drawn by the CodeShadingAdorner
    // overlay rather than a Background here, so a theme recolour never re-formats the code (INV-017).
    private static WpfBlock ProjectCodeBlock(LeafBlock codeBlock, string? language)
    {
        var paragraph = new Paragraph
        {
            Tag = new CodeBlockRole(string.IsNullOrEmpty(language) ? null : language),
            FontFamily = MonospaceFont,
            Language = NoProofingLanguage,
            Padding = new Thickness(8),
            Margin = BodySpacing,
        };

        var lines = ExtractCode(codeBlock).Split('\n');
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

    private static string ExtractCode(LeafBlock codeBlock)
    {
        var lines = codeBlock.Lines;
        var slices = new List<string>(lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            slices.Add(lines.Lines[i].Slice.ToString());
        }

        return string.Join("\n", slices);
    }

    // A thematic break becomes an empty paragraph drawn as a horizontal rule (a bottom border). The
    // BlockSemantic.ThematicBreak tag lets Capture re-emit "---".
    private static WpfBlock ProjectThematicBreak()
    {
        var paragraph = new Paragraph
        {
            Tag = BlockSemantic.ThematicBreak,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Margin = new Thickness(0, 6, 0, 6),
        };
        paragraph.SetResourceReference(WpfBlock.BorderBrushProperty, "BorderBrush");
        paragraph.Inlines.Add(new Run(string.Empty));
        return paragraph;
    }

    // A GFM pipe table becomes a WPF Table. The TableRole tag records each column's alignment so
    // Capture can reproduce the delimiter row; the first (header) row is emboldened.
    private static WpfBlock ProjectTable(MarkdigTable table)
    {
        var alignments = table.ColumnDefinitions
            .Select(column => MapAlignment(column.Alignment))
            .ToList();

        var wpfTable = new Table
        {
            Tag = new TableRole(alignments),
            CellSpacing = 0,
            Margin = BodySpacing,
        };
        for (var i = 0; i < alignments.Count; i++)
        {
            wpfTable.Columns.Add(new TableColumn());
        }

        var group = new TableRowGroup();
        foreach (var child in table)
        {
            if (child is not MarkdigTableRow row)
            {
                continue;
            }

            var wpfRow = new TableRow();
            foreach (var cellChild in row)
            {
                if (cellChild is not MarkdigTableCell cell)
                {
                    continue;
                }

                var paragraph = new Paragraph { Margin = new Thickness(0) };
                foreach (var cellBlock in cell)
                {
                    if (cellBlock is ParagraphBlock cellParagraph)
                    {
                        AppendInlines(paragraph.Inlines, cellParagraph.Inline);
                    }
                }

                if (row.IsHeader)
                {
                    paragraph.FontWeight = FontWeights.Bold;
                }

                var wpfCell = new TableCell(paragraph)
                {
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(6, 3, 6, 3),
                };
                wpfCell.SetResourceReference(WpfBlock.BorderBrushProperty, "BorderBrush");
                wpfRow.Cells.Add(wpfCell);
            }

            group.Rows.Add(wpfRow);
        }

        wpfTable.RowGroups.Add(group);
        return wpfTable;
    }

    private static ColumnAlignment MapAlignment(MarkdigTableColumnAlign? alignment) => alignment switch
    {
        MarkdigTableColumnAlign.Left => ColumnAlignment.Left,
        MarkdigTableColumnAlign.Center => ColumnAlignment.Center,
        MarkdigTableColumnAlign.Right => ColumnAlignment.Right,
        _ => ColumnAlignment.None,
    };

    private static void AppendInlines(InlineCollection target, ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }

        foreach (var inline in container)
        {
            var projected = ProjectInline(inline);
            if (projected is not null)
            {
                target.Add(projected);
            }
        }
    }

    private static WpfInline? ProjectInline(Markdig.Syntax.Inlines.Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                return new Run(literal.Content.ToString());

            case CodeInline code:
            {
                // Monospace alone reads like body text; an accent colour plus Code Shading make an
                // inline code span visibly code. The Code tag (not the styling) is what Capture keys
                // on, and it is also what the CodeShadingScanner finds to shade the span. The shade is
                // drawn by the CodeShadingAdorner overlay, not a Background here, so a theme recolour
                // never re-formats the span (INV-017).
                var run = new Run(code.Content)
                {
                    Tag = InlineSemantic.Code,
                    FontFamily = MonospaceFont,
                    Language = NoProofingLanguage,
                };
                run.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
                return run;
            }

            case EmphasisInline emphasis:
                return ProjectEmphasis(emphasis);

            case MarkdigTaskList task:
                return new Run(task.Checked ? "☑ " : "☐ ") { Tag = new TaskMarkerRole(task.Checked) };

            case LinkInline { IsImage: true } image:
                return ProjectImage(image);

            case LinkInline link:
                return ProjectLink(link);

            case AutolinkInline autolink:
                // A bare URL re-autolinks when rendered, so a plain Run round-trips to the same link.
                return new Run(autolink.Url);

            case LineBreakInline lineBreak:
                return new LineBreak { Tag = lineBreak.IsHard ? InlineSemantic.HardBreak : null };

            default:
                return null;
        }
    }

    private static WpfInline ProjectLink(LinkInline link)
    {
        var hyperlink = new Hyperlink
        {
            Tag = new LinkRole(link.Url ?? string.Empty, link.Title),
        };
        AppendInlines(hyperlink.Inlines, link);
        if (Uri.TryCreate(link.Url, UriKind.RelativeOrAbsolute, out var uri))
        {
            hyperlink.NavigateUri = uri;
        }

        return hyperlink;
    }

    private static WpfInline ProjectImage(LinkInline image)
    {
        var alt = ExtractText(image);
        return new Run(alt) { Tag = new ImageRole(image.Url ?? string.Empty, alt, image.Title) };
    }

    private static string ExtractText(ContainerInline container)
    {
        var builder = new StringBuilder();
        foreach (var child in container)
        {
            switch (child)
            {
                case LiteralInline literal:
                    builder.Append(literal.Content.ToString());
                    break;
                case ContainerInline nested:
                    builder.Append(ExtractText(nested));
                    break;
            }
        }

        return builder.ToString();
    }

    private static WpfInline ProjectEmphasis(EmphasisInline emphasis)
    {
        Span span = emphasis.DelimiterChar switch
        {
            '~' => new Span { Tag = InlineSemantic.Strikethrough, TextDecorations = TextDecorations.Strikethrough },
            _ when emphasis.DelimiterCount == 2 => new Bold(),
            _ => new Italic(),
        };

        AppendInlines(span.Inlines, emphasis);
        return span;
    }

    private static double HeadingFontSize(int level) => level switch
    {
        1 => 28,
        2 => 24,
        3 => 20,
        4 => 17,
        5 => 15,
        _ => 13,
    };
}
