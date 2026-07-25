using System.Linq;
using System.Windows.Documents;
using Shouldly;
using UI.Wysiwyg;
using Xunit;

namespace UI.Tests.Wysiwyg;

/// <summary>
/// Tests for the projection and Capture of a Definition List: each Definition Term flush against the
/// margin with its Definition Descriptions indented beneath it, Captured back to the exact canonical
/// syntax the construct demands (INV-066).
/// </summary>
public sealed class DefinitionListProjectionTests
{
    private static readonly MarkdownToFlowDocumentProjector Projector = new();
    private static readonly FlowDocumentToMarkdownCapturer Capturer = new();

    [Fact]
    public void Project_ShowsTheTermFlushAndTheDescriptionIndented_INV066()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Markdown\n:   A markup language.");

            var list = DefinitionList(document);
            var term = list.Blocks.OfType<Paragraph>().Single(block => block.Tag is BlockSemantic.DefinitionTerm);
            var description = list.Blocks.OfType<Section>()
                .Single(block => block.Tag is BlockSemantic.DefinitionDescription);

            Text(term).ShouldBe("Markdown");
            Text(description).ShouldContain("A markup language.");
            term.Margin.Left.ShouldBe(0);
            description.Margin.Left.ShouldBeGreaterThan(term.Margin.Left);
        });
    }

    [Fact]
    public void Project_ShowsNoRawColonSyntax_INV066()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Markdown\n:   A markup language.");

            new TextRange(document.ContentStart, document.ContentEnd).Text.ShouldNotContain(":");
        });
    }

    [Fact]
    public void Project_GivenAnItemWithTwoTerms_KeepsBoth_INV066()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Term one\nTerm two\n:   Shared description.");

            DefinitionList(document).Blocks.OfType<Paragraph>()
                .Where(block => block.Tag is BlockSemantic.DefinitionTerm)
                .Select(Text)
                .ShouldBe(["Term one", "Term two"]);
        });
    }

    [Fact]
    public void Project_GivenATermLessItem_ShowsAFurtherDescription_INV066()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Round-Trip\n:   One.\n:   Two.");

            var list = DefinitionList(document);

            list.Blocks.OfType<Paragraph>().Count(block => block.Tag is BlockSemantic.DefinitionTerm)
                .ShouldBe(1);
            list.Blocks.OfType<Section>()
                .Where(block => block.Tag is BlockSemantic.DefinitionDescription)
                .Select(description => Text(description).Trim())
                .ShouldBe(["One.", "Two."]);
        });
    }

    [Fact]
    public void Project_GivenAMultiBlockDescription_KeepsEveryBlock_INV066()
    {
        StaThread.Run(() =>
        {
            var document = Projector.Project("Term\n:   intro\n\n    - one\n    - two");

            var description = DefinitionList(document).Blocks.OfType<Section>()
                .Single(block => block.Tag is BlockSemantic.DefinitionDescription);

            description.Blocks.OfType<Paragraph>().Count().ShouldBe(1);
            description.Blocks.OfType<List>().Count().ShouldBe(1);
        });
    }

    [Fact]
    public void Capture_EmitsAColonAndThreeSpaces_BecauseFewerIsNotADefinitionList_INV066()
    {
        StaThread.Run(() =>
        {
            Capturer.Capture(Projector.Project("Markdown\n:   A markup language."))
                .ShouldBe("Markdown\n:   A markup language.");
        });
    }

    [Fact]
    public void Capture_SeparatesATermedItemWithABlankLine_INV066()
    {
        StaThread.Run(() =>
        {
            // Joined tight, the second Term would be absorbed into the first Item's Description.
            Capturer.Capture(Projector.Project("A\n:   one.\n\nB\n:   two."))
                .ShouldBe("A\n:   one.\n\nB\n:   two.");
        });
    }

    [Fact]
    public void Capture_SeparatesATermLessItemWithoutABlankLine_INV066()
    {
        StaThread.Run(() =>
        {
            // A blank line here would make the Description above it loose, changing the Rendered Output.
            Capturer.Capture(Projector.Project("Round-Trip\n:   One.\n:   Two."))
                .ShouldBe("Round-Trip\n:   One.\n:   Two.");
        });
    }

    [Fact]
    public void Capture_IndentsAContinuationLine_INV066()
    {
        StaThread.Run(() =>
        {
            Capturer.Capture(Projector.Project("Term\n:   para one\n\n    para two"))
                .ShouldBe("Term\n:   para one\n\n    para two");
        });
    }

    [Fact]
    public void Capture_SeparatesTheListFromTheBlockAboveIt_SoNoParagraphBecomesATerm_INV066()
    {
        StaThread.Run(() =>
        {
            Capturer.Capture(Projector.Project("Intro para.\n\nTerm\n:   desc."))
                .ShouldBe("Intro para.\n\nTerm\n:   desc.");
        });
    }

    private static Section DefinitionList(FlowDocument document) =>
        document.Blocks.OfType<Section>().Single(block => block.Tag is BlockSemantic.DefinitionList);

    private static string Text(TextElement element) =>
        new TextRange(element.ContentStart, element.ContentEnd).Text;
}
