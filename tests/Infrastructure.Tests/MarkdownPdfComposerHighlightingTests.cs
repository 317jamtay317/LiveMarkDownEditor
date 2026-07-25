using System.Text;
using Infrastructure.Markdown;
using Infrastructure.Pdf;
using MigraDoc.DocumentObjectModel;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests that an Export as PDF colors a Code Block by the same Code Tokens the Visual Document
/// shows, and that coloring it never changes the code the PDF carries (INV-064, INV-033).
/// </summary>
public sealed class MarkdownPdfComposerHighlightingTests
{
    private static Document Compose(string markdown, bool colored = true)
    {
        var ast = Markdig.Markdown.Parse(markdown, GfmPipeline.Create());
        return new MarkdownPdfComposer(
            diagrams: null,
            syntaxHighlighter: colored ? new ColorCodeSyntaxHighlighter() : null).Compose(ast);
    }

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
                case Character { SymbolName: SymbolName.LineBreak }:
                    text.Append('\n');
                    break;
            }
        }
    }

    // Every colored piece of the composed document, as (text, color) pairs.
    private static List<(string Text, Color Color)> ColoredRuns(Document doc)
    {
        var runs = new List<(string, Color)>();
        foreach (var paragraph in doc.LastSection.Elements.OfType<Paragraph>())
        {
            Collect(paragraph.Elements, runs);
        }

        return runs;

        static void Collect(ParagraphElements elements, List<(string, Color)> runs)
        {
            foreach (var element in elements)
            {
                if (element is FormattedText formatted)
                {
                    var text = new StringBuilder();
                    CollectText(formatted.Elements, text);
                    runs.Add((text.ToString(), formatted.Font.Color));
                }
            }
        }
    }

    [Fact]
    public void Compose_ColorsACodeBlocksKeywordsAndComments_INV064()
    {
        var doc = Compose("```csharp\n// note\nconst int N = 42;\n```");

        var colored = ColoredRuns(doc);
        colored.ShouldContain(run => run.Text == "// note");
        colored.ShouldContain(run => run.Text == "const");
        colored.ShouldContain(run => run.Text == "42");
    }

    [Fact]
    public void Compose_GivesDifferentCodeTokenKindsDifferentColors_INV064()
    {
        var doc = Compose("```csharp\n// note\nconst int N = 42;\n```");

        var colored = ColoredRuns(doc);
        var comment = colored.First(run => run.Text == "// note").Color;
        var keyword = colored.First(run => run.Text == "const").Color;

        keyword.ShouldNotBe(comment);
    }

    [Fact]
    public void Compose_CarriesExactlyTheSameCodeAsAnUncoloredExport_INV064()
    {
        // The coloring splits the code across runs; it must never change what those runs say.
        const string markdown = "```csharp\n// note\nconst int N = 42;\nvar s = \"hi\";\n```";

        SectionText(Compose(markdown)).ShouldBe(SectionText(Compose(markdown, colored: false)));
    }

    [Fact]
    public void Compose_KeepsEachCodeLineOnItsOwnLine_INV064()
    {
        var doc = Compose("```csharp\nint a = 1;\nint b = 2;\n```");

        SectionText(doc).ShouldBe("int a = 1;\nint b = 2;");
    }

    [Fact]
    public void Compose_GivenNoHighlightingLanguage_WritesTheCodeUncolored_INV064()
    {
        const string markdown = "```\npublic class Foo { }\n```";

        SectionText(Compose(markdown)).ShouldBe("public class Foo { }");
        ColoredRuns(Compose(markdown)).ShouldBeEmpty();
    }

    [Fact]
    public void Compose_GivenAnUnknownLanguage_WritesTheCodeUncolored_INV064()
    {
        const string markdown = "```bash\nls -la | grep x\n```";

        SectionText(Compose(markdown)).ShouldBe("ls -la | grep x");
        ColoredRuns(Compose(markdown)).ShouldBeEmpty();
    }

    [Fact]
    public void Compose_WithNoHighlighter_WritesTheCodeExactlyAsBefore_INV064()
    {
        const string markdown = "```csharp\nconst int N = 42;\n```";

        SectionText(Compose(markdown, colored: false)).ShouldBe("const int N = 42;");
        ColoredRuns(Compose(markdown, colored: false)).ShouldBeEmpty();
    }

    [Fact]
    public void Compose_FallingBackFromAnUnrenderedMermaidDiagram_WritesItsSourcePlain_INV064()
    {
        // A Mermaid Diagram with no image falls back to its source as an ordinary Code Block
        // (INV-050); mermaid has no grammar, so that fallback stays uncolored.
        var doc = Compose("```mermaid\ngraph TD\n  A-->B\n```");

        SectionText(doc).ShouldBe("graph TD\n  A-->B");
        ColoredRuns(doc).ShouldBeEmpty();
    }
}
