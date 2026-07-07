using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Infrastructure.Markdown;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
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
/// with its <see cref="InlineSemantic"/> / <see cref="HeadingRole"/> so the inverse
/// <see cref="FlowDocumentToMarkdownCapturer"/> can reconstruct the original Markdown.
/// Satisfies INV-003: the projection is a pure function of the source text.
/// </remarks>
public sealed class MarkdownToFlowDocumentProjector
{
    private static readonly FontFamily MonospaceFont = new("Consolas, Cascadia Mono, Courier New");

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
                };
                AppendInlines(paragraph.Inlines, heading.Inline);
                return paragraph;
            }

            case ParagraphBlock paragraphBlock:
            {
                var paragraph = new Paragraph();
                AppendInlines(paragraph.Inlines, paragraphBlock.Inline);
                return paragraph;
            }

            default:
                return null;
        }
    }

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
                return new Run(code.Content) { Tag = InlineSemantic.Code, FontFamily = MonospaceFont };

            case EmphasisInline emphasis:
                return ProjectEmphasis(emphasis);

            case LineBreakInline:
                return new LineBreak();

            default:
                return null;
        }
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
