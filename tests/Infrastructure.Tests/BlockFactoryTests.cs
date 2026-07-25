using Infrastructure.Markdown;
using Infrastructure.Pdf;
using Infrastructure.Pdf.Blocks;
using Markdig.Extensions.DefinitionLists;
using Markdig.Syntax;
using MigraDoc.DocumentObjectModel;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests for <see cref="BlockFactory"/>: it is the one place that knows which Block Writer writes
/// which Markdown block, so an Export as PDF gains a construct by gaining a writer (INV-033).
/// </summary>
public sealed class BlockFactoryTests
{
    private const string Diagram = "```mermaid\ngraph TD\n  A-->B\n```";
    private const string DiagramSource = "graph TD\n  A-->B";

    private static Block FirstBlockOf(string markdown) =>
        Markdig.Markdown.Parse(markdown, GfmPipeline.Create())[0];

    private static Block LastBlockOf(string markdown)
    {
        var ast = Markdig.Markdown.Parse(markdown, GfmPipeline.Create());
        return ast[ast.Count - 1];
    }

    private static IBlockWriter WriterFor(Block block, IReadOnlyDictionary<string, PreparedDiagram>? diagrams = null) =>
        new BlockFactory(diagrams).Create(block);

    [Theory]
    [InlineData("# Heading", typeof(HeadingWriter))]
    [InlineData("Just prose.", typeof(ParagraphWriter))]
    [InlineData("- one\n- two", typeof(ListWriter))]
    [InlineData("> quoted", typeof(BlockQuoteWriter))]
    [InlineData("```csharp\nvar x = 1;\n```", typeof(CodeBlockWriter))]
    [InlineData("---", typeof(ThematicBreakWriter))]
    [InlineData("| a | b |\n| --- | --- |\n| 1 | 2 |", typeof(TableWriter))]
    [InlineData("Markdown\n:   A markup language.", typeof(DefinitionListWriter))]
    public void Create_GivenABlock_CreatesTheWriterForThatKind(string markdown, Type expected)
    {
        WriterFor(FirstBlockOf(markdown)).ShouldBeOfType(expected);
    }

    [Fact]
    public void Create_GivenTheFootnoteSection_CreatesTheFootnoteSectionWriter_INV065()
    {
        WriterFor(LastBlockOf("A claim.[^a]\n\n[^a]: the note")).ShouldBeOfType<FootnoteSectionWriter>();
    }

    [Fact]
    public void Create_GivenAMermaidCodeBlockWithARenderedImage_CreatesTheDiagramWriter_INV050()
    {
        var diagrams = new Dictionary<string, PreparedDiagram>
        {
            [DiagramSource] = new(@"C:\temp\diagram.png", 200, 120),
        };

        WriterFor(FirstBlockOf(Diagram), diagrams).ShouldBeOfType<MermaidDiagramWriter>();
    }

    [Fact]
    public void Create_GivenAMermaidCodeBlockWithNoRenderedImage_CreatesTheCodeBlockWriter_INV050()
    {
        // With no image the diagram falls back to its source text, which is an ordinary Code Block.
        WriterFor(FirstBlockOf(Diagram)).ShouldBeOfType<CodeBlockWriter>();
    }

    [Fact]
    public void Create_GivenANonMermaidCodeBlockWithAMatchingImage_CreatesTheCodeBlockWriter_INV050()
    {
        var diagrams = new Dictionary<string, PreparedDiagram>
        {
            ["var x = 1;"] = new(@"C:\temp\diagram.png", 200, 120),
        };

        WriterFor(FirstBlockOf("```csharp\nvar x = 1;\n```"), diagrams).ShouldBeOfType<CodeBlockWriter>();
    }

    [Fact]
    public void Create_GivenALeafBlockCarryingInlines_CreatesTheParagraphWriter()
    {
        // A Definition Term is a leaf block that is not a paragraph; its inlines still print as one.
        var term = ((DefinitionItem)((DefinitionList)FirstBlockOf("Markdown\n:   A markup language."))[0])[0];

        WriterFor(term).ShouldBeOfType<ParagraphWriter>();
    }

    [Fact]
    public void Create_GivenAContainerOfOtherBlocks_CreatesTheNestedBlocksWriter()
    {
        WriterFor(LastBlockOf("[ref]: https://example.com")).ShouldBeOfType<NestedBlocksWriter>();
    }

    [Fact]
    public void Create_GivenABlockAPdfCarriesNothingFor_CreatesAWriterThatWritesNothing()
    {
        var writer = WriterFor(FirstBlockOf("<div>markup</div>"));

        writer.ShouldBeOfType<SkippedBlockWriter>();

        var section = new Document().AddSection();
        writer.Write(FirstBlockOf("<div>markup</div>"), new BlockContext(section, new BlockFactory()));

        section.Elements.Count.ShouldBe(0);
    }

    [Fact]
    public void Create_GivenTwoBlocksOfTheSameKind_CreatesTheSameWriter()
    {
        // A Block Writer holds no state of its own, so one instance serves every block of its kind.
        var factory = new BlockFactory();

        factory.Create(FirstBlockOf("# One")).ShouldBeSameAs(factory.Create(FirstBlockOf("## Two")));
    }
}
