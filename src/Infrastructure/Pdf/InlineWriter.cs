using System.Globalization;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.TaskLists;
using Markdig.Syntax.Inlines;
using MigraDoc.DocumentObjectModel;

namespace Infrastructure.Pdf;

/// <summary>
/// Writes the inline content of a block — its text and the formatting carried within it — into a
/// MigraDoc paragraph a Block Writer has already shaped.
/// </summary>
/// <remarks>
/// Strikethrough has no MigraDoc equivalent, so struck text keeps its content without the rule, and
/// an inline a PDF carries nothing for (raw HTML, a Task List marker a List Item writes itself) is
/// passed over rather than printed as source.
/// </remarks>
internal static class InlineWriter
{
    /// <summary>Writes every inline of a block into the given paragraph.</summary>
    /// <param name="inlines">The block's inlines. <see langword="null"/> writes nothing.</param>
    /// <param name="paragraph">The paragraph the inlines are written into.</param>
    public static void Write(ContainerInline? inlines, Paragraph paragraph) =>
        Write(inlines, paragraph, default);

    private static void Write(ContainerInline? inlines, Paragraph paragraph, InlineStyle style)
    {
        if (inlines is null)
        {
            return;
        }

        foreach (var inline in inlines)
        {
            Write(inline, paragraph, style);
        }
    }

    private static void Write(Inline inline, Paragraph paragraph, InlineStyle style)
    {
        switch (inline)
        {
            case LiteralInline literal:
                AddRun(paragraph, literal.Content.ToString(), style);
                break;
            case EmphasisInline emphasis:
                Write(emphasis, paragraph, style.WithEmphasis(emphasis));
                break;
            case CodeInline code:
                AddRun(paragraph, code.Content, style with { Code = true });
                break;
            case LinkInline { IsImage: true } image:
                WriteImage(image, paragraph, style);
                break;
            case LinkInline link:
                Write(link, paragraph, style with { Link = true });
                break;
            case AutolinkInline autolink:
                AddRun(paragraph, autolink.Url, style with { Link = true });
                break;
            case LineBreakInline lineBreak:
                if (lineBreak.IsHard)
                {
                    paragraph.AddLineBreak();
                }
                else
                {
                    AddRun(paragraph, " ", style);
                }

                break;
            case HtmlEntityInline entity:
                AddRun(paragraph, entity.Transcoded.ToString(), style);
                break;
            // A Footnote Reference is the superscript Footnote Number that stands in the prose; the
            // back-reference under the note is the Rendered Output's navigation and prints nothing
            // (INV-065).
            case FootnoteLink { IsBackLink: true }:
                break;
            case FootnoteLink reference:
                AddRun(
                    paragraph,
                    reference.Footnote.Order.ToString(CultureInfo.InvariantCulture),
                    style with { Superscript = true });
                break;
            case TaskList:
            case HtmlInline:
                break;
            case ContainerInline container:
                Write(container, paragraph, style);
                break;
        }
    }

    private static void WriteImage(LinkInline image, Paragraph paragraph, InlineStyle style)
    {
        // No Base Directory is available here, so the alt text (the image's child inlines) is shown
        // rather than the picture — the correct fallback for an image that cannot be embedded (INV-031).
        if (image.FirstChild is not null)
        {
            Write(image, paragraph, style);
        }
        else
        {
            AddRun(paragraph, image.Url ?? string.Empty, style);
        }
    }

    private static void AddRun(Paragraph paragraph, string text, InlineStyle style)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var run = paragraph.AddFormattedText(text);
        run.Bold = style.Bold;
        run.Italic = style.Italic;
        if (style.Code)
        {
            run.Font.Name = PdfStyle.CodeFont;
        }

        if (style.Link)
        {
            run.Font.Color = Colors.RoyalBlue;
            run.Font.Underline = Underline.Single;
        }

        if (style.Superscript)
        {
            run.Font.Superscript = true;
        }
    }
}
