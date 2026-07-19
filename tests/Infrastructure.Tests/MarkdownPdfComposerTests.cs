using System.Text;
using Infrastructure.Markdown;
using Infrastructure.Pdf;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="MarkdownPdfComposer"/>: it places a rendered Mermaid Diagram image where a
/// <c>mermaid</c> Code Block is, and falls back to the code text when no image was rendered (INV-050).
/// </summary>
public sealed class MarkdownPdfComposerTests
{
    private const string Diagram = "```mermaid\ngraph TD\n  A-->B\n```";
    private const string DiagramSource = "graph TD\n  A-->B";

    private static Document Compose(string markdown, IReadOnlyDictionary<string, PreparedDiagram>? diagrams = null)
    {
        var ast = Markdig.Markdown.Parse(markdown, GfmPipeline.Create());
        return new MarkdownPdfComposer(diagrams).Compose(ast);
    }

    private static int ImageCount(Document doc) => doc.LastSection.Elements.OfType<Image>().Count();

    private static string SectionText(Document doc)
    {
        var text = new StringBuilder();
        foreach (var paragraph in doc.LastSection.Elements.OfType<Paragraph>())
        {
            CollectText(paragraph.Elements, text);
        }

        return text.ToString();
    }

    private static void CollectText(ParagraphElements elements, StringBuilder text)
    {
        foreach (var element in elements)
        {
            switch (element)
            {
                case Text run:
                    text.Append(run.Content);
                    break;
                case FormattedText formatted:
                    CollectText(formatted.Elements, text);
                    break;
            }
        }
    }

    [Fact]
    public void Compose_WithAProvidedDiagramImage_PlacesAnImage_NotTheCode_INV050()
    {
        var diagrams = new Dictionary<string, PreparedDiagram>
        {
            [DiagramSource] = new(@"C:\temp\diagram.png", 200, 120),
        };

        var doc = Compose(Diagram, diagrams);

        ImageCount(doc).ShouldBe(1);
        SectionText(doc).ShouldNotContain("graph TD");
    }

    [Fact]
    public void Compose_WithNoImageForAMermaidBlock_FallsBackToTheCodeText_INV050()
    {
        var doc = Compose(Diagram);

        ImageCount(doc).ShouldBe(0);
        SectionText(doc).ShouldContain("graph TD");
    }

    [Fact]
    public void Compose_ANonMermaidCodeBlock_IsNeverPlacedAsAnImage_INV050()
    {
        // Only a `mermaid` Code Block becomes an image; an ordinary code block is always its text.
        var diagrams = new Dictionary<string, PreparedDiagram>
        {
            ["var x = 1;"] = new(@"C:\temp\diagram.png", 200, 120),
        };

        var doc = Compose("```csharp\nvar x = 1;\n```", diagrams);

        ImageCount(doc).ShouldBe(0);
        SectionText(doc).ShouldContain("var x = 1;");
    }
}
