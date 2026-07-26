using Infrastructure.Markdown;
using Infrastructure.Pdf;
using Infrastructure.Pdf.Blocks;
using Markdig.Syntax;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Shapes;
using MigraDoc.DocumentObjectModel.Tables;
using Shouldly;
using Xunit;

namespace Infrastructure.Tests;

/// <summary>
/// Tests each Block Writer: an Export as PDF re-lays-out the Markdown, so every construct must land
/// on the page in the shape the Visual Document shows it in (INV-033).
/// </summary>
public sealed class BlockWriterTests
{
    private static Section Write(string markdown, IReadOnlyDictionary<string, PreparedDiagram>? diagrams = null)
    {
        var ast = Markdig.Markdown.Parse(markdown, GfmPipeline.Create());
        var section = new Document().AddSection();
        new BlockContext(section, new BlockFactory(diagrams)).WriteBlocks(ast);
        return section;
    }

    private static Block FirstBlockOf(string markdown) =>
        Markdig.Markdown.Parse(markdown, GfmPipeline.Create())[0];

    private static List<Paragraph> ParagraphsOf(Section section) =>
        section.Elements.OfType<Paragraph>().ToList();

    [Theory]
    [InlineData(1, 20)]
    [InlineData(2, 17)]
    [InlineData(3, 15)]
    [InlineData(4, 13)]
    [InlineData(5, 12)]
    [InlineData(6, 11)]
    public void Write_GivenAHeading_SizesItByItsHeadingLevel(int level, double size)
    {
        var section = Write(new string('#', level) + " Title");

        var heading = ParagraphsOf(section).Single();
        heading.Format.Font.Size.Point.ShouldBe(size);
        ComposedText.Of(heading).ShouldBe("Title");
    }

    [Fact]
    public void Write_GivenAHeading_KeepsItWithWhatFollowsIt()
    {
        // A Heading stranded at the foot of a page is what KeepWithNext exists to prevent.
        var section = Write("# Title\n\nProse.");

        ParagraphsOf(section)[0].Format.KeepWithNext.ShouldBe(true);
    }

    [Fact]
    public void Write_GivenAHeadingInsideABlockQuote_LeavesItUnquoted()
    {
        // A Heading is distinguished by size alone; the quote rule beside it would say nothing more.
        var section = Write("> # Quoted title");

        ParagraphsOf(section).Single().Format.Borders.Left.Width.Point.ShouldBe(0);
    }

    [Fact]
    public void Write_GivenAParagraph_WritesItsText()
    {
        ComposedText.Of(Write("Just prose.")).ShouldBe("Just prose.");
    }

    [Fact]
    public void Write_GivenAnUnorderedList_MarksEachItemWithABullet()
    {
        var section = Write("- one\n- two");

        ParagraphsOf(section).Select(ComposedText.Of).ShouldBe(["• one", "• two"]);
    }

    [Fact]
    public void Write_GivenAnOrderedList_NumbersTheItemsFromItsStart()
    {
        var section = Write("3. three\n4. four");

        ParagraphsOf(section).Select(ComposedText.Of).ShouldBe(["3. three", "4. four"]);
    }

    [Fact]
    public void Write_GivenATaskList_MarksEachItemCheckedOrUnchecked()
    {
        var section = Write("- [x] done\n- [ ] todo");

        // The Task Marker is written in place of the checkbox; the item's own text follows it.
        ParagraphsOf(section).Select(ComposedText.Of).ShouldBe(["[x]  done", "[ ]  todo"]);
    }

    [Fact]
    public void Write_GivenAList_IndentsItsItems()
    {
        var section = Write("- one");

        ParagraphsOf(section).Single().Format.LeftIndent.Point.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Write_GivenABlockQuote_IndentsItAndSetsItOffWithARule()
    {
        var section = Write("> quoted");

        var quoted = ParagraphsOf(section).Single();
        quoted.Format.LeftIndent.Point.ShouldBeGreaterThan(0);
        quoted.Format.Borders.Left.Width.Point.ShouldBeGreaterThan(0);
        quoted.Format.Font.Color.ShouldBe(Colors.DimGray);
    }

    [Fact]
    public void Write_GivenANestedBlockQuote_IndentsItFurtherThanTheOuterOne()
    {
        var section = Write("> outer\n>\n> > inner");

        var paragraphs = ParagraphsOf(section);
        paragraphs[1].Format.LeftIndent.Point.ShouldBeGreaterThan(paragraphs[0].Format.LeftIndent.Point);
    }

    [Fact]
    public void Write_GivenAThematicBreak_DrawsARule()
    {
        var section = Write("---");

        ParagraphsOf(section).Single().Format.Borders.Bottom.Width.Point.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Write_GivenACodeBlock_SetsItMonospacedAndShaded()
    {
        var section = Write("```\nvar x = 1;\n```");

        var code = ParagraphsOf(section).Single();
        code.Format.Font.Name.ShouldBe("Courier New");
        code.Format.Shading.Color.ShouldBe(Colors.WhiteSmoke);
        ComposedText.Of(code).ShouldBe("var x = 1;");
    }

    [Fact]
    public void Write_GivenATable_AddsAColumnForEachOfItsColumns()
    {
        var section = Write("| a | b |\n| --- | --- |\n| 1 | 2 |");

        section.Elements.OfType<Table>().Single().Columns.Count.ShouldBe(2);
    }

    [Fact]
    public void Write_GivenATable_BoldsItsHeaderRowAlone()
    {
        var section = Write("| a | b |\n| --- | --- |\n| 1 | 2 |");

        var table = section.Elements.OfType<Table>().Single();
        CellParagraph(table, row: 0, column: 0).Format.Font.Bold.ShouldBe(true);
        CellParagraph(table, row: 1, column: 0).Format.Font.Bold.ShouldBe(false);
    }

    [Fact]
    public void Write_GivenATable_AlignsEachColumnTheWayTheTableSays()
    {
        var section = Write("| a | b | c |\n|:---|:---:|---:|\n| 1 | 2 | 3 |");

        var table = section.Elements.OfType<Table>().Single();
        CellParagraph(table, row: 1, column: 0).Format.Alignment.ShouldBe(ParagraphAlignment.Left);
        CellParagraph(table, row: 1, column: 1).Format.Alignment.ShouldBe(ParagraphAlignment.Center);
        CellParagraph(table, row: 1, column: 2).Format.Alignment.ShouldBe(ParagraphAlignment.Right);
    }

    [Fact]
    public void Write_GivenATable_WritesEachCellsText()
    {
        var section = Write("| a | b |\n| --- | --- |\n| 1 | 2 |");

        var table = section.Elements.OfType<Table>().Single();
        ComposedText.Of(CellParagraph(table, row: 1, column: 1)).ShouldBe("2");
    }

    [Fact]
    public void Write_GivenATable_BandsEveryOtherBodyRow_INV068()
    {
        var section = Write("| a | b |\n| --- | --- |\n| 1 | 2 |\n| 3 | 4 |\n| 5 | 6 |\n| 7 | 8 |");

        // A banded Table stays banded wherever it is shown (INV-068). Row 0 is the header — never
        // banded — and the first Body Row is left plain, so the shade starts on the second.
        var table = section.Elements.OfType<Table>().Single();
        Banding(table).ShouldBe([false, false, true, false, true]);
    }

    [Fact]
    public void Write_GivenADiagramNarrowerThanThePage_KeepsItsNaturalSize_INV050()
    {
        // 200px at 96 DPI is 5.29cm — well inside the 16cm the page has room for.
        var section = Write(
            "```mermaid\ngraph TD\n  A-->B\n```",
            new Dictionary<string, PreparedDiagram> { ["graph TD\n  A-->B"] = new(@"C:\temp\d.png", 200, 120) });

        var image = section.Elements.OfType<Image>().Single();
        image.Width.Centimeter.ShouldBe(200 / 96.0 * 2.54, tolerance: 0.01);
        image.LockAspectRatio.ShouldBe(true);
    }

    [Fact]
    public void Write_GivenADiagramWiderThanThePage_ScalesItToTheUsableWidth_INV050()
    {
        var section = Write(
            "```mermaid\ngraph TD\n  A-->B\n```",
            new Dictionary<string, PreparedDiagram> { ["graph TD\n  A-->B"] = new(@"C:\temp\d.png", 2000, 1200) });

        section.Elements.OfType<Image>().Single().Width.Centimeter.ShouldBe(16.0, tolerance: 0.01);
    }

    [Fact]
    public void Write_GivenAContainerOfOtherBlocks_WritesTheBlocksInside()
    {
        var container = (ContainerBlock)FirstBlockOf("> inner one\n>\n> inner two");
        var section = new Document().AddSection();

        new NestedBlocksWriter().Write(container, new BlockContext(section, new BlockFactory()));

        ParagraphsOf(section).Select(ComposedText.Of).ShouldBe(["inner one", "inner two"]);
    }

    private static Paragraph CellParagraph(Table table, int row, int column) =>
        table.Rows[row].Cells[column].Elements.OfType<Paragraph>().First();

    // Whether each row of the PDF table carries Row Banding's shade, top to bottom (INV-068).
    private static IReadOnlyList<bool> Banding(Table table) =>
        [.. Enumerable.Range(0, table.Rows.Count).Select(row => !table.Rows[row].Shading.Color.IsEmpty)];
}
