using System.Text;
using MigraDoc.DocumentObjectModel;

namespace Infrastructure.Tests;

/// <summary>
/// Reads back the text a composed MigraDoc document carries, so a test can assert on what an
/// Export as PDF says without inspecting MigraDoc's element tree by hand.
/// </summary>
internal static class ComposedText
{
    /// <summary>The text of every paragraph in the section, in order.</summary>
    /// <param name="section">The composed section.</param>
    /// <returns>The section's text, a line break written as <c>\n</c>.</returns>
    public static string Of(Section section)
    {
        var text = new StringBuilder();
        foreach (var paragraph in section.Elements.OfType<Paragraph>())
        {
            text.Append(Of(paragraph));
        }

        return text.ToString();
    }

    /// <summary>The text of a single paragraph.</summary>
    /// <param name="paragraph">The composed paragraph.</param>
    /// <returns>The paragraph's text, a line break written as <c>\n</c>.</returns>
    public static string Of(Paragraph paragraph)
    {
        var text = new StringBuilder();
        Collect(paragraph.Elements, text);
        return text.ToString();
    }

    private static void Collect(ParagraphElements elements, StringBuilder text)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case Text run:
                    text.Append(run.Content);
                    break;
                case FormattedText formatted:
                    Collect(formatted.Elements, text);
                    break;
                case Character { SymbolName: SymbolName.LineBreak }:
                    text.Append('\n');
                    break;
            }
        }
    }
}
